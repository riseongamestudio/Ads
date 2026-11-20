#import "RANRectAdLayoutValidator.h"

static NSString *const kRANTag = @"RectAd";

static const CGFloat kRANMinVideoMedia = 120;      // pt, dp on Android
static const CGFloat kRANMinVideoLongSidePx = 256; // px, same as Android
static const CGFloat kRANMinVisibleAssetSizePx = 1;
static const CGFloat kRANMinBadgeSizePx = 15;
static const CGFloat kRANVideoSizeTolerance = 0.5;
static const CGFloat kRANTextBoundsTolerance = 1;
static const CGFloat kRANMinMediaAspectRatio = 0.2f;
static const CGFloat kRANMaxMediaAspectRatio = 5.0f;
static const int64_t kRANGeometryHashOffset = 1469598103934665603LL;
static const int64_t kRANGeometryHashPrime = 1099511628211LL;

@implementation RANRectAdLayoutValidator {
    GADNativeAd *_nativeAd;
    RANRectAdViewFactory *_viewFactory;
    CGFloat _screenScale;
}

- (instancetype)initWithNativeAd:(GADNativeAd *)nativeAd
                     viewFactory:(RANRectAdViewFactory *)viewFactory {
    self = [super init];
    if (self == nil) return nil;
    _nativeAd = nativeAd;
    _viewFactory = viewFactory;
    _screenScale = MAX(1, UIScreen.mainScreen.nativeScale);
    return self;
}

- (CGFloat)ran_ptFromPx:(CGFloat)px {
    return px / _screenScale;
}

- (BOOL)ran_failure:(BOOL)logFailure reason:(NSString *)reason {
    _lastFailureReason = reason;
    if (logFailure) {
        NSLog(@"%@: Rect ad validation failed: %@", kRANTag, reason);
    }
    return NO;
}

// nil when the view is absent, hidden, GONE or empty - the same conditions
// DescendantBounds treated as "not on screen".
- (NSValue *)ran_boundsOf:(UIView *)descendant inRoot:(UIView *)root {
    if (descendant == nil
            || descendant.ran_gone
            || descendant.hidden
            || descendant.frame.size.width <= 0
            || descendant.frame.size.height <= 0) {
        return nil;
    }
    CGRect rect = [descendant convertRect:descendant.bounds toView:root];
    return [NSValue valueWithCGRect:rect];
}

- (CGSize)ran_rootSizeOf:(UIView *)root {
    CGSize size = root.bounds.size;
    if (size.width <= 0 || size.height <= 0) size = root.ran_measuredSize;
    return size;
}

- (BOOL)ran_addValidatedAsset:(UIView *)asset
                        root:(UIView *)root
                        name:(NSString *)name
                    minWidth:(CGFloat)minWidth
                   minHeight:(CGFloat)minHeight
                      assets:(NSMutableArray<UIView *> *)assets
                  logFailure:(BOOL)logFailure {
    NSValue *boxed = [self ran_boundsOf:asset inRoot:root];
    if (boxed == nil) {
        return [self ran_failure:logFailure
                         reason:[NSString stringWithFormat:
                                @"%@ is missing or too small", name]];
    }
    CGRect rect = boxed.CGRectValue;
    if (rect.size.width < minWidth || rect.size.height < minHeight) {
        return [self ran_failure:logFailure
                         reason:[NSString stringWithFormat:
                                @"%@ is missing or too small", name]];
    }
    CGSize rootSize = [self ran_rootSizeOf:root];
    if (rect.origin.x < 0
            || rect.origin.y < 0
            || CGRectGetMaxX(rect) > rootSize.width + kRANTextBoundsTolerance
            || CGRectGetMaxY(rect) > rootSize.height + kRANTextBoundsTolerance) {
        return [self ran_failure:logFailure
                         reason:[NSString stringWithFormat:
                                @"%@ is clipped", name]];
    }

    if ([asset isKindOfClass:RANAdTextLabel.class]) {
        RANAdTextLabel *text = (RANAdTextLabel *)asset;
        // A scrolling line still needs its full height: squeezed, it shows
        // the tops of its glyphs going by. Android checks every TextView's
        // layout height the same way.
        CGFloat neededHeight = [text sizeThatFits:
                CGSizeMake(rect.size.width, CGFLOAT_MAX)].height;
        if (neededHeight > rect.size.height + kRANTextBoundsTolerance) {
            return [self ran_failure:logFailure
                             reason:[NSString stringWithFormat:
                                    @"%@ text is clipped", name]];
        }
        if (!text.marquee) {
            if (![text ran_fitsWidthWithoutOverflow:rect.size.width]) {
                return [self ran_failure:logFailure
                                 reason:[NSString stringWithFormat:
                                        @"%@ text overflows horizontally"
                                      , name]];
            }
        }
    } else if ([asset isKindOfClass:UIButton.class]) {
        UIButton *button = (UIButton *)asset;
        CGSize needed = [button.titleLabel sizeThatFits:
                CGSizeMake(
                        rect.size.width
                                - button.contentEdgeInsets.left
                                - button.contentEdgeInsets.right
                      , CGFLOAT_MAX)];
        CGFloat available = rect.size.height
                - button.contentEdgeInsets.top
                - button.contentEdgeInsets.bottom;
        if (needed.height > available + kRANTextBoundsTolerance) {
            return [self ran_failure:logFailure
                             reason:[NSString stringWithFormat:
                                    @"%@ text is clipped", name]];
        }
    }

    [assets addObject:asset];
    return YES;
}

