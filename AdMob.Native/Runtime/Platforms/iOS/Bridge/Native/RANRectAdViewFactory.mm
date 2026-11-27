#import "RANRectAdViewFactory.h"
#import "RANAdTheme.h"

static NSString *const kRANTag = @"RectAd";

// Values carried over verbatim; dp reads as points, px floors convert
// through the screen scale where noted.
// The 120pt floor holds for every creative, a still picture's as well as a
// video's: the SDK flags a smaller MediaView as an implementation problem and
// warns that such slots will be demonetized. A slot too small for it takes a
// layout without the picture.
static const CGFloat kRANMinVideoMediaSize = 120;
static const CGFloat kRANMinImageMediaSize = 120;
// The least line width worth giving a headline next to an icon. Below it
// even a marquee reads as a sliver, so the icon leaves the line and the
// text takes the full width instead.
static const CGFloat kRANRectAdMinIconRowTextWidth = 72;
// The dimming over the ambient backdrop - dark enough that the fitted
// creative in front stays the one that reads as the picture.
static const CGFloat kRANRectAdAmbientDimAlpha = 0.55f;
static const CGFloat kRANMinAttributionSizePx = 15;
static const CGFloat kRANAttributionWidth = 24;
static const CGFloat kRANAttributionHeight = 18;
// The AdChoices view's side, whatever the cell: the SDK draws its mark in
// the view, so the validator and the dodge both see the real mark.
static const CGFloat kRANAdChoicesMarkSize = 15;
static const CGFloat kRANAdChoicesDodgeGap = 2;
static const NSInteger kRANAdChoicesDodgePasses = 3;
// On a 96dp slot the short-side ratio lands on this floor every time, so
// the floor is the height; 24dp presses (the whole rect is clickable) and
// leaves the assets above it their share.
static const CGFloat kRANCtaMinHeight = 24;
// The smallest target a finger finds reliably (Material's 48dp), so a
// two-letter label does not shrink the button to a sliver.
static const CGFloat kRANCtaMinWidth = 48;
static const CGFloat kRANVideoRailMinWidthCompact = 72;
static const CGFloat kRANVideoRailMinWidthRegular = 88;
static const CGFloat kRANVideoRailMinWidthRoomy = 104;
static const CGFloat kRANVideoRailIconCompact = 36;
static const CGFloat kRANVideoRailIconRegular = 40;
static const CGFloat kRANVideoRailIconRoomy = 48;
static const CGFloat kRANVideoRailCtaHeightCompact = 32;
static const CGFloat kRANVideoRailCtaHeightRegular = 36;
static const CGFloat kRANVideoRailCtaHeightRoomy = 40;
static const CGFloat kRANVideoRailEdgePadding = 2;
static const CGFloat kRANVideoHeadlineHorizontalPadding = 4;
static const CGFloat kRANVideoHeadlineVerticalPadding = 2;
static const CGFloat kRANMediaLeftTextWidthCompact = 80;
static const CGFloat kRANMediaLeftTextWidthRegular = 96;
static const CGFloat kRANMediaLeftTextWidthRoomy = 120;
// The floor for an icon standing in for the media: anything under this
// reads as decoration, not as an asset worth the space it was given.
static const CGFloat kRANMinFillerIcon = 24;
// Rating and advertiser stop riding the scale ladder below this.
static const CGFloat kRANOptionalTextMinSize = 10;
static const CGFloat kRANBadgeShortSideRatio = 0.10f;
static const CGFloat kRANAttributionAspectRatio = 4.0f / 3.0f;
static const CGFloat kRANAttributionTextHeightRatio = 0.55f;
// Boxes are sized from the ad, labels are sized from their box; the call to
// action box is deliberately 1.8x the badge box.
static const CGFloat kRANCtaShortSideRatio = 0.18f;
static const CGFloat kRANCtaMaxHeight = 36;
static const CGFloat kRANCtaTextHeightRatio = 0.45f;
static const double kRANMaxStarRating = 5;
// The tier is a ladder of text sizes, not a claim about the cell: a 96pt
// cell can win on the roomy rung, and when it does its borders must still
// be a small cell's borders. Every spacing the tier hands out is therefore
// capped by the cell's own short side.
static const CGFloat kRANCellSpacingRatio = 0.02f;
static const CGFloat kRANCellCtaPaddingRatio = 0.035f;
static const CGFloat kRANCellEdgePaddingRatio = 0.015f;
// How far a straight-edged asset has to stand back from a rounded corner
// of radius r so that its own corner just touches the arc: the arc's
// center sits r in from both edges, the asset's corner sits d in from
// both, and the two are r apart exactly when (r - d) * sqrt(2) = r, so
// d = r * (1 - 1 / sqrt(2)). Less and the corner is cut off; more and the
// cell wastes border it was never asked for.
static const double kRANCornerInsetRatio = 1.0 - M_SQRT1_2;

static UIColor *RANRectAdARGB(uint32_t argb) {
    return [UIColor colorWithRed:((argb >> 16) & 0xFF) / 255.0
                           green:((argb >> 8) & 0xFF) / 255.0
                            blue:(argb & 0xFF) / 255.0
                           alpha:((argb >> 24) & 0xFF) / 255.0];
}

@implementation RANRectAdAssetViews
@end

@implementation RANRectAdProbeLayout
@end

@implementation RANRectAdNativeAdViewResult
@end

// The height handed to the button is a minimum; the row it sits in can
// stretch it, and the label always follows the height the button actually
// ends up with.
@interface RANRectAdCallToActionButton : UIButton
- (void)ran_applyLabelSizeForHeight:(CGFloat)height;
@end

@implementation RANRectAdCallToActionButton

- (void)layoutSubviews {
    [super layoutSubviews];
    [self ran_applyLabelSizeForHeight:self.bounds.size.height];
    // A pill, whatever height the row stretched it to.
    CGSize size = self.bounds.size;
    self.layer.cornerRadius = MIN(size.width, size.height) / 2;
}

- (void)ran_applyLabelSizeForHeight:(CGFloat)height {
    CGFloat target = MAX(1, height) * kRANCtaTextHeightRatio;
    if (fabs(self.titleLabel.font.pointSize - target) < 0.5) return;
    self.titleLabel.font = [self.titleLabel.font fontWithSize:target];
}

@end

// The filler absorbs leftover height, so what it asks for is only the
// smallest icon worth showing; the weighted slot then stretches it over
// whatever the fitted text left free. Without this an unconstrained measure
// asks the image for its bitmap size - routinely taller than the slot - and
// every icon variant is rejected as not fitting.
@interface RANRectAdIconFillerView : RANFrameLayoutView
@property (nonatomic) CGFloat reservedHeight;
@end

@implementation RANRectAdIconFillerView

- (void)ran_measureWithWidthSpec:(RANMeasureSpec)widthSpec
                     heightSpec:(RANMeasureSpec)heightSpec {
    if (heightSpec.mode == RANMeasureSpecExactly) {
        [super ran_measureWithWidthSpec:widthSpec heightSpec:heightSpec];
        return;
    }

    CGFloat height = self.reservedHeight;
    if (heightSpec.mode == RANMeasureSpecAtMost) {
        height = MIN(height, heightSpec.size);
    }
    [super ran_measureWithWidthSpec:widthSpec
                        heightSpec:RANMeasureSpecMake(
                                RANMeasureSpecExactly, height)];
}

@end

// A GADMediaView whose still-image fallback child always fills it.
@interface RANRectAdMediaView : GADMediaView
@property (nonatomic, strong, nullable) UIImageView *fallbackImageView;
@property (nonatomic, strong, nullable) UIView *ambientBackdropView;
@end

@implementation RANRectAdMediaView

- (void)layoutSubviews {
    [super layoutSubviews];
    self.ambientBackdropView.frame = self.bounds;
    self.fallbackImageView.frame = self.bounds;
}

@end

// One anchor per block: where the icon stands alone above the text, the
// text centers under it. A centered icon over left-aligned lines reads as
// two blocks that never agreed with each other.
static void RANRectAdCentreUnderIcon(UIView *view) {
    if (view == nil) return;

    if ([view isKindOfClass:UILabel.class]) {
        ((UILabel *)view).textAlignment = NSTextAlignmentCenter;
    } else if ([view isKindOfClass:RANAdTextLabel.class]) {
        // The headline, body and advertiser are not UILabels; their lines
        // centre through their own alignment.
        ((RANAdTextLabel *)view).textAlignment = NSTextAlignmentCenter;
    }
    view.ran_layoutGravity = RANGravityCenterHorizontal;
}

static void RANRectAdCentreBlock(RANRectAdAssetViews *views) {
    RANRectAdCentreUnderIcon(views.headline);
    RANRectAdCentreUnderIcon(views.body);
    RANRectAdCentreUnderIcon(views.advertiser);
    RANRectAdCentreUnderIcon(views.rating);
}

@implementation RANRectAdViewFactory {
    GADNativeAd *_nativeAd;
    CGFloat _slotShortSide;
    CGFloat _screenScale;
    // The clearance every asset keeps from the cell's rounded corners; 0
    // on a square cell. Spent once, as the outer column's padding on every
    // template but the scrim one, where the background picture is meant
    // to run under the curve and only the block in front stands back.
    CGFloat _cornerInset;
    // Whether the texts sit on the scrim template's veil, dark whatever the
    // theme, rather than on the theme's surface.
    BOOL _onVeil;
}

