#import "RANMeasureLayout.h"

#import <objc/runtime.h>

const CGFloat RANLayoutMatchParent = -1;
const CGFloat RANLayoutWrapContent = -2;

static const void *kRANLayoutWidthKey = &kRANLayoutWidthKey;
static const void *kRANLayoutHeightKey = &kRANLayoutHeightKey;
static const void *kRANLayoutWeightKey = &kRANLayoutWeightKey;
static const void *kRANLayoutGravityKey = &kRANLayoutGravityKey;
static const void *kRANLayoutMarginsKey = &kRANLayoutMarginsKey;
static const void *kRANMeasuredSizeKey = &kRANMeasuredSizeKey;
static const void *kRANGoneKey = &kRANGoneKey;
static const void *kRANMinimumSizeKey = &kRANMinimumSizeKey;

static CGFloat RANResolveSize(CGFloat desired, RANMeasureSpec spec) {
    switch (spec.mode) {
        case RANMeasureSpecExactly: return spec.size;
        case RANMeasureSpecAtMost: return MIN(desired, spec.size);
        case RANMeasureSpecUnspecified:
        default: return desired;
    }
}

@implementation UIView (RANMeasureLayout)

- (CGFloat)ran_layoutWidth {
    NSNumber *value = objc_getAssociatedObject(self, kRANLayoutWidthKey);
    return value == nil ? RANLayoutWrapContent : value.doubleValue;
}

- (void)setRan_layoutWidth:(CGFloat)width {
    objc_setAssociatedObject(
            self, kRANLayoutWidthKey, @(width)
          , OBJC_ASSOCIATION_RETAIN_NONATOMIC);
}

- (CGFloat)ran_layoutHeight {
    NSNumber *value = objc_getAssociatedObject(self, kRANLayoutHeightKey);
    return value == nil ? RANLayoutWrapContent : value.doubleValue;
}

- (void)setRan_layoutHeight:(CGFloat)height {
    objc_setAssociatedObject(
            self, kRANLayoutHeightKey, @(height)
          , OBJC_ASSOCIATION_RETAIN_NONATOMIC);
}

- (CGFloat)ran_layoutWeight {
    NSNumber *value = objc_getAssociatedObject(self, kRANLayoutWeightKey);
    return value == nil ? 0 : value.doubleValue;
}

- (void)setRan_layoutWeight:(CGFloat)weight {
    objc_setAssociatedObject(
            self, kRANLayoutWeightKey, @(weight)
          , OBJC_ASSOCIATION_RETAIN_NONATOMIC);
}

- (RANGravity)ran_layoutGravity {
    NSNumber *value = objc_getAssociatedObject(self, kRANLayoutGravityKey);
    return value == nil ? 0 : (RANGravity)value.unsignedIntegerValue;
}

- (void)setRan_layoutGravity:(RANGravity)gravity {
    objc_setAssociatedObject(
            self, kRANLayoutGravityKey, @(gravity)
          , OBJC_ASSOCIATION_RETAIN_NONATOMIC);
}

- (UIEdgeInsets)ran_layoutMargins {
    NSValue *value = objc_getAssociatedObject(self, kRANLayoutMarginsKey);
    return value == nil ? UIEdgeInsetsZero : value.UIEdgeInsetsValue;
}

- (void)setRan_layoutMargins:(UIEdgeInsets)margins {
    objc_setAssociatedObject(
            self, kRANLayoutMarginsKey
          , [NSValue valueWithUIEdgeInsets:margins]
          , OBJC_ASSOCIATION_RETAIN_NONATOMIC);
}

- (CGSize)ran_measuredSize {
    NSValue *value = objc_getAssociatedObject(self, kRANMeasuredSizeKey);
    return value == nil ? CGSizeZero : value.CGSizeValue;
}

- (void)setRan_measuredSize:(CGSize)size {
    objc_setAssociatedObject(
            self, kRANMeasuredSizeKey
          , [NSValue valueWithCGSize:size]
          , OBJC_ASSOCIATION_RETAIN_NONATOMIC);
}

- (BOOL)ran_gone {
    NSNumber *value = objc_getAssociatedObject(self, kRANGoneKey);
    return value.boolValue;
}

- (void)setRan_gone:(BOOL)gone {
    objc_setAssociatedObject(
            self, kRANGoneKey, @(gone)
          , OBJC_ASSOCIATION_RETAIN_NONATOMIC);
}

