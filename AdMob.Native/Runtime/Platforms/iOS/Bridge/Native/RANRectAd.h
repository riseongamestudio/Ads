// Counterpart of com.riseon.ads.admobnative.RectAd: a native ad in a rect the
// game places, a feed cell or a block such as an MRec. The ads come from the
// unit's RANNativeAdPool, muted, shared with every other format drawing from
// the same unit; the rect takes one while shown, and gives back one it never
// displayed. Everything rect-shaped (layout, dwell, rotation, watchdogs) lives
// in RANRectAdSlot, same as RectAdSlot on Android. One never configured still
// joins the pool with its cache size.

#import <Foundation/Foundation.h>

#import "RANBaseAd.h"

NS_ASSUME_NONNULL_BEGIN

// The loading and paid callbacks describe the pool; the others the rect.
typedef struct {
    RANNativeAdLoadingStartedCallback _Nullable loadingStarted;
    RANNativeAdLoadingCompletedCallback _Nullable loadingCompleted;
    RANNativeAdPaidCallback _Nullable adPaid;
    RANNativeAdDisplayedCallback _Nullable displayed;
    RANNativeAdShowNotReadyCallback _Nullable showNotReady;
    RANNativeAdPresentationFailedCallback _Nullable presentationFailed;
} RANRectAdListenerCallbacks;

@interface RANRectAd : RANBaseAd

- (instancetype)initWithAdUnitID:(NSString *)adUnitID
                       cacheSize:(NSInteger)cacheSize
                 backgroundColor:(int32_t)backgroundColor
                      instanceID:(int32_t)instanceID;

- (void)setRectAdListenerCallbacks:
        (RANRectAdListenerCallbacks)callbacks;

// Points, converted from Unity's pixels at the bridge boundary.
- (void)configureWithX:(CGFloat)xPt
                     y:(CGFloat)yPt
                 width:(CGFloat)widthPt
                height:(CGFloat)heightPt
           roundCorner:(CGFloat)roundCornerPt;
- (void)show;
- (void)hide;
- (void)setPositionX:(CGFloat)xPt y:(CGFloat)yPt;
- (void)releaseAd;

@end

NS_ASSUME_NONNULL_END