// Either the value is on screen in full, or it is on a line that scrolls
// and will be. A part of it is not an outcome the layout may settle for.
// The exemption is per text: a plan may scroll its body while its headline
// is held to showing everything.
// The Java side runs its policy text check on the call to action as well -
// a Button is a TextView over there, so one method serves both. Here the
// button's title lives in a plain UILabel and RANAdTextLabel's check cannot
// reach it, so the same question is asked directly: would the whole title
// need more height than the label's line cap grants at the width it has?
//
// Every unknown answers YES on purpose. A check that cannot see the
// geometry must not reject a plan: failing open leaves the behavior that
// shipped, while failing closed would empty the feed on one platform.
- (BOOL)ran_validateButtonPolicyText:(UIButton *)button
                               name:(NSString *)name
                         logFailure:(BOOL)logFailure {
    UILabel *label = button.titleLabel;
    NSString *title = label.text ?: @"";
    if (label == nil || title.length == 0 || label.font == nil) return YES;

    CGFloat width = label.frame.size.width;
    if (width <= 0) {
        // The box minus the padding the title is laid out inside - the same
        // subtraction the button branch of ran_addValidatedAsset: makes. The
        // full box would measure the title against up to 20pt it never gets,
        // and pass a label that wraps.
        width = button.frame.size.width
                - button.contentEdgeInsets.left
                - button.contentEdgeInsets.right;
    }
    if (width <= 0) return YES;

    CGRect unbounded = [title
            boundingRectWithSize:CGSizeMake(width, CGFLOAT_MAX)
                         options:NSStringDrawingUsesLineFragmentOrigin
                      attributes:@{NSFontAttributeName: label.font}
                         context:nil];
    NSInteger cappedLines = label.numberOfLines;
    CGFloat allowedHeight = cappedLines <= 0
            ? CGFLOAT_MAX
            : cappedLines * label.font.lineHeight;
    if (ceil(unbounded.size.height) <= ceil(allowedHeight) + 1) return YES;

    return [self ran_failure:logFailure
                     reason:[NSString stringWithFormat:
                            @"%@ shows only part of its value without "
                             "scrolling", name]];
}

- (BOOL)ran_validatePolicyVisibleText:(RANAdTextLabel *)text
                                name:(NSString *)name
                          logFailure:(BOOL)logFailure {
    if (text == nil || text.marquee) return YES;

    CGFloat width = text.frame.size.width;
    if (width <= 0) width = text.ran_measuredSize.width;
    if ([text ran_showsEntireTextForWidth:width]) return YES;

    return [self ran_failure:logFailure
                     reason:[NSString stringWithFormat:
                            @"%@ shows only part of its value without "
                             "scrolling", name]];
}

