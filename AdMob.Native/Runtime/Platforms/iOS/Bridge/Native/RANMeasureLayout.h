// A deliberately small re-creation of Android's measure and layout model -
// MeasureSpec, LinearLayout, FrameLayout, gravity, margins, GONE - just deep
// enough for the rect ad layout engine and the two view factories to port
// line for line. The engine's whole candidate search is phrased in terms of
// measure(UNSPECIFIED) versus measure(EXACTLY) passes; recreating that model
// is what keeps the iOS port a transcription instead of a redesign.
//
// Everything here works in points; one point stands where Android's px
// stood, and the factories convert dp thresholds through the screen scale
// at their boundary the way the Android factories convert through density.

#import <UIKit/UIKit.h>

NS_ASSUME_NONNULL_BEGIN

typedef NS_ENUM(NSInteger, RANMeasureSpecMode) {
    RANMeasureSpecUnspecified = 0
  , RANMeasureSpecExactly
  , RANMeasureSpecAtMost
};

typedef struct {
    RANMeasureSpecMode mode;
    CGFloat size;
} RANMeasureSpec;

static inline RANMeasureSpec RANMeasureSpecMake(
        RANMeasureSpecMode mode
      , CGFloat size) {
    RANMeasureSpec spec;
    spec.mode = mode;
    spec.size = size;
    return spec;
}

// Android's LayoutParams dimension sentinels.
extern const CGFloat RANLayoutMatchParent; // -1
extern const CGFloat RANLayoutWrapContent; // -2

// Android's Gravity subset, combinable.
typedef NS_OPTIONS(NSUInteger, RANGravity) {
    RANGravityLeft = 1 << 0
  , RANGravityTop = 1 << 1
  , RANGravityRight = 1 << 2
  , RANGravityBottom = 1 << 3
  , RANGravityCenterHorizontal = 1 << 4
  , RANGravityCenterVertical = 1 << 5
};

// Layout state carried per child, the counterpart of LayoutParams plus the
// measured size a measure pass leaves behind.
@interface UIView (RANMeasureLayout)

@property (nonatomic) CGFloat ran_layoutWidth;   // pt, MatchParent or WrapContent
@property (nonatomic) CGFloat ran_layoutHeight;  // pt, MatchParent or WrapContent
@property (nonatomic) CGFloat ran_layoutWeight;
@property (nonatomic) RANGravity ran_layoutGravity; // 0 = container decides
@property (nonatomic) UIEdgeInsets ran_layoutMargins;
@property (nonatomic) CGSize ran_measuredSize;
// Android GONE: skipped by measure and layout entirely. UIView.hidden alone
// is Android INVISIBLE - it still takes its space.
@property (nonatomic) BOOL ran_gone;
// Minimum content size honoured by the default leaf measurement, the
// counterpart of setMinimumWidth/Height.
@property (nonatomic) CGSize ran_minimumSize;

// Measures the view against the given specs and stores ran_measuredSize.
// Containers override; the default resolves a leaf through sizeThatFits.
- (void)ran_measureWithWidthSpec:(RANMeasureSpec)widthSpec
                     heightSpec:(RANMeasureSpec)heightSpec;

// Positions the view at the frame a container computed from measured sizes.
- (void)ran_layoutWithFrame:(CGRect)frame;

// Android's getChildMeasureSpec, verbatim.
+ (RANMeasureSpec)ran_childMeasureSpecForParentSpec:(RANMeasureSpec)parentSpec
                                          padding:(CGFloat)padding
                                   childDimension:(CGFloat)childDimension;

@end

// Container base holding padding and the shared child enumeration.
@interface RANLayoutContainerView : UIView

@property (nonatomic) UIEdgeInsets ran_padding;

@end

// Android LinearLayout: sequential main-axis placement, weights absorbing
// the leftover of an EXACTLY pass, and - load-bearing for the engine - a
// weighted zero-height child measured under UNSPECIFIED is treated as
// WRAP_CONTENT rather than collapsing to nothing.
@interface RANLinearLayoutView : RANLayoutContainerView

@property (nonatomic) BOOL ran_vertical;
@property (nonatomic) RANGravity ran_gravity;

@end

// Android FrameLayout: children stacked, sized to the frame minus margins,
// positioned by their own gravity.
@interface RANFrameLayoutView : RANLayoutContainerView

@end

NS_ASSUME_NONNULL_END