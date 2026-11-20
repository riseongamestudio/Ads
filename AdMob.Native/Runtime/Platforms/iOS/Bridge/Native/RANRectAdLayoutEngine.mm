#import "RANRectAdLayoutEngine.h"

static NSString *const kRANTag = @"RectAd";

static const CGFloat kRANMinNativeAdSize = 32; // dp read as pt
static const NSInteger kRANMediaExpansionIterations = 14;
static const NSInteger kRANMaxExpandedMediaPlans = 8;
// Size is what a player sees, so it is scored as drawn: the headline by the
// size it ends up at, the media by the share of the cell it visibly takes.
// Assets come first, so the whole headline range (6-17pt) and a full cell of
// media together stay under one asset: size picks between layouts showing the
// same things, never between showing and dropping.
static const int64_t kRANHeadlinePointWeight = 400;
static const int64_t kRANMediaAreaPercentWeight = 50;
static const CGFloat kRANUnknownVideoAspectRatio = 1;
static const CGFloat kRANDefaultImageAspectRatio = 1.91f;
static const CGFloat kRANMinMediaAspectRatio = 0.2f;
static const CGFloat kRANMaxMediaAspectRatio = 5.0f;
static const CGFloat kRANMinVisibleTextSlotPx = 1;

typedef NS_ENUM(NSInteger, RANTextMode) {
    RANTextModeWrapped = 0
  , RANTextModeSecondaryMarquee
  , RANTextModeAllMarquee
};

// Text is worth more still than moving, and worth more big than small - in
// that order only while it stays readable. Whole text gets the first rungs
// down to 0.7x; past that, one secondary line scrolling at a larger size
// beats dragging the entire group into illegibility, so the scrolling rungs
// interleave from there - still always below a size wrapping has already
// failed at, with the headline joining the scroll last of all.
static const RANTextMode kRANTextLadderModes[] = {
    RANTextModeWrapped
  , RANTextModeWrapped
  , RANTextModeWrapped
  , RANTextModeSecondaryMarquee
  , RANTextModeSecondaryMarquee
  , RANTextModeWrapped
  , RANTextModeSecondaryMarquee
  , RANTextModeAllMarquee
  , RANTextModeAllMarquee
  , RANTextModeAllMarquee
};
static const CGFloat kRANTextLadderScales[] = {
    1, 0.85, 0.7, 0.8, 0.65, 0.55, 0.5, 0.8, 0.65, 0.5
};
static const NSInteger kRANTextLadderRungCount =
        sizeof(kRANTextLadderScales) / sizeof(kRANTextLadderScales[0]);

CGFloat RANClamp(CGFloat value, CGFloat minimum, CGFloat maximum) {
    return MAX(minimum, MIN(maximum, value));
}

@implementation RANRectAdLayoutPlan

- (instancetype)init {
    self = [super init];
    if (self != nil) _textScale = 1;
    return self;
}

@end

NSString *RANRectAdTemplateName(RANRectAdTemplate layoutTemplate) {
    if (layoutTemplate == RANRectAdTemplateCompactRow) return @"COMPACT_ROW";
    if (layoutTemplate == RANRectAdTemplateCompactColumn) {
        return @"COMPACT_COLUMN";
    }
    if (layoutTemplate == RANRectAdTemplateMediaLeft) return @"MEDIA_LEFT";
    if (layoutTemplate == RANRectAdTemplateMediaTop) return @"MEDIA_TOP";
    if (layoutTemplate == RANRectAdTemplateMediaBackground) {
        return @"MEDIA_BACKGROUND";
    }
    return @"MEDIA_RIGHT";
}

NSString *RANRectAdTierName(RANRectAdTier tier) {
    if (tier == RANRectAdTierCompact) return @"COMPACT";
    if (tier == RANRectAdTierRegular) return @"REGULAR";
    return @"ROOMY";
}

NSString *RANRectAdMediaName(RANRectAdLayoutPlan *plan) {
    if (!plan.showMedia) return @"NONE";
    return plan.renderVideo ? @"VIDEO" : @"IMAGE";
}

NSString *RANRectAdDescribePlan(RANRectAdLayoutPlan *plan) {
    if (plan == nil) return @"none";
    return [NSString stringWithFormat:
            @"%@/%@/%@/icon=%@/body=%@/advertiser=%@/rating=%@/textx%g/marq=%@"
          , RANRectAdTemplateName(plan.layoutTemplate)
          , RANRectAdTierName(plan.tier)
          , RANRectAdMediaName(plan)
          , plan.showIcon ? @"true" : @"false"
          , plan.showBody ? @"true" : @"false"
          , plan.showAdvertiser ? @"true" : @"false"
          , plan.showRating ? @"true" : @"false"
          , plan.textScale
          , plan.marqueeHeadline
                    ? @"all"
                    : (plan.marqueeSecondary ? @"sec" : @"none")];
}

// The probe cache entry: text scale and marquee mode are not part of the
// key - configureProbeLayout re-applies them so one tree serves the whole
// ladder.
@interface RANCachedProbeLayout : NSObject
@property (nonatomic, strong) RANRectAdProbeLayout *probe;
@property (nonatomic) BOOL showBody;
@property (nonatomic) BOOL showAdvertiser;
@property (nonatomic) BOOL showRating;
@property (nonatomic) BOOL showIcon;
@end

@implementation RANCachedProbeLayout
@end