- (BOOL)validateAssetGeometryForRoot:(UIView *)rootView
                               views:(RANRectAdAssetViews *)views
                                plan:(RANRectAdLayoutPlan *)plan
                          logFailure:(BOOL)logFailure {
    _lastFailureReason = nil;
    if (rootView == nil || views == nil || plan == nil) {
        return [self ran_failure:logFailure
                         reason:@"missing layout root or assets"];
    }
    CGSize rootSize = [self ran_rootSizeOf:rootView];
    if (rootSize.width <= 0 || rootSize.height <= 0) {
        return [self ran_failure:logFailure reason:@"layout has zero size"];
    }

    CGFloat minVisible = [self ran_ptFromPx:kRANMinVisibleAssetSizePx];
    CGFloat minBadge = [self ran_ptFromPx:kRANMinBadgeSizePx];
    NSMutableArray<UIView *> *visibleAssets = [NSMutableArray array];

    if (_nativeAd.headline.length == 0
            || ![self ran_addValidatedAsset:views.headline
                                      root:rootView
                                      name:@"headline"
                                  minWidth:minVisible
                                 minHeight:minVisible
                                    assets:visibleAssets
                                logFailure:logFailure]
            || ![self ran_validatePolicyVisibleText:views.headline
                                              name:@"headline"
                                        logFailure:logFailure]) {
        return NO;
    }

    if (plan.showBody && _nativeAd.body.length > 0) {
        if (![self ran_addValidatedAsset:views.body
                                   root:rootView
                                   name:@"body"
                               minWidth:minVisible
                              minHeight:minVisible
                                 assets:visibleAssets
                             logFailure:logFailure]
                || ![self ran_validatePolicyVisibleText:views.body
                                                  name:@"body"
                                            logFailure:logFailure]) {
            return NO;
        }
    }

    if (_nativeAd.callToAction.length == 0
            || ![self ran_addValidatedAsset:views.callToAction
                                      root:rootView
                                      name:@"call to action"
                                  minWidth:minVisible
                                 minHeight:[_viewFactory
                                        callToActionHeightForPlan:plan]
                                    assets:visibleAssets
                                logFailure:logFailure]
            || ![self ran_validateButtonPolicyText:views.callToAction
                                             name:@"call to action"
                                       logFailure:logFailure]) {
        return NO;
    }

    if (plan.showIcon && [_viewFactory hasRenderableIcon]) {
        if (![self ran_addValidatedAsset:views.icon
                                   root:rootView
                                   name:@"icon"
                               minWidth:minVisible
                              minHeight:minVisible
                                 assets:visibleAssets
                             logFailure:logFailure]) {
            return NO;
        }
    }

    if (![self ran_addValidatedAsset:views.attribution
                               root:rootView
                               name:@"ad attribution"
                           minWidth:minBadge
                          minHeight:minBadge
                             assets:visibleAssets
                         logFailure:logFailure]) {
        return NO;
    }
    if (views.adChoicesReserve != nil
            && ![self ran_addValidatedAsset:views.adChoicesReserve
                                      root:rootView
                                      name:@"AdChoices"
                                  minWidth:minBadge
                                 minHeight:minBadge
                                    assets:visibleAssets
                                logFailure:logFailure]) {
        return NO;
    }

    if (plan.renderVideo) {
        if (![self ran_addValidatedAsset:views.media
                                   root:rootView
                                   name:@"MediaView"
                               minWidth:kRANMinVideoMedia
                              minHeight:kRANMinVideoMedia
                                 assets:visibleAssets
                             logFailure:logFailure]) {
            return NO;
        }
        NSValue *mediaBounds = [self ran_boundsOf:views.media inRoot:rootView];
        CGRect mediaRect = mediaBounds.CGRectValue;
        if (mediaBounds == nil
                || ![self isPolicySafeVideoSizeWithWidth:mediaRect.size.width
                                                  height:mediaRect.size.height]) {
            return [self ran_failure:logFailure
                             reason:@"fitted video is below the "
                                     "120dp/256px minimum"];
        }
    } else if (plan.showMedia) {
        // Video's floor for a video creative, the image floor otherwise: the
        // SDK's 120pt warning is conditional on the view showing video, and a
        // creative with none never will.
        if (![self ran_addValidatedAsset:views.media
                                   root:rootView
                                   name:@"main image"
                               minWidth:[_viewFactory mediaPolicyFloor]
                              minHeight:[_viewFactory mediaPolicyFloor]
                                 assets:visibleAssets
                             logFailure:logFailure]) {
            return NO;
        }
    }

    if (plan.showAdvertiser && views.advertiser != nil) {
        if (![self ran_addValidatedAsset:views.advertiser
                                   root:rootView
                                   name:@"advertiser"
                               minWidth:minVisible
                              minHeight:minVisible
                                 assets:visibleAssets
                             logFailure:logFailure]
                || ![self ran_validatePolicyVisibleText:views.advertiser
                                                  name:@"advertiser"
                                            logFailure:logFailure]) {
            return NO;
        }
    }
    if (plan.showRating && views.rating != nil) {
        if (![self ran_addValidatedAsset:views.rating
                                   root:rootView
                                   name:@"star rating"
                               minWidth:minVisible
                              minHeight:minVisible
                                 assets:visibleAssets
                             logFailure:logFailure]) {
            return NO;
        }
    }

    for (NSUInteger first = 0; first < visibleAssets.count; ++first) {
        NSValue *firstBoxed =
                [self ran_boundsOf:visibleAssets[first] inRoot:rootView];
        if (firstBoxed == nil) continue;
        for (NSUInteger second = first + 1;
             second < visibleAssets.count;
             ++second) {
            NSValue *secondBoxed =
                    [self ran_boundsOf:visibleAssets[second] inRoot:rootView];
            if (secondBoxed == nil) continue;
            if (CGRectIntersectsRect(
                        firstBoxed.CGRectValue
                      , secondBoxed.CGRectValue)
                    && ![self ran_isAllowedBadgeOverlay:visibleAssets[first]
                                                second:visibleAssets[second]
                                                 views:views]
                    && ![self ran_isAllowedScrimOverlay:visibleAssets[first]
                                                second:visibleAssets[second]
                                                 views:views]) {
                return [self ran_failure:logFailure
                                 reason:@"registered assets overlap"];
            }
        }
    }
    return YES;
}

