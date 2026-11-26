#import "RANOverlayAd.h"

#import "RANOverlayAdContentView.h"
#import "RANOverlayAdPresentation.h"
#import "RANNativeAdPool.h"

static NSString *const kRANTag = @"Overlay";
static const int32_t kRANLoadSuccessCode = 0;
static const NSTimeInterval kRANMaxCachedAdAge = kRANNativeAdPoolMaxCachedAdAge;
// The layout retry: a creative this panel cannot show is one creative's
// fault and the next one usually fits, so the first attempts go out
// immediately. And one valve: while NOTHING has ever laid out here the panel
// is the likelier culprit, and the unit settles into a slow poll instead of
// asking on. All three numbers are the rect ad's.
static const NSInteger kRANRetryImmediateLayoutAttempts = 2;
static const NSInteger kRANMaxUnprovenLayoutFailures = 20;
static const NSTimeInterval kRANUnprovenLayoutRetryDelay = 300.0;

// Only ad ages are timed here, so on the pool's clock.
static NSTimeInterval RANNow(void) {
    return RANNativeAdPoolNow();
}

// An ad and the moment it arrived - the only two things needed to know
// whether it may still be shown.
@interface RANOverlayCachedAd : NSObject
@property (nonatomic, strong) GADNativeAd *ad;
@property (nonatomic, assign) NSTimeInterval loadedAt;
@end

@implementation RANOverlayCachedAd
@end

// The style snapshot Configure publishes and every show reads - the same
// immutable OverlayStyle the Java side passes around.
@interface RANOverlayStyle : NSObject
@property (nonatomic, readonly) BOOL fullScreen;
@property (nonatomic, readonly) int32_t cooldown;
// Ordinals shared with the C# enums: Left 0, Right 1, Random 2, and for the
// timer OppositeOfClose 3, SameAsClose 4.
@property (nonatomic, readonly) int32_t closeSide;
@property (nonatomic, readonly) int32_t timerSide;
@property (nonatomic, readonly) float heightRatio;
@property (nonatomic, readonly) int32_t backgroundColor;
// The close button commits the ad's click on its way out.
@property (nonatomic, readonly) BOOL redirectOnClose;
// How a full-screen ad arranges its card, ordinals shared with the C#
// FullScreenLayout: Interstitial 0, AppOpen 1, EndCard 2.
@property (nonatomic, readonly) int32_t layout;
@end

@implementation RANOverlayStyle

- (instancetype)initWithFullScreen:(BOOL)fullScreen
                          cooldown:(int32_t)cooldown
                         closeSide:(int32_t)closeSide
                         timerSide:(int32_t)timerSide
                       heightRatio:(float)heightRatio
                   backgroundColor:(int32_t)backgroundColor
                   redirectOnClose:(BOOL)redirectOnClose
                            layout:(int32_t)layout {
    self = [super init];
    if (self == nil) return nil;
    _fullScreen = fullScreen;
    _cooldown = MAX(0, cooldown);
    _closeSide = closeSide;
    _timerSide = timerSide;
    _heightRatio = heightRatio;
    _backgroundColor = backgroundColor;
    _redirectOnClose = redirectOnClose;
    _layout = layout;
    return self;
}

- (RANOverlayStyle *)styleWithCooldown:(int32_t)cooldown
                            closeSide:(int32_t)closeSide
                            timerSide:(int32_t)timerSide
                      redirectOnClose:(BOOL)redirectOnClose {
    return [[RANOverlayStyle alloc]
            initWithFullScreen:self.fullScreen
                      cooldown:cooldown
                     closeSide:closeSide
                     timerSide:timerSide
                   heightRatio:self.heightRatio
               backgroundColor:self.backgroundColor
               redirectOnClose:redirectOnClose
                        layout:self.layout];
}

- (RANOverlayStyle *)styleWithLayout:(int32_t)layout {
    return [[RANOverlayStyle alloc]
            initWithFullScreen:self.fullScreen
                      cooldown:self.cooldown
                     closeSide:self.closeSide
                     timerSide:self.timerSide
                   heightRatio:self.heightRatio
               backgroundColor:self.backgroundColor
               redirectOnClose:self.redirectOnClose
                        layout:layout];
}

// Random is answered once per presentation; the relative timer modes then
// read that answer rather than rolling again.
- (BOOL)resolveCloseOnLeft {
    if (self.closeSide == 0) return YES;
    if (self.closeSide == 1) return NO;
    return arc4random_uniform(2) == 0;
}

- (BOOL)resolveTimerOnLeft:(BOOL)closeOnLeft {
    switch (self.timerSide) {
        case 0:  return YES;
        case 1:  return NO;
        case 3:  return !closeOnLeft;
        case 4:  return closeOnLeft;
        default: return arc4random_uniform(2) == 0;
    }
}

