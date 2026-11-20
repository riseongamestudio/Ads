#import "RANRectAdPresentation.h"

#import "RANMeasureLayout.h"
#import "RANRectAdLayoutEngine.h"
#import "RANRectAdLayoutTypes.h"
#import "RANRectAdLayoutValidator.h"
#import "RANRectAdViewFactory.h"
#import "RANLayerOrder.h"

static NSString *const kRANTag = @"RectAd";

static const NSInteger kRANMinStableLayoutPasses = 3;
static const NSTimeInterval kRANMinFinalLayoutObservation = 0.08;
static const NSTimeInterval kRANMaxFinalLayoutObservation = 0.5;
static const NSTimeInterval kRANRootReadyRecheckDelay = 0.05;
static const NSTimeInterval kRANMaxRootWait = 10.0;

// Elapsed time is measured on the clock that only goes forward, never on
// NSDate: a wall clock is a statement about what time it is, not about how
// long something has taken, and an NTP correction landing mid-wait would
// move a deadline that has nothing to do with the calendar. The same clock
// the rest of this port already keeps - and the exact counterpart of the
// SystemClock.uptimeMillis the Java side measures these three windows with,
// deep sleep excluded on both.
static NSTimeInterval RANNow(void) {
    return [NSProcessInfo processInfo].systemUptime;
}

static void *kRANRootBoundsContext = &kRANRootBoundsContext;


@implementation RANRectAdPresentation {
    __weak UIViewController *_hostViewController;
    GADNativeAd *_nativeAd;
    CGFloat _requestedX;
    CGFloat _requestedY;
    CGFloat _requestedWidth;
    CGFloat _requestedHeight;
    int32_t _backgroundColor;
    CGFloat _roundCorner;
    __weak id<RANRectAdPresentationListener> _listener;

    RANRectAdViewFactory *_viewFactory;
    RANRectAdLayoutValidator *_validator;
    RANRectAdLayoutEngine *_layoutEngine;
    NSMutableArray<RANRectAdLayoutPlan *> *_layoutPlans;

    GADNativeAdView *_nativeAdView;
    RANRectAdAssetViews *_boundViews;
    RANRectAdLayoutPlan *_activePlan;
    UIImage *_mainImage;
    NSUInteger _nextLayoutPlanIndex;
    NSString *_lastLayoutFailure;

    CADisplayLink *_observationLink;
    __weak CALayer *_observedRootLayer;
    CGSize _observedRootSize;
    BOOL _appActive;
    NSTimeInterval _finalObservationStartedAt;
    NSTimeInterval _candidateObservationStartedAt;
    NSInteger _stablePasses;
    NSInteger _invalidStablePasses;
    int64_t _lastGeometrySignature;
    int64_t _lastInvalidGeometrySignature;

    BOOL _waitingForRoot;
    NSTimeInterval _rootWaitStartedAt;
    BOOL _visibleRequested;
    BOOL _layoutReady;
    BOOL _readyNotified;
    BOOL _displayedNotified;
    BOOL _displayNotificationPending;
    BOOL _actualVisibilityKnown;
    BOOL _lastActualVisibility;
    BOOL _dismissed;
    BOOL _clickCommitted;
    NSString *_failureMessage;
}