- (instancetype)initWithNativeAd:(GADNativeAd *)nativeAd
                 slotShortSidePt:(CGFloat)slotShortSidePt
                   roundCornerPt:(CGFloat)roundCornerPt {
    self = [super init];
    if (self == nil) return nil;
    _nativeAd = nativeAd;
    _slotShortSide = MAX(1, slotShortSidePt);
    _screenScale = MAX(1, UIScreen.mainScreen.nativeScale);
    _cornerInset = roundCornerPt <= 0
            ? 0
            : ceil(roundCornerPt * kRANCornerInsetRatio);
    return self;
}

- (CGFloat)ran_ptFromPx:(CGFloat)px {
    return px / _screenScale;
}

// ---------------------------------------------------------------------------
// Asset queries
// ---------------------------------------------------------------------------

- (BOOL)hasVideoContent {
    return _nativeAd.mediaContent.hasVideoContent;
}

- (BOOL)hasValidStarRating {
    return [self resolveStarRating] > 0;
}

- (double)resolveStarRating {
    NSDecimalNumber *starRating = _nativeAd.starRating;
    double value = starRating.doubleValue;
    if (starRating == nil || value <= 0 || isnan(value) || isinf(value)) {
        return 0;
    }
    return MIN(kRANMaxStarRating, value);
}

- (BOOL)canRenderMainImage:(UIImage *)mainImage {
    return mainImage != nil;
}

- (BOOL)hasRenderableIcon {
    return _nativeAd.icon.image != nil;
}

- (UIImage *)findMainImage {
    for (GADNativeAdImage *image in _nativeAd.images) {
        if (image.image != nil) return image.image;
    }
    GADMediaContent *content = _nativeAd.mediaContent;
    if (content != nil && !content.hasVideoContent) {
        return content.mainImage;
    }
    return nil;
}

// ---------------------------------------------------------------------------
// Metrics
// ---------------------------------------------------------------------------

- (CGFloat)minimumWidthForTier:(RANRectAdTier)tier {
    return [self mediaPolicyFloor] + [self paddingForTier:tier] * 2;
}

// Video's floor for a video creative, the image floor otherwise.
- (CGFloat)mediaPolicyFloor {
    return [self hasVideoContent]
            ? kRANMinVideoMediaSize
            : kRANMinImageMediaSize;
}

- (CGFloat)mediaSizeForTier:(RANRectAdTier)tier {
    if ([self hasVideoContent]) {
        if (tier == RANRectAdTierCompact) return kRANMinVideoMediaSize;
        if (tier == RANRectAdTierRegular) return 144;
        return 180;
    }
    return kRANMinImageMediaSize;
}

// The slot is already bounded by the card it sits in, so a tier's own edge
// inset bought nothing. The one inset the cell keeps is the corner
// clearance, the same on every tier: what the outer column spends, and so
// what the engine has to budget for on both sides.
- (CGFloat)paddingForTier:(RANRectAdTier)tier {
    return _cornerInset;
}

- (CGFloat)gapForTier:(RANRectAdTier)tier {
    CGFloat tierGap;
    if (tier == RANRectAdTierCompact) {
        tierGap = 1;
    } else if (tier == RANRectAdTierRegular) {
        tierGap = 3;
    } else {
        tierGap = 5;
    }
    return MIN(tierGap, [self ran_cellSpacingCap]);
}

// The scrim block's own inset from the cell's edges - a hair on a small
// cell, a few points on a large one, never a tier's whim.
- (CGFloat)ran_cellEdgePadding {
    return MAX(1, MIN(4, round(_slotShortSide * kRANCellEdgePaddingRatio)));
}

// What this cell can afford to spend between its rows, whatever rung the
// scorer climbed to.
- (CGFloat)ran_cellSpacingCap {
    return MAX(1, MIN(5, round(_slotShortSide * kRANCellSpacingRatio)));
}

- (RANRectAdTier)preferredTierForWidth:(CGFloat)width height:(CGFloat)height {
    CGFloat shortSide = MIN(width, height);
    if (shortSide >= 280) return RANRectAdTierRoomy;
    if (shortSide >= 150) return RANRectAdTierRegular;
    return RANRectAdTierCompact;
}

- (CGFloat)minimumMediaLeftTextSlotWidthForPlan:(RANRectAdLayoutPlan *)plan {
    if (plan.renderVideo) {
        if (plan.tier == RANRectAdTierRoomy) return kRANVideoRailMinWidthRoomy;
        if (plan.tier == RANRectAdTierRegular) {
            return kRANVideoRailMinWidthRegular;
        }
        return kRANVideoRailMinWidthCompact;
    }
    if (plan.tier == RANRectAdTierRoomy) return kRANMediaLeftTextWidthRoomy;
    if (plan.tier == RANRectAdTierRegular) return kRANMediaLeftTextWidthRegular;
    return kRANMediaLeftTextWidthCompact;
}

- (CGFloat)callToActionHeightForPlan:(RANRectAdLayoutPlan *)plan {
    if (plan.layoutTemplate == RANRectAdTemplateMediaLeft
            && plan.renderVideo) {
        if (plan.tier == RANRectAdTierCompact) return kRANVideoRailCtaHeightCompact;
        if (plan.tier == RANRectAdTierRegular) return kRANVideoRailCtaHeightRegular;
        return kRANVideoRailCtaHeightRoomy;
    }
    // Sized from the slot rather than from any one plan, so the button comes
    // out the same whichever layout wins.
    CGFloat desired = _slotShortSide * kRANCtaShortSideRatio;
    return MAX(kRANCtaMinHeight, MIN(kRANCtaMaxHeight, desired));
}

- (CGFloat)ran_iconSizeForTier:(RANRectAdTier)tier {
    if (tier == RANRectAdTierCompact) return 24;
    if (tier == RANRectAdTierRegular) return 36;
    return 48;
}

- (CGFloat)ran_videoRailIconSizeForTier:(RANRectAdTier)tier {
    if (tier == RANRectAdTierCompact) return kRANVideoRailIconCompact;
    if (tier == RANRectAdTierRegular) return kRANVideoRailIconRegular;
    return kRANVideoRailIconRoomy;
}

- (CGFloat)ran_ctaVerticalPaddingForTier:(RANRectAdTier)tier {
    if (tier == RANRectAdTierCompact) return 1;
    if (tier == RANRectAdTierRegular) return 2;
    return 4;
}

- (CGFloat)ran_ctaHorizontalPaddingForTier:(RANRectAdTier)tier {
    CGFloat tierPadding;
    if (tier == RANRectAdTierCompact) {
        tierPadding = 3;
    } else if (tier == RANRectAdTierRegular) {
        tierPadding = 8;
    } else {
        tierPadding = 10;
    }
    return MIN(tierPadding, round(_slotShortSide * kRANCellCtaPaddingRatio));
}

// Every text size passes through the plan's scale, so a candidate that has
// to shrink keeps its proportions.
- (CGFloat)ran_scaled:(CGFloat)size plan:(RANRectAdLayoutPlan *)plan {
    return size * MAX(0.1, plan.textScale);
}

- (CGFloat)headlineSizeForPlan:(RANRectAdLayoutPlan *)plan {
    CGFloat base = plan.tier == RANRectAdTierCompact
            ? 12
            : (plan.tier == RANRectAdTierRegular ? 16 : 17);
    return [self ran_scaled:base plan:plan];
}

- (CGFloat)ran_bodySizeForPlan:(RANRectAdLayoutPlan *)plan {
    CGFloat base = plan.tier == RANRectAdTierCompact
            ? 12
            : (plan.tier == RANRectAdTierRegular ? 13 : 14);
    return [self ran_scaled:base plan:plan];
}

// One short line is never what makes a candidate too tall; below the floor
// it stops reading as information.
- (CGFloat)ran_optionalSizeForPlan:(RANRectAdLayoutPlan *)plan {
    CGFloat base = plan.tier == RANRectAdTierCompact ? 11 : 12;
    return MAX(kRANOptionalTextMinSize, [self ran_scaled:base plan:plan]);
}

- (NSInteger)ran_headlineMaxLinesForPlan:(RANRectAdLayoutPlan *)plan {
    if (plan.renderVideo) return 1;
    if (RANRectAdTemplateIsMediaSide(plan.layoutTemplate)) {
        return plan.tier == RANRectAdTierRoomy ? 3 : 2;
    }
    if (plan.layoutTemplate == RANRectAdTemplateMediaTop) {
        return plan.tier == RANRectAdTierCompact ? 2 : 3;
    }
    if (plan.layoutTemplate == RANRectAdTemplateCompactColumn) {
        return plan.tier == RANRectAdTierRoomy ? 3 : 2;
    }
    return 2;
}

- (BOOL)ran_shouldPrioritizeMediaSizeForPlan:(RANRectAdLayoutPlan *)plan {
    if (plan.renderVideo) return YES;
    if (!RANRectAdTemplateIsMediaSide(plan.layoutTemplate)) return NO;
    return plan.mediaWidth <= 72
            || plan.height <= plan.mediaHeight + 12
            || plan.tier == RANRectAdTierCompact;
}

- (NSInteger)ran_bodyMaxLinesForPlan:(RANRectAdLayoutPlan *)plan {
    if (plan.renderVideo) return 0;
    if (RANRectAdTemplateIsMediaSide(plan.layoutTemplate)) {
        if ([self ran_shouldPrioritizeMediaSizeForPlan:plan]) return 1;
        return plan.tier == RANRectAdTierRoomy ? 3 : 2;
    }
    if (plan.layoutTemplate == RANRectAdTemplateMediaTop
            || plan.layoutTemplate == RANRectAdTemplateCompactColumn) {
        return plan.tier == RANRectAdTierCompact ? 2 : 3;
    }
    return plan.tier == RANRectAdTierCompact ? 1 : 2;
}