static int64_t RANVisibleMediaPercent(RANRectAdLayoutPlan *plan) {
    if (!plan.showMedia
            || plan.layoutTemplate == RANRectAdTemplateMediaBackground
            || plan.width <= 0
            || plan.height <= 0) {
        return 0;
    }
    return MIN(
            100LL
          , (int64_t)llround(
                100.0 * plan.mediaWidth * plan.mediaHeight
                        / (plan.width * plan.height)));
}

@implementation RANRectAdLayoutEngine {
    GADNativeAd *_nativeAd;
    CGFloat _requestedX;
    CGFloat _requestedY;
    CGFloat _requestedWidth;
    CGFloat _requestedHeight;
    RANRectAdViewFactory *_viewFactory;
    RANRectAdLayoutValidator *_validator;
    NSMutableArray<RANCachedProbeLayout *> *_probeLayouts;
    NSMutableDictionary<NSNumber *, NSString *> *_rejectionReasons;
    CGFloat _screenScale;
}

- (instancetype)initWithNativeAd:(GADNativeAd *)nativeAd
                      requestedX:(CGFloat)requestedX
                      requestedY:(CGFloat)requestedY
                  requestedWidth:(CGFloat)requestedWidth
                 requestedHeight:(CGFloat)requestedHeight
                     viewFactory:(RANRectAdViewFactory *)viewFactory
                       validator:(RANRectAdLayoutValidator *)validator {
    self = [super init];
    if (self == nil) return nil;
    _nativeAd = nativeAd;
    _requestedX = requestedX;
    _requestedY = requestedY;
    _requestedWidth = requestedWidth;
    _requestedHeight = requestedHeight;
    _viewFactory = viewFactory;
    _validator = validator;
    _probeLayouts = [NSMutableArray array];
    _rejectionReasons = [NSMutableDictionary dictionary];
    _screenScale = MAX(1, UIScreen.mainScreen.nativeScale);
    return self;
}

- (void)setPositionX:(CGFloat)xPt y:(CGFloat)yPt {
    _requestedX = xPt;
    _requestedY = yPt;
}

- (void)clear {
    [_probeLayouts removeAllObjects];
}

// Keeps the first few distinct reasons per template rather than only the
// first: with one slot, every diagnosis of a slot that renders nothing was
// made from whichever variant happened to fail first, which repeatedly
// pointed at the wrong constraint.
static const NSInteger kRANMaxRejectionReasonsPerTemplate = 3;

// Within one layout, whole always beats moving when nothing else is traded
// away: a text a hair too long for its lines is shrunk alone, in small steps
// down to a floor, before any rung is allowed to scroll it.
static const CGFloat kRANNudgeTextStep = 0.92f;
static const CGFloat kRANNudgeTextFloor = 0.7f;
static const NSInteger kRANNudgeTextMaxSteps = 5;

- (void)ran_nudgeCutTextsWholeInProbe:(RANRectAdProbeLayout *)probeLayout
                                plan:(RANRectAdLayoutPlan *)plan
                               width:(CGFloat)width
                              height:(CGFloat)height {
    RANRectAdAssetViews *views = probeLayout.views;
    [self ran_nudgeTextWhole:plan.marqueeHeadline ? nil : views.headline
                    inProbe:probeLayout.root
                      width:width
                     height:height];
    [self ran_nudgeTextWhole:plan.marqueeSecondary ? nil : views.body
                    inProbe:probeLayout.root
                      width:width
                     height:height];
    [self ran_nudgeTextWhole:plan.marqueeSecondary ? nil : views.advertiser
                    inProbe:probeLayout.root
                      width:width
                     height:height];
}

- (void)ran_nudgeTextWhole:(RANAdTextLabel *)text
                  inProbe:(UIView *)probeRoot
                    width:(CGFloat)width
                   height:(CGFloat)height {
    if (text == nil || text.hidden) return;

    CGFloat floorSize = text.font.pointSize * kRANNudgeTextFloor;
    for (NSInteger step = 0; step < kRANNudgeTextMaxSteps; ++step) {
        CGFloat labelWidth = text.frame.size.width;
        if (labelWidth <= 0) labelWidth = text.ran_measuredSize.width;
        if ([text ran_showsEntireTextForWidth:labelWidth]) return;

        CGFloat next = text.font.pointSize * kRANNudgeTextStep;
        if (next < floorSize) return;

        text.font = [text.font fontWithSize:next];
        [probeRoot ran_measureWithWidthSpec:
                        RANMeasureSpecMake(RANMeasureSpecExactly, width)
                                 heightSpec:
                        RANMeasureSpecMake(RANMeasureSpecExactly, height)];
        [probeRoot ran_layoutWithFrame:CGRectMake(0, 0, width, height)];
    }
}

- (void)ran_recordRejectionForTemplate:(RANRectAdTemplate)layoutTemplate
                                 tier:(RANRectAdTier)tier
                               reason:(NSString *)reason {
    NSNumber *key = @(layoutTemplate);
    if (reason == nil) return;

    NSString *entry = [NSString stringWithFormat:
            @"%@: %@", RANRectAdTierName(tier), reason];
    NSString *existing = _rejectionReasons[key];
    if (existing == nil) {
        _rejectionReasons[key] = entry;
        return;
    }
    if ([existing containsString:reason]) return;

    NSArray<NSString *> *kept =
            [existing componentsSeparatedByString:@" | "];
    if ((NSInteger)kept.count >= kRANMaxRejectionReasonsPerTemplate) return;

    _rejectionReasons[key] =
            [existing stringByAppendingFormat:@" | %@", entry];
}