- (CGSize)ran_minimumSize {
    NSValue *value = objc_getAssociatedObject(self, kRANMinimumSizeKey);
    return value == nil ? CGSizeZero : value.CGSizeValue;
}

- (void)setRan_minimumSize:(CGSize)size {
    objc_setAssociatedObject(
            self, kRANMinimumSizeKey
          , [NSValue valueWithCGSize:size]
          , OBJC_ASSOCIATION_RETAIN_NONATOMIC);
}

// Leaf measurement. Labels, images and buttons size through sizeThatFits,
// which honours numberOfLines and image dimensions the way Android's
// onMeasure honours maxLines and drawable bounds. A plain UIView reports
// zero content - sizeThatFits on it would echo its current bounds, and a
// spacer that remembers its old size breaks every weighted pass.
- (void)ran_measureWithWidthSpec:(RANMeasureSpec)widthSpec
                     heightSpec:(RANMeasureSpec)heightSpec {
    CGSize content = CGSizeZero;
    if (![self isMemberOfClass:UIView.class]) {
        CGFloat availableWidth = widthSpec.mode == RANMeasureSpecUnspecified
                ? CGFLOAT_MAX
                : widthSpec.size;
        CGFloat availableHeight = heightSpec.mode == RANMeasureSpecUnspecified
                ? CGFLOAT_MAX
                : heightSpec.size;
        content = [self sizeThatFits:
                CGSizeMake(availableWidth, availableHeight)];
    }
    CGSize minimum = self.ran_minimumSize;
    content.width = MAX(content.width, minimum.width);
    content.height = MAX(content.height, minimum.height);
    self.ran_measuredSize = CGSizeMake(
            RANResolveSize(ceil(content.width), widthSpec)
          , RANResolveSize(ceil(content.height), heightSpec));
}

- (void)ran_layoutWithFrame:(CGRect)frame {
    self.frame = frame;
}

+ (RANMeasureSpec)ran_childMeasureSpecForParentSpec:(RANMeasureSpec)parentSpec
                                          padding:(CGFloat)padding
                                   childDimension:(CGFloat)childDimension {
    CGFloat available = MAX(0, parentSpec.size - padding);
    if (childDimension >= 0) {
        return RANMeasureSpecMake(RANMeasureSpecExactly, childDimension);
    }

    switch (parentSpec.mode) {
        case RANMeasureSpecExactly:
            return childDimension == RANLayoutMatchParent
                    ? RANMeasureSpecMake(RANMeasureSpecExactly, available)
                    : RANMeasureSpecMake(RANMeasureSpecAtMost, available);
        case RANMeasureSpecAtMost:
            return RANMeasureSpecMake(RANMeasureSpecAtMost, available);
        case RANMeasureSpecUnspecified:
        default:
            return RANMeasureSpecMake(RANMeasureSpecUnspecified, 0);
    }
}

@end

@implementation RANLayoutContainerView

- (instancetype)initWithFrame:(CGRect)frame {
    self = [super initWithFrame:frame];
    if (self != nil) _ran_padding = UIEdgeInsetsZero;
    return self;
}

@end

// Axis-neutral accessors keep one implementation honest for both
// orientations instead of two long near-copies drifting apart.
static CGFloat RANAxisSize(CGSize size, BOOL mainIsVertical) {
    return mainIsVertical ? size.height : size.width;
}

static CGFloat RANCrossSize(CGSize size, BOOL mainIsVertical) {
    return mainIsVertical ? size.width : size.height;
}

static CGFloat RANAxisMargins(UIEdgeInsets margins, BOOL mainIsVertical) {
    return mainIsVertical
            ? margins.top + margins.bottom
            : margins.left + margins.right;
}

static CGFloat RANCrossMargins(UIEdgeInsets margins, BOOL mainIsVertical) {
    return mainIsVertical
            ? margins.left + margins.right
            : margins.top + margins.bottom;
}

@implementation RANLinearLayoutView

- (CGFloat)ran_mainDimensionOf:(UIView *)child {
    return self.ran_vertical ? child.ran_layoutHeight : child.ran_layoutWidth;
}

- (CGFloat)ran_crossDimensionOf:(UIView *)child {
    return self.ran_vertical ? child.ran_layoutWidth : child.ran_layoutHeight;
}