- (instancetype)initWithHostViewController:(UIViewController *)hostViewController
                                  nativeAd:(GADNativeAd *)nativeAd
                                         x:(CGFloat)xPt
                                         y:(CGFloat)yPt
                                     width:(CGFloat)widthPt
                                    height:(CGFloat)heightPt
                           backgroundColor:(int32_t)backgroundColor
                               roundCorner:(CGFloat)roundCornerPt
                                  listener:(id<RANRectAdPresentationListener>)listener {
    self = [super initWithFrame:CGRectZero];
    if (self == nil) return nil;

    _hostViewController = hostViewController;
    _nativeAd = nativeAd;
    _requestedX = xPt;
    _requestedY = yPt;
    _requestedWidth = MAX(1, widthPt);
    _requestedHeight = MAX(1, heightPt);
    _backgroundColor = backgroundColor;
    // A radius past half the short side is a pill that no longer has
    // straight edges to inset from; the cell caps it there.
    _roundCorner = MAX(
            0
          , MIN(roundCornerPt, MIN(_requestedWidth, _requestedHeight) / 2));
    _listener = listener;
    _visibleRequested = YES;
    _layoutPlans = [NSMutableArray array];

    _viewFactory = [[RANRectAdViewFactory alloc]
            initWithNativeAd:nativeAd
             slotShortSidePt:MIN(_requestedWidth, _requestedHeight)
               roundCornerPt:_roundCorner];
    _validator = [[RANRectAdLayoutValidator alloc]
            initWithNativeAd:nativeAd
                 viewFactory:_viewFactory];
    _layoutEngine = [[RANRectAdLayoutEngine alloc]
            initWithNativeAd:nativeAd
                  requestedX:xPt
                  requestedY:yPt
              requestedWidth:_requestedWidth
             requestedHeight:_requestedHeight
                 viewFactory:_viewFactory
                   validator:_validator];

    // Whole from the caller. A fully transparent color is a real answer
    // here, not an unset value - a feed cell that wants no backdrop of its
    // own asks for exactly that.
    self.backgroundColor = [UIColor
            colorWithRed:(((uint32_t)_backgroundColor >> 16) & 0xFF) / 255.0
                   green:(((uint32_t)_backgroundColor >> 8) & 0xFF) / 255.0
                    blue:((uint32_t)_backgroundColor & 0xFF) / 255.0
                   alpha:(((uint32_t)_backgroundColor >> 24) & 0xFF) / 255.0];
    // The rounded backdrop doubles as the clip (clipsToBounds follows the
    // layer's corner radius): everything inside the cell is cut to the
    // same curve, which is what lets the scrim layout's background picture
    // fill the cell edge to edge and still end at the corner. The assets
    // in front keep clear of the curve on their own (the view factory's
    // corner inset). A transparent color still clips - the shape is the
    // layer's, not the paint's.
    self.layer.cornerRadius = _roundCorner;
    self.clipsToBounds = YES;

    // Read once rather than assumed: a slot built while the app is coming
    // back from the background would otherwise start out believing it is on
    // screen, and start charging dwell for it.
    _appActive = UIApplication.sharedApplication.applicationState
            == UIApplicationStateActive;
    [NSNotificationCenter.defaultCenter
            addObserver:self
               selector:@selector(ran_applicationDidBecomeActive)
                   name:UIApplicationDidBecomeActiveNotification
                 object:nil];
    [NSNotificationCenter.defaultCenter
            addObserver:self
               selector:@selector(ran_applicationWillResignActive)
                   name:UIApplicationWillResignActiveNotification
                 object:nil];
    return self;
}

- (void)dealloc {
    [NSNotificationCenter.defaultCenter removeObserver:self];
    [self ran_stopObservingRootBounds];
}

- (NSString *)failureMessage {
    return _failureMessage;
}

- (void)commitAdClick {
    _clickCommitted = YES;
}

- (void)ran_applicationDidBecomeActive {
    // The Android latch released on window visibility because the panel
    // never takes focus; here the overlay the click opened has gone away.
    _clickCommitted = NO;
    _appActive = YES;
    [self ran_notifyActualVisibilityIfChanged];
}

// The counterpart of the Activity losing window focus on Android. Read from
// the notification rather than from applicationState, because at the moment
// this fires the application is still reported as active.
- (void)ran_applicationWillResignActive {
    _appActive = NO;
    [self ran_notifyActualVisibilityIfChanged];
}

// The same latch the full-screen container uses: once a click is committed
// every further touch is swallowed until the slot comes back.
- (UIView *)hitTest:(CGPoint)point withEvent:(UIEvent *)event {
    UIView *hit = [super hitTest:point withEvent:event];
    if (_clickCommitted && hit != nil) return self;
    return hit;
}

- (BOOL)ran_fail:(NSString *)message {
    [self ran_recordFailure:message];
    return NO;
}