- (NSInteger)ran_advertiserMaxLinesForPlan:(RANRectAdLayoutPlan *)plan {
    if (plan.renderVideo) return 0;
    if (RANRectAdTemplateIsMediaSide(plan.layoutTemplate)) {
        if ([self ran_shouldPrioritizeMediaSizeForPlan:plan]) return 1;
        return plan.tier == RANRectAdTierRoomy ? 2 : 1;
    }
    return plan.tier == RANRectAdTierRoomy ? 2 : 1;
}

// ---------------------------------------------------------------------------
// Probe and final builds
// ---------------------------------------------------------------------------

- (RANRectAdProbeLayout *)createProbeForPlan:(RANRectAdLayoutPlan *)plan {
    RANRectAdProbeLayout *probe = [[RANRectAdProbeLayout alloc] init];
    probe.layoutTemplate = plan.layoutTemplate;
    probe.tier = plan.tier;
    probe.showMedia = plan.showMedia;
    probe.renderVideo = plan.renderVideo;
    probe.views = [[RANRectAdAssetViews alloc] init];
    UIView *content = [self ran_buildContentForPlan:plan
                                             probe:YES
                                             views:probe.views
                                         mainImage:nil];
    RANFrameLayoutView *root = [[RANFrameLayoutView alloc] init];
    content.ran_layoutWidth = RANLayoutMatchParent;
    content.ran_layoutHeight = RANLayoutMatchParent;
    [root addSubview:content];
    probe.root = root;
    return probe;
}

- (void)configureProbeLayout:(RANRectAdProbeLayout *)probe
                     forPlan:(RANRectAdLayoutPlan *)plan {
    [self ran_configureInsetContent:probe.views plan:plan];
    [self ran_configureBadgeOverlays:probe.views plan:plan];
    [self ran_applyMarkDodges:probe.views plan:plan];
    [self ran_applyPlanTextConfiguration:probe.views plan:plan];

    if (probe.views.mediaSlot != nil) {
        probe.views.mediaSlot.ran_layoutWidth = plan.mediaWidth;
        probe.views.mediaSlot.ran_layoutHeight = plan.mediaHeight;
    }
    if (probe.views.videoRail != nil) {
        probe.views.videoRail.ran_layoutHeight = plan.mediaHeight;
    }
}

// A cached probe tree was built for some earlier rung of the ladder; this
// walks it back onto the plan being tried. Sizes and modes are the only
// things the ladder varies - everything structural is in the cache key.
- (void)ran_applyPlanTextConfiguration:(RANRectAdAssetViews *)views
                                 plan:(RANRectAdLayoutPlan *)plan {
    if (views.headline != nil) {
        views.headline.font = [UIFont boldSystemFontOfSize:
                [self headlineSizeForPlan:plan]];
        if (!plan.renderVideo) {
            [self ran_applyTextMode:plan.marqueeHeadline
                             label:views.headline
                          maxLines:[self ran_headlineMaxLinesForPlan:plan]];
        }
    }
    if (views.body != nil) {
        views.body.font = [UIFont systemFontOfSize:
                [self ran_bodySizeForPlan:plan]];
        [self ran_applyTextMode:plan.marqueeSecondary
                         label:views.body
                      maxLines:[self ran_bodyMaxLinesForPlan:plan]];
    }
    if (views.advertiser != nil) {
        views.advertiser.font = [UIFont systemFontOfSize:
                [self ran_optionalSizeForPlan:plan]];
        [self ran_applyTextMode:plan.marqueeSecondary
                         label:views.advertiser
                      maxLines:[self ran_advertiserMaxLinesForPlan:plan]];
    }
    if (views.rating != nil) {
        views.rating.font = [UIFont systemFontOfSize:
                [self ran_optionalSizeForPlan:plan]];
    }
}

- (RANRectAdNativeAdViewResult *)buildNativeAdViewForPlan:(RANRectAdLayoutPlan *)plan
                                               mainImage:(UIImage *)mainImage {
    GADNativeAdView *nativeAdView = [[GADNativeAdView alloc] init];
    RANRectAdAssetViews *views = [[RANRectAdAssetViews alloc] init];
    UIView *content = [self ran_buildContentForPlan:plan
                                             probe:NO
                                             views:views
                                         mainImage:mainImage];
    content.ran_layoutWidth = RANLayoutMatchParent;
    content.ran_layoutHeight = RANLayoutMatchParent;
    [nativeAdView addSubview:content];

    if (views.headline != nil) nativeAdView.headlineView = views.headline;
    if (views.body != nil) nativeAdView.bodyView = views.body;
    if (views.icon != nil) nativeAdView.iconView = views.icon;
    if (views.callToAction != nil) {
        nativeAdView.callToActionView = views.callToAction;
    }
    if (views.advertiser != nil) {
        nativeAdView.advertiserView = views.advertiser;
    }
    if (views.rating != nil) nativeAdView.starRatingView = views.rating;
    if (plan.showMedia && views.media != nil) {
        // The primary asset must always register through GADMediaView -
        // registering an image view is what made AdMob reject every request
        // for the Android unit with error code 3.
        if (![views.media isKindOfClass:GADMediaView.class]) {
            [NSException raise:@"RANRectAdBindException"
                        format:@"Selected media layout created an "
                                "incompatible view"];
        }
        nativeAdView.mediaView = (GADMediaView *)views.media;
    }

    if ([views.adChoicesReserve isKindOfClass:GADAdChoicesView.class]) {
        nativeAdView.adChoicesView = (GADAdChoicesView *)views.adChoicesReserve;
    }
    nativeAdView.nativeAd = _nativeAd;
    RANRectAdNativeAdViewResult *result =
            [[RANRectAdNativeAdViewResult alloc] init];
    result.nativeAdView = nativeAdView;
    result.assetViews = views;
    return result;
}

// ---------------------------------------------------------------------------
// Content assembly
// ---------------------------------------------------------------------------

