#import "RANRectAdSlot.h"

#import "RANRectAdPresentation.h"

static NSString *const kRANTag = @"RectAd";

static const NSInteger kRANRetryImmediateLayoutAttempts = 2;
static const NSInteger kRANMaxUnprovenLayoutFailures = 20;
static const NSTimeInterval kRANUnprovenLayoutRetryDelay = 300;
// Rotation follows appearances rather than a clock: a slot that keeps being
// shown and hidden earns a fresh ad every time it comes back, and the dwell
// interval only covers a slot that never goes away.
static const NSTimeInterval kRANDwellRefreshInterval = 30;
// How long an ad has to have been on screen before a hide is allowed to
// rotate it away. A second is a glance; the ad is gone before it has been
// read, and the one that replaces it burns an impression on a slot the player
// is leaving.
static const NSTimeInterval kRANMinDwell = 4;
static const NSTimeInterval kRANMinSwapInterval = 3;
static const NSTimeInterval kRANWatchdogInterval = 1;
static const NSTimeInterval kRANForegroundRecheckDelay = 1;
static const NSTimeInterval kRANActiveEntryVisibilityTimeout = 3;
// Last-resort net only; the presentation bounds its own waits and ends as a
// dismissal, which is the path that normally clears a slot.
static const NSTimeInterval kRANMaterializingEntryTimeout = 20;
static const NSTimeInterval kRANNoTimestamp = -1;

@interface RANRectAdSlotRect : NSObject
@property (nonatomic) CGFloat x;
@property (nonatomic) CGFloat y;
@property (nonatomic) CGFloat width;
@property (nonatomic) CGFloat height;
@property (nonatomic) CGFloat roundCorner;
@end

@implementation RANRectAdSlotRect
@end

@interface RANRectAdDisplayEntry : NSObject
@property (nonatomic, strong) GADNativeAd *ad;
@property (nonatomic, strong) RANRectAdPresentation *presentation;
@property (nonatomic) NSTimeInterval loadedAt;
@property (nonatomic) NSTimeInterval accumulatedVisible;
@property (nonatomic) NSTimeInterval visibleStartedAt;
@property (nonatomic) NSTimeInterval visibleWaitStartedAt;
@property (nonatomic) NSTimeInterval displayedAt;
@property (nonatomic) BOOL ready;
@property (nonatomic) BOOL actuallyVisible;
@end

@implementation RANRectAdDisplayEntry

- (instancetype)init {
    self = [super init];
    if (self != nil) {
        _visibleStartedAt = kRANNoTimestamp;
        _visibleWaitStartedAt = kRANNoTimestamp;
        _displayedAt = kRANNoTimestamp;
    }
    return self;
}

@end

@interface RANRectAdSlot () <RANRectAdPresentationListener>
@end

@implementation RANRectAdSlot {
    // The owner holds the slots for the life of the unit; weak back-reference
    // so the pair cannot keep each other alive after Release.
    __weak RANRectAd *_owner;
    RANRectAdSlotRect *_rect;
    RANRectAdDisplayEntry *_activeEntry;
    RANRectAdDisplayEntry *_materializingEntry;
    NSTimeInterval _lastSwapAt;
    BOOL _configured;
    BOOL _visibleRequested;
    BOOL _layoutRetryScheduled;
    NSInteger _layoutFailStreak;
    BOOL _layoutProven;

    NSTimer *_refreshTimer;
    NSTimer *_layoutRetryTimer;
    NSTimer *_entryExpiryTimer;
    NSTimer *_watchdogTimer;
    NSTimer *_foregroundRecheckTimer;
}

static NSTimeInterval RANNow(void) {
    return [NSProcessInfo processInfo].systemUptime;
}

- (instancetype)initWithOwner:(RANRectAd *)owner {
    self = [super init];
    if (self == nil) return nil;

    _owner = owner;
    _lastSwapAt = kRANNoTimestamp;
    return self;
}

- (BOOL)ran_isReleased {
    RANRectAd *owner = _owner;
    return owner == nil || owner.released;
}

// ---------------------------------------------------------------------------
// Operations; the owner already hopped to main and vetted the host
// ---------------------------------------------------------------------------