- (BOOL)pausesGame {
    return self.fullScreen;
}

@end

@interface RANOverlayAd () <RANNativeAdPoolConsumer>
@end

// The face of the creative's assets at one moment. A presentation built
// at load time is only valid while this stays the same: assets that finish
// arriving later change the layout the ad needs, and a stale face must be
// rebuilt rather than shown.
static NSString *RANMediaSignature(GADNativeAd *nativeAd) {
    GADMediaContent *mediaContent = nativeAd.mediaContent;
    BOOL hasVideo = mediaContent.hasVideoContent;
    BOOL hasMainImage = mediaContent.mainImage != nil;
    CGFloat aspectRatio = mediaContent != nil ? mediaContent.aspectRatio : 0;
    BOOL hasAnyImage = NO;
    for (GADNativeAdImage *image in nativeAd.images) {
        if (image.image != nil) {
            hasAnyImage = YES;
            break;
        }
    }
    return [NSString stringWithFormat:@"%d|%d|%d|%g"
          , hasVideo, hasMainImage, hasAnyImage, aspectRatio];
}

@implementation RANOverlayAd {
    NSString *_adUnitID;
    RANOverlayStyle *_configuredStyle;
    // The panel height as the main thread last resolved it, for callers on
    // any other thread.
    CGFloat _panelHeight;
    BOOL _configured;
    BOOL _isAdLoading;
    // The layout retry. The generation is how a pending one is canceled:
    // dispatch_after cannot be recalled, so the block checks it is still the
    // current one.
    BOOL _retryScheduled;
    NSInteger _retryGeneration;
    NSInteger _layoutFailStreak;
    // Whether anything has ever laid out in this placement.
    BOOL _layoutProven;

    // Main thread only; joined at configure, left at release.
    RANNativeAdPool *_pool;
    NSInteger _cacheSize;
    // The head it holds from the pool, at most one: the ad the next show
    // takes, and the only one worth a prepared face.
    NSMutableArray<RANOverlayCachedAd *> *_cachedAds;
    NSTimer *_cacheExpiryTimer;
    // The ads it holds, the head and the one on screen. The paid event fires
    // on whatever thread the SDK picked; the guarded set answers there.
    NSHashTable<GADNativeAd *> *_ownedAds;
    GADNativeAd *_activeNativeAd;
    // When the ad on screen loaded, for its age if it goes back to the pool.
    NSTimeInterval _activeLoadedAt;
    RANOverlayAdPresentation *_presentation;
    RANOverlayAdPresentation *_preparedPresentation;
    GADNativeAd *_preparedNativeAd;
    RANOverlayStyle *_preparedStyle;
    NSString *_preparedMediaSignature;
    RANNativeAdShowCompletedCallback _activeShowCompleted;
    int32_t _activeShowID;
}

- (instancetype)initWithAdUnitID:(NSString *)adUnitID
                      instanceID:(int32_t)instanceID {
    self = [super initWithInstanceID:instanceID];
    if (self == nil) return nil;
    _adUnitID = [adUnitID copy];
    _cacheSize = 1;
    _cachedAds = [NSMutableArray array];
    _ownedAds = [NSHashTable weakObjectsHashTable];
    return self;
}

- (void)configureWithFullScreen:(BOOL)fullScreen
                    heightRatio:(float)heightRatio
                backgroundColor:(int32_t)backgroundColor
                      cacheSize:(int32_t)cacheSize
                       cooldown:(int32_t)cooldown
                      closeSide:(int32_t)closeSide
                      timerSide:(int32_t)timerSide
                redirectOnClose:(BOOL)redirectOnClose {
    [RANBaseAd runOnMainThread:^{
        if (self.released) {
            NSLog(@"%@: Configure ignored after Release", kRANTag);
            return;
        }
        if (self->_configured) {
            NSLog(@"%@: Configure may only be called once per instance", kRANTag);
            return;
        }

        // How many ads the pool keeps warm for it. One - always hold a
        // spare - is the placement that never asked; a chained placement
        // raises it so the follow-up is already in hand when the first ad
        // closes.
        self->_cacheSize = MAX(1, (NSInteger)cacheSize);
        self->_configuredStyle = [[RANOverlayStyle alloc]
                initWithFullScreen:fullScreen
                          cooldown:cooldown
                         closeSide:closeSide
                         timerSide:timerSide
                       heightRatio:heightRatio
                   backgroundColor:backgroundColor
                   redirectOnClose:redirectOnClose
                            layout:0];
        self->_configured = YES;
        [self resolvePanelHeight];
        // A game paused under the ad lets its video play with sound; one
        // beside the game keeps it muted.
        self->_pool = [RANNativeAdPool joinWithAdUnitID:self->_adUnitID
                                             startMuted:!self->_configuredStyle.pausesGame
                                               consumer:self];
    }];
}