- (void)ran_measureChild:(UIView *)child
           mainAxisSpec:(RANMeasureSpec)mainSpec
          crossAxisSpec:(RANMeasureSpec)crossSpec
               mainUsed:(CGFloat)mainUsed
          mainDimension:(CGFloat)mainDimension {
    UIEdgeInsets padding = self.ran_padding;
    UIEdgeInsets margins = child.ran_layoutMargins;
    BOOL vertical = self.ran_vertical;
    CGFloat mainPadding = vertical
            ? padding.top + padding.bottom
            : padding.left + padding.right;
    CGFloat crossPadding = vertical
            ? padding.left + padding.right
            : padding.top + padding.bottom;

    RANMeasureSpec childMainSpec = [UIView
            ran_childMeasureSpecForParentSpec:mainSpec
                                     padding:mainPadding
                                             + RANAxisMargins(margins, vertical)
                                             + mainUsed
                              childDimension:mainDimension];
    RANMeasureSpec childCrossSpec = [UIView
            ran_childMeasureSpecForParentSpec:crossSpec
                                     padding:crossPadding
                                             + RANCrossMargins(margins, vertical)
                              childDimension:[self ran_crossDimensionOf:child]];
    if (vertical) {
        [child ran_measureWithWidthSpec:childCrossSpec
                            heightSpec:childMainSpec];
    } else {
        [child ran_measureWithWidthSpec:childMainSpec
                            heightSpec:childCrossSpec];
    }
}

- (void)ran_measureWithWidthSpec:(RANMeasureSpec)widthSpec
                     heightSpec:(RANMeasureSpec)heightSpec {
    BOOL vertical = self.ran_vertical;
    RANMeasureSpec mainSpec = vertical ? heightSpec : widthSpec;
    RANMeasureSpec crossSpec = vertical ? widthSpec : heightSpec;
    UIEdgeInsets padding = self.ran_padding;
    CGFloat mainPadding = vertical
            ? padding.top + padding.bottom
            : padding.left + padding.right;
    CGFloat crossPadding = vertical
            ? padding.left + padding.right
            : padding.top + padding.bottom;

    CGFloat totalMain = 0;
    CGFloat maxCross = 0;
    CGFloat totalWeight = 0;
    for (UIView *child in self.subviews) {
        if (child.ran_gone) continue;

        CGFloat weight = child.ran_layoutWeight;
        totalWeight += weight;
        UIEdgeInsets margins = child.ran_layoutMargins;
        CGFloat mainDimension = [self ran_mainDimensionOf:child];
        if (mainSpec.mode == RANMeasureSpecExactly
                && mainDimension == 0
                && weight > 0) {
            // Uses only leftover space; measured in the weight pass.
            totalMain += RANAxisMargins(margins, vertical);
            continue;
        }
        if (mainDimension == 0 && weight > 0) {
            // Not EXACTLY, so there is no leftover to claim: the child
            // wanted to stretch, translate that to WRAP_CONTENT so it does
            // not end up with a size of zero. This is the Android rule the
            // rect ad engine's natural-height pass depends on.
            mainDimension = RANLayoutWrapContent;
        }

        // As Android's LinearLayout: once a weighted child is in, the space
        // already used is not taken off the next child's spec - the weight
        // pass settles the sizes - so a child is not measured against less
        // room than the Android tree gives it.
        [self ran_measureChild:child
                 mainAxisSpec:mainSpec
                crossAxisSpec:crossSpec
                     mainUsed:totalWeight == 0 ? totalMain : 0
                mainDimension:mainDimension];
        CGSize measured = child.ran_measuredSize;
        totalMain += RANAxisSize(measured, vertical)
                + RANAxisMargins(margins, vertical);
        maxCross = MAX(
                maxCross
              , RANCrossSize(measured, vertical)
                        + RANCrossMargins(margins, vertical));
    }
    totalMain += mainPadding;

    // As Android's LinearLayout: a minimum along the main axis is part of the
    // size the weights share out, so a stretched spacer claims it.
    CGSize minimum = self.ran_minimumSize;
    CGFloat minimumMain = vertical ? minimum.height : minimum.width;
    CGFloat resolvedMain = RANResolveSize(MAX(totalMain, minimumMain), mainSpec);
    CGFloat delta = resolvedMain - totalMain;
    if (totalWeight > 0 && delta != 0) {
        CGFloat remainingWeight = totalWeight;
        CGFloat remainingDelta = delta;
        totalMain = mainPadding;
        maxCross = 0;
        for (UIView *child in self.subviews) {
            if (child.ran_gone) continue;

            UIEdgeInsets margins = child.ran_layoutMargins;
            CGFloat weight = child.ran_layoutWeight;
            if (weight > 0) {
                CGFloat share = remainingDelta * weight / remainingWeight;
                remainingWeight -= weight;
                remainingDelta -= share;
                CGFloat mainDimension = [self ran_mainDimensionOf:child];
                CGFloat base = mainDimension == 0
                        ? 0
                        : RANAxisSize(child.ran_measuredSize, vertical);
                [self ran_measureChild:child
                         mainAxisSpec:RANMeasureSpecMake(
                                RANMeasureSpecExactly
                              , MAX(0, base + share))
                        crossAxisSpec:crossSpec
                             mainUsed:0
                        mainDimension:RANLayoutMatchParent];
            }
            CGSize measured = child.ran_measuredSize;
            totalMain += RANAxisSize(measured, vertical)
                    + RANAxisMargins(margins, vertical);
            maxCross = MAX(
                    maxCross
                  , RANCrossSize(measured, vertical)
                            + RANCrossMargins(margins, vertical));
        }
        resolvedMain = RANResolveSize(MAX(totalMain, minimumMain), mainSpec);
    }

    CGFloat resolvedCross = RANResolveSize(maxCross + crossPadding, crossSpec);
    CGSize resolved = vertical
            ? CGSizeMake(resolvedCross, resolvedMain)
            : CGSizeMake(resolvedMain, resolvedCross);
    resolved.width = MAX(resolved.width, minimum.width);
    resolved.height = MAX(resolved.height, minimum.height);
    self.ran_measuredSize = resolved;
}