- (void)ran_recordFailure:(NSString *)message {
    if (_failureMessage != nil) return;
    _failureMessage = message;
    NSLog(@"%@: %@", kRANTag, message);
}

- (UIView *)ran_contentRoot {
    return _hostViewController.view;
}

- (BOOL)ran_isContentRootReady {
    UIView *contentRoot = [self ran_contentRoot];
    return contentRoot != nil
            && contentRoot.window != nil
            && contentRoot.bounds.size.width > 0
            && contentRoot.bounds.size.height > 0;
}

- (BOOL)show {
    if (_dismissed) {
        return [self ran_fail:@"Rect ad Show rejected because presentation "
                              "is dismissed"];
    }
    if (_nativeAd == nil) {
        return [self ran_fail:@"Rect ad Show rejected because nativeAd is nil"];
    }
    // The counterpart of Activity.isFinishing: the reference is weak, so a
    // host that has gone reads as nil and there is nothing left to show into.
    if (_hostViewController == nil) {
        return [self ran_fail:@"Rect ad Show rejected because the host "
                              "controller is gone"];
    }
    if (_waitingForRoot) return YES;

    if (![self ran_isContentRootReady]) {
        [self ran_waitForContentRootReady];
        return YES;
    }

    UIView *contentRoot = [self ran_contentRoot];
    CGFloat screenWidth = contentRoot.bounds.size.width;
    CGFloat screenHeight = contentRoot.bounds.size.height;
    if (_nativeAd.headline.length == 0
            || _nativeAd.callToAction.length == 0) {
        return [self ran_fail:@"Rect ad creative is missing required headline "
                              "or CTA"];
    }

    _mainImage = [_viewFactory findMainImage];
    BOOL hasVideo = [_viewFactory hasVideoContent];
    BOOL hasMainImage = [_viewFactory canRenderMainImage:_mainImage];
    [_layoutPlans removeAllObjects];
    [_layoutPlans addObjectsFromArray:
            [_layoutEngine choosePlansForScreenWidth:screenWidth
                                        screenHeight:screenHeight
                                            hasVideo:hasVideo
                                        hasMainImage:hasMainImage]];
    _nextLayoutPlanIndex = 0;
    _lastLayoutFailure = nil;
    if (_layoutPlans.count == 0) {
        NSString *rejectionSummary = [_layoutEngine describeLastRejections];
        return [self ran_fail:[NSString stringWithFormat:
                @"Rect ad cannot build a policy-safe layout for %@; "
                 "hasVideo=%d; hasMainImage=%d%@"
              , [self ran_describeRequestedRect]
              , hasVideo
              , hasMainImage
              , rejectionSummary.length == 0
                        ? @""
                        : [NSString stringWithFormat:@"; rejected={%@}"
                                  , rejectionSummary]]];
    }

    if (![self ran_activateNextLayoutPlan]) {
        return [self ran_fail:[NSString stringWithFormat:
                @"Rect ad could not bind any policy-safe layout for %@%@"
              , [self ran_describeRequestedRect]
              , [self ran_describeLastLayoutFailure]]];
    }

    self.hidden = YES;
    // The lowest tier: still above the game, because the game is this
    // view's PARENT (rootVC.view is UnityView itself), never a sibling, and
    // below every other layer - unmarked ones included - which is exactly
    // where rect ads belong. A newer cell lands above the older ones.
    RANInsertSubviewAtTier(contentRoot, self, kRANRectAdLayerTier);
    if (![self ran_fitHostInsideContentRoot]) {
        return [self ran_fail:@"Rect ad could not place its presentation view"];
    }
    _finalObservationStartedAt = RANNow();
    [self ran_beginObservingFinalAssetGeometry];
    return YES;
}

- (void)ran_waitForContentRootReady {
    if (_waitingForRoot) return;
    _waitingForRoot = YES;
    _rootWaitStartedAt = RANNow();
    [self ran_scheduleRootReadyRecheck];
}

