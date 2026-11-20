#import "RANNativeAdPool.h"

#include <time.h>

#import "RANBaseAd.h"

static NSString *const kRANTag = @"NativeAdPool";
static const NSInteger kRANMaxRetryExponent = 5;
static const NSTimeInterval kRANRetryBaseDelay = 1.0;

NSTimeInterval RANNativeAdPoolBackoffDelay(
        NSInteger streak
      , NSInteger immediateAttempts) {
    if (streak <= immediateAttempts) return 0;
    NSInteger exponent = MIN(
            kRANMaxRetryExponent, streak - immediateAttempts);
    return kRANRetryBaseDelay * (NSTimeInterval)(1LL << exponent);
}

NSTimeInterval RANNativeAdPoolNow(void) {
    return (NSTimeInterval)clock_gettime_nsec_np(CLOCK_MONOTONIC) / NSEC_PER_SEC;
}

static NSTimeInterval RANNow(void) {
    return RANNativeAdPoolNow();
}

@implementation RANNativeAdPoolCachedAd
- (instancetype)init {
    self = [super init];
    if (self != nil) _unfitSizes = [NSMutableSet set];
    return self;
}
@end

@interface RANNativeAdPool () <GADNativeAdLoaderDelegate
                                 , GADNativeAdDelegate>
@end

@implementation RANNativeAdPool {
    NSString *_key;
    BOOL _startMuted;
    NSHashTable<id<RANNativeAdPoolConsumer>> *_consumers;
    // Oldest first: the head is the first to go stale.
    NSMutableArray<RANNativeAdPoolCachedAd *> *_cachedAds;
    // Who holds each ad taken out, for its clicks.
    NSMapTable<GADNativeAd *, id<RANNativeAdPoolConsumer>> *_holders;
    GADAdLoader *_adLoader;
    BOOL _released;
    BOOL _isAdLoading;
    BOOL _retryScheduled;
    NSInteger _noFillStreak;
    NSTimer *_retryTimer;
    NSTimer *_cacheExpiryTimer;
}

static NSMutableDictionary<NSString *, RANNativeAdPool *> *RANPools(void) {
    static NSMutableDictionary<NSString *, RANNativeAdPool *> *pools;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ pools = [NSMutableDictionary dictionary]; });
    return pools;
}

+ (RANNativeAdPool *)joinWithAdUnitID:(NSString *)adUnitID
                           startMuted:(BOOL)startMuted
                             consumer:(id<RANNativeAdPoolConsumer>)consumer {
    NSString *key = [NSString stringWithFormat:@"%@|%d", adUnitID, startMuted];
    RANNativeAdPool *pool = RANPools()[key];
    if (pool == nil) {
        pool = [[RANNativeAdPool alloc] initWithKey:key
                                              adUnitID:adUnitID
                                            startMuted:startMuted];
        RANPools()[key] = pool;
    }
    [pool->_consumers addObject:consumer];
    return pool;
}

- (instancetype)initWithKey:(NSString *)key
                       adUnitID:(NSString *)adUnitID
                     startMuted:(BOOL)startMuted {
    self = [super init];
    if (self == nil) return nil;

    _key = [key copy];
    _adUnitID = [adUnitID copy];
    _startMuted = startMuted;
    _consumers = [NSHashTable weakObjectsHashTable];
    _cachedAds = [NSMutableArray array];
    _holders = [NSMapTable strongToWeakObjectsMapTable];
    return self;
}

- (void)leave:(id<RANNativeAdPoolConsumer>)consumer {
    [_consumers removeObject:consumer];
    for (GADNativeAd *ad in [[_holders keyEnumerator] allObjects]) {
        if ([_holders objectForKey:ad] == consumer) [_holders removeObjectForKey:ad];
    }
    if (_consumers.allObjects.count > 0 || _released) return;

    _released = YES;
    [_retryTimer invalidate];
    [_cacheExpiryTimer invalidate];
    for (RANNativeAdPoolCachedAd *cached in _cachedAds) {
        [self ran_forgetAd:cached.ad];
    }
    [_cachedAds removeAllObjects];
    [_holders removeAllObjects];
    _adLoader = nil;
    [RANPools() removeObjectForKey:_key];
}

- (NSInteger)cachedCount {
    return (NSInteger)_cachedAds.count;
}