- (void)configureWithX:(CGFloat)xPt
                     y:(CGFloat)yPt
                 width:(CGFloat)widthPt
                height:(CGFloat)heightPt
           roundCorner:(CGFloat)roundCornerPt {
    RANRectAdSlotRect *newRect = [[RANRectAdSlotRect alloc] init];
    newRect.x = xPt;
    newRect.y = yPt;
    newRect.width = MAX(1, widthPt);
    newRect.height = MAX(1, heightPt);
    newRect.roundCorner = MAX(0, roundCornerPt);
    RANRectAdSlotRect *oldRect = _rect;
    BOOL rectChanged = _configured
            && (oldRect == nil
                    || oldRect.x != newRect.x
                    || oldRect.y != newRect.y
                    || oldRect.width != newRect.width
                    || oldRect.height != newRect.height
                    || oldRect.roundCorner != newRect.roundCorner);

    _visibleRequested = NO;
    [_refreshTimer invalidate];
    _refreshTimer = nil;
    [self ran_pauseVisibleTimer:_activeEntry];
    [_activeEntry.presentation setVisible:NO];
    [_materializingEntry.presentation setVisible:NO];

    if (rectChanged) {
        // A different rect has to prove itself again.
        _layoutFailStreak = 0;
        _layoutProven = NO;
        [self ran_returnEntry:_materializingEntry unfitSizeKey:kRANNativeAdPoolAnySize];
        _materializingEntry = nil;
        [self ran_destroyEntry:_activeEntry];
        _activeEntry = nil;
    }

    _rect = newRect;
    _configured = YES;
    [self ran_removeExpiredEntries];
    // Configuring hides the rect, so it lets go of what a hidden one may not
    // hold; the next show takes an ad for the new rect.
    [self ran_releaseHeldAds];
    [_watchdogTimer invalidate];
    _watchdogTimer = nil;
    [self ran_scheduleEntryExpiry];
}

- (void)show {
    [self ran_showCore];
    // The entry on screen ages like any other. Re-armed after every show,
    // because a slot that stays open - a popup left up while the phone sits
    // in a pocket - has no other moment that would catch it.
    [self ran_scheduleEntryExpiry];
    [self ran_scheduleWatchdog];
}

- (void)ran_showCore {
    RANRectAd *owner = _owner;
    if (owner == nil || owner.released || !_configured) {
        NSLog(@"%@: Show ignored before Configure or after Release", kRANTag);
        return;
    }

    NSLog(@"%@: Show: active=%@ materializing=%@ cached=%ld"
          , kRANTag
          , [self ran_describeEntry:_activeEntry]
          , [self ran_describeEntry:_materializingEntry]
          , (long)[owner cachedCount]);
    _visibleRequested = YES;

    [self ran_removeExpiredEntries];
    [_activeEntry.presentation requestDisplayNotification];
    [_materializingEntry.presentation requestDisplayNotification];
    BOOL readyForShow = _activeEntry != nil
            || (_materializingEntry != nil && _materializingEntry.ready);
    if (!readyForShow) [owner notifyShowNotReady];
    if (_materializingEntry != nil) {
        if (_materializingEntry.ready) {
            // Already laid out, so open straight onto it. Showing the
            // outgoing ad first is what made one appear and get replaced a
            // moment later.
            [_materializingEntry.presentation setVisible:YES];
        } else {
            [self ran_showActiveEntry];
            if (_activeEntry == nil) {
                [_materializingEntry.presentation setVisible:YES];
            }
        }
        return;
    }
    if (_activeEntry != nil) {
        if ([self ran_showActiveEntry]) [self ran_scheduleRefresh];
        return;
    }
    if ([self ran_hasFittingAd]) {
        [self presentCachedAd];
        return;
    }

    [self ran_requestFittingAd];
}

- (void)hide {
    if ([self ran_isReleased]) return;

    RANRectAd *owner = _owner;
    NSLog(@"%@: Hide: active=%@ materializing=%@ cached=%ld"
          , kRANTag
          , [self ran_describeEntry:_activeEntry]
          , [self ran_describeEntry:_materializingEntry]
          , (long)[owner cachedCount]);
    _visibleRequested = NO;
    [_refreshTimer invalidate];
    _refreshTimer = nil;
    [self ran_pauseVisibleTimer:_activeEntry];
    [_activeEntry.presentation setVisible:NO];
    [_materializingEntry.presentation setVisible:NO];
    [self ran_releaseHeldAds];
    _activeEntry.visibleWaitStartedAt = kRANNoTimestamp;
    [_watchdogTimer invalidate];
    _watchdogTimer = nil;
    [self ran_scheduleEntryExpiry];
}