- (NSString *)describeLastRejections {
    NSMutableArray<NSString *> *parts = [NSMutableArray array];
    for (NSInteger layoutTemplate = RANRectAdTemplateCompactRow;
         layoutTemplate <= RANRectAdTemplateMediaRight;
         ++layoutTemplate) {
        NSString *reason = _rejectionReasons[@(layoutTemplate)];
        if (reason == nil) continue;
        [parts addObject:[NSString stringWithFormat:@"%@=%@"
              , RANRectAdTemplateName((RANRectAdTemplate)layoutTemplate)
              , reason]];
    }
    return [parts componentsJoinedByString:@"; "];
}

- (NSArray<RANRectAdLayoutPlan *> *)choosePlansForScreenWidth:(CGFloat)screenWidth
                                                screenHeight:(CGFloat)screenHeight
                                                    hasVideo:(BOOL)hasVideo
                                                hasMainImage:(BOOL)hasMainImage {
    [_rejectionReasons removeAllObjects];
    NSMutableArray<RANRectAdLayoutPlan *> *plans = [NSMutableArray array];
    if (_requestedWidth < kRANMinNativeAdSize
            || _requestedHeight < kRANMinNativeAdSize) {
        NSLog(@"%@: Rect ad requested rect is below the %gdp minimum"
              , kRANTag, kRANMinNativeAdSize);
        return plans;
    }
    CGFloat desiredWidth = MIN(screenWidth, _requestedWidth);
    CGFloat desiredHeight = MIN(screenHeight, _requestedHeight);
    if (desiredWidth < kRANMinNativeAdSize
            || desiredHeight < kRANMinNativeAdSize) {
        NSLog(@"%@: Rect ad content surface clamps requested rect below "
                "the %gdp minimum", kRANTag, kRANMinNativeAdSize);
        return plans;
    }

    NSMutableArray<RANRectAdLayoutPlan *> *generalVariants =
            [NSMutableArray array];
    NSMutableArray<RANRectAdLayoutPlan *> *videoVariants =
            [NSMutableArray array];
    BOOL hasBody = _nativeAd.body.length > 0;
    BOOL hasAdvertiser = _nativeAd.advertiser.length > 0;
    BOOL hasRating = [_viewFactory hasValidStarRating];
    BOOL hasIcon = [_viewFactory hasRenderableIcon];
    CGFloat width = MAX(1, MIN(screenWidth, desiredWidth));
    for (NSInteger tier = RANRectAdTierCompact;
         tier <= RANRectAdTierRoomy;
         ++tier) {
        for (NSInteger optionalMask = 0; optionalMask < 16; ++optionalMask) {
            BOOL showBody = (optionalMask & 1) != 0;
            BOOL showAdvertiser = (optionalMask & 2) != 0;
            BOOL showRating = (optionalMask & 4) != 0;
            // The icon is optional exactly the way the texts are; it is the
            // widest of them, so dropping it is often what leaves the
            // headline its minimum.
            BOOL showIcon = (optionalMask & 8) != 0;
            if ((showBody && !hasBody)
                    || (showAdvertiser && !hasAdvertiser)
                    || (showRating && !hasRating)
                    || (showIcon && !hasIcon)) {
                continue;
            }

            if (hasVideo) {
                [self ran_updateVariantBest:videoVariants
                                 candidate:[self ran_evaluatePlanForTemplate:RANRectAdTemplateMediaLeft
                                        tier:(RANRectAdTier)tier
                                   showMedia:YES
                                 renderVideo:YES
                                    showBody:showBody
                              showAdvertiser:showAdvertiser
                                  showRating:showRating
                                    showIcon:showIcon
                              candidateWidth:width
                               desiredHeight:desiredHeight
                                 screenWidth:screenWidth
                                screenHeight:screenHeight]];
                // Video never serves as a background: a veil over a playing
                // video is a viewability problem, not a layout. The
                // background template is images only.
                // A video creative has no way down from a video plan: Google
                // serves video only into a MediaView of 120pt or more, and
                // the validator flags any smaller one, or none, once the
                // bound ad carries video. A rect that cannot host one fails
                // the layout, and the slot fetches another creative at once.
                continue;
            }

            for (NSInteger compactTemplate = RANRectAdTemplateCompactRow;
                 compactTemplate <= RANRectAdTemplateCompactColumn;
                 ++compactTemplate) {
                [self ran_updateVariantBest:generalVariants
                                 candidate:[self ran_evaluatePlanForTemplate:(RANRectAdTemplate)compactTemplate
                                        tier:(RANRectAdTier)tier
                                   showMedia:NO
                                 renderVideo:NO
                                    showBody:showBody
                              showAdvertiser:showAdvertiser
                                  showRating:showRating
                                    showIcon:showIcon
                              candidateWidth:width
                               desiredHeight:desiredHeight
                                 screenWidth:screenWidth
                                screenHeight:screenHeight]];
            }

            if (hasMainImage) {
                for (NSInteger mediaTemplate = RANRectAdTemplateMediaLeft;
                     mediaTemplate <= RANRectAdTemplateMediaRight;
                     ++mediaTemplate) {
                    [self ran_updateVariantBest:generalVariants
                                     candidate:[self ran_evaluatePlanForTemplate:(RANRectAdTemplate)mediaTemplate
                                            tier:(RANRectAdTier)tier
                                       showMedia:YES
                                     renderVideo:NO
                                        showBody:showBody
                                  showAdvertiser:showAdvertiser
                                      showRating:showRating
                                        showIcon:showIcon
                                  candidateWidth:width
                                   desiredHeight:desiredHeight
                                     screenWidth:screenWidth
                                    screenHeight:screenHeight]];
                }
            }
        }
    }

    NSMutableArray<RANRectAdLayoutPlan *> *primaryVariants =
            hasVideo ? videoVariants : generalVariants;
    [plans addObjectsFromArray:primaryVariants];
    [self ran_finalizePlanList:plans];

    [self clear];
    return plans;
}