- (NSInteger)capacity {
    return [self ran_capacity];
}

- (BOOL)isLoading {
    return _isAdLoading;
}

- (void)startLoadingIfNeeded {
    [self ran_startLoad];
}

- (BOOL)hasAdForSizeKey:(int64_t)sizeKey {
    [self ran_removeExpiredAds];
    for (RANNativeAdPoolCachedAd *cached in _cachedAds) {
        if ([RANNativeAdPool ran_cached:cached fits:sizeKey]) return YES;
    }
    return NO;
}

- (nullable RANNativeAdPoolCachedAd *)takeForSizeKey:(int64_t)sizeKey
                                            consumer:(id<RANNativeAdPoolConsumer>)consumer {
    [self ran_removeExpiredAds];
    for (RANNativeAdPoolCachedAd *cached in _cachedAds) {
        if (![RANNativeAdPool ran_cached:cached fits:sizeKey]) continue;

        [_cachedAds removeObject:cached];
        [_holders setObject:consumer forKey:cached.ad];
        [self ran_scheduleCacheExpiry];
        [self ran_startLoad];
        return cached;
    }
    return nil;
}

- (void)giveBackAd:(GADNativeAd *)ad
          loadedAt:(NSTimeInterval)loadedAt
      unfitSizeKey:(int64_t)unfitSizeKey {
    if (ad == nil) return;

    [_holders removeObjectForKey:ad];
    if (_released) {
        [self ran_forgetAd:ad];
        return;
    }

    RANNativeAdPoolCachedAd *returned = [[RANNativeAdPoolCachedAd alloc] init];
    returned.ad = ad;
    returned.loadedAt = loadedAt;
    if (unfitSizeKey != kRANNativeAdPoolAnySize) {
        [returned.unfitSizes addObject:@(unfitSizeKey)];
    }
    NSUInteger index = 0;
    while (index < _cachedAds.count && _cachedAds[index].loadedAt <= loadedAt) {
        ++index;
    }
    [_cachedAds insertObject:returned atIndex:index];
    [self ran_scheduleCacheExpiry];
    if (unfitSizeKey != kRANNativeAdPoolAnySize) {
        NSLog(@"%@: Took back an ad unfit for size %lld: %@", kRANTag, (long long)unfitSizeKey, [self describe]);
    }
    // The others hear of it on the next turn, after the one giving it back
    // is done.
    __weak RANNativeAdPool *weakSelf = self;
    dispatch_async(dispatch_get_main_queue(), ^{
        [weakSelf ran_notifyPoolChanged];
    });
}

- (void)destroyAd:(nullable GADNativeAd *)ad {
    if (ad == nil) return;

    [_holders removeObjectForKey:ad];
    [self ran_forgetAd:ad];
}

- (void)requestForSizeKey:(int64_t)sizeKey {
    if (_released) return;
    if ((NSInteger)_cachedAds.count >= [self ran_capacity]) {
        [self ran_dropUnfitAdForSizeKey:sizeKey];
    }
    if ([self ran_startLoad]) return;
    if (!_isAdLoading && !_retryScheduled && [self ran_hasFreeSeat]) {
        [self ran_postRetry:RANNativeAdPoolBackoffDelay(MAX(1, _noFillStreak), 0)];
    }
}

+ (BOOL)ran_cached:(RANNativeAdPoolCachedAd *)cached fits:(int64_t)sizeKey {
    return sizeKey == kRANNativeAdPoolAnySize
            || ![cached.unfitSizes containsObject:@(sizeKey)];
}

// The ads it keeps warm when full: the most any consumer wants. The consumers
// share the ads and seldom all show at once, so the largest want covers them;
// each ad taken is reloaded, so consumers that find the pool empty get one in
// turn.
- (NSInteger)ran_capacity {
    NSInteger wanted = 1;
    for (id<RANNativeAdPoolConsumer> consumer in _consumers.allObjects) {
        wanted = MAX(wanted, [consumer poolWantedCount]);
    }
    return wanted;
}

- (BOOL)ran_hasFreeSeat {
    return (NSInteger)_cachedAds.count < [self ran_capacity];
}