// A hidden rect holds nothing another placement could use: an ad it took and
// never displayed goes back to the pool as old as it was, and a displayed one
// that had its turn is destroyed. A displayed one still short of kRANMinDwell
// stays for this rect's next show, as no other placement could show it: its
// impression is already counted.
- (void)ran_releaseHeldAds {
    [_refreshTimer invalidate];
    _refreshTimer = nil;
    [self ran_cancelLayoutRetry];
    [self ran_returnEntry:_materializingEntry unfitSizeKey:kRANNativeAdPoolAnySize];
    _materializingEntry = nil;
    [self ran_pauseVisibleTimer:_activeEntry];
    if ([self ran_isActiveEntryConsumed]) {
        [self ran_destroyEntry:_activeEntry];
        _activeEntry = nil;
    }
}

- (void)setPositionX:(CGFloat)xPt y:(CGFloat)yPt {
    if ([self ran_isReleased] || !_configured || _rect == nil) {
        NSLog(@"%@: SetPosition ignored before Configure or after Release"
              , kRANTag);
        return;
    }
    _rect.x = xPt;
    _rect.y = yPt;
    [_materializingEntry.presentation setPositionX:xPt y:yPt];
    [_activeEntry.presentation setPositionX:xPt y:yPt];
}

- (void)releaseOnMain {
    _layoutRetryScheduled = NO;
    _visibleRequested = NO;
    [_refreshTimer invalidate];
    [_layoutRetryTimer invalidate];
    [_entryExpiryTimer invalidate];
    [_watchdogTimer invalidate];
    [_foregroundRecheckTimer invalidate];
    [self ran_pauseVisibleTimer:_activeEntry];
    [self ran_destroyEntry:_materializingEntry];
    _materializingEntry = nil;
    [self ran_destroyEntry:_activeEntry];
    _activeEntry = nil;
    _rect = nil;
}

// The configured size in pixels, packed, or kRANNativeAdPoolAnySize: pixels,
// as Android keys them, so the same rect is the same key on both.
- (int64_t)sizeKey {
    if (!_configured || _rect == nil) return kRANNativeAdPoolAnySize;

    CGFloat scale = MAX(1, UIScreen.mainScreen.nativeScale);
    int64_t width = (int64_t)llround(_rect.width * scale);
    int64_t height = (int64_t)llround(_rect.height * scale);
    return (width << 32) | (height & 0xffffffffLL);
}

- (BOOL)ran_hasFittingAd {
    RANRectAd *owner = _owner;
    int64_t sizeKey = [self sizeKey];
    return owner != nil
            && sizeKey != kRANNativeAdPoolAnySize
            && [owner hasCachedAdForSizeKey:sizeKey];
}

- (void)ran_requestFittingAd {
    [_owner requestLoadForSizeKey:[self sizeKey]];
}

// A click on the ad, if this slot holds it.
- (void)commitAdClickForAd:(GADNativeAd *)ad {
    if (_activeEntry.ad == ad) [_activeEntry.presentation commitAdClick];
    if (_materializingEntry.ad == ad) [_materializingEntry.presentation commitAdClick];
}

- (BOOL)wantsCachedAd {
    return _configured
            && _visibleRequested
            && _activeEntry == nil
            && _materializingEntry == nil;
}

// ---------------------------------------------------------------------------
// Materialization
// ---------------------------------------------------------------------------

- (BOOL)ran_isHostVisible {
    // A backgrounded host is still usable but does not draw; everything that
    // depends on drawing waits for the foreground instead of failing.
    UIViewController *host = [RANBaseAd unityViewController];
    return [RANBaseAd isViewControllerUsable:host]
            && UIApplication.sharedApplication.applicationState
                    == UIApplicationStateActive;
}

