#import "RANRectAd.h"

#import "RANRectAdSlot.h"
#import "RANNativeAdPool.h"

static NSString *const kRANTag = @"RectAd";

static const int32_t kRANLoadSuccessCode = 0;

NSTimeInterval RANRectAdBackoffDelay(
        NSInteger streak
      , NSInteger immediateAttempts) {
    return RANNativeAdPoolBackoffDelay(streak, immediateAttempts);
}

@interface RANRectAd () <RANNativeAdPoolConsumer>
@end

@implementation RANRectAd {
    NSString *_adUnitID;
    NSInteger _cacheSize;
    int32_t _backgroundColor;
    RANRectAdSlot *_slot;
    // Main thread only; joined on the first turn, left on release.
    RANNativeAdPool *_pool;
    // The ads the rect holds. The paid event asks from whatever thread the
    // SDK chose; the guarded set keeps the membership test safe.
    NSHashTable<GADNativeAd *> *_ownedAds;

    // Read on SDK threads, written from Unity's thread; the struct swap is
    // guarded so a reader never sees half of an update.
    RANRectAdListenerCallbacks _rectAdCallbacks;
    NSObject *_rectAdCallbacksLock;
}

- (instancetype)initWithAdUnitID:(NSString *)adUnitID
                       cacheSize:(NSInteger)cacheSize
                 backgroundColor:(int32_t)backgroundColor
                      instanceID:(int32_t)instanceID {
    self = [super initWithInstanceID:instanceID];
    if (self == nil) return nil;

    _adUnitID = [adUnitID copy];
    _cacheSize = MAX(1, cacheSize);
    _backgroundColor = backgroundColor;
    _ownedAds = [NSHashTable weakObjectsHashTable];
    _rectAdCallbacksLock = [NSObject new];
    _slot = [[RANRectAdSlot alloc] initWithOwner:self];
    [RANBaseAd runOnMainThread:^{
        if (self.released) return;

        self->_pool = [RANNativeAdPool joinWithAdUnitID:self->_adUnitID
                                             startMuted:YES
                                               consumer:self];
        [self->_pool startLoadingIfNeeded];
    }];
    return self;
}


- (void)setRectAdListenerCallbacks:
        (RANRectAdListenerCallbacks)callbacks {
    if (self.released) return;
    @synchronized (_rectAdCallbacksLock) {
        _rectAdCallbacks = callbacks;
    }
}

- (RANRectAdListenerCallbacks)ran_currentRectAdCallbacks {
    @synchronized (_rectAdCallbacksLock) {
        return _rectAdCallbacks;
    }
}

// The corner radius travels with the rect: the rect is measured on screen by
// the caller, which only then knows both.
- (void)configureWithX:(CGFloat)xPt
                     y:(CGFloat)yPt
                 width:(CGFloat)widthPt
                height:(CGFloat)heightPt
           roundCorner:(CGFloat)roundCornerPt {
    [RANBaseAd runOnMainThread:^{
        if (self.released) {
            NSLog(@"%@: Configure ignored after Release", kRANTag);
            return;
        }
        if (![RANBaseAd isViewControllerUsable:
                [RANBaseAd unityViewController]]) {
            NSLog(@"%@: Configure requires a usable host", kRANTag);
            return;
        }
        [self->_slot configureWithX:xPt
                                  y:yPt
                              width:widthPt
                             height:heightPt
                        roundCorner:roundCornerPt];
    }];
}

- (void)show {
    if (self.released) return;
    [RANBaseAd runOnMainThread:^{
        if (self.released) return;
        if (![RANBaseAd isViewControllerUsable:
                [RANBaseAd unityViewController]]) {
            NSLog(@"%@: Show ignored because the host is not usable", kRANTag);
            return;
        }
        [self->_slot show];
    }];
}

- (void)hide {
    if (self.released) return;
    [RANBaseAd runOnMainThread:^{
        if (!self.released) [self->_slot hide];
    }];
}