// Anything inside the scrim block sits over the full-cell veil the
// media-background template paints, so it may lie over the media without
// costing readability - the same warrant the badges carry. The scrim only
// exists on that template.
- (BOOL)ran_isAllowedScrimOverlay:(UIView *)first
                          second:(UIView *)second
                           views:(RANRectAdAssetViews *)views {
    if (views.scrim == nil) return NO;

    BOOL (^insideScrim)(UIView *) = ^BOOL(UIView *view) {
        UIView *node = view;
        while (node != nil) {
            if (node == views.scrim) return YES;
            node = node.superview;
        }
        return NO;
    };
    return (first == views.media && insideScrim(second))
            || (second == views.media && insideScrim(first));
}

// A badge sits over a picture without hiding anything that has to be read;
// text under a badge cannot be read at all, so those overlaps stay fatal.
- (BOOL)ran_isAllowedBadgeOverlay:(UIView *)first
                          second:(UIView *)second
                           views:(RANRectAdAssetViews *)views {
    BOOL (^isBadge)(UIView *) = ^BOOL(UIView *view) {
        return view == views.attribution || view == views.adChoicesReserve;
    };
    BOOL (^isReadableText)(UIView *) = ^BOOL(UIView *view) {
        return view == views.headline
                || view == views.body
                || view == views.advertiser
                || view == views.rating
                || view == views.callToAction;
    };
    return (isBadge(first) && !isReadableText(second))
            || (isBadge(second) && !isReadableText(first));
}

// ---------------------------------------------------------------------------
// Geometry signature - the stability probe the observation loop hashes.
// ---------------------------------------------------------------------------

static int64_t RANMixGeometry(int64_t signature, int64_t value) {
    return (int64_t)(((uint64_t)signature ^ (uint64_t)value)
            * (uint64_t)kRANGeometryHashPrime);
}