// Only while shown: a hidden rect holds no ad another placement could use.
- (void)presentCachedAd {
    RANRectAd *owner = _owner;
    UIViewController *host = [RANBaseAd unityViewController];
    if (owner == nil
            || owner.released
            || !_configured
            || !_visibleRequested
            || _materializingEntry != nil
            || ![self ran_hasFittingAd]
            || ![RANBaseAd isViewControllerUsable:host]) {
        return;
    }
    if (![self ran_isHostVisible]) {
        // The geometry observation only advances on draw passes; presenting
        // into a window that is not drawing parks the slot.
        [self ran_scheduleForegroundRecheck];
        return;
    }

    RANNativeAdPoolCachedAd *next = [owner takeCachedAdForSizeKey:[self sizeKey]];
    if (next == nil) {
        [self ran_requestFittingAd];
        return;
    }

    RANRectAdDisplayEntry *entry = [[RANRectAdDisplayEntry alloc] init];
    entry.ad = next.ad;
    entry.loadedAt = next.loadedAt;
    RANRectAdPresentation *presentation =
            [[RANRectAdPresentation alloc]
                    initWithHostViewController:host
                                      nativeAd:next.ad
                                             x:_rect.x
                                             y:_rect.y
                                         width:_rect.width
                                        height:_rect.height
                               backgroundColor:[owner slotBackgroundColor]
                                   roundCorner:_rect.roundCorner
                                      listener:self];
    entry.presentation = presentation;
    _materializingEntry = entry;
    // Armed where the entry is BORN, not where some caller remembered.
    // Eight call sites reach this method and three of them re-armed
    // afterwards, which held only while a timer happened to be pending
    // already; once an expiry emptied the slot the schedule went quiet, and
    // the next ad to arrive inherited no deadline at all. If the
    // presentation below fails and this entry is destroyed, the pending
    // timer is a harmless early wakeup that re-arms on what it finds.
    [self ran_scheduleEntryExpiry];

    [presentation setVisible:(_visibleRequested && _activeEntry == nil)];
    if (![presentation show]) {
        NSString *failureMessage = presentation.failureMessage;
        _materializingEntry = nil;
        [self ran_returnEntry:entry unfitSizeKey:[self sizeKey]];
        [self ran_showActiveEntry];
        NSTimeInterval retryDelay = [self ran_scheduleLayoutRetry];
        [owner notifyPresentationFailedWithCode:RANNativeAdInternalPresentationError
                                        message:[self ran_failureMessage:
                                           failureMessage
                                                   ?: @"Rect ad presentation "
                                                       "failed before display"
                                                         retryDelay:retryDelay]];
    }
    [self ran_scheduleWatchdog];
}

- (void)rectAdPresentationReady {
    RANRectAdDisplayEntry *entry = _materializingEntry;
    if ([self ran_isReleased] || entry == nil) return;
    entry.ready = YES;
    if (_visibleRequested) [entry.presentation setVisible:YES];
}

- (void)rectAdPresentationDisplayed {
    RANRectAd *owner = _owner;
    RANRectAdDisplayEntry *entry = _materializingEntry ?: _activeEntry;
    if (owner == nil || owner.released || entry == nil) return;
    if (!_visibleRequested) {
        [entry.presentation setVisible:NO];
        return;
    }

    if (_materializingEntry == entry) {
        RANRectAdDisplayEntry *previous = _activeEntry;
        _activeEntry = entry;
        _materializingEntry = nil;
        _layoutFailStreak = 0;
        _layoutProven = YES;
        _lastSwapAt = RANNow();
        [self ran_destroyEntry:previous];
    }
    if (entry.displayedAt == kRANNoTimestamp) entry.displayedAt = RANNow();

    if (entry.actuallyVisible) {
        [self ran_resumeVisibleTimer:entry];
    } else {
        [self ran_pauseVisibleTimer:entry];
    }
    [self ran_scheduleWatchdog];
    [owner notifyDisplayed];
    [self ran_scheduleRefresh];
}

- (void)rectAdPresentationActualVisibilityChanged:(BOOL)isActuallyVisible {
    // Both entries report through here; refresh only follows the active one.
    RANRectAdDisplayEntry *materializing = _materializingEntry;
    RANRectAdDisplayEntry *active = _activeEntry;
    RANRectAdDisplayEntry *entry = nil;
    if (materializing != nil
            && materializing.actuallyVisible != isActuallyVisible) {
        entry = materializing;
    }
    if (active != nil && active.actuallyVisible != isActuallyVisible) {
        entry = entry ?: active;
    }
    if (entry == nil) return;

    entry.actuallyVisible = isActuallyVisible;
    [self ran_markVisibilityWait:entry];
    if (entry != _activeEntry) return;

    if (_visibleRequested && isActuallyVisible) {
        [self ran_resumeVisibleTimer:entry];
        [self ran_scheduleRefresh];
    } else {
        [_refreshTimer invalidate];
        _refreshTimer = nil;
        [self ran_pauseVisibleTimer:entry];
    }
    [self ran_scheduleWatchdog];
}