- (void)ran_scheduleRootReadyRecheck {
    __weak RANRectAdPresentation *weakSelf = self;
    dispatch_after(
            dispatch_time(DISPATCH_TIME_NOW
                  , (int64_t)(kRANRootReadyRecheckDelay * NSEC_PER_SEC))
          , dispatch_get_main_queue()
          , ^{ [weakSelf ran_checkContentRootReady]; });
}

- (void)ran_checkContentRootReady {
    if (_dismissed || !_waitingForRoot) return;
    // A host that has been torn down is never going to become ready, so it
    // ends here rather than after the full wait - the ten seconds are for a
    // root that is slow, not for one that no longer exists.
    if (_hostViewController == nil) {
        [self ran_recordFailure:@"Rect ad content root never became ready "
                                "because the host controller is gone"];
        [self ran_dismissWithNotify:YES removeFromParent:YES];
        return;
    }
    if (![self ran_isContentRootReady]) {
        // Bounded so a root that never settles ends as a normal dismissal
        // instead of parking the slot and blocking every later load.
        if (RANNow() - _rootWaitStartedAt >= kRANMaxRootWait) {
            [self ran_recordFailure:[NSString stringWithFormat:
                    @"Rect ad content root did not become ready within %gms"
                  , kRANMaxRootWait * 1000]];
            [self ran_dismissWithNotify:YES removeFromParent:YES];
            return;
        }
        [self ran_scheduleRootReadyRecheck];
        return;
    }

    _waitingForRoot = NO;
    if (![self show]) [self ran_dismissWithNotify:YES removeFromParent:YES];
}

- (BOOL)ran_activateNextLayoutPlan {
    [self ran_destroyNativeAdView];
    while (_nextLayoutPlanIndex < _layoutPlans.count) {
        RANRectAdLayoutPlan *plan = _layoutPlans[_nextLayoutPlanIndex];
        ++_nextLayoutPlanIndex;
        @try {
            RANRectAdNativeAdViewResult *viewResult =
                    [_viewFactory buildNativeAdViewForPlan:plan
                                                 mainImage:_mainImage];
            _nativeAdView = viewResult.nativeAdView;
            _boundViews = viewResult.assetViews;
            _activePlan = plan;
            _nativeAdView.frame = CGRectMake(0, 0, plan.width, plan.height);
            [self addSubview:_nativeAdView];
            if (self.superview != nil
                    && ![self ran_fitHostInsideContentRoot]) {
                _lastLayoutFailure = [NSString stringWithFormat:
                        @"layout no longer fits content root: %@"
                      , RANRectAdDescribePlan(plan)];
                [self ran_destroyNativeAdView];
                continue;
            }
            [self ran_layoutBoundContent];
            return YES;
        } @catch (NSException *exception) {
            _lastLayoutFailure = [NSString stringWithFormat:
                    @"failed to bind %@: %@"
                  , RANRectAdDescribePlan(plan)
                  , exception.reason];
            NSLog(@"%@: %@", kRANTag, _lastLayoutFailure);
            [self ran_destroyNativeAdView];
        }
    }
    return NO;
}

- (void)ran_destroyNativeAdView {
    if (_nativeAdView != nil) {
        [_nativeAdView removeFromSuperview];
        _nativeAdView.nativeAd = nil;
    }
    _nativeAdView = nil;
    _boundViews = nil;
    _activePlan = nil;
    _layoutReady = NO;
}

// Runs the measure model over the bound tree - the pass Android's view
// hierarchy ran on its own.
- (void)ran_layoutBoundContent {
    if (_nativeAdView == nil || _activePlan == nil) return;
    _nativeAdView.frame =
            CGRectMake(0, 0, _activePlan.width, _activePlan.height);
    for (UIView *child in _nativeAdView.subviews) {
        // Views placed by Auto Layout are the SDK's - its AdChoices
        // container and overlays; everything this pack builds is placed by
        // frame. A frame from this pass would only fight their constraints:
        // it stretched the AdChoices container over the whole cell until
        // Auto Layout next ran, and would undo the pins moved just below.
        if (!child.translatesAutoresizingMaskIntoConstraints) continue;

        [child ran_measureWithWidthSpec:RANMeasureSpecMake(
                        RANMeasureSpecExactly, _activePlan.width)
                            heightSpec:RANMeasureSpecMake(
                        RANMeasureSpecExactly, _activePlan.height)];
        [child ran_layoutWithFrame:CGRectMake(
                0, 0, _activePlan.width, _activePlan.height)];
    }
    [_viewFactory insetSDKAdChoicesInNativeAdView:_nativeAdView
                                        logResult:NO];
}