- (UIView *)ran_buildContentForPlan:(RANRectAdLayoutPlan *)plan
                             probe:(BOOL)probe
                             views:(RANRectAdAssetViews *)views
                         mainImage:(UIImage *)mainImage {
    CGFloat gap = [self gapForTier:plan.tier];
    RANFrameLayoutView *root = [[RANFrameLayoutView alloc] init];

    RANLinearLayoutView *outer = [[RANLinearLayoutView alloc] init];
    views.outer = outer;
    outer.ran_vertical = YES;
    outer.ran_gravity = RANGravityTop | RANGravityCenterHorizontal;
    // The corner clearance, spent here once for everything the column
    // holds - media band, side rail, texts, button. The scrim template
    // spends none here: its ambient backdrop runs under the curve, the
    // picture over it and the block in front each keep the clearance on
    // their own.
    BOOL mediaIsBackground =
            plan.layoutTemplate == RANRectAdTemplateMediaBackground;
    _onVeil = mediaIsBackground;
    CGFloat outerInset = mediaIsBackground ? 0 : _cornerInset;
    outer.ran_padding = UIEdgeInsetsMake(
            outerInset, outerInset, outerInset, outerInset);

    if (mediaIsBackground) {
        UIView *backgroundMedia = [self ran_createMediaViewForPlan:plan
                                                            views:views
                                                            probe:probe
                                                        mainImage:mainImage];
        // The ambient backdrop lives BESIDE the MediaView, never inside it
        // - the SDK owns the MediaView's subviews and they do not survive
        // binding. The media ground is transparent on this template so the
        // backdrop shows through what the fitted picture leaves.
        if (mainImage != nil) {
            UIImageView *ambientBackdrop =
                    [[UIImageView alloc] initWithImage:mainImage];
            ambientBackdrop.contentMode = UIViewContentModeScaleAspectFill;
            ambientBackdrop.clipsToBounds = YES;
            UIView *ambientDim = [[UIView alloc] init];
            ambientDim.backgroundColor =
                    [UIColor colorWithWhite:0
                                      alpha:kRANRectAdAmbientDimAlpha];
            ambientDim.autoresizingMask = UIViewAutoresizingFlexibleWidth
                    | UIViewAutoresizingFlexibleHeight;
            ambientDim.frame = ambientBackdrop.bounds;
            [ambientBackdrop addSubview:ambientDim];
            ambientBackdrop.ran_layoutWidth = RANLayoutMatchParent;
            ambientBackdrop.ran_layoutHeight = RANLayoutMatchParent;
            [root addSubview:ambientBackdrop];
        }
        // The picture itself stands clear of the curve, so its corners
        // touch it and not a pixel of the creative is cut: the native policy
        // lets a picture lose only width.
        backgroundMedia.ran_layoutWidth = RANLayoutMatchParent;
        backgroundMedia.ran_layoutHeight = RANLayoutMatchParent;
        backgroundMedia.ran_layoutMargins = UIEdgeInsetsMake(
                _cornerInset, _cornerInset, _cornerInset, _cornerInset);
        [root addSubview:backgroundMedia];

        // One veil over the whole cell, not a band that stops short of the
        // top edge: the media reads as the panel's background through it and
        // no uncovered strip is left above the text.
        UIView *veil = [[UIView alloc] init];
        veil.backgroundColor = RANRectAdARGB(0xB3000000);
        veil.userInteractionEnabled = NO;
        veil.ran_layoutWidth = RANLayoutMatchParent;
        veil.ran_layoutHeight = RANLayoutMatchParent;
        [root addSubview:veil];

        RANLinearLayoutView *scrim = [[RANLinearLayoutView alloc] init];
        scrim.ran_vertical = YES;
        // Slim borders: the veil already separates the text from the
        // picture, so the block spends only the tier's own gap on every
        // side - a tiny cell cannot afford more.
        // A hair of breathing room, sized by the cell instead of by the
        // tier: enough that the text is not printed onto the picture's edge,
        // never enough to read as a border. It counts from that edge, which
        // stands the corner clearance in from the cell's.
        CGFloat scrimPad = _cornerInset + [self ran_cellEdgePadding];
        scrim.ran_padding = UIEdgeInsetsMake(
                scrimPad, scrimPad, scrimPad, scrimPad);

        views.headline = [self ran_createTextWithValue:_nativeAd.headline
                                                 size:[self headlineSizeForPlan:plan]
                                                 bold:YES];
        [self ran_applyTextMode:plan.marqueeHeadline
                         label:views.headline
                      maxLines:[self ran_headlineMaxLinesForPlan:plan]];
        // The icon only shares the headline's line while the line keeps a
        // usable width for text - a marquee squeezed into a sliver reads
        // worse than no icon row at all. Below that floor the icon stands
        // alone and every text follows at full width.
        CGFloat scrimContentWidth =
                MAX(0, plan.width - 2 * scrimPad);
        BOOL iconStandsAlone = plan.showIcon
                && [self hasRenderableIcon]
                && scrimContentWidth
                            - [self ran_iconSizeForTier:plan.tier]
                            - gap
                        < kRANRectAdMinIconRowTextWidth;
        RANLinearLayoutView *headlineRow = nil;
        if (iconStandsAlone) {
            [self ran_addIconTo:scrim views:views tier:plan.tier gap:0];
            if (views.icon != nil) {
                views.icon.ran_layoutGravity = RANGravityCenterHorizontal;
                views.icon.ran_layoutMargins =
                        UIEdgeInsetsMake(0, 0, gap, 0);
            }
            views.headline.ran_layoutWidth = RANLayoutMatchParent;
            [scrim addSubview:views.headline];
        } else {
            headlineRow = [[RANLinearLayoutView alloc] init];
            headlineRow.ran_vertical = NO;
            headlineRow.ran_gravity = RANGravityCenterVertical;
            if (plan.showIcon) {
                [self ran_addIconTo:headlineRow
                             views:views
                              tier:plan.tier
                               gap:gap];
            }
            RANFrameLayoutView *headlineSlot =
                    [[RANFrameLayoutView alloc] init];
            [headlineSlot addSubview:views.headline];
            headlineSlot.ran_layoutWidth = 0;
            headlineSlot.ran_layoutWeight = 1;
            [headlineRow addSubview:headlineSlot];
            headlineRow.ran_layoutWidth = RANLayoutMatchParent;
            [scrim addSubview:headlineRow];
        }
        [self ran_addBodyAndOptionalTo:scrim views:views plan:plan];
        [self ran_addCallToActionTo:scrim
                             views:views
                              plan:plan
                               gap:gap
                         fullWidth:YES];
        if (iconStandsAlone) RANRectAdCentreBlock(views);
        if (headlineRow != nil) {
            [self ran_setBadgeBesideIconInRow:headlineRow
                                       scrim:scrim
                                       views:views
                                        plan:plan
                                    scrimPad:scrimPad];
        }

        // Not the inset content: the inset pass would reset the scrim's own
        // padding to the slot edge.
        views.scrim = scrim;
        outer.ran_gravity = RANGravityBottom | RANGravityCenterHorizontal;
        scrim.ran_layoutWidth = RANLayoutMatchParent;
        [outer addSubview:scrim];
        return [self ran_finishContentRoot:root outer:outer views:views plan:plan];
    }

    if (plan.layoutTemplate == RANRectAdTemplateCompactRow) {
        RANLinearLayoutView *row = [[RANLinearLayoutView alloc] init];
        row.ran_vertical = NO;
        row.ran_gravity = RANGravityCenterVertical;
        views.insetContent = row;
        views.insetContentAvoidsBadges = YES;
        if (plan.showIcon) {
            [self ran_addIconTo:row views:views tier:plan.tier gap:gap];
        }

        RANLinearLayoutView *texts = [self ran_buildTextStack:views plan:plan];
        texts.ran_layoutWidth = 0;
        texts.ran_layoutWeight = 1;
        [row addSubview:texts];
        [self ran_addCallToActionTo:row
                             views:views
                              plan:plan
                               gap:gap
                         fullWidth:NO];
        // One row of assets in a tall slot looked broken glued to the top
        // edge; centering moves where it sits without touching what the
        // engine measures.
        outer.ran_gravity = RANGravityCenterVertical | RANGravityCenterHorizontal;
        row.ran_layoutWidth = RANLayoutMatchParent;
        [outer addSubview:row];
        return [self ran_finishContentRoot:root outer:outer views:views plan:plan];
    }

    if (plan.layoutTemplate == RANRectAdTemplateCompactColumn) {
        RANLinearLayoutView *content =
                [self ran_buildHeadlineAndActionStack:views plan:plan];
        views.insetContent = content;
        // ran_buildHeadlineAndActionStack decides the badge reserve: it knows
        // whether the top band holds the icon or the headline.
        content.ran_layoutWidth = RANLayoutMatchParent;
        content.ran_layoutHeight = 0;
        content.ran_layoutWeight = 1;
        [outer addSubview:content];
        return [self ran_finishContentRoot:root outer:outer views:views plan:plan];
    }

    UIView *mediaView = [self ran_createMediaViewForPlan:plan
                                                  views:views
                                                  probe:probe
                                              mainImage:mainImage];
    if (plan.layoutTemplate == RANRectAdTemplateMediaLeft) {
        if (plan.renderVideo) {
            [self ran_buildVideoMediaLeftContent:outer
                                      mediaView:mediaView
                                          views:views
                                           plan:plan];
            return [self ran_finishContentRoot:root
                                        outer:outer
                                        views:views
                                         plan:plan];
        }

        RANLinearLayoutView *row = [[RANLinearLayoutView alloc] init];
        row.ran_vertical = NO;
        row.ran_gravity = RANGravityCenterVertical;
        UIView *mediaFrame = [self ran_frameBadgesOnMedia:mediaView views:views];
        mediaFrame.ran_layoutWidth = plan.mediaWidth;
        mediaFrame.ran_layoutHeight = plan.mediaHeight;
        mediaFrame.ran_layoutMargins = UIEdgeInsetsMake(0, 0, 0, gap);
        [row addSubview:mediaFrame];

        CGFloat leftover =
                [self ran_sideLayoutLeftoverHeightForPlan:plan];
        BOOL callToActionBelow =
                leftover >= [self callToActionHeightForPlan:plan] + gap;
        RANLinearLayoutView *content =
                [self ran_buildIdentityAndText:views
                                         plan:plan
                          includeCallToAction:!callToActionBelow];
        views.insetContent = content;
        views.insetContentAvoidsBadges = NO;
        content.ran_layoutWidth = 0;
        content.ran_layoutHeight = RANLayoutWrapContent;
        content.ran_layoutWeight = 1;
        [row addSubview:content];
        row.ran_layoutWidth = RANLayoutMatchParent;
        [outer addSubview:row];
        if (callToActionBelow) {
            [self ran_addCallToActionTo:outer
                                 views:views
                                  plan:plan
                                   gap:gap
                             fullWidth:YES];
        }
        return [self ran_finishContentRoot:root outer:outer views:views plan:plan];
    }

    if (plan.layoutTemplate == RANRectAdTemplateMediaRight) {
        RANLinearLayoutView *row = [[RANLinearLayoutView alloc] init];
        row.ran_vertical = NO;
        row.ran_gravity = RANGravityCenterVertical;

        // The same leftover test the mirrored template runs. Without it
        // this branch kept the button inside the text column while
        // MEDIA_LEFT moved it under the row, so one creative got two
        // different buttons depending on which side its picture landed.
        CGFloat leftover =
                [self ran_sideLayoutLeftoverHeightForPlan:plan];
        BOOL callToActionBelow =
                leftover >= [self callToActionHeightForPlan:plan] + gap;
        RANLinearLayoutView *content =
                [self ran_buildIdentityAndText:views
                                         plan:plan
                          includeCallToAction:!callToActionBelow];
        views.insetContent = content;
        views.insetContentAvoidsBadges = NO;
        content.ran_layoutWidth = 0;
        content.ran_layoutHeight = RANLayoutWrapContent;
        content.ran_layoutWeight = 1;
        [row addSubview:content];

        UIView *mediaFrame = [self ran_frameBadgesOnMedia:mediaView views:views];
        mediaFrame.ran_layoutWidth = plan.mediaWidth;
        mediaFrame.ran_layoutHeight = plan.mediaHeight;
        mediaFrame.ran_layoutMargins = UIEdgeInsetsMake(0, gap, 0, 0);
        [row addSubview:mediaFrame];
        row.ran_layoutWidth = RANLayoutMatchParent;
        [outer addSubview:row];
        if (callToActionBelow) {
            [self ran_addCallToActionTo:outer
                                 views:views
                                  plan:plan
                                   gap:gap
                             fullWidth:YES];
        }
        return [self ran_finishContentRoot:root outer:outer views:views plan:plan];
    }

    // The image media takes the plan's size as its floor and absorbs
    // whatever height the slot has left over, so free space enlarges the
    // picture instead of sitting as an empty band inside the text block.
    // The minimums keep the engine's natural measure - and so the
    // validator's math - exactly the plan's.
    mediaView.ran_minimumSize =
            CGSizeMake(plan.mediaWidth, plan.mediaHeight);
    mediaView.ran_layoutWidth = plan.mediaWidth;
    mediaView.ran_layoutHeight = 0;
    mediaView.ran_layoutWeight = 1;
    mediaView.ran_layoutGravity = RANGravityCenterHorizontal;
    mediaView.ran_layoutMargins = UIEdgeInsetsMake(0, 0, gap, 0);
    [outer addSubview:mediaView];

    RANLinearLayoutView *content =
            [self ran_buildHeadlineAndActionStack:views plan:plan];
    views.insetContent = content;
    views.insetContentAvoidsBadges = NO;
    content.ran_layoutWidth = RANLayoutMatchParent;
    content.ran_layoutHeight = RANLayoutWrapContent;
    [outer addSubview:content];
    return [self ran_finishContentRoot:root outer:outer views:views plan:plan];
}

