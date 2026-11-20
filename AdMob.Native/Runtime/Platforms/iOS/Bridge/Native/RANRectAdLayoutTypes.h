// Shared vocabulary of the rect ad layout system: templates, tiers, and the
// LayoutPlan record the engine scores and the factory builds from. Values
// mirror RectAdLayoutEngine's nested types exactly.

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>

NS_ASSUME_NONNULL_BEGIN

typedef NS_ENUM(NSInteger, RANRectAdTemplate) {
    RANRectAdTemplateCompactRow = 0
  , RANRectAdTemplateCompactColumn = 1
  , RANRectAdTemplateMediaLeft = 2
  , RANRectAdTemplateMediaTop = 3
  , RANRectAdTemplateMediaBackground = 4
  , RANRectAdTemplateMediaRight = 5
};

static inline BOOL RANRectAdTemplateIsMediaSide(
        RANRectAdTemplate layoutTemplate) {
    return layoutTemplate == RANRectAdTemplateMediaLeft
            || layoutTemplate == RANRectAdTemplateMediaRight;
}

typedef NS_ENUM(NSInteger, RANRectAdTier) {
    RANRectAdTierCompact = 0
  , RANRectAdTierRegular = 1
  , RANRectAdTierRoomy = 2
};

@interface RANRectAdLayoutPlan : NSObject

@property (nonatomic) RANRectAdTemplate layoutTemplate;
@property (nonatomic) RANRectAdTier tier;
@property (nonatomic) BOOL showMedia;
@property (nonatomic) BOOL renderVideo;
@property (nonatomic) BOOL showBody;
@property (nonatomic) BOOL showAdvertiser;
@property (nonatomic) BOOL showRating;
@property (nonatomic) BOOL showIcon;
@property (nonatomic) CGFloat width;
@property (nonatomic) CGFloat height;
@property (nonatomic) CGFloat mediaWidth;
@property (nonatomic) CGFloat mediaHeight;
@property (nonatomic) CGFloat measuredContentHeight;
@property (nonatomic) CGFloat textScale;
// Marquee is decided per text: the headline only ever scrolls after
// scrolling just the secondary lines was not enough.
@property (nonatomic) BOOL marqueeHeadline;
@property (nonatomic) BOOL marqueeSecondary;
// Right margins that keep each text clear of the AdChoices mark, found on the
// probe and replayed on the tree bound for real.
@property (nonatomic) CGFloat headlineMarkDodge;
@property (nonatomic) CGFloat bodyMarkDodge;
@property (nonatomic) CGFloat advertiserMarkDodge;
@property (nonatomic) CGFloat ratingMarkDodge;
@property (nonatomic) CGFloat callToActionMarkDodge;
@property (nonatomic) int64_t score;

@end

NSString *RANRectAdTemplateName(RANRectAdTemplate layoutTemplate);
NSString *RANRectAdTierName(RANRectAdTier tier);
NSString *RANRectAdMediaName(RANRectAdLayoutPlan *plan);
NSString *RANRectAdDescribePlan(RANRectAdLayoutPlan *_Nullable plan);

NS_ASSUME_NONNULL_END