- (int64_t)ran_mixView:(UIView *)view
                 root:(UIView *)root
            signature:(int64_t)signature {
    if (view == nil) return RANMixGeometry(signature, -1);

    signature = RANMixGeometry(signature, view.hidden ? 1 : 0);
    NSValue *boxed = [self ran_boundsOf:view inRoot:root];
    if (boxed == nil) return RANMixGeometry(signature, -2);

    CGRect rect = boxed.CGRectValue;
    signature = RANMixGeometry(signature, (int64_t)llround(rect.origin.x * 2));
    signature = RANMixGeometry(signature, (int64_t)llround(rect.origin.y * 2));
    signature = RANMixGeometry(
            signature, (int64_t)llround(CGRectGetMaxX(rect) * 2));
    signature = RANMixGeometry(
            signature, (int64_t)llround(CGRectGetMaxY(rect) * 2));
    if ([view isKindOfClass:RANAdTextLabel.class]) {
        RANAdTextLabel *text = (RANAdTextLabel *)view;
        signature = RANMixGeometry(signature, (int64_t)text.text.length);
        signature = RANMixGeometry(
                signature
              , [text ran_showsEntireTextForWidth:rect.size.width] ? 1 : 0);
    }
    return signature;
}

- (int64_t)geometrySignatureForHost:(UIView *)host
                               root:(UIView *)root
                              views:(RANRectAdAssetViews *)views {
    int64_t signature = kRANGeometryHashOffset;
    CGSize rootSize = [self ran_rootSizeOf:root];
    signature = RANMixGeometry(signature, (int64_t)llround(rootSize.width));
    signature = RANMixGeometry(signature, (int64_t)llround(rootSize.height));
    signature = RANMixGeometry(
            signature, (int64_t)llround(host.frame.origin.x));
    signature = RANMixGeometry(
            signature, (int64_t)llround(host.frame.origin.y));
    signature = [self ran_mixView:views.headline root:root signature:signature];
    signature = [self ran_mixView:views.body root:root signature:signature];
    signature = [self ran_mixView:views.advertiser root:root signature:signature];
    signature = [self ran_mixView:views.rating root:root signature:signature];
    signature = [self ran_mixView:views.icon root:root signature:signature];
    signature = [self ran_mixView:views.callToAction root:root signature:signature];
    signature = [self ran_mixView:views.media root:root signature:signature];
    signature = [self ran_mixView:views.attribution root:root signature:signature];
    return [self ran_mixView:views.adChoicesReserve
                       root:root
                  signature:signature];
}

// ---------------------------------------------------------------------------
// Video policy sizes - the 256 side stays a pixel figure like Android's.
// ---------------------------------------------------------------------------

- (CGFloat)ran_minVideoLongSide {
    return [self ran_ptFromPx:kRANMinVideoLongSidePx];
}

- (CGFloat)ran_videoAspectRatio {
    GADMediaContent *content = _nativeAd.mediaContent;
    CGFloat aspect = content.aspectRatio;
    if (content != nil && aspect > 0) {
        return MAX(kRANMinMediaAspectRatio, MIN(kRANMaxMediaAspectRatio, aspect));
    }
    return 1;
}

- (CGFloat)requiredVideoWidthForAspect:(CGFloat)creativeAspect {
    CGFloat minimumContentWidth = creativeAspect >= 1
            ? [self ran_minVideoLongSide]
            : ceil([self ran_minVideoLongSide] * creativeAspect);
    return MAX(kRANMinVideoMedia, minimumContentWidth);
}

- (CGFloat)requiredVideoHeightForAspect:(CGFloat)creativeAspect {
    CGFloat minimumContentHeight = creativeAspect >= 1
            ? ceil([self ran_minVideoLongSide] / creativeAspect)
            : [self ran_minVideoLongSide];
    return MAX(kRANMinVideoMedia, minimumContentHeight);
}

- (BOOL)isPolicySafeVideoSizeWithWidth:(CGFloat)width height:(CGFloat)height {
    if (width < kRANMinVideoMedia || height < kRANMinVideoMedia) return NO;

    CGFloat aspect = [self ran_videoAspectRatio];
    CGFloat fittedWidth = width;
    CGFloat fittedHeight = fittedWidth / aspect;
    if (fittedHeight > height) {
        fittedHeight = height;
        fittedWidth = fittedHeight * aspect;
    }
    return MAX(fittedWidth, fittedHeight) + kRANVideoSizeTolerance
            >= [self ran_minVideoLongSide];
}

@end