- (void)setPositionX:(CGFloat)xPt y:(CGFloat)yPt {
    if (self.released) return;
    [RANBaseAd runOnMainThread:^{
        if (!self.released) [self->_slot setPositionX:xPt y:yPt];
    }];
}

- (void)releaseAd {
    if (self.released) return;
    [self markReleased];
    [RANBaseAd runOnMainThread:^{
        [self->_slot releaseOnMain];
        [self->_pool leave:self];
        self->_pool = nil;
        RANRectAdListenerCallbacks empty = {0};
        @synchronized (self->_rectAdCallbacksLock) {
            self->_rectAdCallbacks = empty;
        }
    }];
}

// ---------------------------------------------------------------------------
// The pool, for the slot
// ---------------------------------------------------------------------------

- (BOOL)hasCachedAdForSizeKey:(int64_t)sizeKey {
    return _pool != nil && [_pool hasAdForSizeKey:sizeKey];
}

- (NSInteger)cachedCount {
    return _pool.cachedCount;
}

// The oldest ad that may fit the size, now held by this rect.
- (nullable RANNativeAdPoolCachedAd *)takeCachedAdForSizeKey:(int64_t)sizeKey {
    if (_pool == nil) return nil;

    RANNativeAdPoolCachedAd *taken = [_pool takeForSizeKey:sizeKey consumer:self];
    if (taken == nil) return nil;

    GADNativeAd *ad = taken.ad;
    @synchronized (_ownedAds) {
        [_ownedAds addObject:ad];
    }
    __weak RANRectAd *weakSelf = self;
    __weak GADNativeAd *weakAd = ad;
    [self bindPaidEventForAd:ad
                paidAdUnitID:_adUnitID
                 isCurrentAd:^BOOL{
        RANRectAd *strongSelf = weakSelf;
        GADNativeAd *strongAd = weakAd;
        if (strongSelf == nil || strongAd == nil) return NO;
        @synchronized (strongSelf->_ownedAds) {
            return [strongSelf->_ownedAds containsObject:strongAd];
        }
    }];
    return taken;
}

// From a shown rect that found nothing for its size.
- (void)requestLoadForSizeKey:(int64_t)sizeKey {
    [_pool requestForSizeKey:sizeKey];
}

- (void)requestLoad {
    [self requestLoadForSizeKey:kRANNativeAdPoolAnySize];
}

// An ad the rect took and never displayed.
- (void)returnCachedAd:(GADNativeAd *)ad
              loadedAt:(NSTimeInterval)loadedAt
          unfitSizeKey:(int64_t)unfitSizeKey {
    if (ad == nil) return;

    @synchronized (_ownedAds) {
        [_ownedAds removeObject:ad];
    }
    ad.paidEventHandler = nil;
    if (_pool == nil) {
        ad.delegate = nil;
        return;
    }

    [_pool giveBackAd:ad loadedAt:loadedAt unfitSizeKey:unfitSizeKey];
}

- (void)destroyAd:(GADNativeAd *)ad {
    if (ad == nil) return;
    @synchronized (_ownedAds) {
        [_ownedAds removeObject:ad];
    }
    ad.paidEventHandler = nil;
    if (_pool == nil) {
        ad.delegate = nil;
        return;
    }

    [_pool destroyAd:ad];
}

// ---------------------------------------------------------------------------
// RANNativeAdPoolConsumer
// ---------------------------------------------------------------------------

- (NSInteger)poolWantedCount {
    return _cacheSize;
}

- (BOOL)poolCouldUse:(RANNativeAdPoolCachedAd *)cached {
    int64_t sizeKey = [_slot sizeKey];
    return sizeKey != kRANNativeAdPoolAnySize
            && ![cached.unfitSizes containsObject:@(sizeKey)];
}

// A freshly loaded or returned ad goes to the rect if it is shown with
// nothing on screen and nothing being built, and the ad may fit it.
- (void)poolDidChange {
    if (self.released) return;

    if ([_slot wantsCachedAd]
            && [self hasCachedAdForSizeKey:[_slot sizeKey]]) {
        [_slot presentCachedAd];
    }
}