// Straight after Configure. The pool it joined may already hold an ad
// another placement warmed, so a face prepared from it is rebuilt as
// setClose's is.
- (void)setLayout:(int32_t)layout {
    [RANBaseAd runOnMainThread:^{
        RANOverlayStyle *currentStyle = self->_configuredStyle;
        if (!self->_configured || self.released || currentStyle == nil) {
            NSLog(@"%@: SetLayout ignored before Configure or after "
                    "Release", kRANTag);
            return;
        }
        RANOverlayStyle *updatedStyle = [currentStyle styleWithLayout:layout];
        self->_configuredStyle = updatedStyle;
        [self ran_rebuildPreparedPresentationForStyle:updatedStyle];
    }];
}

- (void)setCloseWithCooldown:(int32_t)cooldown
                   closeSide:(int32_t)closeSide
                   timerSide:(int32_t)timerSide
             redirectOnClose:(BOOL)redirectOnClose {
    [RANBaseAd runOnMainThread:^{
        RANOverlayStyle *currentStyle = self->_configuredStyle;
        if (!self->_configured || self.released || currentStyle == nil) {
            NSLog(@"%@: SetClose ignored before Configure or after "
                    "Release", kRANTag);
            return;
        }
        RANOverlayStyle *updatedStyle =
                [currentStyle styleWithCooldown:cooldown
                                             closeSide:closeSide
                                             timerSide:timerSide
                                       redirectOnClose:redirectOnClose];
        self->_configuredStyle = updatedStyle;
        [self ran_rebuildPreparedPresentationForStyle:updatedStyle];
    }];
}

- (void)load {
    [RANBaseAd runOnMainThread:^{
        if (!self->_configured || self.released) {
            NSLog(@"%@: Load ignored before Configure or after Release"
                  , kRANTag);
            return;
        }
        UIViewController *host = [RANBaseAd unityViewController];
        if (![RANBaseAd isViewControllerUsable:host]) {
            NSLog(@"%@: Load ignored because the host controller is not "
                    "usable", kRANTag);
            return;
        }
        [self ran_refill];
    }];
}

#pragma mark - The pool

// The ads come from the unit's RANNativeAdPool, shared with every format
// drawing from the same unit and mute policy. This unit holds one of them at
// a time - the head, the one the next show takes and the only one worth a
// prepared face - and the pool keeps the rest for whoever asks first. A
// creative this panel cannot lay out goes back, marked with the panel's size
// key, for a format that can.
//
// One valve on top: if nothing has EVER laid out here and creatives keep
// failing, the panel is the likelier culprit, so the unit stops asking and
// settles into a slow poll.

static const int64_t kRANPanelSizeTag = 1LL << 62;

// Holds the head when the pool has one this panel may fit, else asks the
// pool for more. Says whether a head is held.
- (BOOL)ran_refill {
    if (self.released || !_configured || _pool == nil) return NO;
    [_pool startLoadingIfNeeded];
    if (_cachedAds.count > 0) return YES;
    if (_retryScheduled) return NO;

    RANNativeAdPoolCachedAd *taken = [_pool takeForSizeKey:[self ran_sizeKey]
                                                  consumer:self];
    if (taken == nil) {
        [_pool requestForSizeKey:[self ran_sizeKey]];
        return NO;
    }

    GADNativeAd *ad = taken.ad;
    @synchronized (_ownedAds) {
        [_ownedAds addObject:ad];
    }
    RANOverlayCachedAd *cached = [[RANOverlayCachedAd alloc] init];
    cached.ad = ad;
    cached.loadedAt = taken.loadedAt;
    [_cachedAds addObject:cached];
    [self ran_scheduleCacheExpiry];
    __weak RANOverlayAd *weakSelf = self;
    __weak GADNativeAd *weakAd = ad;
    [self bindPaidEventForAd:ad
                paidAdUnitID:_adUnitID
                 isCurrentAd:^BOOL{
        RANOverlayAd *strongSelf = weakSelf;
        GADNativeAd *strongAd = weakAd;
        if (strongSelf == nil || strongAd == nil) return NO;
        @synchronized (strongSelf->_ownedAds) {
            return [strongSelf->_ownedAds containsObject:strongAd];
        }
    }];
    [self ran_notifyCurrentState];
    [self ran_prepareHeadFace];
    return YES;
}

