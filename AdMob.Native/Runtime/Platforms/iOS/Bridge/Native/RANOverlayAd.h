// Counterpart of com.riseon.ads.admobnative.Overlay: the cached-ad state machine.
// Cache size ads are kept warm; Show consumes the head, completion hands
// the result back through the bridge callback, and the head's collapsible
// presentation is prepared ahead of the show exactly as the Java side
// warms its dialog.

#import <Foundation/Foundation.h>
#import <GoogleMobileAds/GoogleMobileAds.h>

#import "RANBaseAd.h"

NS_ASSUME_NONNULL_BEGIN

@interface RANOverlayAd : RANBaseAd

- (instancetype)initWithAdUnitID:(NSString *)adUnitID
                      instanceID:(int32_t)instanceID;

- (void)configureWithFullScreen:(BOOL)fullScreen
                    heightRatio:(float)heightRatio
                backgroundColor:(int32_t)backgroundColor
                      cacheSize:(int32_t)cacheSize
                       cooldown:(int32_t)cooldown
                      closeSide:(int32_t)closeSide
                      timerSide:(int32_t)timerSide
                redirectOnClose:(BOOL)redirectOnClose;
- (void)setLayout:(int32_t)layout;
- (void)setCloseWithCooldown:(int32_t)cooldown
                   closeSide:(int32_t)closeSide
                   timerSide:(int32_t)timerSide
             redirectOnClose:(BOOL)redirectOnClose;
- (void)load;
// The half-screen panel's height in points: the number its view gets at show
// time, and the one something standing behind it has to match. Known before
// any ad has loaded, because the creative never moves it.
- (CGFloat)resolvePanelHeight;
- (void)showWithShowID:(int32_t)showID
               slideIn:(BOOL)slideIn
       slideOutOnClose:(BOOL)slideOutOnClose
           onCompleted:(RANNativeAdShowCompletedCallback _Nullable)onCompleted;
- (void)hideSlidingOut:(BOOL)slideOut;
- (void)releaseAd;

@end

NS_ASSUME_NONNULL_END