- (void)rectAdPresentationDismissed {
    // Failure paths funnel through dismissal; whichever entry lost its
    // presentation is cleared and the retry ladder decides what follows.
    RANRectAd *owner = _owner;
    if (owner == nil) return;

    RANRectAdDisplayEntry *dismissed = nil;
    if (_materializingEntry != nil
            && ![_materializingEntry.presentation isShowingPresentation]) {
        dismissed = _materializingEntry;
        _materializingEntry = nil;
    } else if (_activeEntry != nil
            && ![_activeEntry.presentation isShowingPresentation]) {
        dismissed = _activeEntry;
        [self ran_pauseVisibleTimer:dismissed];
        _activeEntry = nil;
    }
    if (dismissed == nil) return;

    NSString *failureMessage = dismissed.presentation.failureMessage;
    // A creative that failed before it was ever displayed goes back for a
    // rect of another size; one that was displayed is spent.
    if (failureMessage != nil && dismissed.displayedAt == kRANNoTimestamp) {
        [owner returnCachedAd:dismissed.ad
                     loadedAt:dismissed.loadedAt
                 unfitSizeKey:[self sizeKey]];
    } else {
        [owner destroyAd:dismissed.ad];
    }

    if (owner.released) return;
    [self ran_showActiveEntry];
    NSTimeInterval retryDelay = [self ran_scheduleLayoutRetry];
    [self ran_scheduleWatchdog];
    if (failureMessage != nil) {
        [owner notifyPresentationFailedWithCode:RANNativeAdInternalPresentationError
                                        message:[self ran_failureMessage:failureMessage
                                                             retryDelay:retryDelay]];
    }
}

// ---------------------------------------------------------------------------
// Rotation
// ---------------------------------------------------------------------------

- (BOOL)ran_showActiveEntry {
    if (!_visibleRequested || _activeEntry == nil) return NO;
    if (![_activeEntry.presentation setVisible:YES]) return NO;
    if (_activeEntry.actuallyVisible) {
        [self ran_resumeVisibleTimer:_activeEntry];
    }
    return YES;
}

- (BOOL)ran_isActiveEntryConsumed {
    return _activeEntry != nil
            && _activeEntry.displayedAt != kRANNoTimestamp
            && [self ran_currentVisibleDuration:_activeEntry] >= kRANMinDwell;
}

// Rotate to a warm ad once the one on screen has had its turn; swaps are
// spaced so a burst of show and hide cannot drain the cache.
- (void)ran_trySwapActiveEntry {
    RANRectAd *owner = _owner;
    if (owner == nil
            || owner.released
            || _materializingEntry != nil
            || _activeEntry == nil
            || ![self ran_hasFittingAd]
            || ![self ran_isActiveEntryConsumed]) {
        return;
    }
    NSTimeInterval now = RANNow();
    if (_lastSwapAt != kRANNoTimestamp
            && now - _lastSwapAt < kRANMinSwapInterval) {
        return;
    }
    [self presentCachedAd];
}

- (void)ran_scheduleRefresh {
    [_refreshTimer invalidate];
    _refreshTimer = nil;
    if ([self ran_isReleased]
            || !_visibleRequested
            || _activeEntry == nil
            || !_activeEntry.actuallyVisible) {
        return;
    }

    NSTimeInterval remaining = MAX(
            0
          , kRANDwellRefreshInterval
                    - [self ran_currentVisibleDuration:_activeEntry]);
    __weak RANRectAdSlot *weakSelf = self;
    _refreshTimer = [NSTimer scheduledTimerWithTimeInterval:MAX(0.01, remaining)
                                                    repeats:NO
                                                      block:^(NSTimer *timer) {
        [weakSelf ran_handleRefresh];
    }];
}

- (void)ran_handleRefresh {
    if ([self ran_isReleased]) return;
    if (!_visibleRequested
            || _activeEntry == nil
            || !_activeEntry.actuallyVisible) {
        [self ran_pauseVisibleTimer:_activeEntry];
        return;
    }

    [self ran_resumeVisibleTimer:_activeEntry];
    NSTimeInterval remaining = kRANDwellRefreshInterval
            - [self ran_currentVisibleDuration:_activeEntry];
    if (remaining > 0) {
        [self ran_scheduleRefresh];
        return;
    }

    [self ran_trySwapActiveEntry];
    // Start the next dwell window whether or not a swap happened, or the
    // trigger re-arms every tick.
    _activeEntry.accumulatedVisible = 0;
    _activeEntry.visibleStartedAt =
            (_visibleRequested && _activeEntry.actuallyVisible)
                    ? RANNow()
                    : kRANNoTimestamp;
    [self ran_scheduleRefresh];
}

// ---------------------------------------------------------------------------
// Layout retry ladder
// ---------------------------------------------------------------------------

// A layout failure is a property of this creative, not of inventory; the
// first attempts fetch a replacement straight away and only a long run of
// failures treats the rect itself as unusable.
- (NSTimeInterval)ran_scheduleLayoutRetry {
    [self ran_cancelLayoutRetry];
    if ([self ran_isReleased] || !_configured) return kRANRectAdNoRetryScheduled;

    ++_layoutFailStreak;
    if (!_layoutProven
            && _layoutFailStreak >= kRANMaxUnprovenLayoutFailures) {
        NSLog(@"%@: %ld creatives in a row failed to lay out in %@ and none "
               "has ever rendered there; falling back to a slow poll"
              , kRANTag
              , (long)_layoutFailStreak
              , [self ran_describeRequestedRect]);
        return [self ran_postLayoutRetry:kRANUnprovenLayoutRetryDelay];
    }
    return [self ran_postLayoutRetry:RANRectAdBackoffDelay(
            _layoutFailStreak, kRANRetryImmediateLayoutAttempts)];
}