- (void)ran_rejectActivePlanAndTryNext:(NSString *)reason {
    NSString *rejectedPlan = RANRectAdDescribePlan(_activePlan);
    _lastLayoutFailure = [NSString stringWithFormat:@"%@; rejected=%@"
          , reason, rejectedPlan];
    NSLog(@"%@: %@", kRANTag, _lastLayoutFailure);
    _layoutReady = NO;
    self.hidden = YES;

    if ([self ran_activateNextLayoutPlan]) {
        NSLog(@"%@: Rect ad trying fallback layout %@ after %@"
              , kRANTag, RANRectAdDescribePlan(_activePlan), rejectedPlan);
        [self ran_beginObservingFinalAssetGeometry];
        return;
    }

    [self ran_recordFailure:[NSString stringWithFormat:
            @"Rect ad exhausted %lu policy-safe layout candidate(s) for %@%@"
          , (unsigned long)_layoutPlans.count
          , [self ran_describeRequestedRect]
          , [self ran_describeLastLayoutFailure]]];
    [self ran_dismissWithNotify:YES removeFromParent:YES];
}

// ---------------------------------------------------------------------------
// Final geometry observation - the pre-draw loop, on a display link.
// ---------------------------------------------------------------------------

- (void)ran_beginObservingFinalAssetGeometry {
    [_observationLink invalidate];
    _candidateObservationStartedAt = RANNow();
    _stablePasses = 0;
    _invalidStablePasses = 0;
    _lastGeometrySignature = INT64_MIN;
    _lastInvalidGeometrySignature = INT64_MIN;
    _observationLink = [CADisplayLink
            displayLinkWithTarget:self
                         selector:@selector(ran_observationTick)];
    [_observationLink addToRunLoop:NSRunLoop.mainRunLoop
                           forMode:NSRunLoopCommonModes];
}

// Rotation, split-screen and a resized game view all change the root out
// from under a layout that was validated against the old size, and the
// display link above has already stopped by then - it only runs while a plan
// is being judged. Android watches the same thing for the whole life of the
// presentation with addOnLayoutChangeListener; this is that listener.
//
// The observation is on the LAYER, not on the view. CALayer documents its
// properties as KVC and KVO compliant; UIView promises nothing of the sort
// for its own frame and bounds, and the layer changes on exactly the same
// occasions.
- (void)ran_startObservingRootBounds {
    if (_observedRootLayer != nil) return;

    UIView *contentRoot = [self ran_contentRoot];
    if (contentRoot == nil) return;

    _observedRootSize = contentRoot.bounds.size;
    _observedRootLayer = contentRoot.layer;
    [contentRoot.layer addObserver:self
                        forKeyPath:@"bounds"
                           options:0
                           context:kRANRootBoundsContext];
}

- (void)ran_stopObservingRootBounds {
    CALayer *observed = _observedRootLayer;
    _observedRootLayer = nil;
    if (observed == nil) return;

    [observed removeObserver:self
                  forKeyPath:@"bounds"
                     context:kRANRootBoundsContext];
}

- (void)observeValueForKeyPath:(NSString *)keyPath
                      ofObject:(id)object
                        change:(NSDictionary *)change
                       context:(void *)context {
    if (context != kRANRootBoundsContext) {
        [super observeValueForKeyPath:keyPath
                             ofObject:object
                               change:change
                              context:context];
        return;
    }
    [self ran_handleRootBoundsChanged];
}

