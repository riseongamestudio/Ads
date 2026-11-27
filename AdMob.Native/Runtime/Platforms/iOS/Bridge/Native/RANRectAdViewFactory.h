// Counterpart of com.riseon.ads.admobnative.RectAdViewFactory: builds probe trees for
// the engine's candidate search and the final GADNativeAdView for the plan
// that wins. All sizes are points (1pt here stands where 1dp stood); the
// Java Dp() conversions collapse to identity and only the log lines keep
// reporting pixels through the screen scale.

#import <UIKit/UIKit.h>
#import <GoogleMobileAds/GoogleMobileAds.h>

#import "RANAdTextLabel.h"
#import "RANMeasureLayout.h"
#import "RANRectAdLayoutTypes.h"

NS_ASSUME_NONNULL_BEGIN

@interface RANRectAdAssetViews : NSObject

@property (nonatomic, strong, nullable) RANAdTextLabel *headline;
@property (nonatomic, strong, nullable) RANAdTextLabel *body;
@property (nonatomic, strong, nullable) RANAdTextLabel *advertiser;
@property (nonatomic, strong, nullable) RANAdTextLabel *rating;
@property (nonatomic, strong, nullable) UIImageView *icon;
@property (nonatomic, strong, nullable) UIButton *callToAction;
@property (nonatomic, strong, nullable) UIView *media;
@property (nonatomic, strong, nullable) UILabel *attribution;
@property (nonatomic, strong, nullable) UIView *adChoicesReserve;
@property (nonatomic, strong, nullable) RANLinearLayoutView *outer;
@property (nonatomic, strong, nullable) UIView *mediaSlot;
@property (nonatomic, strong, nullable) UIView *videoRail;
@property (nonatomic, strong, nullable) UIView *scrim;
@property (nonatomic, strong, nullable) UIView *insetContent;
@property (nonatomic) BOOL insetContentAvoidsBadges;
// The picture's frame on a side template: the badge and the AdChoices view
// stand on its top corners, as on the full-screen faces, not on the cell's.
// Nil: they stand on the cell's.
@property (nonatomic, strong, nullable) RANFrameLayoutView *badgeHost;
// Where the badge stands when the scrim's first row reached its corner:
// beside the icon, over the name.
@property (nonatomic) BOOL attributionBesideIcon;
@property (nonatomic) CGPoint attributionOrigin;
// Each text's own right margin, before any mark dodge was added.
@property (nonatomic, strong, nullable)
        NSMapTable<UIView *, NSNumber *> *baseRightMargins;

@end

@interface RANRectAdProbeLayout : NSObject

@property (nonatomic) RANRectAdTemplate layoutTemplate;
@property (nonatomic) RANRectAdTier tier;
@property (nonatomic) BOOL showMedia;
@property (nonatomic) BOOL renderVideo;
@property (nonatomic, strong) UIView *root;
@property (nonatomic, strong) RANRectAdAssetViews *views;

@end

@interface RANRectAdNativeAdViewResult : NSObject

@property (nonatomic, strong) GADNativeAdView *nativeAdView;
@property (nonatomic, strong) RANRectAdAssetViews *assetViews;

@end

@interface RANRectAdViewFactory : NSObject

- (instancetype)initWithNativeAd:(GADNativeAd *)nativeAd
                 slotShortSidePt:(CGFloat)slotShortSidePt
                   roundCornerPt:(CGFloat)roundCornerPt;

- (RANRectAdProbeLayout *)createProbeForPlan:(RANRectAdLayoutPlan *)plan;
- (void)configureProbeLayout:(RANRectAdProbeLayout *)probe
                     forPlan:(RANRectAdLayoutPlan *)plan;
- (void)dodgeAdChoicesMarkInProbe:(RANRectAdProbeLayout *)probe
                             plan:(RANRectAdLayoutPlan *)plan
                            width:(CGFloat)width
                           height:(CGFloat)height;
// Throws (NSException) on bind problems the Java side throws for; the
// presentation catches and walks to the next plan.
- (RANRectAdNativeAdViewResult *)buildNativeAdViewForPlan:(RANRectAdLayoutPlan *)plan
                                               mainImage:(UIImage *_Nullable)mainImage;

- (BOOL)hasVideoContent;
- (BOOL)hasValidStarRating;
- (double)resolveStarRating;
- (BOOL)canRenderMainImage:(UIImage *_Nullable)mainImage;
- (BOOL)hasRenderableIcon;
- (UIImage *_Nullable)findMainImage;

- (CGFloat)minimumWidthForTier:(RANRectAdTier)tier;
// The smallest a registered MediaView may end up on either side.
- (CGFloat)mediaPolicyFloor;
- (CGFloat)mediaSizeForTier:(RANRectAdTier)tier;
- (CGFloat)paddingForTier:(RANRectAdTier)tier;
- (CGFloat)gapForTier:(RANRectAdTier)tier;
- (RANRectAdTier)preferredTierForWidth:(CGFloat)width height:(CGFloat)height;
- (CGFloat)minimumMediaLeftTextSlotWidthForPlan:(RANRectAdLayoutPlan *)plan;
- (CGFloat)callToActionHeightForPlan:(RANRectAdLayoutPlan *)plan;
- (CGFloat)headlineSizeForPlan:(RANRectAdLayoutPlan *)plan;

@end

NS_ASSUME_NONNULL_END