- (void)ran_finalizePlanList:(NSMutableArray<RANRectAdLayoutPlan *> *)plans {
    [plans sortWithOptions:NSSortStable
           usingComparator:^NSComparisonResult(RANRectAdLayoutPlan *first
                                             , RANRectAdLayoutPlan *second) {
        if (first.score < second.score) return NSOrderedAscending;
        if (first.score > second.score) return NSOrderedDescending;
        return NSOrderedSame;
    }];
    [self ran_pruneDominatedPlans:plans];
    NSInteger expandedMediaPlans = 0;
    for (NSUInteger index = 0;
         index < plans.count && expandedMediaPlans < kRANMaxExpandedMediaPlans;
         ++index) {
        RANRectAdLayoutPlan *plan = plans[index];
        if (!plan.showMedia) continue;
        [self ran_expandMediaWithinPlan:plan];
        ++expandedMediaPlans;
    }
    [plans sortWithOptions:NSSortStable
           usingComparator:^NSComparisonResult(RANRectAdLayoutPlan *first
                                             , RANRectAdLayoutPlan *second) {
        if (first.score < second.score) return NSOrderedAscending;
        if (first.score > second.score) return NSOrderedDescending;
        return NSOrderedSame;
    }];
}

- (void)ran_pruneDominatedPlans:(NSMutableArray<RANRectAdLayoutPlan *> *)plans {
    NSMutableArray<RANRectAdLayoutPlan *> *selected = [NSMutableArray array];
    for (RANRectAdLayoutPlan *plan in plans) {
        BOOL hasFamily = NO;
        BOOL hasRequiredOnlyFallback = NO;
        for (RANRectAdLayoutPlan *kept in selected) {
            if (kept.layoutTemplate != plan.layoutTemplate
                    || kept.tier != plan.tier
                    || kept.showMedia != plan.showMedia
                    || kept.renderVideo != plan.renderVideo) {
                continue;
            }
            hasFamily = YES;
            if (!kept.showBody && !kept.showAdvertiser && !kept.showRating) {
                hasRequiredOnlyFallback = YES;
            }
        }
        BOOL requiredOnly =
                !plan.showBody && !plan.showAdvertiser && !plan.showRating;
        if (!hasFamily || (requiredOnly && !hasRequiredOnlyFallback)) {
            [selected addObject:plan];
        }
    }
    [plans removeAllObjects];
    [plans addObjectsFromArray:selected];
}

// Best per variant key; the icon is part of the key so an icon variant and
// its iconless twin both survive as fallbacks.
- (void)ran_updateVariantBest:(NSMutableArray<RANRectAdLayoutPlan *> *)variants
                   candidate:(RANRectAdLayoutPlan *)candidate {
    if (candidate == nil) return;
    for (NSUInteger index = 0; index < variants.count; ++index) {
        RANRectAdLayoutPlan *current = variants[index];
        if (current.layoutTemplate == candidate.layoutTemplate
                && current.tier == candidate.tier
                && current.showMedia == candidate.showMedia
                && current.renderVideo == candidate.renderVideo
                && current.showBody == candidate.showBody
                && current.showAdvertiser == candidate.showAdvertiser
                && current.showRating == candidate.showRating
                && current.showIcon == candidate.showIcon) {
            if (candidate.score < current.score) {
                variants[index] = candidate;
            }
            return;
        }
    }
    [variants addObject:candidate];
}

- (RANRectAdProbeLayout *)ran_probeLayoutForPlan:(RANRectAdLayoutPlan *)plan {
    for (RANCachedProbeLayout *cached in _probeLayouts) {
        RANRectAdProbeLayout *probe = cached.probe;
        if (probe.layoutTemplate == plan.layoutTemplate
                && probe.tier == plan.tier
                && probe.showMedia == plan.showMedia
                && probe.renderVideo == plan.renderVideo
                && cached.showBody == plan.showBody
                && cached.showAdvertiser == plan.showAdvertiser
                && cached.showRating == plan.showRating
                && cached.showIcon == plan.showIcon) {
            return probe;
        }
    }

    RANRectAdProbeLayout *probe = [_viewFactory createProbeForPlan:plan];
    RANCachedProbeLayout *cached = [[RANCachedProbeLayout alloc] init];
    cached.probe = probe;
    cached.showBody = plan.showBody;
    cached.showAdvertiser = plan.showAdvertiser;
    cached.showRating = plan.showRating;
    cached.showIcon = plan.showIcon;
    [_probeLayouts addObject:cached];
    return probe;
}