// A rotation animates, so this arrives several times for one turn. Only a
// size that actually differs from the one the layout was judged against is
// worth a re-fit, which leaves the intermediate frames costing a comparison.
- (void)ran_handleRootBoundsChanged {
    if (_dismissed || !_layoutReady) return;

    UIView *contentRoot = [self ran_contentRoot];
    if (contentRoot == nil) return;

    CGSize size = contentRoot.bounds.size;
    if (size.width <= 0 || size.height <= 0) return;
    if (CGSizeEqualToSize(size, _observedRootSize)) return;

    _observedRootSize = size;
    if ([self ran_fitHostInsideContentRoot]) return;

    [self ran_recordFailure:[NSString stringWithFormat:
            @"Rect ad active layout no longer fits after content surface "
             "resize; %@"
          , [self ran_describeRequestedRect]]];
    [self ran_dismissWithNotify:YES removeFromParent:YES];
}

- (void)ran_stopObserving {
    [_observationLink invalidate];
    _observationLink = nil;
}

- (void)ran_observationTick {
    if (_dismissed || self.superview == nil) {
        [self ran_stopObserving];
        if (!_dismissed) [self ran_dismissWithNotify:YES removeFromParent:NO];
        return;
    }

    [self ran_layoutBoundContent];
    NSTimeInterval candidateObservation =
            RANNow() - _candidateObservationStartedAt;
    NSTimeInterval totalObservation =
            RANNow() - _finalObservationStartedAt;
    BOOL validationDeadlineReached =
            totalObservation >= kRANMaxFinalLayoutObservation;
    BOOL hostFits = [self ran_fitHostInsideContentRoot];
    BOOL valid = hostFits
            && [_validator validateAssetGeometryForRoot:_nativeAdView
                                                  views:_boundViews
                                                   plan:_activePlan
                                             logFailure:validationDeadlineReached];
    if (!valid) {
        int64_t invalidSignature =
                [_validator geometrySignatureForHost:self
                                                root:_nativeAdView
                                               views:_boundViews];
        if (invalidSignature == _lastInvalidGeometrySignature) {
            ++_invalidStablePasses;
        } else {
            _lastInvalidGeometrySignature = invalidSignature;
            _invalidStablePasses = 1;
        }
        BOOL deterministicFailure =
                _invalidStablePasses >= kRANMinStableLayoutPasses;
        BOOL invalidObservationComplete = deterministicFailure
                || candidateObservation >= kRANMinFinalLayoutObservation;
        if (!invalidObservationComplete) {
            _stablePasses = 0;
            _lastGeometrySignature = INT64_MIN;
            return;
        }
        [self ran_stopObserving];
        NSString *validationReason = hostFits
                ? _validator.lastFailureReason
                : @"layout host is outside content root";
        [self ran_rejectActivePlanAndTryNext:[NSString stringWithFormat:
                @"Rect ad rejected after final layout; required asset is "
                 "clipped, empty, or overlapping%@"
              , validationReason.length == 0
                        ? @""
                        : [NSString stringWithFormat:@": %@"
                                  , validationReason]]];
        return;
    }
    _invalidStablePasses = 0;
    _lastInvalidGeometrySignature = INT64_MIN;

    int64_t signature = [_validator geometrySignatureForHost:self
                                                        root:_nativeAdView
                                                       views:_boundViews];
    if (signature == _lastGeometrySignature) {
        ++_stablePasses;
    } else {
        _lastGeometrySignature = signature;
        _stablePasses = 1;
    }
    BOOL observedLongEnough =
            candidateObservation >= kRANMinFinalLayoutObservation;
    if (_stablePasses < kRANMinStableLayoutPasses || !observedLongEnough) {
        BOOL stabilizationDeadlineReached =
                candidateObservation >= kRANMaxFinalLayoutObservation
                || (totalObservation >= kRANMaxFinalLayoutObservation
                        && observedLongEnough);
        if (stabilizationDeadlineReached) {
            [self ran_stopObserving];
            [self ran_rejectActivePlanAndTryNext:
                    @"Rect ad rejected after final layout; asset geometry "
                     "did not become stable"];
        }
        return;
    }

    [self ran_stopObserving];
    _layoutReady = YES;
    // The layout has settled, so the SDK has built whatever it was going to:
    // what the inset finds now is the verdict worth reporting.
    [_viewFactory insetSDKAdChoicesInNativeAdView:_nativeAdView
                                        logResult:YES];
    [self ran_startObservingRootBounds];
    NSLog(@"%@: Rect ad layout ready %@ for %@"
          , kRANTag
          , RANRectAdDescribePlan(_activePlan)
          , [self ran_describeRequestedRect]);
    if (!_readyNotified && _listener != nil) {
        _readyNotified = YES;
        [_listener rectAdPresentationReady];
    }
    [self ran_applyRequestedVisibility];
}