- (NSTimeInterval)ran_postLayoutRetry:(NSTimeInterval)delay {
    [self ran_cancelLayoutRetry];
    if ([self ran_isReleased] || !_configured) return kRANRectAdNoRetryScheduled;

    _layoutRetryScheduled = YES;
    __weak RANRectAdSlot *weakSelf = self;
    _layoutRetryTimer = [NSTimer scheduledTimerWithTimeInterval:MAX(0.01, delay)
                                                        repeats:NO
                                                          block:^(NSTimer *timer) {
        [weakSelf ran_handleLayoutRetry];
    }];
    return delay;
}

- (void)ran_cancelLayoutRetry {
    [_layoutRetryTimer invalidate];
    _layoutRetryTimer = nil;
    _layoutRetryScheduled = NO;
}

- (void)ran_handleLayoutRetry {
    _layoutRetryScheduled = NO;
    RANRectAd *owner = _owner;
    if (owner == nil || owner.released || !_configured || !_visibleRequested) return;
    if ([self ran_hasFittingAd]) {
        [self presentCachedAd];
        return;
    }

    [self ran_requestFittingAd];
}

// ---------------------------------------------------------------------------
// Watchdogs and expiry
// ---------------------------------------------------------------------------

- (void)ran_markVisibilityWait:(RANRectAdDisplayEntry *)entry {
    if (entry == nil) return;
    if (!_visibleRequested || entry.actuallyVisible) {
        entry.visibleWaitStartedAt = kRANNoTimestamp;
        return;
    }
    if (entry.visibleWaitStartedAt == kRANNoTimestamp) {
        entry.visibleWaitStartedAt = RANNow();
    }
}

// Only armed while something is actually waiting to be seen.
- (void)ran_scheduleWatchdog {
    [_watchdogTimer invalidate];
    _watchdogTimer = nil;
    if ([self ran_isReleased] || !_visibleRequested) return;
    if (![self ran_isHostVisible]) {
        _activeEntry.visibleWaitStartedAt = kRANNoTimestamp;
        _materializingEntry.visibleWaitStartedAt = kRANNoTimestamp;
        [self ran_scheduleForegroundRecheck];
        return;
    }

    [self ran_markVisibilityWait:_activeEntry];
    [self ran_markVisibilityWait:_materializingEntry];
    if (_materializingEntry == nil && _activeEntry == nil) return;

    __weak RANRectAdSlot *weakSelf = self;
    _watchdogTimer = [NSTimer
            scheduledTimerWithTimeInterval:kRANWatchdogInterval
                                   repeats:NO
                                     block:^(NSTimer *timer) {
        [weakSelf ran_handleWatchdog];
    }];
}

// Without this both entries can park forever; either state would otherwise
// only clear after the cache age limit or an app restart.
- (void)ran_handleWatchdog {
    if ([self ran_isReleased]) return;
    if (![self ran_isHostVisible]) {
        [self ran_scheduleWatchdog];
        return;
    }

    NSTimeInterval now = RANNow();
    if (_materializingEntry != nil) {
        if (_materializingEntry.visibleWaitStartedAt == kRANNoTimestamp
                || now - _materializingEntry.visibleWaitStartedAt
                        < kRANMaterializingEntryTimeout) {
            [self ran_scheduleWatchdog];
            return;
        }

        NSLog(@"%@: Rect ad materializing slot was never displayed within "
               "%gms; dropping it and reloading"
              , kRANTag, kRANMaterializingEntryTimeout * 1000);
        RANRectAdDisplayEntry *stuck = _materializingEntry;
        _materializingEntry = nil;
        [self ran_destroyEntry:stuck];
        [self ran_showActiveEntry];
        [self ran_requestReplacementAd];
        [self ran_scheduleWatchdog];
        return;
    }

    if (_visibleRequested
            && _activeEntry != nil
            && !_activeEntry.actuallyVisible
            && _activeEntry.visibleWaitStartedAt != kRANNoTimestamp
            && now - _activeEntry.visibleWaitStartedAt
                    >= kRANActiveEntryVisibilityTimeout) {
        NSLog(@"%@: Rect ad active slot never became visible within %gms; "
               "dropping it and reloading"
              , kRANTag, kRANActiveEntryVisibilityTimeout * 1000);
        RANRectAdDisplayEntry *stuck = _activeEntry;
        [self ran_pauseVisibleTimer:stuck];
        _activeEntry = nil;
        [self ran_destroyEntry:stuck];
        [self ran_requestReplacementAd];
    }

    [self ran_scheduleWatchdog];
}