- (CGFloat)ran_measureNaturalContentHeight:(UIView *)view
                                    width:(CGFloat)width {
    [view ran_measureWithWidthSpec:RANMeasureSpecMake(RANMeasureSpecExactly, width)
                       heightSpec:RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
    return MAX(kRANMinNativeAdSize, view.ran_measuredSize.height);
}

- (RANRectAdLayoutPlan *)ran_evaluatePlanForTemplate:(RANRectAdTemplate)layoutTemplate
                                              tier:(RANRectAdTier)tier
                                         showMedia:(BOOL)showMedia
                                       renderVideo:(BOOL)renderVideo
                                          showBody:(BOOL)showBody
                                    showAdvertiser:(BOOL)showAdvertiser
                                        showRating:(BOOL)showRating
                                          showIcon:(BOOL)showIcon
                                    candidateWidth:(CGFloat)candidateWidth
                                     desiredHeight:(CGFloat)desiredHeight
                                       screenWidth:(CGFloat)screenWidth
                                      screenHeight:(CGFloat)screenHeight {
    CGFloat minWidth = [_viewFactory minimumWidthForTier:tier];
    BOOL compactWithoutMedia = !renderVideo
            && !showMedia
            && (layoutTemplate == RANRectAdTemplateCompactRow
                    || layoutTemplate == RANRectAdTemplateCompactColumn);
    if (!compactWithoutMedia && candidateWidth < minWidth) {
        [self ran_recordRejectionForTemplate:layoutTemplate
                                       tier:tier
                                     reason:[NSString stringWithFormat:
                                            @"width %g < %g"
                                          , candidateWidth, minWidth]];
        return nil;
    }
    if (candidateWidth <= 0 || candidateWidth > screenWidth) {
        [self ran_recordRejectionForTemplate:layoutTemplate
                                       tier:tier
                                     reason:[NSString stringWithFormat:
                                            @"width %g is outside screen "
                                             "width %g"
                                          , candidateWidth, screenWidth]];
        return nil;
    }

    RANRectAdLayoutPlan *plan = [[RANRectAdLayoutPlan alloc] init];
    plan.layoutTemplate = layoutTemplate;
    plan.tier = tier;
    plan.showMedia = showMedia;
    plan.renderVideo = renderVideo;
    plan.showBody = showBody && _nativeAd.body.length > 0;
    plan.showAdvertiser = showAdvertiser && _nativeAd.advertiser.length > 0;
    plan.showIcon = showIcon;
    plan.showRating = showRating && [_viewFactory hasValidStarRating];
    if (renderVideo) {
        plan.showBody = NO;
        plan.showAdvertiser = NO;
        plan.showRating = NO;
        plan.showIcon = NO;
    }
    plan.width = candidateWidth;
    plan.height = desiredHeight;
    if (![self ran_resolveMediaDimensionsForPlan:plan]) return nil;
    // Shrinking to fit mutates the media dimensions on the plan; each rung
    // starts from the resolved size or a rejected shrink poisons the rest.
    CGFloat baseMediaWidth = plan.mediaWidth;
    CGFloat baseMediaHeight = plan.mediaHeight;

    CGFloat naturalContentHeight = 0;
    NSString *lastFailure = nil;
    BOOL fitted = NO;
    BOOL hasSecondaryText = plan.showBody || plan.showAdvertiser;
    for (NSInteger rung = 0; rung < kRANTextLadderRungCount; ++rung) {
        RANTextMode textMode = kRANTextLadderModes[rung];
        if (textMode == RANTextModeSecondaryMarquee && !hasSecondaryText) {
            continue;
        }
        plan.marqueeHeadline = textMode == RANTextModeAllMarquee;
        plan.marqueeSecondary = textMode != RANTextModeWrapped;
        plan.textScale = kRANTextLadderScales[rung];
        plan.mediaWidth = baseMediaWidth;
        plan.mediaHeight = baseMediaHeight;

        RANRectAdProbeLayout *probeLayout = [self ran_probeLayoutForPlan:plan];
        [_viewFactory configureProbeLayout:probeLayout forPlan:plan];
        [_viewFactory dodgeAdChoicesMarkInProbe:probeLayout
                                           plan:plan
                                          width:plan.width
                                         height:desiredHeight];
        UIView *finalProbe = probeLayout.root;
        naturalContentHeight =
                [self ran_measureNaturalContentHeight:finalProbe
                                               width:plan.width];
        if (naturalContentHeight > desiredHeight) {
            naturalContentHeight =
                    [self ran_shrinkImageToFitHeightForPlan:plan
                                                     probe:probeLayout
                                                     width:plan.width
                                             desiredHeight:desiredHeight
                                     originalNaturalHeight:naturalContentHeight];
        }
        if (naturalContentHeight > desiredHeight) {
            lastFailure = [NSString stringWithFormat:
                    @"natural height %g > %g at text x%g"
                  , naturalContentHeight, desiredHeight, plan.textScale];
            continue;
        }

        [finalProbe ran_measureWithWidthSpec:
                        RANMeasureSpecMake(RANMeasureSpecExactly, plan.width)
                                 heightSpec:
                        RANMeasureSpecMake(RANMeasureSpecExactly, desiredHeight)];
        [finalProbe ran_layoutWithFrame:
                CGRectMake(0, 0, plan.width, desiredHeight)];
        [self ran_nudgeCutTextsWholeInProbe:probeLayout
                                      plan:plan
                                     width:plan.width
                                    height:desiredHeight];
        if (![_validator validateAssetGeometryForRoot:finalProbe
                                                views:probeLayout.views
                                                 plan:plan
                                           logFailure:NO]) {
            NSString *validationReason = _validator.lastFailureReason;
            lastFailure = [NSString stringWithFormat:@"%@ at text x%g"
                  , validationReason ?: @"asset geometry rejected"
                  , plan.textScale];
            continue;
        }

        fitted = YES;
        break;
    }
    if (!fitted) {
        [self ran_recordRejectionForTemplate:layoutTemplate
                                       tier:tier
                                     reason:lastFailure];
        return nil;
    }

    plan.measuredContentHeight = naturalContentHeight;

    int64_t movement = llabs((int64_t)llround(
            RANClamp(_requestedX, 0, MAX(0, screenWidth - plan.width))
                    - _requestedX))
            + llabs((int64_t)llround(
            RANClamp(_requestedY, 0, MAX(0, screenHeight - desiredHeight))
                    - _requestedY));
    int64_t whitespace = [self ran_whitespacePxForPlan:plan];
    NSInteger richness = 0;
    if (showMedia) richness += 8;
    if (renderVideo) richness += 3;
    if (plan.showBody) richness += 1;
    if (plan.showAdvertiser) richness += 1;
    if (plan.showRating) richness += 1;
    if (plan.showIcon) richness += 1;

    RANRectAdTier preferredTier =
            [_viewFactory preferredTierForWidth:plan.width
                                         height:desiredHeight];
    int64_t tierPenalty = labs((long)(tier - preferredTier));
    // Counted per scrolling text: scrolling is a price paid for an element
    // or for the group's legibility, never a preference of its own.
    int64_t marqueePenalty = 0;
    if (plan.marqueeHeadline) ++marqueePenalty;
    if (plan.marqueeSecondary && hasSecondaryText) ++marqueePenalty;
    // Media sits on the left by preference - most people are
    // right-handed, so the text and the button belong under the thumb.
    int64_t sidePenalty =
            plan.layoutTemplate == RANRectAdTemplateMediaRight ? 1 : 0;
    // The background template is how media survives a cell that no band
    // layout can host, not a first choice: its picture sits under the veil
    // and the text, so it earns no media area, and the penalty keeps it
    // below a band showing the same assets.
    int64_t backgroundPenalty =
            plan.layoutTemplate == RANRectAdTemplateMediaBackground
                    ? 6000LL
                    : 0LL;
    plan.score = movement * 1000000LL
            + backgroundPenalty
            + whitespace * 100LL
            + tierPenalty * 1000LL
            + marqueePenalty * 3000LL
            + sidePenalty * 500LL
            - RANVisibleMediaPercent(plan) * kRANMediaAreaPercentWeight
            - (int64_t)llround(
                    [_viewFactory headlineSizeForPlan:plan]
                            * kRANHeadlinePointWeight)
            - (int64_t)richness * 10000LL;
    return plan;
}

// A still image gives up size before the plan gives up the image: on top it
// frees height for the text below, beside the text it fits a cell shorter
// than the tier's media size. Never under the media floor.
- (CGFloat)ran_shrinkImageToFitHeightForPlan:(RANRectAdLayoutPlan *)plan
                                      probe:(RANRectAdProbeLayout *)probeLayout
                                      width:(CGFloat)width
                              desiredHeight:(CGFloat)desiredHeight
                      originalNaturalHeight:(CGFloat)originalNaturalHeight {
    if ((plan.layoutTemplate != RANRectAdTemplateMediaTop
                    && !RANRectAdTemplateIsMediaSide(plan.layoutTemplate))
            || !plan.showMedia
            || plan.renderVideo
            || plan.mediaWidth <= 0
            || plan.mediaHeight <= 0) {
        return originalNaturalHeight;
    }

    CGFloat originalMediaWidth = plan.mediaWidth;
    CGFloat originalMediaHeight = plan.mediaHeight;
    // The validator holds any MediaView to the media policy floor, so
    // shrinking below it manufactures plans that can never validate.
    CGFloat minimumMediaSide = [_viewFactory mediaPolicyFloor];
    CGFloat minimumScale = MAX(
            minimumMediaSide / originalMediaWidth
          , minimumMediaSide / originalMediaHeight);
    if (minimumScale >= 1) return originalNaturalHeight;

    plan.mediaWidth = MAX(
            minimumMediaSide, ceil(originalMediaWidth * minimumScale));
    plan.mediaHeight = MAX(
            minimumMediaSide, ceil(originalMediaHeight * minimumScale));
    [_viewFactory configureProbeLayout:probeLayout forPlan:plan];
    CGFloat minimumNaturalHeight =
            [self ran_measureNaturalContentHeight:probeLayout.root width:width];
    if (minimumNaturalHeight > desiredHeight) {
        plan.mediaWidth = originalMediaWidth;
        plan.mediaHeight = originalMediaHeight;
        [_viewFactory configureProbeLayout:probeLayout forPlan:plan];
        return originalNaturalHeight;
    }

    CGFloat bestMediaWidth = plan.mediaWidth;
    CGFloat bestMediaHeight = plan.mediaHeight;
    CGFloat bestNaturalHeight = minimumNaturalHeight;
    CGFloat low = minimumScale;
    CGFloat high = 1;
    for (NSInteger iteration = 0;
         iteration < kRANMediaExpansionIterations;
         ++iteration) {
        CGFloat scale = (low + high) / 2;
        plan.mediaWidth = MAX(
                minimumMediaSide, floor(originalMediaWidth * scale));
        plan.mediaHeight = MAX(
                minimumMediaSide, floor(originalMediaHeight * scale));
        [_viewFactory configureProbeLayout:probeLayout forPlan:plan];
        CGFloat naturalHeight =
                [self ran_measureNaturalContentHeight:probeLayout.root
                                               width:width];
        if (naturalHeight <= desiredHeight) {
            bestMediaWidth = plan.mediaWidth;
            bestMediaHeight = plan.mediaHeight;
            bestNaturalHeight = naturalHeight;
            low = scale;
        } else {
            high = scale;
        }
    }

    plan.mediaWidth = bestMediaWidth;
    plan.mediaHeight = bestMediaHeight;
    [_viewFactory configureProbeLayout:probeLayout forPlan:plan];
    return bestNaturalHeight;
}

- (void)ran_expandMediaWithinPlan:(RANRectAdLayoutPlan *)plan {
    if (plan == nil
            || !plan.showMedia
            || plan.mediaWidth <= 0
            || plan.mediaHeight <= 0) {
        return;
    }

    CGFloat maxMediaWidth = [self ran_contentWidthForPlan:plan];
    if (RANRectAdTemplateIsMediaSide(plan.layoutTemplate)) {
        maxMediaWidth -= [_viewFactory gapForTier:plan.tier]
                + [self ran_requiredTextSlotWidthForPlan:plan];
    }
    if (maxMediaWidth <= plan.mediaWidth) return;

    CGFloat maxScale = MIN(
            maxMediaWidth / plan.mediaWidth
          , [self ran_contentHeightForPlan:plan] / plan.mediaHeight);
    if (maxScale <= 1) return;

    CGFloat baseWidth = plan.mediaWidth;
    CGFloat baseHeight = plan.mediaHeight;
    CGFloat bestWidth = baseWidth;
    CGFloat bestHeight = baseHeight;
    CGFloat bestNaturalHeight = plan.measuredContentHeight;
    CGFloat low = 1;
    CGFloat high = maxScale;
    RANRectAdProbeLayout *probe = [self ran_probeLayoutForPlan:plan];

    for (NSInteger iteration = 0;
         iteration < kRANMediaExpansionIterations;
         ++iteration) {
        CGFloat scale = (low + high) / 2;
        plan.mediaWidth = MAX(baseWidth, floor(baseWidth * scale));
        plan.mediaHeight = MAX(baseHeight, floor(baseHeight * scale));
        [_viewFactory configureProbeLayout:probe forPlan:plan];
        CGFloat naturalHeight =
                [self ran_measureNaturalContentHeight:probe.root
                                               width:plan.width];

        BOOL fits = naturalHeight <= plan.height;
        if (fits) {
            [probe.root ran_measureWithWidthSpec:
                            RANMeasureSpecMake(RANMeasureSpecExactly, plan.width)
                                     heightSpec:
                            RANMeasureSpecMake(RANMeasureSpecExactly, plan.height)];
            [probe.root ran_layoutWithFrame:
                    CGRectMake(0, 0, plan.width, plan.height)];
            fits = [_validator validateAssetGeometryForRoot:probe.root
                                                      views:probe.views
                                                       plan:plan
                                                 logFailure:NO];
        }

        if (fits) {
            bestWidth = plan.mediaWidth;
            bestHeight = plan.mediaHeight;
            bestNaturalHeight = naturalHeight;
            low = scale;
        } else {
            high = scale;
        }
    }

    int64_t beforePercent = RANVisibleMediaPercent(plan);
    int64_t beforeWhitespace = [self ran_whitespacePxForPlan:plan];
    plan.mediaWidth = bestWidth;
    plan.mediaHeight = bestHeight;
    plan.measuredContentHeight = bestNaturalHeight;
    // The plan was ranked at its smallest media; it is re-ranked at the size
    // it will be shown at.
    plan.score += (beforePercent - RANVisibleMediaPercent(plan))
                    * kRANMediaAreaPercentWeight
            + ([self ran_whitespacePxForPlan:plan] - beforeWhitespace) * 100LL;
}

// Unused area, in pixel rows of the cell's width: the band left under the
// content, and beside a media band narrower than the column - the strips
// either side of a picture on top are as empty as a gap under it.
- (int64_t)ran_whitespacePxForPlan:(RANRectAdLayoutPlan *)plan {
    CGFloat whitespace = MAX(0, plan.height - plan.measuredContentHeight);
    if (plan.layoutTemplate == RANRectAdTemplateMediaTop
            && plan.showMedia
            && plan.width > 0) {
        whitespace += MAX(0, [self ran_contentWidthForPlan:plan] - plan.mediaWidth)
                * plan.mediaHeight
                / plan.width;
    }
    return (int64_t)llround(whitespace * _screenScale);
}

// What the outer column leaves for content once the corner clearance is
// spent on both sides: the width and height every media size is resolved
// against, so a media band planned here lands inside the column's padding
// instead of running over it into the corner. The background template is
// the one exception and reads plan.width and plan.height itself - its
// picture is meant to fill the cell.
- (CGFloat)ran_contentWidthForPlan:(RANRectAdLayoutPlan *)plan {
    return plan.width - 2 * [_viewFactory paddingForTier:plan.tier];
}

- (CGFloat)ran_contentHeightForPlan:(RANRectAdLayoutPlan *)plan {
    return plan.height - 2 * [_viewFactory paddingForTier:plan.tier];
}

- (CGFloat)ran_requiredTextSlotWidthForPlan:(RANRectAdLayoutPlan *)plan {
    if (RANRectAdTemplateIsMediaSide(plan.layoutTemplate)) {
        return [_viewFactory minimumMediaLeftTextSlotWidthForPlan:plan];
    }
    return kRANMinVisibleTextSlotPx / _screenScale
            + 2 * [_viewFactory paddingForTier:plan.tier];
}

- (BOOL)ran_resolveMediaDimensionsForPlan:(RANRectAdLayoutPlan *)plan {
    if (!plan.showMedia) {
        plan.mediaWidth = 0;
        plan.mediaHeight = 0;
        return YES;
    }

    CGFloat contentWidth = [self ran_contentWidthForPlan:plan];
    if (contentWidth <= 0) {
        [self ran_recordRejectionForTemplate:plan.layoutTemplate
                                       tier:plan.tier
                                     reason:[NSString stringWithFormat:
                                            @"safe content width is %g"
                                          , contentWidth]];
        return NO;
    }

    CGFloat aspect = [self ran_mediaAspectRatioForVideo:plan.renderVideo];
    if (plan.layoutTemplate == RANRectAdTemplateMediaBackground) {
        // Both dimensions carry the registered MediaView, so both are
        // held to the media minimum - video's floor for a video creative,
        // the far lower image floor otherwise.
        CGFloat minSide = [_viewFactory mediaPolicyFloor];
        if (plan.width < minSide || plan.height < minSide) {
            [self ran_recordRejectionForTemplate:plan.layoutTemplate
                                           tier:plan.tier
                                         reason:[NSString stringWithFormat:
                                                @"slot %gx%g is under the "
                                                 "media minimum"
                                              , plan.width, plan.height]];
            return NO;
        }
        plan.mediaWidth = plan.width;
        plan.mediaHeight = plan.height;
        return YES;
    }
    if (plan.layoutTemplate == RANRectAdTemplateMediaTop) {
        plan.mediaWidth = contentWidth;
        plan.mediaHeight = MAX(1, ceil(contentWidth / aspect));
        return YES;
    }

    CGFloat height = [_viewFactory mediaSizeForTier:plan.tier];
    CGFloat width;
    if (plan.renderVideo) {
        CGFloat maximumMediaWidth = contentWidth
                - [_viewFactory gapForTier:plan.tier]
                - [self ran_requiredTextSlotWidthForPlan:plan];
        CGSize videoSize = [self ran_resolveVideoSlotWithPreferredHeight:height
                                                          maximumWidth:maximumMediaWidth
                                                        creativeAspect:aspect];
        if (videoSize.width <= 0) {
            [self ran_recordRejectionForTemplate:plan.layoutTemplate
                                           tier:plan.tier
                                         reason:[NSString stringWithFormat:
                                                @"video cannot fit media "
                                                 "width %g", maximumMediaWidth]];
            return NO;
        }
        width = videoSize.width;
        height = videoSize.height;
    } else {
        width = MAX(1, ceil(height * aspect));
    }

    CGFloat minimumTextWidth = [self ran_requiredTextSlotWidthForPlan:plan];
    CGFloat requiredWidth = width
            + [_viewFactory gapForTier:plan.tier]
            + minimumTextWidth;
    if (requiredWidth > contentWidth) {
        [self ran_recordRejectionForTemplate:plan.layoutTemplate
                                       tier:plan.tier
                                     reason:[NSString stringWithFormat:
                                            @"media and text need %g > safe "
                                             "width %g"
                                          , requiredWidth, contentWidth]];
        return NO;
    }
    plan.mediaWidth = width;
    plan.mediaHeight = height;
    return YES;
}

- (CGSize)ran_resolveVideoSlotWithPreferredHeight:(CGFloat)preferredHeight
                                    maximumWidth:(CGFloat)maximumWidth
                                  creativeAspect:(CGFloat)creativeAspect {
    CGFloat requiredWidth =
            [_validator requiredVideoWidthForAspect:creativeAspect];
    CGFloat requiredHeight =
            [_validator requiredVideoHeightForAspect:creativeAspect];
    if (maximumWidth < requiredWidth) return CGSizeZero;

    CGFloat height = MAX(requiredHeight, preferredHeight);
    CGFloat aspectWidth = MAX(requiredWidth, ceil(height * creativeAspect));
    CGFloat width = MIN(maximumWidth, aspectWidth);

    if (![_validator isPolicySafeVideoSizeWithWidth:width height:height]) {
        return CGSizeZero;
    }
    return CGSizeMake(width, height);
}

- (CGFloat)ran_mediaAspectRatioForVideo:(BOOL)renderVideo {
    if (renderVideo) {
        GADMediaContent *content = _nativeAd.mediaContent;
        if (content != nil && content.aspectRatio > 0) {
            return RANClamp(
                    content.aspectRatio
                  , kRANMinMediaAspectRatio
                  , kRANMaxMediaAspectRatio);
        }
        return kRANUnknownVideoAspectRatio;
    }

    UIImage *image = [_viewFactory findMainImage];
    if (image != nil && image.size.width > 0 && image.size.height > 0) {
        return RANClamp(
                image.size.width / image.size.height
              , kRANMinMediaAspectRatio
              , kRANMaxMediaAspectRatio);
    }
    return kRANDefaultImageAspectRatio;
}

@end