// ---------------------------------------------------------------------------
// Visibility and geometry
// ---------------------------------------------------------------------------

- (BOOL)setVisible:(BOOL)visible {
    if (_dismissed) return NO;
    _visibleRequested = visible;
    [self ran_applyRequestedVisibility];
    return _layoutReady;
}

- (void)requestDisplayNotification {
    if (_dismissed) return;
    _displayedNotified = NO;
    [self ran_applyRequestedVisibility];
}

- (BOOL)setPositionX:(CGFloat)xPt y:(CGFloat)yPt {
    if (_dismissed) return NO;
    _requestedX = xPt;
    _requestedY = yPt;
    [_layoutEngine setPositionX:xPt y:yPt];
    return [self ran_fitHostInsideContentRoot];
}

- (void)ran_applyRequestedVisibility {
    if (_dismissed || !_layoutReady || self.superview == nil) return;

    if (!_visibleRequested) {
        self.hidden = YES;
        self.userInteractionEnabled = NO;
        [self ran_notifyActualVisibilityIfChanged];
        return;
    }

    self.userInteractionEnabled = YES;
    self.hidden = NO;
    // The slot came back into sight: the click latch releases here the way
    // the Android one released on window visibility.
    _clickCommitted = NO;
    [self ran_notifyActualVisibilityIfChanged];
    if (_displayedNotified || _displayNotificationPending) return;

    _displayNotificationPending = YES;
    __weak RANRectAdPresentation *weakSelf = self;
    dispatch_async(dispatch_get_main_queue(), ^{
        RANRectAdPresentation *strongSelf = weakSelf;
        if (strongSelf == nil) return;
        strongSelf->_displayNotificationPending = NO;
        if (!strongSelf->_dismissed
                && strongSelf->_visibleRequested
                && strongSelf.window != nil
                && !strongSelf.hidden
                && strongSelf.superview != nil
                && strongSelf->_listener != nil) {
            strongSelf->_displayedNotified = YES;
            [strongSelf->_listener rectAdPresentationDisplayed];
        }
    });
}

// Being in a window is not the same as being looked at. Backgrounded, or
// under the share sheet a click opened, the view is still in its window and
// still unhidden - and without this term the dwell clock would keep running
// through it and rotate the creative where nobody could see either one.
//
// Not an exact twin of the Android test, which reads getWindowVisibility and
// isShown on the panel: those go false when the Activity STOPS, so Android
// keeps counting through a notification shade or a Control Center pull where
// this stops. The case both cover is the one this was written for - the app
// actually in the background - and erring towards not charging dwell for
// time the player did not spend looking is the safe direction.
- (BOOL)ran_isActuallyVisible {
    return !_dismissed
            && _layoutReady
            && _visibleRequested
            && _appActive
            && self.superview != nil
            && self.window != nil
            && !self.hidden;
}

- (void)ran_notifyActualVisibilityIfChanged {
    BOOL isActuallyVisible = [self ran_isActuallyVisible];
    if (_actualVisibilityKnown
            && _lastActualVisibility == isActuallyVisible) {
        return;
    }
    _actualVisibilityKnown = YES;
    _lastActualVisibility = isActuallyVisible;
    [_listener rectAdPresentationActualVisibilityChanged:isActuallyVisible];
}

- (void)didMoveToWindow {
    [super didMoveToWindow];
    [self ran_notifyActualVisibilityIfChanged];
    if (self.window == nil && !_dismissed && self.superview == nil) {
        [self ran_dismissWithNotify:YES removeFromParent:NO];
    }
}