- (void)ran_requestReplacementAd {
    RANRectAd *owner = _owner;
    if (owner != nil && [self ran_hasFittingAd]) {
        [self presentCachedAd];
        return;
    }
    // Through the retry backoff so a slot that keeps failing to display
    // cannot churn ad requests.
    [self ran_scheduleLayoutRetry];
}

- (void)ran_scheduleForegroundRecheck {
    [_foregroundRecheckTimer invalidate];
    _foregroundRecheckTimer = nil;
    RANRectAd *owner = _owner;
    if (owner == nil || owner.released || [self ran_isHostVisible]) return;
    if (!_visibleRequested) return;

    __weak RANRectAdSlot *weakSelf = self;
    _foregroundRecheckTimer = [NSTimer
            scheduledTimerWithTimeInterval:kRANForegroundRecheckDelay
                                   repeats:NO
                                     block:^(NSTimer *timer) {
        [weakSelf ran_handleForegroundRecheck];
    }];
}

- (void)ran_handleForegroundRecheck {
    RANRectAd *owner = _owner;
    if (owner == nil || owner.released) return;
    if (![self ran_isHostVisible]) {
        [self ran_scheduleForegroundRecheck];
        return;
    }

    // Age is measured on the system clock, which counts the hours the phone
    // spent asleep; the expiry timer does not fire while the app is
    // suspended. So the timer alone cannot be trusted across a long sleep,
    // and coming back to the foreground is where the entry gets read for age
    // instead of waited on. The overlay's cache learned this first and
    // sweeps on the way into every show.
    [self ran_removeExpiredEntries];

    // Coming back to the foreground is a resume, not a rotation. Present only
    // what could not be presented while the window was dark - an empty slot -
    // and leave whatever survived the background on screen. presentCachedAd
    // is also the swap, so calling it here for a slot that already has an ad
    // turns every unfocus and focus into a rotation, with none of the dwell
    // the two rotation triggers apply.
    if ([self wantsCachedAd] && [self ran_hasFittingAd]) [self presentCachedAd];
    [self ran_showActiveEntry];
    [self ran_scheduleWatchdog];
    [self ran_scheduleRefresh];
}

- (void)ran_scheduleEntryExpiry {
    [_entryExpiryTimer invalidate];
    _entryExpiryTimer = nil;
    if ([self ran_isReleased]) return;

    NSTimeInterval nextExpiryAt = DBL_MAX;
    if (_materializingEntry != nil) {
        nextExpiryAt = MIN(
                nextExpiryAt
              , _materializingEntry.loadedAt + kRANRectAdMaxCachedAdAge);
    }
    if (_activeEntry != nil) {
        nextExpiryAt = MIN(
                nextExpiryAt, _activeEntry.loadedAt + kRANRectAdMaxCachedAdAge);
    }
    if (nextExpiryAt == DBL_MAX) return;

    __weak RANRectAdSlot *weakSelf = self;
    _entryExpiryTimer = [NSTimer
            scheduledTimerWithTimeInterval:MAX(0.01, nextExpiryAt - RANNativeAdPoolNow())
                                   repeats:NO
                                     block:^(NSTimer *timer) {
        [weakSelf ran_handleEntryExpiry];
    }];
}

// An expired creative may not stay on screen, so the sweep runs whether the
// rect is shown or hidden; only a shown one takes a replacement. The
// replacement deliberately skips the dwell and swap-interval floors - the old
// entry is already destroyed by then, so there is nothing left for a dwell
// rule to protect. Do not "restore" that check here. With nothing warm behind
// it the rect goes blank until a load lands - the only honest outcome: the
// impression count belongs to the SDK, so the pack cannot keep the ad up and
// stop counting it.
- (void)ran_handleEntryExpiry {
    RANRectAd *owner = _owner;
    if (owner == nil || owner.released) return;

    [self ran_removeExpiredEntries];
    if (!_configured || !_visibleRequested) {
        [self ran_scheduleEntryExpiry];
        return;
    }
    if ([self ran_hasFittingAd]) {
        [self presentCachedAd];
    } else {
        [self ran_requestFittingAd];
    }
    [self ran_scheduleEntryExpiry];
}