- (UIView *)ran_finishContentRoot:(RANFrameLayoutView *)root
                           outer:(RANLinearLayoutView *)outer
                           views:(RANRectAdAssetViews *)views
                            plan:(RANRectAdLayoutPlan *)plan {
    BOOL fillsAvailableHeight =
            plan.layoutTemplate == RANRectAdTemplateCompactColumn
                    || plan.layoutTemplate == RANRectAdTemplateMediaTop
                    || plan.layoutTemplate
                            == RANRectAdTemplateMediaBackground;
    outer.ran_layoutWidth = RANLayoutMatchParent;
    outer.ran_layoutHeight = fillsAvailableHeight
            ? RANLayoutMatchParent
            : RANLayoutWrapContent;
    [root addSubview:outer];
    [self ran_addBadgeOverlaysTo:root views:views];
    [self ran_configureInsetContent:views plan:plan];
    [self ran_configureBadgeOverlays:views plan:plan];
    [self ran_applyMarkDodges:views plan:plan];
    return root;
}

// No text may lie under the AdChoices mark. A text the layout puts there
// gives up just the width it shares with the mark, through its right margin -
// width is cheaper than height in a small cell - and the tree is laid out
// again, since a narrower text may wrap and push another under the mark.
- (void)dodgeAdChoicesMarkInProbe:(RANRectAdProbeLayout *)probe
                             plan:(RANRectAdLayoutPlan *)plan
                            width:(CGFloat)width
                           height:(CGFloat)height {
    plan.headlineMarkDodge = 0;
    plan.bodyMarkDodge = 0;
    plan.advertiserMarkDodge = 0;
    plan.ratingMarkDodge = 0;
    plan.callToActionMarkDodge = 0;
    RANRectAdAssetViews *views = probe.views;
    [self ran_applyMarkDodges:views plan:plan];
    if (views.adChoicesReserve == nil) return;

    for (NSInteger pass = 0; pass < kRANAdChoicesDodgePasses; ++pass) {
        [probe.root ran_measureWithWidthSpec:
                        RANMeasureSpecMake(RANMeasureSpecExactly, width)
                                 heightSpec:
                        RANMeasureSpecMake(RANMeasureSpecExactly, height)];
        [probe.root ran_layoutWithFrame:CGRectMake(0, 0, width, height)];
        CGRect mark = [views.adChoicesReserve convertRect:
                                views.adChoicesReserve.bounds
                                                  toView:probe.root];
        if (CGRectIsEmpty(mark)) return;

        CGFloat headline = RANMarkOverlap(probe.root, views.headline, mark);
        CGFloat body = RANMarkOverlap(probe.root, views.body, mark);
        CGFloat advertiser =
                RANMarkOverlap(probe.root, views.advertiser, mark);
        CGFloat rating = RANMarkOverlap(probe.root, views.rating, mark);
        CGFloat callToAction =
                RANMarkOverlap(probe.root, views.callToAction, mark);
        if (headline + body + advertiser + rating + callToAction <= 0) return;

        plan.headlineMarkDodge += headline;
        plan.bodyMarkDodge += body;
        plan.advertiserMarkDodge += advertiser;
        plan.ratingMarkDodge += rating;
        plan.callToActionMarkDodge += callToAction;
        [self ran_applyMarkDodges:views plan:plan];
    }
}

static CGFloat RANMarkOverlap(UIView *root, UIView *_Nullable view, CGRect mark) {
    if (view == nil || view.hidden || view.superview == nil) return 0;

    CGRect bounds = [view convertRect:view.bounds toView:root];
    CGRect shared = CGRectIntersection(bounds, mark);
    if (CGRectIsNull(shared) || shared.size.width <= 0 || shared.size.height <= 0) {
        return 0;
    }
    return CGRectGetMaxX(bounds) - CGRectGetMinX(mark) + kRANAdChoicesDodgeGap;
}

- (void)ran_applyMarkDodges:(RANRectAdAssetViews *)views
                       plan:(RANRectAdLayoutPlan *)plan {
    [self ran_applyMarkDodge:plan.headlineMarkDodge to:views.headline views:views];
    [self ran_applyMarkDodge:plan.bodyMarkDodge to:views.body views:views];
    [self ran_applyMarkDodge:plan.advertiserMarkDodge to:views.advertiser views:views];
    [self ran_applyMarkDodge:plan.ratingMarkDodge to:views.rating views:views];
    [self ran_applyMarkDodge:plan.callToActionMarkDodge
                          to:views.callToAction
                       views:views];
}

- (void)ran_applyMarkDodge:(CGFloat)dodge
                        to:(UIView *_Nullable)view
                     views:(RANRectAdAssetViews *)views {
    if (view == nil) return;

    if (views.baseRightMargins == nil) {
        views.baseRightMargins = [NSMapTable weakToStrongObjectsMapTable];
    }
    NSNumber *base = [views.baseRightMargins objectForKey:view];
    if (base == nil) {
        base = @(view.ran_layoutMargins.right);
        [views.baseRightMargins setObject:base forKey:view];
    }
    UIEdgeInsets margins = view.ran_layoutMargins;
    margins.right = base.doubleValue + dodge;
    view.ran_layoutMargins = margins;
}

// The picture in a frame of its own size, which the badge and the AdChoices
// view join (ran_addBadgeOverlaysTo:) so they keep its corners wherever the
// row places it.
- (UIView *)ran_frameBadgesOnMedia:(UIView *)mediaView
                            views:(RANRectAdAssetViews *)views {
    RANFrameLayoutView *frame = [[RANFrameLayoutView alloc] init];
    mediaView.ran_layoutWidth = RANLayoutMatchParent;
    mediaView.ran_layoutHeight = RANLayoutMatchParent;
    [frame addSubview:mediaView];
    views.badgeHost = frame;
    // The probe sizes the slot for each plan: the frame, the picture filling
    // it.
    views.mediaSlot = frame;
    return frame;
}

- (void)ran_addBadgeOverlaysTo:(RANFrameLayoutView *)root
                        views:(RANRectAdAssetViews *)views {
    RANFrameLayoutView *host = views.badgeHost ?: root;
    UILabel *attribution = [[UILabel alloc] init];
    attribution.text = @"Ad";
    attribution.textColor = RANRectAdARGB(kRANThemeAttributionText);
    attribution.font = [UIFont systemFontOfSize:10];
    attribution.textAlignment = NSTextAlignmentCenter;
    attribution.backgroundColor = RANRectAdARGB(kRANThemeAttribution);
    attribution.layer.masksToBounds = YES;
    attribution.userInteractionEnabled = NO;
    views.attribution = attribution;
    [host addSubview:attribution];

    // The SDK's AdChoices mark goes in this view, at the corner the layout
    // gives it: GADNativeAdView's adChoicesView, set as the ad binds.
    GADAdChoicesView *adChoicesReserve = [[GADAdChoicesView alloc] init];
    views.adChoicesReserve = adChoicesReserve;
    [host addSubview:adChoicesReserve];
}

- (CGFloat)ran_badgeHeightForPlan:(RANRectAdLayoutPlan *)plan {
    CGFloat desired = round(_slotShortSide * kRANBadgeShortSideRatio);
    CGFloat minimum = [self ran_ptFromPx:kRANMinAttributionSizePx];
    return MAX(minimum, MIN(kRANAttributionHeight, desired));
}

- (void)ran_configureInsetContent:(RANRectAdAssetViews *)views
                            plan:(RANRectAdLayoutPlan *)plan {
    UIView *insetContent = views.insetContent;
    if (insetContent == nil
            || ![insetContent isKindOfClass:RANLayoutContainerView.class]) {
        return;
    }
    RANLayoutContainerView *container = (RANLayoutContainerView *)insetContent;

    if (plan.layoutTemplate == RANRectAdTemplateMediaLeft
            && plan.renderVideo) {
        container.ran_padding = UIEdgeInsetsMake(
                [self ran_badgeHeightForPlan:plan] + [self gapForTier:plan.tier]
              , kRANVideoRailEdgePadding
              , kRANVideoRailEdgePadding
              , kRANVideoRailEdgePadding);
        return;
    }

    // Only the badge strip is reserved - the badges have to stay visible.
    // Everything else meets the column's edge, the way the badges already
    // do; the corner clearance is the outer column's padding, spent once,
    // so nothing is added here.
    CGFloat topPadding = views.insetContentAvoidsBadges
            ? [self ran_badgeHeightForPlan:plan]
            : 0;
    container.ran_padding = UIEdgeInsetsMake(topPadding, 0, 0, 0);
    // The column beside the media is at least as tall as the media, so its
    // spacer can drop the button to the media's foot, and taller when its
    // texts need it: held to the media height, it would spill what does not
    // fit while the measured height said all was well.
    if (RANRectAdTemplateIsMediaSide(plan.layoutTemplate)) {
        container.ran_minimumSize = CGSizeMake(0, plan.mediaHeight);
    }
}