- (void)ran_dropUnfitAdForSizeKey:(int64_t)sizeKey {
    RANNativeAdPoolCachedAd *dropped = nil;
    for (RANNativeAdPoolCachedAd *cached in _cachedAds) {
        if ([self ran_fitsNoConsumer:cached]) {
            dropped = cached;
            break;
        }
        if (dropped == nil && ![RANNativeAdPool ran_cached:cached fits:sizeKey]) {
            dropped = cached;
        }
    }
    if (dropped == nil) return;

    [_cachedAds removeObject:dropped];
    [self ran_forgetAd:dropped.ad];
    [self ran_scheduleCacheExpiry];
    NSLog(@"%@: Dropped an ad unfit for size %lld to make room: %@", kRANTag, (long long)sizeKey, [self describe]);
}

- (BOOL)ran_fitsNoConsumer:(RANNativeAdPoolCachedAd *)cached {
    for (id<RANNativeAdPoolConsumer> consumer in _consumers.allObjects) {
        if ([consumer poolCouldUse:cached]) return NO;
    }
    return YES;
}

- (BOOL)ran_startLoad {
    UIViewController *host = [RANBaseAd unityViewController];
    if (_released
            || _isAdLoading
            || _retryScheduled
            || ![self ran_hasFreeSeat]
            || ![RANBaseAd isViewControllerUsable:host]) {
        return NO;
    }

    _isAdLoading = YES;
    for (id<RANNativeAdPoolConsumer> consumer in _consumers.allObjects) {
        [consumer poolDidStartLoading];
    }

    _adLoader = [[GADAdLoader alloc]
            initWithAdUnitID:_adUnitID
          rootViewController:host
                     adTypes:@[ GADAdLoaderAdTypeNative ]
                     options:[RANBaseAd adLoaderOptionsWithStartMuted:_startMuted]];
    _adLoader.delegate = self;
    [_adLoader loadRequest:[GADRequest request]];
    return YES;
}

- (void)adLoader:(GADAdLoader *)adLoader
        didReceiveNativeAd:(GADNativeAd *)nativeAd {
    [RANBaseAd runOnMainThread:^{
        // Only the loader still held may speak for the pool.
        if (self->_released || adLoader != self->_adLoader) return;

        self->_noFillStreak = 0;
        nativeAd.delegate = self;
        RANNativeAdPoolCachedAd *cached = [[RANNativeAdPoolCachedAd alloc] init];
        cached.ad = nativeAd;
        cached.loadedAt = RANNow();
        [self->_cachedAds addObject:cached];
        NSLog(@"%@: Loaded: %@", kRANTag, [self describe]);
        [self ran_scheduleCacheExpiry];

        self->_isAdLoading = NO;
        [self ran_notifyLoadCompletedWithCode:0
                                      message:@""
                                   retryDelay:kRANNativeAdPoolNoRetryScheduled];
        [self ran_notifyPoolChanged];
        [self ran_startLoad];
    }];
}

- (void)adLoader:(GADAdLoader *)adLoader
        didFailToReceiveAdWithError:(NSError *)error {
    [RANBaseAd runOnMainThread:^{
        if (self->_released || adLoader != self->_adLoader) return;

        self->_isAdLoading = NO;
        NSTimeInterval retryDelay = [self ran_scheduleNoFillRetry];
        [self ran_notifyLoadCompletedWithCode:(int32_t)error.code
                                      message:error.localizedDescription ?: @""
                                   retryDelay:retryDelay];
    }];
}

// Not gated on the loader: the ad clicked may come from an earlier load.
- (void)nativeAdDidRecordClick:(GADNativeAd *)nativeAd {
    [RANBaseAd runOnMainThread:^{
        id<RANNativeAdPoolConsumer> holder = [self->_holders objectForKey:nativeAd];
        [holder poolAdDidRecordClick:nativeAd];
    }];
}

- (void)nativeAdWillPresentScreen:(GADNativeAd *)nativeAd {
    [RANBaseAd runOnMainThread:^{
        id<RANNativeAdPoolConsumer> holder = [self->_holders objectForKey:nativeAd];
        if ([holder respondsToSelector:@selector(poolAdWillPresentScreen:)]) {
            [holder poolAdWillPresentScreen:nativeAd];
        }
    }];
}

- (void)nativeAdDidDismissScreen:(GADNativeAd *)nativeAd {
    [RANBaseAd runOnMainThread:^{
        id<RANNativeAdPoolConsumer> holder = [self->_holders objectForKey:nativeAd];
        if ([holder respondsToSelector:@selector(poolAdDidDismissScreen:)]) {
            [holder poolAdDidDismissScreen:nativeAd];
        }
    }];
}