- (void)ran_removeExpiredEntries {
    NSTimeInterval now = RANNativeAdPoolNow();
    if (_materializingEntry != nil
            && now - _materializingEntry.loadedAt >= kRANRectAdMaxCachedAdAge) {
        [self ran_destroyEntry:_materializingEntry];
        _materializingEntry = nil;
    }
    if (_activeEntry != nil
            && now - _activeEntry.loadedAt >= kRANRectAdMaxCachedAdAge) {
        [self ran_pauseVisibleTimer:_activeEntry];
        [self ran_destroyEntry:_activeEntry];
        _activeEntry = nil;
    }
}

// ---------------------------------------------------------------------------
// Visible-time accounting and teardown
// ---------------------------------------------------------------------------

- (void)ran_resumeVisibleTimer:(RANRectAdDisplayEntry *)entry {
    if (entry == nil
            || entry.visibleStartedAt != kRANNoTimestamp
            || !_visibleRequested
            || !entry.actuallyVisible) {
        return;
    }
    entry.visibleStartedAt = RANNow();
}

- (void)ran_pauseVisibleTimer:(RANRectAdDisplayEntry *)entry {
    if (entry == nil || entry.visibleStartedAt == kRANNoTimestamp) return;
    entry.accumulatedVisible += MAX(0, RANNow() - entry.visibleStartedAt);
    entry.visibleStartedAt = kRANNoTimestamp;
}

- (NSTimeInterval)ran_currentVisibleDuration:(RANRectAdDisplayEntry *)entry {
    if (entry == nil) return 0;
    NSTimeInterval duration = entry.accumulatedVisible;
    if (entry.visibleStartedAt != kRANNoTimestamp) {
        duration += MAX(0, RANNow() - entry.visibleStartedAt);
    }
    return duration;
}

- (void)ran_destroyEntry:(RANRectAdDisplayEntry *)entry {
    if (entry == nil) return;
    [entry.presentation releasePresentation];
    [_owner destroyAd:entry.ad];
}

// An entry never displayed: its view goes, its ad back to the pool, marked as
// not fitting unfitSizeKey unless that is kRANNativeAdPoolAnySize.
- (void)ran_returnEntry:(RANRectAdDisplayEntry *)entry
           unfitSizeKey:(int64_t)unfitSizeKey {
    if (entry == nil) return;
    if (entry.displayedAt != kRANNoTimestamp) {
        [self ran_destroyEntry:entry];
        return;
    }

    [entry.presentation releasePresentation];
    [_owner returnCachedAd:entry.ad
                  loadedAt:entry.loadedAt
              unfitSizeKey:unfitSizeKey];
}

// ---------------------------------------------------------------------------
// Messages
// ---------------------------------------------------------------------------

- (NSString *)ran_describeEntry:(RANRectAdDisplayEntry *)entry {
    if (entry == nil) return @"none";
    return [NSString stringWithFormat:
            @"{ready=%@,visible=%@,displayed=%@}"
          , entry.ready ? @"true" : @"false"
          , entry.actuallyVisible ? @"true" : @"false"
          , entry.displayedAt != kRANNoTimestamp ? @"true" : @"false"];
}

- (NSString *)ran_describeRequestedRect {
    if (_rect == nil) return @"unavailable";
    CGFloat scale = MAX(1, UIScreen.mainScreen.nativeScale);
    return [NSString stringWithFormat:
            @"[%lld,%lld,%lld,%lld]px = [%lld,%lld]dp (density=%g)"
          , (long long)llround(_rect.x * scale)
          , (long long)llround(_rect.y * scale)
          , (long long)llround(_rect.width * scale)
          , (long long)llround(_rect.height * scale)
          , (long long)llround(_rect.width)
          , (long long)llround(_rect.height)
          , scale];
}

- (NSString *)ran_failureMessage:(NSString *)reason
                     retryDelay:(NSTimeInterval)retryDelay {
    RANRectAd *owner = _owner;
    NSString *retryDescription = retryDelay == kRANRectAdNoRetryScheduled
            ? @"not scheduled"
            : [NSString stringWithFormat:@"%lldms"
                      , (long long)llround(retryDelay * 1000)];
    return [NSString stringWithFormat:
            @"%@ | adUnitID=%@ | rect=%@ | noFill=%ld | "
             "layoutFail=%ld | retry=%@"
          , reason ?: @""
          , owner == nil ? @"" : [owner unitAdUnitID]
          , [self ran_describeRequestedRect]
          , owner == nil ? 0L : (long)[owner noFillStreak]
          , (long)_layoutFailStreak
          , retryDescription];
}

@end