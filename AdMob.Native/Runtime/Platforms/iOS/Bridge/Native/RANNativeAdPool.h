// Counterpart of com.riseon.ads.admobnative.NativeAdPool: the warm native ads of
// one ad unit id and mute policy, shared by every format drawing from that unit.
// A rect ad, a collapsible panel and a full-screen ad all show the same
// Native advanced creative, only laid out differently; the unit id stays with
// the ad because the request went to it. Whether a video starts muted is a
// request option: GADVideoController.muted does nothing unless custom controls
// are enabled, which they are only for Ad Manager reservations
// (developers.google.com/admob/ios/api/reference/Classes/GADVideoController),
// hence one pool per mute policy too.
//
// A consumer takes an ad, and gives back one it never displayed: one that did
// not fit is marked with the consumer's size key, and the others still get it.
// A full pool whose ads fit nobody who asks would stop loading, so a consumer
// finding nothing for its size drops one: an ad that fits no consumer, else the
// oldest that does not fit it.
//
// Main thread only.

#import <Foundation/Foundation.h>
#import <GoogleMobileAds/GoogleMobileAds.h>

NS_ASSUME_NONNULL_BEGIN

static const int64_t kRANNativeAdPoolAnySize = -1;
static const NSTimeInterval kRANNativeAdPoolNoRetryScheduled = -1;
// developers.google.com/admob/ios/native/start: ads expire after an hour.
static const NSTimeInterval kRANNativeAdPoolMaxCachedAdAge = 3600;

NSTimeInterval RANNativeAdPoolBackoffDelay(
        NSInteger streak
      , NSInteger immediateAttempts);

// The clock an ad's age is told by, loadedAt included: it keeps counting while
// the device sleeps, which systemUptime does not, so an ad an hour old by the
// wall is an hour old here too.
NSTimeInterval RANNativeAdPoolNow(void);

@interface RANNativeAdPoolCachedAd : NSObject
@property (nonatomic, strong) GADNativeAd *ad;
@property (nonatomic) NSTimeInterval loadedAt;
// The size keys it failed to lay out in.
@property (nonatomic, strong) NSMutableSet<NSNumber *> *unfitSizes;
@end

@protocol RANNativeAdPoolConsumer <NSObject>
// How many warm ads it would like on hand.
- (NSInteger)poolWantedCount;
// Whether it could lay the ad out somewhere it takes ads for now.
- (BOOL)poolCouldUse:(RANNativeAdPoolCachedAd *)cached;
// Ads arrived or came back; it may take one.
- (void)poolDidChange;
- (void)poolDidStartLoading;
- (void)poolDidCompleteLoadingWithCode:(int32_t)code
                               message:(NSString *)message
                            retryDelay:(NSTimeInterval)retryDelay;
// A click on an ad it holds.
- (void)poolAdDidRecordClick:(GADNativeAd *)ad;
@optional
// The click's landing page coming to the front, and leaving.
- (void)poolAdWillPresentScreen:(GADNativeAd *)ad;
- (void)poolAdDidDismissScreen:(GADNativeAd *)ad;
@end

@interface RANNativeAdPool : NSObject

// The pool of the unit and mute policy, made on first use; the consumer joins
// it, held weakly.
+ (RANNativeAdPool *)joinWithAdUnitID:(NSString *)adUnitID
                           startMuted:(BOOL)startMuted
                             consumer:(id<RANNativeAdPoolConsumer>)consumer;

// The last consumer to leave takes the pool with it.
- (void)leave:(id<RANNativeAdPoolConsumer>)consumer;

@property (nonatomic, readonly) NSString *adUnitID;
@property (nonatomic, readonly) NSInteger noFillStreak;
@property (nonatomic, readonly) NSInteger cachedCount;
// The ads it keeps warm when full: the most any consumer wants.
@property (nonatomic, readonly) NSInteger capacity;
@property (nonatomic, readonly) BOOL isLoading;

// Loads when a seat is free and a host is usable.
- (void)startLoadingIfNeeded;
- (BOOL)hasAdForSizeKey:(int64_t)sizeKey;
// The oldest ad that may fit, taken out for the consumer; its seat is
// refilled at once.
- (nullable RANNativeAdPoolCachedAd *)takeForSizeKey:(int64_t)sizeKey
                                            consumer:(id<RANNativeAdPoolConsumer>)consumer;
// An ad taken and never displayed comes back in age order; with a size key,
// it did not fit there.
- (void)giveBackAd:(GADNativeAd *)ad
          loadedAt:(NSTimeInterval)loadedAt
      unfitSizeKey:(int64_t)unfitSizeKey;
// An ad that was displayed, or that its holder is done with.
- (void)destroyAd:(nullable GADNativeAd *)ad;
// From a consumer that found nothing for its size: makes room in a full
// pool, then loads when it may.
- (void)requestForSizeKey:(int64_t)sizeKey;
- (NSString *)describe;

@end

NS_ASSUME_NONNULL_END