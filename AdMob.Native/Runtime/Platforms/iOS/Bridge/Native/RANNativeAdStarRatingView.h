// Counterpart of com.riseon.ads.admobnative.NativeAdStarRatingView.

#import <UIKit/UIKit.h>

#import "RANMeasureLayout.h"

NS_ASSUME_NONNULL_BEGIN

// Five stars filled to the rating, the last one partly, sized to stand beside
// text of this point size; empty ones in a grey for a dark ground when onDark.
UIImage *RANStarsImage(CGFloat rating, CGFloat pointSize, BOOL onDark);

// The stars as text runs them in: centred on the digits of this font.
NSAttributedString *RANStarsText(CGFloat rating, UIFont *font, BOOL onDark);

// The ad's star rating as every ad of the pack draws it, the rect ad and the
// Ads placeholders alike: five stars filled to the score, as a store's
// listing shows it, then the score in the advertiser's grey.
@interface RANNativeAdStarRatingView : UIView

// Clamped to [0, 5].
@property (nonatomic) CGFloat rating;

@property (nonatomic, strong) UIColor *textColor;

// 13pt unless a face makes the rating stand out.
@property (nonatomic) CGFloat textSize;

@end

NS_ASSUME_NONNULL_END