- (void)poolDidStartLoading {
    [self ran_notifyLoadingStarted];
}

- (void)poolDidCompleteLoadingWithCode:(int32_t)code
                               message:(NSString *)message
                            retryDelay:(NSTimeInterval)retryDelay {
    [self ran_notifyLoadingCompletedWithCode:code
                                    message:code == kRANLoadSuccessCode
                                            ? @""
                                            : [self ran_failureMessage:message
                                                            retryDelay:retryDelay]];
}

- (void)poolAdDidRecordClick:(GADNativeAd *)ad {
    [_slot commitAdClickForAd:ad];
}

// ---------------------------------------------------------------------------
// Accessors and notifications for the slot
// ---------------------------------------------------------------------------

- (float)slotBackgroundColor {
    return _backgroundColor;
}

- (NSString *)unitAdUnitID {
    return _adUnitID;
}

- (NSInteger)noFillStreak {
    return _pool.noFillStreak;
}

- (void)ran_notifyLoadingStarted {
    RANRectAdListenerCallbacks callbacks =
            [self ran_currentRectAdCallbacks];
    if (callbacks.loadingStarted == NULL) return;
    callbacks.loadingStarted(self.instanceID);
}

- (void)ran_notifyLoadingCompletedWithCode:(int32_t)errorCode
                                  message:(NSString *)errorMessage {
    RANRectAdListenerCallbacks callbacks =
            [self ran_currentRectAdCallbacks];
    if (callbacks.loadingCompleted == NULL) return;
    callbacks.loadingCompleted(
            self.instanceID
          , errorCode
          , (errorMessage ?: @"").UTF8String
          , (int32_t)(_pool != nil ? _pool.cachedCount : 0)
          , (int32_t)(_pool != nil ? _pool.capacity : _cacheSize));
}

// The base's paid binding delivers through this hook.
- (void)notifyAdPaidWithSource:(NSString *)source
                      adUnitID:(NSString *)adUnitID
                         value:(double)value
                  currencyCode:(NSString *)currencyCode
                     precision:(int32_t)precision {
    RANRectAdListenerCallbacks callbacks =
            [self ran_currentRectAdCallbacks];
    if (callbacks.adPaid == NULL) return;
    callbacks.adPaid(
            self.instanceID
          , (source ?: @"").UTF8String
          , (adUnitID ?: @"").UTF8String
          , value
          , (currencyCode ?: @"").UTF8String
          , precision);
}

- (void)notifyDisplayed {
    RANRectAdListenerCallbacks callbacks =
            [self ran_currentRectAdCallbacks];
    if (callbacks.displayed == NULL) return;
    callbacks.displayed(self.instanceID);
}

- (void)notifyShowNotReady {
    RANRectAdListenerCallbacks callbacks =
            [self ran_currentRectAdCallbacks];
    if (callbacks.showNotReady == NULL) return;
    callbacks.showNotReady(self.instanceID);
}

- (void)notifyPresentationFailedWithCode:(int32_t)errorCode
                                 message:(NSString *)errorMessage {
    RANRectAdListenerCallbacks callbacks =
            [self ran_currentRectAdCallbacks];
    if (callbacks.presentationFailed == NULL) return;
    callbacks.presentationFailed(
            self.instanceID
          , errorCode
          , (errorMessage ?: @"").UTF8String);
}

- (NSString *)ran_failureMessage:(NSString *)reason
                     retryDelay:(NSTimeInterval)retryDelay {
    NSString *retryDescription = retryDelay == kRANRectAdNoRetryScheduled
            ? @"not scheduled"
            : [NSString stringWithFormat:@"%lldms"
                      , (long long)llround(retryDelay * 1000)];
    return [NSString stringWithFormat:@"%@ | %@ | retry=%@"
          , reason ?: @""
          , _pool != nil ? [_pool describe] : @"no pool"
          , retryDescription];
}

@end