// The panel this unit lays creatives out in, as the pool keys sizes: tagged
// so it never meets a rect ad.
- (int64_t)ran_sizeKey {
    RANOverlayStyle *style = _configuredStyle;
    if (style == nil) return kRANNativeAdPoolAnySize;

    float heightRatio = style.heightRatio;
    uint32_t heightBits = 0;
    memcpy(&heightBits, &heightRatio, sizeof(heightBits));
    return kRANPanelSizeTag
            | ((int64_t)(style.fullScreen ? 1 : 0) << 32)
            | (int64_t)heightBits;
}

- (NSTimeInterval)ran_scheduleLayoutRetry {
    [self ran_cancelRetry];
    if (self.released) return kRANNativeAdPoolNoRetryScheduled;

    ++_layoutFailStreak;
    if (!_layoutProven
            && _layoutFailStreak >= kRANMaxUnprovenLayoutFailures) {
        NSLog(@"%@: %ld creatives in a row could not be laid out in this"
               " panel and none has ever rendered here; falling back to a"
               " slow poll", kRANTag, (long)_layoutFailStreak);
        return [self ran_postRetryAfter:kRANUnprovenLayoutRetryDelay];
    }
    return [self ran_postRetryAfter:
            RANNativeAdPoolBackoffDelay(
                    _layoutFailStreak
                  , kRANRetryImmediateLayoutAttempts)];
}

// A creative this panel cannot lay out: back to the pool for a format that
// can, and the next one asked for.
- (void)ran_dropUnrenderableAd:(GADNativeAd *)ad {
    if (ad == nil) return;

    RANOverlayCachedAd *dropped = nil;
    for (RANOverlayCachedAd *cached in _cachedAds) {
        if (cached.ad == ad) {
            dropped = cached;
            break;
        }
    }
    if (dropped != nil) {
        [_cachedAds removeObject:dropped];
        [self ran_giveBackAd:ad
                    loadedAt:dropped.loadedAt
                unfitSizeKey:[self ran_sizeKey]];
    }
    NSTimeInterval retryDelay = [self ran_scheduleLayoutRetry];
    NSLog(@"%@: layout: gave back unrenderable creative, retry in %.0fms"
          , kRANTag, retryDelay * 1000.0);
    [self ran_notifyCurrentState];
}

- (void)ran_giveBackAd:(GADNativeAd *)ad
              loadedAt:(NSTimeInterval)loadedAt
          unfitSizeKey:(int64_t)unfitSizeKey {
    if (ad == nil) return;

    @synchronized (_ownedAds) {
        [_ownedAds removeObject:ad];
    }
    ad.paidEventHandler = nil;
    [_pool giveBackAd:ad loadedAt:loadedAt unfitSizeKey:unfitSizeKey];
}

- (void)ran_forgetAd:(GADNativeAd *)ad {
    if (ad == nil) return;

    @synchronized (_ownedAds) {
        [_ownedAds removeObject:ad];
    }
    ad.paidEventHandler = nil;
    if (_pool != nil) {
        [_pool destroyAd:ad];
    } else {
        ad.delegate = nil;
    }
}

- (NSTimeInterval)ran_postRetryAfter:(NSTimeInterval)delay {
    [self ran_cancelRetry];
    if (self.released) return kRANNativeAdPoolNoRetryScheduled;

    NSTimeInterval resolvedDelay = MAX(0.0, delay);
    _retryScheduled = YES;
    NSInteger generation = ++_retryGeneration;
    dispatch_after(
            dispatch_time(
                    DISPATCH_TIME_NOW
                  , (int64_t)(resolvedDelay * NSEC_PER_SEC))
          , dispatch_get_main_queue()
          , ^{
                if (self.released
                        || generation != self->_retryGeneration) {
                    return;
                }
                self->_retryScheduled = NO;
                [self ran_refill];
            });
    return resolvedDelay;
}

- (void)ran_cancelRetry {
    ++_retryGeneration;
    _retryScheduled = NO;
}

#pragma mark - RANNativeAdPoolConsumer

- (NSInteger)poolWantedCount {
    return _cacheSize;
}

- (BOOL)poolCouldUse:(RANNativeAdPoolCachedAd *)cached {
    return _configured && ![cached.unfitSizes containsObject:@([self ran_sizeKey])];
}

- (void)poolDidChange {
    if (self.released || !_configured || _cachedAds.count > 0) return;

    [self ran_refill];
}

- (void)poolDidStartLoading {
    _isAdLoading = YES;
    [self ran_notifyCurrentState];
    [self notifyLoadingStarted];
}