- (void)ran_notifyLoadCompletedWithCode:(int32_t)code
                                message:(NSString *)message
                             retryDelay:(NSTimeInterval)retryDelay {
    for (id<RANNativeAdPoolConsumer> consumer in _consumers.allObjects) {
        [consumer poolDidCompleteLoadingWithCode:code
                                         message:message
                                      retryDelay:retryDelay];
    }
}

- (void)ran_notifyPoolChanged {
    if (_released) return;

    for (id<RANNativeAdPoolConsumer> consumer in _consumers.allObjects) {
        if (_released || _cachedAds.count == 0) return;
        [consumer poolDidChange];
    }
}

// No fill is a property of inventory: waiting is the only thing that can
// help, so back off from the first miss.
- (NSTimeInterval)ran_scheduleNoFillRetry {
    ++_noFillStreak;
    return [self ran_postRetry:RANNativeAdPoolBackoffDelay(_noFillStreak, 0)];
}

- (NSTimeInterval)ran_postRetry:(NSTimeInterval)delay {
    [_retryTimer invalidate];
    _retryTimer = nil;
    _retryScheduled = NO;
    if (_released) return kRANNativeAdPoolNoRetryScheduled;

    _retryScheduled = YES;
    __weak RANNativeAdPool *weakSelf = self;
    _retryTimer = [NSTimer scheduledTimerWithTimeInterval:MAX(0.01, delay)
                                                  repeats:NO
                                                    block:^(NSTimer *timer) {
        [weakSelf ran_handleRetry];
    }];
    return delay;
}

- (void)ran_handleRetry {
    _retryScheduled = NO;
    if (_released) return;

    [self ran_notifyPoolChanged];
    if ([self ran_startLoad]) return;
    // A missing host ends nothing: one arrives with the next call.
    if (!_isAdLoading && [self ran_hasFreeSeat]) {
        [self ran_postRetry:RANNativeAdPoolBackoffDelay(MAX(1, _noFillStreak), 0)];
    }
}

- (void)ran_scheduleCacheExpiry {
    [_cacheExpiryTimer invalidate];
    _cacheExpiryTimer = nil;
    if (_released) return;

    NSTimeInterval nextExpiryAt = DBL_MAX;
    for (RANNativeAdPoolCachedAd *cached in _cachedAds) {
        nextExpiryAt = MIN(
                nextExpiryAt, cached.loadedAt + kRANNativeAdPoolMaxCachedAdAge);
    }
    if (nextExpiryAt == DBL_MAX) return;

    __weak RANNativeAdPool *weakSelf = self;
    _cacheExpiryTimer = [NSTimer
            scheduledTimerWithTimeInterval:MAX(0.01, nextExpiryAt - RANNow())
                                   repeats:NO
                                     block:^(NSTimer *timer) {
        [weakSelf ran_handleCacheExpiry];
    }];
}

- (void)ran_handleCacheExpiry {
    if (_released) return;

    [self ran_removeExpiredAds];
    [self ran_startLoad];
    [self ran_scheduleCacheExpiry];
}

- (void)ran_removeExpiredAds {
    NSTimeInterval now = RANNow();
    NSMutableArray<RANNativeAdPoolCachedAd *> *expired = [NSMutableArray array];
    for (RANNativeAdPoolCachedAd *cached in _cachedAds) {
        if (now - cached.loadedAt >= kRANNativeAdPoolMaxCachedAdAge) {
            [expired addObject:cached];
        }
    }
    for (RANNativeAdPoolCachedAd *cached in expired) {
        [self ran_forgetAd:cached.ad];
        [_cachedAds removeObject:cached];
    }
}

- (void)ran_forgetAd:(GADNativeAd *)ad {
    ad.paidEventHandler = nil;
    ad.delegate = nil;
}

- (NSString *)describe {
    return [NSString stringWithFormat:@"unit=%@ muted=%d cache %lu/%ld consumers=%lu"
          , _adUnitID
          , _startMuted
          , (unsigned long)_cachedAds.count
          , (long)[self ran_capacity]
          , (unsigned long)_consumers.allObjects.count];
}

@end