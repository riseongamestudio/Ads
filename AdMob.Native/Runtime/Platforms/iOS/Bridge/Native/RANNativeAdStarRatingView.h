// Counterpart of com.riseon.ads.admobnative.NativeAdStarRatingView.

#import <UIKit/UIKit.h>

#import "RANMeasureLayout.h"

NS_ASSUME_NONNULL_BEGIN

@interface RANNativeAdStarRatingView : UIView

// Clamped to [0, 5], fractional fills allowed.
@property (nonatomic) CGFloat rating;

@end

NS_ASSUME_NONNULL_END