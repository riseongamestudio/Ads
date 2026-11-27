// Internal to the pack: the rect of a RANRectAd - where it is on screen, the
// presentation pair being shown and warmed for it, and every trigger that
// decides when the rect earns a fresh ad. It takes raw ads from its owner's
// pool only while shown and never talks to the network itself; hidden, it
// holds nothing another placement could use (see ran_releaseHeldAds).

#import <Foundation/Foundation.h>
#import <GoogleMobileAds/GoogleMobileAds.h>

#import "RANRectAd.h"
#import "RANNativeAdPool.h"

NS_ASSUME_NONNULL_BEGIN

static const NSTimeInterval kRANRectAdNoRetryScheduled = kRANNativeAdPoolNoRetryScheduled;
static const NSTimeInterval kRANRectAdMaxCachedAdAge = kRANNativeAdPoolMaxCachedAdAge;

// The pool's backoff curve.
NSTimeInterval RANRectAdBackoffDelay(
        NSInteger streak
      , NSInteger immediateAttempts);

@interface RANRectAdSlot : NSObject

- (instancetype)initWithOwner:(RANRectAd *)owner;

- (void)configureWithX:(CGFloat)xPt
                     y:(CGFloat)yPt
                 width:(CGFloat)widthPt
                height:(CGFloat)heightPt
           roundCorner:(CGFloat)roundCornerPt;
- (void)show;
- (void)hide;
- (void)setPositionX:(CGFloat)xPt y:(CGFloat)yPt;
- (void)releaseOnMain;
- (void)commitAdClickForAd:(GADNativeAd *)ad;
// The configured size, packed, or kRANNativeAdPoolAnySize.
- (int64_t)sizeKey;

// A freshly loaded ad goes to a shown rect with nothing on screen and nothing
// being built; rotation of a filled rect pulls from the pool on its own
// schedule instead.
- (BOOL)wantsCachedAd;
- (void)presentCachedAd;

@end

// The owner surface the slot consumes.
@interface RANRectAd (SlotSupport)
- (BOOL)hasCachedAdForSizeKey:(int64_t)sizeKey;
- (nullable RANNativeAdPoolCachedAd *)takeCachedAdForSizeKey:(int64_t)sizeKey;
- (NSInteger)cachedCount;
- (void)requestLoad;
- (void)requestLoadForSizeKey:(int64_t)sizeKey;
- (void)returnCachedAd:(GADNativeAd *)ad
              loadedAt:(NSTimeInterval)loadedAt
          unfitSizeKey:(int64_t)unfitSizeKey;
- (void)destroyAd:(nullable GADNativeAd *)ad;
- (NSString *)unitAdUnitID;
- (NSInteger)noFillStreak;
- (void)notifyDisplayed;
- (void)notifyShowNotReady;
- (void)notifyPresentationFailedWithCode:(int32_t)errorCode
                                 message:(NSString *_Nullable)errorMessage;
@end

NS_ASSUME_NONNULL_END