- (void)poolDidCompleteLoadingWithCode:(int32_t)code
                               message:(NSString *)message
                            retryDelay:(NSTimeInterval)retryDelay {
    _isAdLoading = NO;
    [self ran_notifyCurrentState];
    NSString *failure = code == kRANLoadSuccessCode
            ? @""
            : [NSString stringWithFormat:@"%@ | %@ | layoutFail=%ld | retry=%@"
                      , message
                      , [_pool describe]
                      , (long)_layoutFailStreak
                      , retryDelay < 0.0
                                ? @"not scheduled"
                                : [NSString stringWithFormat:@"%.0fms"
                                          , retryDelay * 1000.0]];
    [self notifyLoadingCompletedWithCode:code
                                message:failure
                            cachedCount:(int32_t)(_pool != nil ? _pool.cachedCount : 0)
                              cacheSize:(int32_t)(_pool != nil ? _pool.capacity : _cacheSize)];
}

- (void)poolAdDidRecordClick:(GADNativeAd *)ad {
    [self nativeAdDidRecordClick:ad];
}

- (void)poolAdWillPresentScreen:(GADNativeAd *)ad {
    [self nativeAdWillPresentScreen:ad];
}

- (void)poolAdDidDismissScreen:(GADNativeAd *)ad {
    [self nativeAdDidDismissScreen:ad];
}

// Not gated on generation: the ad that was clicked may well be the one on
// screen from an earlier load, same as the Android AdListener comment. It IS
// gated on being the ad on screen, though - every cached ad has this object
// for a delegate, and commitAdClick now promotes a waiting close press, so a
// report from an ad nobody can see must not end a show.
- (void)nativeAdDidRecordClick:(GADNativeAd *)nativeAd {
    [RANBaseAd runOnMainThread:^{
        if (self->_activeNativeAd != nativeAd) return;

        [self->_presentation onAdClicked];
    }];
}

// The click's landing page coming to the front. This is what tells a close
// press that its redirect actually arrived - and unlike resigning active, it
// fires for the in-app browser and store sheet too.
- (void)nativeAdWillPresentScreen:(GADNativeAd *)nativeAd {
    [RANBaseAd runOnMainThread:^{
        if (self->_activeNativeAd != nativeAd) return;

        [self->_presentation onAdWillPresentScreen];
    }];
}

- (void)nativeAdDidDismissScreen:(GADNativeAd *)nativeAd {
    [RANBaseAd runOnMainThread:^{
        if (self->_activeNativeAd != nativeAd) return;

        [self->_presentation onAdDidDismissScreen];
    }];
}

// UIScreen is main-thread state, and the Unity thread is the main thread on
// iOS. Any other caller gets the answer the main thread took last - at
// configure time at the latest - rather than wait on the main thread: the
// managed caller holds its ad's lock across this call, and the main thread
// may be waiting on that very lock.
- (CGFloat)resolvePanelHeight {
    if (!NSThread.isMainThread) return _panelHeight;

    RANOverlayStyle *style = _configuredStyle;
    if (style == nil) return _panelHeight;

    _panelHeight = [RANOverlayAdContentView
            resolveInitialPanelHeightForFullScreen:style.fullScreen
                                       heightRatio:style.heightRatio
                                   hasVideoContent:NO];
    return _panelHeight;
}

