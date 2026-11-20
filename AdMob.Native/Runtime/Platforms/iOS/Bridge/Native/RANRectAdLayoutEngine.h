// Counterpart of com.riseon.ads.admobnative.RectAdLayoutEngine: enumerates layout
// candidates over template x tier x optional-asset mask, walks each down
// the text ladder, and scores what fits. Lower score wins; richness of
// assets outweighs whitespace outweighs stillness, the same order the
// Android engine settles.

#import <Foundation/Foundation.h>
#import <GoogleMobileAds/GoogleMobileAds.h>

#import "RANRectAdLayoutTypes.h"
#import "RANRectAdLayoutValidator.h"
#import "RANRectAdViewFactory.h"

NS_ASSUME_NONNULL_BEGIN

@interface RANRectAdLayoutEngine : NSObject

- (instancetype)initWithNativeAd:(GADNativeAd *)nativeAd
                      requestedX:(CGFloat)requestedX
                      requestedY:(CGFloat)requestedY
                  requestedWidth:(CGFloat)requestedWidth
                 requestedHeight:(CGFloat)requestedHeight
                     viewFactory:(RANRectAdViewFactory *)viewFactory
                       validator:(RANRectAdLayoutValidator *)validator;

- (void)setPositionX:(CGFloat)xPt y:(CGFloat)yPt;
- (NSArray<RANRectAdLayoutPlan *> *)choosePlansForScreenWidth:(CGFloat)screenWidth
                                                screenHeight:(CGFloat)screenHeight
                                                    hasVideo:(BOOL)hasVideo
                                                hasMainImage:(BOOL)hasMainImage;
- (NSString *)describeLastRejections;
- (void)clear;

CGFloat RANClamp(CGFloat value, CGFloat minimum, CGFloat maximum);

@end

NS_ASSUME_NONNULL_END