// The scrim's first row: when the name beside the icon is shorter than the
// icon by the badge's height, the badge stands in that room, over the name,
// instead of in the cell's corner - the row keeps its height and the icon its
// place. The name steps down by a margin, not padding, so its view stays
// clear of the badge. The AdChoices side is the mark dodge's.
- (void)ran_setBadgeBesideIconInRow:(RANLinearLayoutView *)row
                              scrim:(RANLinearLayoutView *)scrim
                              views:(RANRectAdAssetViews *)views
                               plan:(RANRectAdLayoutPlan *)plan
                           scrimPad:(CGFloat)scrimPad {
    if (views.icon == nil || views.icon.superview != row) return;

    RANMeasureSpec width = RANMeasureSpecMake(RANMeasureSpecExactly, plan.width);
    RANMeasureSpec free = RANMeasureSpecMake(RANMeasureSpecUnspecified, 0);
    [scrim ran_measureWithWidthSpec:width heightSpec:free];
    UIEdgeInsets iconMargins = views.icon.ran_layoutMargins;
    CGFloat step = [self ran_badgeHeightForPlan:plan] + 2;
    if (views.headline.ran_measuredSize.height + step
            > iconMargins.top
                    + views.icon.ran_measuredSize.height
                    + iconMargins.bottom) {
        return;
    }

    UIEdgeInsets margins = views.headline.ran_layoutMargins;
    margins.top += step;
    views.headline.ran_layoutMargins = margins;
    [scrim ran_measureWithWidthSpec:width heightSpec:free];
    views.attributionBesideIcon = YES;
    views.attributionOrigin = CGPointMake(
            scrimPad
                    + iconMargins.left
                    + views.icon.ran_measuredSize.width
                    + iconMargins.right
          , plan.height
                    - scrim.ran_measuredSize.height
                    + scrimPad
                    + (row.ran_measuredSize.height
                            - views.headline.ran_measuredSize.height
                            - margins.top) / 2);
}

- (CGFloat)ran_attributionWidthForPlan:(RANRectAdLayoutPlan *)plan
                           badgeHeight:(CGFloat)badgeHeight {
    CGFloat minimumPt = [self ran_ptFromPx:kRANMinAttributionSizePx];
    CGFloat desiredWidth = round(badgeHeight * kRANAttributionAspectRatio);
    CGFloat maximumWidth = MAX(minimumPt, plan.width - badgeHeight);
    return MAX(
            minimumPt
          , MIN(desiredWidth, MIN(kRANAttributionWidth, maximumWidth)));
}

- (void)ran_configureBadgeOverlays:(RANRectAdAssetViews *)views
                             plan:(RANRectAdLayoutPlan *)plan {
    CGFloat badgeHeight = [self ran_badgeHeightForPlan:plan];
    // On the picture's frame the badges are flush: the outer column already
    // stands the picture clear of the curve.
    CGFloat edgeInset = views.badgeHost != nil ? 0 : [self ran_badgeEdgeInset];
    if (views.attribution != nil) {
        CGFloat attributionWidth =
                [self ran_attributionWidthForPlan:plan badgeHeight:badgeHeight];
        views.attribution.font = [UIFont systemFontOfSize:
                MAX(1, badgeHeight * kRANAttributionTextHeightRatio)];
        views.attribution.ran_layoutWidth = attributionWidth;
        views.attribution.ran_layoutHeight = badgeHeight;
        views.attribution.ran_layoutGravity = RANGravityTop | RANGravityLeft;
        // The badges sit on the root, outside the outer column, so they
        // carry the corner clearance themselves - on every template, the
        // scrim one included: only the picture runs under the curve.
        views.attribution.ran_layoutMargins = views.attributionBesideIcon
                ? UIEdgeInsetsMake(
                      views.attributionOrigin.y, views.attributionOrigin.x, 0, 0)
                : UIEdgeInsetsMake(edgeInset, edgeInset, 0, 0);
        RANShapeBadge(
                views.attribution
              , !views.attributionBesideIcon && edgeInset == 0);
    }
    if (views.adChoicesReserve != nil) {
        views.adChoicesReserve.ran_layoutWidth =
                MIN(kRANAdChoicesMarkSize, plan.width);
        views.adChoicesReserve.ran_layoutHeight =
                MIN(kRANAdChoicesMarkSize, plan.height);
        views.adChoicesReserve.ran_layoutGravity = RANGravityTop | RANGravityRight;
        views.adChoicesReserve.ran_layoutMargins =
                UIEdgeInsetsMake(edgeInset, 0, 0, edgeInset);
    }
}

// Where both corner marks sit: the attribution and the AdChoices view.
- (CGFloat)ran_badgeEdgeInset {
    return _cornerInset;
}

// ---------------------------------------------------------------------------
// Template pieces
// ---------------------------------------------------------------------------

- (RANLinearLayoutView *)ran_buildIdentityAndText:(RANRectAdAssetViews *)views
                                           plan:(RANRectAdLayoutPlan *)plan {
    return [self ran_buildIdentityAndText:views
                                    plan:plan
                    includeCallToAction:YES];
}

// The band the media leaves unused below a side layout. Anything that fits
// there is better off spanning the panel than squeezed into the column
// beside the picture.
- (CGFloat)ran_sideLayoutLeftoverHeightForPlan:(RANRectAdLayoutPlan *)plan {
    return MAX(0, plan.height - plan.mediaHeight);
}

- (RANLinearLayoutView *)ran_buildIdentityAndText:(RANRectAdAssetViews *)views
                                           plan:(RANRectAdLayoutPlan *)plan
                            includeCallToAction:(BOOL)includeCallToAction {
    CGFloat gap = [self gapForTier:plan.tier];
    RANLinearLayoutView *content = [[RANLinearLayoutView alloc] init];
    content.ran_vertical = YES;
    content.ran_gravity = RANGravityTop;

    // The same floor the scrim template applies: the icon only shares the
    // headline's line while the line keeps a usable text width. In a narrow
    // column beside the media it stands alone instead, and the texts follow
    // below at the column's full width.
    CGFloat textColumnWidth = MAX(0, plan.width - plan.mediaWidth - gap);
    BOOL iconStandsAlone = plan.showIcon
            && [self hasRenderableIcon]
            && textColumnWidth
                        - [self ran_iconSizeForTier:plan.tier]
                        - gap
                    < kRANRectAdMinIconRowTextWidth;
    if (iconStandsAlone) {
        [self ran_addIconTo:content views:views tier:plan.tier gap:0];
        if (views.icon != nil) {
            views.icon.ran_layoutGravity = RANGravityCenterHorizontal;
            views.icon.ran_layoutMargins = UIEdgeInsetsMake(0, 0, gap, 0);
        }
    }

    RANLinearLayoutView *identity = [[RANLinearLayoutView alloc] init];
    identity.ran_vertical = NO;
    identity.ran_gravity = RANGravityCenterVertical;
    if (plan.showIcon && !iconStandsAlone) {
        [self ran_addIconTo:identity views:views tier:plan.tier gap:gap];
    }

    views.headline = [self ran_createTextWithValue:_nativeAd.headline
                                             size:[self headlineSizeForPlan:plan]
                                             bold:YES];
    [self ran_applyTextMode:plan.marqueeHeadline
                     label:views.headline
                  maxLines:[self ran_headlineMaxLinesForPlan:plan]];
    RANFrameLayoutView *headlineSlot = [[RANFrameLayoutView alloc] init];
    [headlineSlot addSubview:views.headline];
    headlineSlot.ran_layoutWidth = 0;
    headlineSlot.ran_layoutWeight = 1;
    [identity addSubview:headlineSlot];
    identity.ran_layoutWidth = RANLayoutMatchParent;
    [content addSubview:identity];

    [self ran_addBodyAndOptionalTo:content views:views plan:plan];

    UIView *spacer = [[UIView alloc] init];
    spacer.ran_layoutWidth = RANLayoutMatchParent;
    spacer.ran_layoutHeight = 0;
    spacer.ran_layoutWeight = 1;
    [content addSubview:spacer];

    if (includeCallToAction) {
        [self ran_addCallToActionTo:content
                             views:views
                              plan:plan
                               gap:gap
                         fullWidth:YES];
    }
    if (iconStandsAlone) RANRectAdCentreBlock(views);
    return content;
}