- (BOOL)ran_fitHostInsideContentRoot {
    UIView *contentRoot = [self ran_contentRoot];
    if (contentRoot == nil || _activePlan == nil) return NO;
    CGFloat rootWidth = contentRoot.bounds.size.width;
    CGFloat rootHeight = contentRoot.bounds.size.height;
    if (rootWidth <= 0
            || rootHeight <= 0
            || _activePlan.width > rootWidth
            || _activePlan.height > rootHeight) {
        return NO;
    }

    CGFloat actualX = RANClamp(_requestedX, 0, rootWidth - _activePlan.width);
    CGFloat actualY = RANClamp(_requestedY, 0, rootHeight - _activePlan.height);
    CGRect targetFrame = CGRectMake(
            actualX, actualY, _activePlan.width, _activePlan.height);
    if (!CGRectEqualToRect(self.frame, targetFrame)) {
        self.frame = targetFrame;
        [self ran_logAdjustmentIfNeededForX:actualX y:actualY];
    }
    return YES;
}

- (BOOL)isShowingPresentation {
    return !_dismissed && (_waitingForRoot || self.superview != nil);
}

- (void)dismissPresentation {
    [self ran_dismissWithNotify:YES removeFromParent:YES];
}

- (void)releasePresentation {
    [self ran_dismissWithNotify:NO removeFromParent:YES];
}

- (void)ran_dismissWithNotify:(BOOL)notify
            removeFromParent:(BOOL)removeFromParent {
    if (_dismissed) return;
    _dismissed = YES;
    [self ran_notifyActualVisibilityIfChanged];
    _waitingForRoot = NO;
    _layoutReady = NO;
    _visibleRequested = NO;
    _displayNotificationPending = NO;
    [self ran_stopObserving];
    [self ran_stopObservingRootBounds];

    if (removeFromParent) [self removeFromSuperview];
    [self ran_destroyNativeAdView];
    [_layoutPlans removeAllObjects];
    [_layoutEngine clear];

    if (notify) [_listener rectAdPresentationDismissed];
}

// ---------------------------------------------------------------------------
// Descriptions
// ---------------------------------------------------------------------------

// Unity lays the slot out in pixels and every policy threshold here is
// points; report both so a log line answers the dp question outright.
- (NSString *)ran_describeRequestedRect {
    CGFloat scale = MAX(1, UIScreen.mainScreen.nativeScale);
    return [NSString stringWithFormat:
            @"requested rect [%lld,%lld,%lld,%lld]px = [%lld,%lld]dp "
             "(density=%g)"
          , (long long)llround(_requestedX * scale)
          , (long long)llround(_requestedY * scale)
          , (long long)llround(_requestedWidth * scale)
          , (long long)llround(_requestedHeight * scale)
          , (long long)llround(_requestedWidth)
          , (long long)llround(_requestedHeight)
          , scale];
}

- (NSString *)ran_describeLastLayoutFailure {
    return _lastLayoutFailure.length == 0
            ? @""
            : [NSString stringWithFormat:@"; lastFailure=%@"
                      , _lastLayoutFailure];
}

- (void)ran_logAdjustmentIfNeededForX:(CGFloat)actualX y:(CGFloat)actualY {
    if (actualX == _requestedX
            && actualY == _requestedY
            && _activePlan.width == _requestedWidth
            && _activePlan.height == _requestedHeight) {
        return;
    }
    NSLog(@"%@: Rect ad adjusted: requested=[%g,%g,%g,%g] "
           "actual=[%g,%g,%g,%g] template=%@ tier=%@ media=%@"
          , kRANTag
          , _requestedX, _requestedY, _requestedWidth, _requestedHeight
          , actualX, actualY, _activePlan.width, _activePlan.height
          , RANRectAdTemplateName(_activePlan.layoutTemplate)
          , RANRectAdTierName(_activePlan.tier)
          , RANRectAdMediaName(_activePlan));
}


@end