// Counterpart of com.riseon.ads.admobnative.NativeAdStarRatingView.

#import <UIKit/UIKit.h>

#import "RANMeasureLayout.h"

NS_ASSUME_NONNULL_BEGIN

@interface RANNativeAdStarRatingView : UIView

// Clamped to [0, 5], fractional fills allowed.
@property (nonatomic) CGFloat rating;

// Dark empty stars, for a light panel where the white ones vanish.
@property (nonatomic) BOOL onLight;

@end

NS_ASSUME_NONNULL_END