- (void)ran_layoutWithFrame:(CGRect)frame {
    self.frame = frame;
    BOOL vertical = self.ran_vertical;
    UIEdgeInsets padding = self.ran_padding;
    CGRect content = UIEdgeInsetsInsetRect(
            CGRectMake(0, 0, frame.size.width, frame.size.height)
          , padding);

    CGFloat totalMain = 0;
    for (UIView *child in self.subviews) {
        if (child.ran_gone) continue;
        totalMain += RANAxisSize(child.ran_measuredSize, vertical)
                + RANAxisMargins(child.ran_layoutMargins, vertical);
    }

    CGFloat leftover = RANAxisSize(content.size, vertical) - totalMain;
    RANGravity gravity = self.ran_gravity;
    CGFloat mainCursor = vertical ? content.origin.y : content.origin.x;
    if (vertical) {
        if (gravity & RANGravityCenterVertical) mainCursor += leftover / 2;
        else if (gravity & RANGravityBottom) mainCursor += leftover;
    } else {
        if (gravity & RANGravityCenterHorizontal) mainCursor += leftover / 2;
        else if (gravity & RANGravityRight) mainCursor += leftover;
    }

    for (UIView *child in self.subviews) {
        if (child.ran_gone) continue;

        UIEdgeInsets margins = child.ran_layoutMargins;
        CGSize measured = child.ran_measuredSize;
        RANGravity childGravity = child.ran_layoutGravity;
        if (childGravity == 0) childGravity = gravity;

        CGFloat crossStart = vertical ? content.origin.x : content.origin.y;
        CGFloat crossAvailable = RANCrossSize(content.size, vertical)
                - RANCrossSize(measured, vertical)
                - RANCrossMargins(margins, vertical);
        CGFloat crossOffset = 0;
        if (vertical) {
            if (childGravity & RANGravityCenterHorizontal) {
                crossOffset = crossAvailable / 2;
            } else if (childGravity & RANGravityRight) {
                crossOffset = crossAvailable;
            }
        } else {
            if (childGravity & RANGravityCenterVertical) {
                crossOffset = crossAvailable / 2;
            } else if (childGravity & RANGravityBottom) {
                crossOffset = crossAvailable;
            }
        }

        CGRect childFrame;
        if (vertical) {
            childFrame = CGRectMake(
                    crossStart + margins.left + crossOffset
                  , mainCursor + margins.top
                  , measured.width
                  , measured.height);
            mainCursor += measured.height + margins.top + margins.bottom;
        } else {
            childFrame = CGRectMake(
                    mainCursor + margins.left
                  , crossStart + margins.top + crossOffset
                  , measured.width
                  , measured.height);
            mainCursor += measured.width + margins.left + margins.right;
        }
        [child ran_layoutWithFrame:childFrame];
    }
}