- (RANLinearLayoutView *)ran_buildHeadlineAndActionStack:(RANRectAdAssetViews *)views
                                                  plan:(RANRectAdLayoutPlan *)plan {
    CGFloat gap = [self gapForTier:plan.tier];
    RANLinearLayoutView *content = [[RANLinearLayoutView alloc] init];
    content.ran_vertical = YES;
    content.ran_gravity = RANGravityTop;

    UIView *filler = [self ran_createIconFiller:views plan:plan];
    // Standing in for the picture, the icon takes the picture's place at the
    // top; with nothing to put there the weighted band goes below the text.
    BOOL fillerCarriesIcon = views.icon != nil;
    // A badge may sit over the icon, never over text. When the icon holds the
    // top band the badge reserve is dropped and the band starts at the slot
    // edge, the same way a media layout starts with its picture. The band's
    // base height keeps it at least badge-tall, so the headline below it
    // never rises under the badges however tight the slot gets.
    views.insetContentAvoidsBadges = !fillerCarriesIcon;
    if (fillerCarriesIcon) {
        filler.ran_layoutWidth = RANLayoutMatchParent;
        filler.ran_layoutHeight = [self ran_badgeHeightForPlan:plan];
        filler.ran_layoutWeight = 1;
        filler.ran_layoutMargins = UIEdgeInsetsMake(0, 0, gap, 0);
        [content addSubview:filler];
    }

    // With neither media nor icon above it, the text takes the whole
    // remaining band and centers itself rather than clinging to the top.
    RANLinearLayoutView *textParent = content;
    if (!fillerCarriesIcon) {
        textParent = [[RANLinearLayoutView alloc] init];
        textParent.ran_vertical = YES;
        textParent.ran_gravity = RANGravityCenterVertical;
    }

    views.headline = [self ran_createTextWithValue:_nativeAd.headline
                                             size:[self headlineSizeForPlan:plan]
                                             bold:YES];
    [self ran_applyTextMode:plan.marqueeHeadline
                     label:views.headline
                  maxLines:[self ran_headlineMaxLinesForPlan:plan]];
    views.headline.ran_layoutWidth = RANLayoutMatchParent;
    [textParent addSubview:views.headline];
    [self ran_addBodyAndOptionalTo:textParent views:views plan:plan];

    if (!fillerCarriesIcon) {
        textParent.ran_layoutWidth = RANLayoutMatchParent;
        textParent.ran_layoutHeight = 0;
        textParent.ran_layoutWeight = 1;
        [content addSubview:textParent];
    }

    RANLinearLayoutView *actionRow = [[RANLinearLayoutView alloc] init];
    actionRow.ran_vertical = NO;
    actionRow.ran_gravity = RANGravityCenterVertical;
    if (views.icon == nil && plan.showIcon) {
        // The icon rides the button's row, so it takes the button's height -
        // a tier-sized icon would stretch the whole row and spend height the
        // media above it needs more.
        [self ran_addSizedIconTo:actionRow
                          views:views
                           size:MIN(
                                [self ran_iconSizeForTier:plan.tier]
                              , [self callToActionHeightForPlan:plan])
                            gap:gap];
    }
    [self ran_addCallToActionTo:actionRow
                         views:views
                          plan:plan
                           gap:gap
                     fullWidth:NO];
    if (views.callToAction != nil) {
        views.callToAction.ran_layoutWidth = 0;
        views.callToAction.ran_layoutWeight = 1;
        UIEdgeInsets margins = views.callToAction.ran_layoutMargins;
        margins.left = 0;
        views.callToAction.ran_layoutMargins = margins;
    }
    // With an icon beside it, the icon already fixes this row's height; a
    // shorter button saves nothing back and only breaks the row's line.
    if (views.callToAction != nil
            && views.icon != nil
            && views.icon.superview == actionRow) {
        CGFloat rowHeight = MIN(
                [self ran_iconSizeForTier:plan.tier]
              , [self callToActionHeightForPlan:plan]);
        views.callToAction.ran_minimumSize = CGSizeMake(0, rowHeight);
        [(RANRectAdCallToActionButton *)views.callToAction
                ran_applyLabelSizeForHeight:rowHeight];
    }

    actionRow.ran_layoutWidth = RANLayoutMatchParent;
    actionRow.ran_layoutMargins = UIEdgeInsetsMake(gap, 0, 0, 0);
    [content addSubview:actionRow];
    // The filler icon occupies the picture's band above the text, so the
    // text centers under it exactly as it does beside a side media.
    if (fillerCarriesIcon) RANRectAdCentreBlock(views);
    return content;
}

- (RANLinearLayoutView *)ran_buildTextStack:(RANRectAdAssetViews *)views
                                     plan:(RANRectAdLayoutPlan *)plan {
    RANLinearLayoutView *texts = [[RANLinearLayoutView alloc] init];
    texts.ran_vertical = YES;
    texts.ran_gravity = RANGravityCenterVertical;

    views.headline = [self ran_createTextWithValue:_nativeAd.headline
                                             size:[self headlineSizeForPlan:plan]
                                             bold:YES];
    [self ran_applyTextMode:plan.marqueeHeadline
                     label:views.headline
                  maxLines:[self ran_headlineMaxLinesForPlan:plan]];
    [texts addSubview:views.headline];
    [self ran_addBodyAndOptionalTo:texts views:views plan:plan];
    return texts;
}

- (void)ran_addBodyAndOptionalTo:(RANLinearLayoutView *)parent
                          views:(RANRectAdAssetViews *)views
                           plan:(RANRectAdLayoutPlan *)plan {
    NSString *bodyValue = _nativeAd.body;
    if (plan.showBody && bodyValue.length > 0) {
        views.body = [self ran_createTextWithValue:bodyValue
                                             size:[self ran_bodySizeForPlan:plan]
                                             bold:NO];
        [self ran_applyTextMode:plan.marqueeSecondary
                         label:views.body
                      maxLines:[self ran_bodyMaxLinesForPlan:plan]];
        views.body.textColor = [self ran_secondaryTextColor];
        [parent addSubview:views.body];
    }

    if (plan.showAdvertiser) {
        views.advertiser = [self ran_createTextWithValue:_nativeAd.advertiser
                                                   size:[self ran_optionalSizeForPlan:plan]
                                                   bold:NO];
        views.advertiser.textColor = [self ran_secondaryTextColor];
        [self ran_applyTextMode:plan.marqueeSecondary
                         label:views.advertiser
                      maxLines:[self ran_advertiserMaxLinesForPlan:plan]];
        [parent addSubview:views.advertiser];
    }

    double starRating = [self resolveStarRating];
    if (plan.showRating && starRating > 0) {
        views.rating = [self ran_createTextWithValue:
                        [NSString stringWithFormat:@"%.1f", starRating]
                                               size:[self ran_optionalSizeForPlan:plan]
                                               bold:NO];
        views.rating.textColor = [self ran_secondaryTextColor];
        views.rating.starsOnDark = _onVeil || RANThemeIsDark();
        views.rating.stars = starRating;
        [parent addSubview:views.rating];
    }
}

// The theme's grey on the surface, white at .8 on the veil.
- (UIColor *)ran_secondaryTextColor {
    return _onVeil
            ? RANRectAdARGB(0xCCFFFFFF)
            : RANRectAdARGB(RANThemeGrey());
}

// A media-free layout leaves a hole where the picture would have been; the
// icon is the one image asset such a creative still carries and takes it.
- (UIView *)ran_createIconFiller:(RANRectAdAssetViews *)views
                           plan:(RANRectAdLayoutPlan *)plan {
    if (plan.showMedia || !plan.showIcon || ![self hasRenderableIcon]) {
        return [[UIView alloc] init];
    }

    RANRectAdIconFillerView *holder = [[RANRectAdIconFillerView alloc] init];
    holder.reservedHeight = kRANMinFillerIcon;
    UIImageView *icon = [[UIImageView alloc] init];
    icon.contentMode = UIViewContentModeScaleAspectFit;
    icon.image = _nativeAd.icon.image;
    icon.ran_layoutWidth = RANLayoutMatchParent;
    icon.ran_layoutHeight = RANLayoutMatchParent;
    [holder addSubview:icon];
    views.icon = icon;
    return holder;
}

- (void)ran_addIconTo:(RANLinearLayoutView *)parent
               views:(RANRectAdAssetViews *)views
                tier:(RANRectAdTier)tier
                 gap:(CGFloat)gap {
    [self ran_addSizedIconTo:parent
                      views:views
                       size:[self ran_iconSizeForTier:tier]
                        gap:gap];
}

- (void)ran_addSizedIconTo:(RANLinearLayoutView *)parent
                    views:(RANRectAdAssetViews *)views
                     size:(CGFloat)size
                      gap:(CGFloat)gap {
    if (![self hasRenderableIcon]) return;

    UIImageView *icon = [[UIImageView alloc] init];
    UIImage *iconImage = _nativeAd.icon.image;
    icon.image = iconImage;
    // CENTER_INSIDE on the Java side: shrink to fit, never enlarge. UIKit
    // has no single mode for that, so the choice is made from the asset -
    // an icon already inside the box is drawn at its own size rather than
    // blown up soft to fill it. The filler icon keeps ScaleAspectFit,
    // matching FIT_CENTER over there.
    icon.contentMode =
            iconImage != nil
                    && iconImage.size.width <= size
                    && iconImage.size.height <= size
                    ? UIViewContentModeCenter
                    : UIViewContentModeScaleAspectFit;
    icon.ran_layoutWidth = size;
    icon.ran_layoutHeight = size;
    // An icon's corners, as a launcher rounds them.
    icon.layer.cornerRadius = size * 0.22f;
    icon.clipsToBounds = YES;
    icon.ran_layoutMargins = UIEdgeInsetsMake(0, 0, 0, gap);
    [parent addSubview:icon];
    views.icon = icon;
}