- (void)showWithShowID:(int32_t)showID
               slideIn:(BOOL)slideIn
       slideOutOnClose:(BOOL)slideOutOnClose
           onCompleted:(RANNativeAdShowCompletedCallback)onCompleted {
    [RANBaseAd runOnMainThread:^{
        if (self.released) {
            [self ran_invokeCompleted:onCompleted
                              showID:showID
                             message:@"Ad released"
                          adConsumed:NO];
            return;
        }
        if (!self->_configured
                || [self ran_isShowing]
                || self->_activeNativeAd != nil) {
            [self ran_invokeCompleted:onCompleted
                              showID:showID
                             message:!self->_configured
                                    ? @"Ad not configured"
                                    : @"Ad already showing"
                          adConsumed:NO];
            return;
        }
        UIViewController *host = [RANBaseAd unityViewController];
        RANOverlayStyle *requestedStyle = self->_configuredStyle;
        [self ran_sweepExpiredCachedAds];
        if (self->_cachedAds.count == 0
                || requestedStyle == nil
                || ![RANBaseAd isViewControllerUsable:host]) {
            [self ran_invokeCompleted:onCompleted
                              showID:showID
                             message:@"Ad not ready"
                          adConsumed:NO];
            return;
        }

        RANOverlayCachedAd *taken = self->_cachedAds.firstObject;
        GADNativeAd *shownAd = taken.ad;
        [self->_cachedAds removeObjectAtIndex:0];
        [self ran_scheduleCacheExpiry];
        self->_activeNativeAd = shownAd;
        self->_activeLoadedAt = taken.loadedAt;
        self->_activeShowCompleted = onCompleted;
        self->_activeShowID = showID;
        [self ran_notifyCurrentState];
        // The head this show took is replaced at once, and earns its face.
        // Both wait a turn so this show takes the face it was promised first.
        dispatch_async(dispatch_get_main_queue(), ^{
            if (self.released) return;

            [self ran_refill];
        });

        RANOverlayAdPresentation *createdPresentation =
                [self ran_takePreparedPresentationForAd:shownAd
                                                 style:requestedStyle];
        if (createdPresentation == nil) {
            createdPresentation = [self ran_createPresentationWithHost:host
                                                                   ad:shownAd
                                                                style:requestedStyle];
            if (![createdPresentation prepare]) {
                [createdPresentation releasePresentation];
                [self ran_completePresentationForAd:shownAd
                                           message:@"Failed to prepare ad "
                                                    "presentation"];
                return;
            }
        }
        self->_presentation = createdPresentation;

        // The prepared-face path already refused unrenderable creatives;
        // this is the path for an ad that never had a face - built seconds
        // before its show, exactly when a creative that fits no layout
        // must NOT slip through.
        if ([createdPresentation isLayoutUnrenderable]) {
            [createdPresentation releasePresentation];
            self->_presentation = nil;
            NSTimeInterval retryDelay = [self ran_scheduleLayoutRetry];
            NSLog(@"%@: layout: refused unrenderable creative at show,"
                   " retry in %.0fms", kRANTag, retryDelay * 1000.0);
            [self ran_completePresentationForAd:shownAd
                                       message:@"Creative cannot be laid"
                                                " out in this panel"
                                         isUnfit:YES];
            return;
        }
        self->_layoutProven = YES;
        self->_layoutFailStreak = 0;

        [createdPresentation setArrivalWithSlideIn:slideIn
                                   slideOutOnClose:slideOutOnClose];
        if (![createdPresentation show]) {
            [createdPresentation releasePresentation];
            if (self->_presentation == createdPresentation) {
                self->_presentation = nil;
            }
            // A non-empty message: the wrapper turns it into a display
            // failure. The empty string that stood here reported a show
            // that never reached the screen as a normal close.
            [self ran_completePresentationForAd:shownAd
                                       message:@"Failed to present ad"];
        }
    }];
}

- (void)hideSlidingOut:(BOOL)slideOut {
    [RANBaseAd runOnMainThread:^{
        RANOverlayAdPresentation *currentPresentation =
                self->_presentation;
        if (currentPresentation != nil && currentPresentation.isShowing) {
            [currentPresentation dismissSlidingOut:slideOut];
        }
    }];
}

- (void)releaseAd {
    if (self.released) return;
    [self markReleased];
    [RANBaseAd runOnMainThread:^{
        self->_isAdLoading = NO;
        [self ran_cancelRetry];
        [self ran_releasePreparedPresentation];

        RANOverlayAdPresentation *currentPresentation =
                self->_presentation;
        self->_presentation = nil;
        [currentPresentation releasePresentation];

        [self->_cacheExpiryTimer invalidate];
        self->_cacheExpiryTimer = nil;
        // The head was never displayed: another format may still show it.
        for (RANOverlayCachedAd *cached in self->_cachedAds) {
            [self ran_giveBackAd:cached.ad
                        loadedAt:cached.loadedAt
                    unfitSizeKey:kRANNativeAdPoolAnySize];
        }
        [self->_cachedAds removeAllObjects];
        [self ran_forgetAd:self->_activeNativeAd];
        self->_activeNativeAd = nil;
        RANNativeAdShowCompletedCallback completed =
                self->_activeShowCompleted;
        int32_t completedShowID = self->_activeShowID;
        self->_activeShowCompleted = NULL;

        self->_configuredStyle = nil;
        [self->_pool leave:self];
        self->_pool = nil;
        [self clearListenerCallbacks];
        [self ran_invokeCompleted:completed
                          showID:completedShowID
                         message:@"Ad released"
                      adConsumed:NO];
    }];
}