@end

@implementation RANFrameLayoutView

- (void)ran_measureWithWidthSpec:(RANMeasureSpec)widthSpec
                     heightSpec:(RANMeasureSpec)heightSpec {
    UIEdgeInsets padding = self.ran_padding;
    CGFloat horizontalPadding = padding.left + padding.right;
    CGFloat verticalPadding = padding.top + padding.bottom;

    CGFloat maxWidth = 0;
    CGFloat maxHeight = 0;
    NSMutableArray<UIView *> *matchParentChildren = [NSMutableArray array];
    for (UIView *child in self.subviews) {
        if (child.ran_gone) continue;

        UIEdgeInsets margins = child.ran_layoutMargins;
        [child ran_measureWithWidthSpec:
                        [UIView ran_childMeasureSpecForParentSpec:widthSpec
                                padding:horizontalPadding
                                        + margins.left + margins.right
                                 childDimension:child.ran_layoutWidth]
                            heightSpec:
                        [UIView ran_childMeasureSpecForParentSpec:heightSpec
                                padding:verticalPadding
                                        + margins.top + margins.bottom
                                 childDimension:child.ran_layoutHeight]];
        CGSize measured = child.ran_measuredSize;
        maxWidth = MAX(
                maxWidth
              , measured.width + margins.left + margins.right);
        maxHeight = MAX(
                maxHeight
              , measured.height + margins.top + margins.bottom);
        if (child.ran_layoutWidth == RANLayoutMatchParent
                || child.ran_layoutHeight == RANLayoutMatchParent) {
            [matchParentChildren addObject:child];
        }
    }

    CGSize minimum = self.ran_minimumSize;
    CGFloat resolvedWidth = MAX(
            RANResolveSize(maxWidth + horizontalPadding, widthSpec)
          , minimum.width);
    CGFloat resolvedHeight = MAX(
            RANResolveSize(maxHeight + verticalPadding, heightSpec)
          , minimum.height);
    self.ran_measuredSize = CGSizeMake(resolvedWidth, resolvedHeight);

    // Same second pass FrameLayout runs: once the frame's own size is known,
    // MATCH_PARENT children are remeasured to fill it exactly.
    for (UIView *child in matchParentChildren) {
        UIEdgeInsets margins = child.ran_layoutMargins;
        CGFloat childWidth = child.ran_layoutWidth == RANLayoutMatchParent
                ? MAX(0, resolvedWidth - horizontalPadding
                        - margins.left - margins.right)
                : child.ran_measuredSize.width;
        CGFloat childHeight = child.ran_layoutHeight == RANLayoutMatchParent
                ? MAX(0, resolvedHeight - verticalPadding
                        - margins.top - margins.bottom)
                : child.ran_measuredSize.height;
        [child ran_measureWithWidthSpec:
                        RANMeasureSpecMake(RANMeasureSpecExactly, childWidth)
                            heightSpec:
                        RANMeasureSpecMake(RANMeasureSpecExactly, childHeight)];
    }
}

- (void)ran_layoutWithFrame:(CGRect)frame {
    self.frame = frame;
    UIEdgeInsets padding = self.ran_padding;
    CGRect content = UIEdgeInsetsInsetRect(
            CGRectMake(0, 0, frame.size.width, frame.size.height)
          , padding);

    for (UIView *child in self.subviews) {
        if (child.ran_gone) continue;

        UIEdgeInsets margins = child.ran_layoutMargins;
        CGSize measured = child.ran_measuredSize;
        RANGravity gravity = child.ran_layoutGravity;

        CGFloat x = content.origin.x + margins.left;
        if (gravity & RANGravityCenterHorizontal) {
            x = content.origin.x
                    + (content.size.width - measured.width) / 2;
        } else if (gravity & RANGravityRight) {
            x = CGRectGetMaxX(content) - measured.width - margins.right;
        }

        CGFloat y = content.origin.y + margins.top;
        if (gravity & RANGravityCenterVertical) {
            y = content.origin.y
                    + (content.size.height - measured.height) / 2;
        } else if (gravity & RANGravityBottom) {
            y = CGRectGetMaxY(content) - measured.height - margins.bottom;
        }

        [child ran_layoutWithFrame:
                CGRectMake(x, y, measured.width, measured.height)];
    }
}

@end