- (void)ran_addCallToActionTo:(RANLinearLayoutView *)parent
                       views:(RANRectAdAssetViews *)views
                        plan:(RANRectAdLayoutPlan *)plan
                         gap:(CGFloat)gap
                   fullWidth:(BOOL)fullWidth {
    NSString *callToActionValue = _nativeAd.callToAction;
    if (callToActionValue.length == 0) return;

    RANRectAdCallToActionButton *callToAction =
            [RANRectAdCallToActionButton buttonWithType:UIButtonTypeCustom];
    [callToAction setTitle:callToActionValue forState:UIControlStateNormal];
    [callToAction setTitleColor:UIColor.whiteColor
                       forState:UIControlStateNormal];
    callToAction.titleLabel.font = [UIFont boldSystemFontOfSize:14];
    callToAction.titleLabel.numberOfLines = 0;
    callToAction.titleLabel.textAlignment = NSTextAlignmentCenter;
    callToAction.titleLabel.lineBreakMode = NSLineBreakByTruncatingTail;
    callToAction.backgroundColor = RANRectAdARGB(RANThemePrimary());
    callToAction.layer.masksToBounds = YES;
    // The native ad view under it takes the touch and reports the click, as
    // Google's native ad guide asks of the call to action.
    callToAction.userInteractionEnabled = NO;
    CGFloat callToActionHeight = [self callToActionHeightForPlan:plan];
    callToAction.ran_minimumSize = CGSizeMake(
            MIN(kRANCtaMinWidth, plan.width / 2)
          , callToActionHeight);
    [callToAction ran_applyLabelSizeForHeight:callToActionHeight];
    CGFloat verticalPadding = [self ran_ctaVerticalPaddingForTier:plan.tier];
    CGFloat horizontalPadding =
            [self ran_ctaHorizontalPaddingForTier:plan.tier];
    callToAction.contentEdgeInsets = UIEdgeInsetsMake(
            verticalPadding
          , horizontalPadding
          , verticalPadding
          , horizontalPadding);

    if (fullWidth) {
        callToAction.ran_layoutWidth = RANLayoutMatchParent;
        callToAction.ran_layoutMargins = UIEdgeInsetsMake(gap, 0, 0, 0);
    } else {
        callToAction.ran_layoutWidth = RANLayoutWrapContent;
        callToAction.ran_layoutMargins = UIEdgeInsetsMake(0, gap, 0, 0);
    }
    [parent addSubview:callToAction];
    views.callToAction = callToAction;
}

- (UIView *)ran_createMediaViewForPlan:(RANRectAdLayoutPlan *)plan
                                views:(RANRectAdAssetViews *)views
                                probe:(BOOL)probe
                            mainImage:(UIImage *)mainImage {
    // The primary asset is always a GADMediaView; drawing pixels through an
    // image view is fine, registering one is what stops the fill.
    RANRectAdMediaView *mediaView = [[RANRectAdMediaView alloc] init];
    BOOL backgroundTemplate =
            plan.layoutTemplate == RANRectAdTemplateMediaBackground;
    // The picture is always shown whole. A band media sits on a
    // deliberately black ground - when a creative reports one ratio but
    // renders less inside it, the black exposes the shortfall. The
    // background template is transparent instead: its ground is the
    // ambient backdrop standing behind the MediaView.
    mediaView.backgroundColor = backgroundTemplate
            ? UIColor.clearColor
            : UIColor.blackColor;
    mediaView.clipsToBounds = YES;
    mediaView.contentMode = UIViewContentModeScaleAspectFit;
    views.media = mediaView;
    views.mediaSlot = mediaView;
    if (probe) return mediaView;

    if (plan.renderVideo) {
        GADMediaContent *mediaContent = _nativeAd.mediaContent;
        if (mediaContent == nil || !mediaContent.hasVideoContent) {
            [NSException raise:@"RANRectAdBindException"
                        format:@"MediaContent is required for a selected "
                                "media layout"];
        }
        mediaView.mediaContent = mediaContent;
        return mediaView;
    }

    // Still fallback drawn inside the MediaView, so a video creative can
    // never auto-play in a slot below the video minimum.
    if (mainImage == nil) {
        [NSException raise:@"RANRectAdBindException"
                    format:@"Main image is required for an image fallback "
                            "layout"];
    }
    UIImageView *fallbackImageView =
            [[UIImageView alloc] initWithImage:mainImage];
    fallbackImageView.contentMode = UIViewContentModeScaleAspectFit;
    fallbackImageView.clipsToBounds = YES;
    [mediaView addSubview:fallbackImageView];
    mediaView.fallbackImageView = fallbackImageView;
    return mediaView;
}

- (void)ran_buildVideoMediaLeftContent:(RANLinearLayoutView *)outer
                            mediaView:(UIView *)mediaView
                                views:(RANRectAdAssetViews *)views
                                 plan:(RANRectAdLayoutPlan *)plan {
    CGFloat gap = [self gapForTier:plan.tier];
    RANLinearLayoutView *row = [[RANLinearLayoutView alloc] init];
    row.ran_vertical = NO;
    row.ran_gravity = RANGravityTop;

    mediaView.ran_layoutWidth = plan.mediaWidth;
    mediaView.ran_layoutHeight = plan.mediaHeight;
    mediaView.ran_layoutMargins = UIEdgeInsetsMake(0, 0, 0, gap);
    [row addSubview:mediaView];

    RANLinearLayoutView *rail = [self ran_buildVideoSideRail:views plan:plan];
    views.videoRail = rail;
    views.insetContent = rail;
    views.insetContentAvoidsBadges = YES;
    rail.ran_layoutWidth = 0;
    rail.ran_layoutHeight = plan.mediaHeight;
    rail.ran_layoutWeight = 1;
    [row addSubview:rail];
    row.ran_layoutWidth = RANLayoutMatchParent;
    [outer addSubview:row];

    views.headline = [self ran_createTextWithValue:_nativeAd.headline
                                             size:[self headlineSizeForPlan:plan]
                                             bold:YES];
    // One line by design, so it marquees rather than being cut.
    [self ran_applyTextMode:NO label:views.headline maxLines:1];
    views.headline.ran_padding = UIEdgeInsetsMake(
            kRANVideoHeadlineVerticalPadding
          , kRANVideoHeadlineHorizontalPadding
          , kRANVideoHeadlineVerticalPadding
          , kRANVideoHeadlineHorizontalPadding);
    views.headline.ran_layoutWidth = RANLayoutMatchParent;
    views.headline.ran_layoutMargins = UIEdgeInsetsMake(gap, 0, 0, 0);
    [outer addSubview:views.headline];
}

- (RANLinearLayoutView *)ran_buildVideoSideRail:(RANRectAdAssetViews *)views
                                         plan:(RANRectAdLayoutPlan *)plan {
    CGFloat gap = [self gapForTier:plan.tier];
    RANLinearLayoutView *rail = [[RANLinearLayoutView alloc] init];
    rail.ran_vertical = YES;
    rail.ran_gravity = RANGravityCenterHorizontal;

    RANLinearLayoutView *iconRow = [[RANLinearLayoutView alloc] init];
    iconRow.ran_vertical = NO;
    iconRow.ran_gravity = RANGravityCenterHorizontal | RANGravityCenterVertical;
    [self ran_addSizedIconTo:iconRow
                      views:views
                       size:[self ran_videoRailIconSizeForTier:plan.tier]
                        gap:0];
    if (views.icon != nil) {
        iconRow.ran_layoutWidth = RANLayoutMatchParent;
        [rail addSubview:iconRow];
    }

    UIView *spacer = [[UIView alloc] init];
    spacer.ran_layoutWidth = RANLayoutMatchParent;
    spacer.ran_layoutHeight = 0;
    spacer.ran_layoutWeight = 1;
    [rail addSubview:spacer];

    [self ran_addCallToActionTo:rail views:views plan:plan gap:gap fullWidth:YES];
    if (views.callToAction != nil) {
        views.callToAction.titleLabel.numberOfLines = 2;
    }
    return rail;
}

// Two modes, no middle ground - and per text: wrapped shows the value whole
// over its allowed lines or the candidate is rejected; marquee is one line
// that scrolls. A single-line grant marquees regardless.
// One allowed line is not a license to scroll: a single-line text that fits -
// or can be nudged to fit - is shown still and whole, and only the ladder's
// scrolling rungs may move it.
- (void)ran_applyTextMode:(BOOL)marquee
                   label:(RANAdTextLabel *)label
                maxLines:(NSInteger)maxLines {
    if (label == nil) return;
    label.maxLines = MAX(1, maxLines);
    label.marquee = marquee;
}

- (RANAdTextLabel *)ran_createTextWithValue:(NSString *)value
                                     size:(CGFloat)size
                                     bold:(BOOL)bold {
    RANAdTextLabel *text = [[RANAdTextLabel alloc] init];
    text.text = value ?: @"";
    text.textColor = _onVeil
            ? UIColor.whiteColor
            : RANRectAdARGB(RANThemeInk());
    text.font = bold
            ? [UIFont boldSystemFontOfSize:size]
            : [UIFont systemFontOfSize:size];
    // An asset the creative never sent is not a blank line to reserve: it
    // leaves no trace at all.
    NSString *trimmed = [text.text
            stringByTrimmingCharactersInSet:
                    NSCharacterSet.whitespaceAndNewlineCharacterSet];
    if (trimmed.length == 0) {
        text.ran_gone = YES;
        text.hidden = YES;
    }
    return text;
}

@end