- (RANOverlayAdPresentation *)
        ran_createPresentationWithHost:(UIViewController *)host
                                   ad:(GADNativeAd *)ad
                                style:(RANOverlayStyle *)style {
    BOOL closeOnLeftForPresentation = [style resolveCloseOnLeft];
    RANOverlayAdPresentation *createdPresentation =
            [[RANOverlayAdPresentation alloc]
                    initWithViewController:host
                                  nativeAd:ad
                              countdownSec:style.cooldown
                               closeOnLeft:closeOnLeftForPresentation
                               timerOnLeft:[style resolveTimerOnLeft:
                                            closeOnLeftForPresentation]
                                fullScreen:style.fullScreen
                               heightRatio:style.heightRatio
                           backgroundColor:style.backgroundColor
                           redirectOnClose:style.redirectOnClose
                                    layout:style.layout];
    __weak RANOverlayAd *weakSelf = self;
    __weak GADNativeAd *weakAd = ad;
    createdPresentation.onShow = ^{
        RANOverlayAd *strongSelf = weakSelf;
        if (strongSelf != nil
                && !strongSelf.released
                && strongSelf->_activeNativeAd == weakAd) {
            [strongSelf notifyDisplayed];
        }
    };
    createdPresentation.onDismiss = ^{
        RANOverlayAd *strongSelf = weakSelf;
        GADNativeAd *strongAd = weakAd;
        if (strongSelf != nil && strongAd != nil) {
            [strongSelf ran_completePresentationForAd:strongAd message:@""];
        }
    };
    return createdPresentation;
}

- (BOOL)ran_isHeadAd:(GADNativeAd *)ad {
    return ad != nil && _cachedAds.firstObject.ad == ad;
}

// An hour after it arrived a warm ad may no longer be shown, so it goes,
// prepared face and all, and the next one is taken.
- (void)ran_handleCacheExpiry {
    if (self.released) return;

    if ([self ran_removeExpiredCachedAds]) [self ran_notifyCurrentState];
    [self ran_refill];
    [self ran_scheduleCacheExpiry];
}

- (void)ran_scheduleCacheExpiry {
    [_cacheExpiryTimer invalidate];
    _cacheExpiryTimer = nil;
    if (self.released) return;

    NSTimeInterval nextExpiryAt = DBL_MAX;
    for (RANOverlayCachedAd *cached in _cachedAds) {
        nextExpiryAt = MIN(nextExpiryAt, cached.loadedAt + kRANMaxCachedAdAge);
    }
    if (nextExpiryAt == DBL_MAX) return;

    __weak RANOverlayAd *weakSelf = self;
    _cacheExpiryTimer = [NSTimer
            scheduledTimerWithTimeInterval:MAX(0.01, nextExpiryAt - RANNow())
                                   repeats:NO
                                     block:^(NSTimer *timer) {
        [weakSelf ran_handleCacheExpiry];
    }];
}

// The show's own sweep: the clock keeps counting while the device sleeps but
// the timer does not fire, so a phone woken after a long night can hold ads
// past their hour before the timer catches up.
- (void)ran_sweepExpiredCachedAds {
    if (![self ran_removeExpiredCachedAds]) return;

    [self ran_notifyCurrentState];
    [self ran_refill];
    [self ran_scheduleCacheExpiry];
}

- (BOOL)ran_removeExpiredCachedAds {
    NSTimeInterval now = RANNow();
    RANOverlayCachedAd *head = _cachedAds.firstObject;
    // Oldest first, so nothing behind the head can be stale while the head
    // is not: one look answers for the whole queue.
    if (head == nil || now - head.loadedAt < kRANMaxCachedAdAge) return NO;

    // The prepared face is built on the ad about to go, so it goes first.
    [self ran_releasePreparedPresentation];
    NSMutableArray<RANOverlayCachedAd *> *expired = [NSMutableArray array];
    for (RANOverlayCachedAd *cached in _cachedAds) {
        if (now - cached.loadedAt >= kRANMaxCachedAdAge) {
            [expired addObject:cached];
        }
    }
    for (RANOverlayCachedAd *cached in expired) {
        [self ran_forgetAd:cached.ad];
        [_cachedAds removeObject:cached];
    }
    return YES;
}

// The face of the ad at the head, prepaid before any show asks for it.
- (void)ran_prepareHeadFace {
    GADNativeAd *head = _cachedAds.firstObject.ad;
    RANOverlayStyle *style = _configuredStyle;
    if (head == nil || style == nil) {
        [self ran_releasePreparedPresentation];
        return;
    }

    [self ran_preparePresentationForAd:head style:style];
}

- (void)ran_preparePresentationForAd:(GADNativeAd *)ad
                              style:(RANOverlayStyle *)style {
    [self ran_releasePreparedPresentation];
    UIViewController *host = [RANBaseAd unityViewController];
    if (self.released
            || ad == nil
            || style == nil
            || ![self ran_isHeadAd:ad]
            || ![RANBaseAd isViewControllerUsable:host]) {
        return;
    }

    RANOverlayAdPresentation *createdPresentation =
            [self ran_createPresentationWithHost:host ad:ad style:style];
    if (![createdPresentation prepare]
            || self.released
            || ![self ran_isHeadAd:ad]
            || _configuredStyle != style) {
        [createdPresentation releasePresentation];
        return;
    }

    // Prepare built the whole view, so the verdict is already in. A
    // creative no layout can show is dropped here, with the ad still in
    // the cache and nothing on screen - the one moment refusing it costs
    // the player nothing.
    if ([createdPresentation isLayoutUnrenderable]) {
        [createdPresentation releasePresentation];
        [self ran_dropUnrenderableAd:ad];
        return;
    }
    _layoutProven = YES;
    _layoutFailStreak = 0;

    _preparedPresentation = createdPresentation;
    _preparedNativeAd = ad;
    _preparedStyle = style;
    _preparedMediaSignature = RANMediaSignature(ad);
}

- (void)ran_rebuildPreparedPresentationForStyle:(RANOverlayStyle *)requestedStyle {
    // Every runtime style setter publishes a new snapshot and passes through
    // here so a prepared face never keeps stale config.
    // A show in progress is no reason to skip: the rebuild only ever
    // touches the prepared face of the ad at the HEAD of the cache, never
    // the one on screen.
    if (self.released
            || requestedStyle == nil
            || _configuredStyle != requestedStyle
            || _cachedAds.count == 0) {
        return;
    }

    GADNativeAd *ad = _cachedAds.firstObject.ad;
    [self ran_releasePreparedPresentation];
    [self ran_preparePresentationForAd:ad style:requestedStyle];
}

- (RANOverlayAdPresentation *)
        ran_takePreparedPresentationForAd:(GADNativeAd *)ad
                                   style:(RANOverlayStyle *)style {
    if (_preparedPresentation == nil
            || _preparedNativeAd != ad
            || _preparedStyle != style
            || ![RANMediaSignature(ad)
                    isEqualToString:_preparedMediaSignature]) {
        [self ran_releasePreparedPresentation];
        return nil;
    }

    RANOverlayAdPresentation *result = _preparedPresentation;
    _preparedPresentation = nil;
    _preparedNativeAd = nil;
    _preparedStyle = nil;
    return result;
}

- (void)ran_releasePreparedPresentation {
    RANOverlayAdPresentation *prepared = _preparedPresentation;
    _preparedPresentation = nil;
    _preparedNativeAd = nil;
    _preparedStyle = nil;
    [prepared releasePresentation];
}

- (void)ran_completePresentationForAd:(GADNativeAd *)shownAd
                             message:(NSString *)errorMessage {
    [self ran_completePresentationForAd:shownAd message:errorMessage isUnfit:NO];
}

// Identity guard so completion and teardown run once per shown ad. isUnfit:
// refused before display because this panel cannot lay it out, so it goes
// back to the pool for a format that can.
- (void)ran_completePresentationForAd:(GADNativeAd *)shownAd
                             message:(NSString *)errorMessage
                             isUnfit:(BOOL)isUnfit {
    if (_activeNativeAd != shownAd) return;

    RANNativeAdShowCompletedCallback onCompleted = _activeShowCompleted;
    int32_t completedShowID = _activeShowID;
    _activeShowCompleted = NULL;
    _activeNativeAd = nil;
    _presentation = nil;
    if (isUnfit) {
        [self ran_giveBackAd:shownAd loadedAt:_activeLoadedAt unfitSizeKey:[self ran_sizeKey]];
    } else {
        [self ran_forgetAd:shownAd];
    }

    // State first, completion second - the Android side carries the reason.
    // It no longer decides anything, because the completion takes the cache
    // count with it; it only keeps the two accounts in agreement.
    [self ran_notifyCurrentState];
    [self ran_invokeCompleted:onCompleted
                      showID:completedShowID
                     message:errorMessage ?: @""
                  adConsumed:YES];
}

- (BOOL)ran_isShowing {
    RANOverlayAdPresentation *currentPresentation = _presentation;
    return currentPresentation != nil && currentPresentation.isShowing;
}

- (void)ran_notifyCurrentState {
    BOOL isReady = _configured
            && !self.released
            && _activeNativeAd == nil
            && _cachedAds.count > 0;
    BOOL isLoading = _configured && !self.released && _isAdLoading;
    [self notifyStateChangedWithReady:isReady loading:isLoading];
}

// The cache count is read here rather than passed in, so every completion
// reports the cache as it stands at the instant it leaves - there is no
// call site that could hand over a number from earlier. The Android side
// reads it the same way, in NotifyCompleted.
- (void)ran_invokeCompleted:(RANNativeAdShowCompletedCallback)onCompleted
                    showID:(int32_t)showID
                   message:(NSString *)errorMessage
                adConsumed:(BOOL)adConsumed {
    if (onCompleted == NULL) return;
    onCompleted(
            self.instanceID
          , showID
          , (errorMessage ?: @"").UTF8String
          , adConsumed
          , (int32_t)_cachedAds.count);
}

@end