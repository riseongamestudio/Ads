#import "RANOverlayAdContentView.h"
#import "RANAdTheme.h"

#import "RANAdTextLabel.h"
#import "RANNativeAdStarRatingView.h"

static NSString *const kRANTag = @"Overlay";
static NSString *const kRANAttributionText = @"Ad";

// The 120pt floor holds for every creative, a still picture's as well as a
// video's: the SDK flags a smaller MediaView as an implementation problem and
// warns that such slots will be demonetized. A slot too small for it takes a
// layout without the picture.
static const CGFloat kRANMinVideoMediaSize = 120;
static const CGFloat kRANMinImageMediaSize = 120;
// The close mark's geometry - the same two numbers the Android side and the
// Ads placeholders draw with, Google's own: ink across 44% of the disc in
// strokes a tenth of it, so the stroke ends' centres span 34%.
static const CGFloat kRANCloseGlyphSpanRatio = 0.34;
static const CGFloat kRANCloseGlyphStroke = 3;
// A probe value only, for the fit checks that need a number. It never
// frames the media: a creative with an unreported ratio is handed the
// whole band and renders inside it as it pleases.
static const CGFloat kRANDefaultMediaAspectRatio = 1;
static const float kRANDefaultHeightRatio = 0.5f;
// 20pt a side spent 40pt of every screen on nothing the ad needed. Five
// rather than eight because the side layout with content below wears this
// padding three times across - one panel edge, the media's left, the other
// panel edge - and it exists to make the plain side layout prettier. Three
// fives are less than the two eights the plain one used to spend, so the
// nicer arrangement can never be the one that runs out of width first.
static const CGFloat kRANHorizontalPadding = 5;
// The stacked half panel's text and button, in from the screen's edges as a
// page's text is; the picture still runs edge to edge, and the control
// avoidance keeps its walls at kRANHorizontalPadding. The Java side's
// STACKED_TEXT_INSET_DP.
static const CGFloat kRANStackedTextInset = 16;
// Under the half panel's button, off the screen's bottom edge.
static const CGFloat kRANHalfBottomPadding = 8;
// The half panel's controls, in from its sides, and the badge and AdChoices
// mark off its corners where there is no picture to carry them.
static const CGFloat kRANHalfMarkInset = 6;
// The half panel's strip: the 30pt disc with 6pt above and below, so a
// control neither meets the panel's edge nor touches the picture. The Java
// side's HALF_CONTROL_STRIP_DP.
static const CGFloat kRANHalfControlStrip = 42;

// Elapsed time is measured on the clock that only goes forward, never on
// NSDate - the same one the rest of this port keeps. Declared per file
// because that is how the other four carry it; see the note in RANBaseAd.h
// if that ever stops being worth the repetition.
static NSTimeInterval RANNow(void) {
    return [NSProcessInfo processInfo].systemUptime;
}
static const CGFloat kRANControlStripHeight = 30;
static const CGFloat kRANControlGap = 2;
// Clear of the AdChoices mark (18pt, 6pt in from the corner) by the close's
// reach past its disc and a little more.
static const CGFloat kRANRightControlInset = 38;
// A control's disc is 30pt; a press counts this far around it, so it answers
// the 44pt target the platform asks for.
static const CGFloat kRANControlTouchSlop = 7;
// A portrait creative fills the panel's height on the left and everything
// else moves into a rail beside it, provided the media still meets its policy
// minimum and the rail keeps enough width to read. Strictly portrait only:
// a square creative belongs stacked on top - MEDIA_TOP wins there - and only
// media clearly taller than wide earns the rail beside it.
static const CGFloat kRANSideMediaMaxAspect = 0.85f;
// Just over half: enough width for a portrait creative to stay imposing,
// while the rail keeps room for whole text and a real button.
static const CGFloat kRANSideMediaMaxWidthShare = 0.56f;
static const CGFloat kRANSideMediaMinRail = 120;
// The rail's side padding follows the rail's width; a narrow column cannot
// afford the full kRANHorizontalPadding on each side.
static const CGFloat kRANRailSidePaddingRatio = 0.03f;
static const CGFloat kRANRailMinSidePadding = 2;

// The side padding one edge of the rail wears on its own.
static inline CGFloat RANSideRailPadding(CGFloat railOuterWidth) {
    return MAX(
            kRANRailMinSidePadding
          , MIN(
                kRANHorizontalPadding
              , railOuterWidth * kRANRailSidePaddingRatio));
}
static const CGFloat kRANRailIconGap = 4;
// Text no larger than the rail can wear: the scale ceiling follows the
// rail's width, reaching full size only in a genuinely wide rail.
static const CGFloat kRANRailScaleCapMinWidth = 160;
static const CGFloat kRANRailScaleCapRange = 240;
// Whole text before size, size before scrolling: how many size steps the
// rail trades away before a line is allowed to scroll.
static const NSInteger kRANRailFullTextSteps = 3;
static const CGFloat kRANRailFullTextScaleStep = 0.34f;
static const NSInteger kRANRailHeadlineMaxLineCount = 4;
static const NSInteger kRANSideBelowBodyMaxLineCount = 2;
static const CGFloat kRANBodyLineHeightRatio = 1.5f;
// The seam between the media and the identity row below it - a hair,
// not a margin.
static const CGFloat kRANMediaLowerSeam = 8;
// A panel meaningfully taller than wide reads as a page: media belongs
// stacked on top of it, not beside it. Side media only suits panels near
// screen proportions.
static const CGFloat kRANSideMediaMaxPanelHeightRatio = 1.3f;
// The rail is narrow by construction, so the icon never shares a line with
// text there: it stands alone and the identity stack follows below.
static const CGFloat kRANSideRailIconWidthRatio = 0.3f;
// Slack absorption in the rail: how many whole body lines it may take, and
// how far the icon may grow, before free height is left alone.
static const NSInteger kRANRailBodyMaxLineCount = 6;
static const CGFloat kRANRailIconGrowthStep = 8;
static const CGFloat kRANRailIconMaxWidthRatio = 0.6f;
// Extra ground the strip-avoiding layout may give before it surrenders and
// lets the corner controls overlay the media instead.
static const CGFloat kRANAvoidCallToActionHeight = 36;
static const CGFloat kRANAvoidIconSize = 28;
// Ratio first: media needs its minimum plus a usable row of content under it;
// a panel that cannot host that drops the media rather than growing past the
// request, and only a panel under the absolute floor is ever grown.
static const CGFloat kRANMinMediaLowerContent = 88;
static const CGFloat kRANMinPanelHeight = 48;
// Below this the panel is a strip, and the strip is one row.
static const CGFloat kRANTickerMaxPanelHeight = 120;
static const CGFloat kRANTickerCtaMinHeight = 32;
static const CGFloat kRANMinBadgeSize = 15;
static const CGFloat kRANAttributionWidth = 26;
static const CGFloat kRANAttributionHeight = 18;
// The AdChoices view's side: the SDK draws its mark in it, and the Ads
// placeholders and the Editor preview draw theirs at this size.
static const CGFloat kRANAdChoicesSize = 18;
static const CGFloat kRANMinIconSize = 36;
static const CGFloat kRANMaxIconSize = 64;
static const CGFloat kRANIconGap = 8;
// The floor gives ground in a tight panel; the ceiling is what a roomy
// panel is allowed to spend.
static const CGFloat kRANMinCallToActionHeight = 44;
static const CGFloat kRANMaxCallToActionHeight = 56;
static const CGFloat kRANMinIdentityVerticalPadding = 2;
static const CGFloat kRANMaxIdentityVerticalPadding = 6;
static const CGFloat kRANMinBodyBottomPadding = 4;
static const CGFloat kRANMaxBodyBottomPadding = 8;
static const CGFloat kRANMinHeadlineTextSize = 15;
static const CGFloat kRANMaxHeadlineTextSize = 20;
static const CGFloat kRANFullScreenHeadlineTextSize = 18;
static const CGFloat kRANMinAdvertiserTextSize = 12;
static const CGFloat kRANMaxAdvertiserTextSize = 14;
static const CGFloat kRANMinBodyTextSize = 13;
static const CGFloat kRANMaxBodyTextSize = 16;
static const CGFloat kRANMinCallToActionTextSize = 14;
static const CGFloat kRANMaxCallToActionTextSize = 17;
static const NSInteger kRANCollapsibleHeadlineMaxLines = 2;
static const NSInteger kRANMinBodyLineCount = 1;
static const NSInteger kRANMaxBodyLineCount = 3;
static const NSInteger kRANResponsiveScaleSearchIterations = 8;
static const CGFloat kRANMaxIconRowWidthRatio = 0.33f;
// A line that moves is harder to read than one that sits still, so text
// that has to scroll never does it at the size that failed to fit whole.
static const CGFloat kRANMarqueeTextShrink = 0.8f;
static const NSTimeInterval kRANCountdownInterval = 0.25;
// How long a let-through close press waits for the SDK's click report.
// Android's CLOSE_REPORT_TIMEOUT_MS, same value, same meaning.
//
// This is not a countdown to anything. A click report says only that a click
// happened, never where - so when one arrives, the one piece of evidence
// that it came from the close chip rather than from a deliberate tap on the
// ad is that a press went through the chip a moment ago. This is how long
// ago still counts as a moment. Inside it the click is the close button's
// and the ad goes; outside it the click belongs to the ad and the ad stays
// up for the player who meant to follow it. Without the expiry a missed
// press would sit there for the rest of the show and close the ad on a
// call-to-action tap half a minute later.
//
// When it runs out the press is simply forgotten. The SDK did not accept the
// touch, so as far as the ad is concerned nothing was pressed, and a button
// that missed should do nothing - a second of silence followed by the ad
// vanishing by itself reads as a glitch instead. The one exception is the
// net below, which is checked at this same moment because this is the
// instant a press is known to have earned nothing.
static const NSTimeInterval kRANCloseReportTimeout = 1.0;
// A missed press does nothing, which is right until it is every press. The
// dead zone is small enough that landing in it three times running is close
// to impossible - and a player pressing a third time is telling us plainly
// that they want out, whatever the SDK thinks. That press waits its second
// and then closes the ad, so the button can never be a trap.
static const NSInteger kRANClosePressesBeforeGivingUp = 3;
// How far apart two touch-downs have to be to be two presses. UIKit asks
// hitTest: several times over for a single touch-down, and without a
// separation of its own every one of those asks would count against the net
// above. The pending flag used to stand in for it, which made the net ask
// for three SLOW presses instead: a whole second of tapping counted once,
// and the tap-tap-tap anyone does at a button that ignored them was the one
// thing that could not reach three. Nobody taps twice inside a tenth of a
// second; one press's repeat asks all land in the same millisecond. Android
// needs none of this - its listener sees ACTION_DOWN once per press.
static const NSTimeInterval kRANClosePressSeparation = 0.1;
// And how long a REPORTED click then waits for the redirect to actually
// take the screen. Longer than the first, because by this point a click is
// certain and only the browser is late. It ends the wait rather than
// deciding it: whatever opened either arrived or is not coming. Android's
// REDIRECT_TIMEOUT_MS.
static const NSTimeInterval kRANRedirectTimeout = 3.0;

// An icon's corners, as a launcher rounds them.
static const CGFloat kRANIconCornerRatio = 0.22f;

// The full-screen arrangements, ordinals shared with the C# FullScreenLayout.
static const int32_t kRANLayoutAppOpen = 1;
static const int32_t kRANLayoutEndCard = 2;
// Behind media narrower or shorter than its box, a picture's or a video's:
// light, so the letterbox reads as part of the card. Black bars beside a tall
// video merged into a dark panel and cut the card in two.
static const uint32_t kRANCardMediaBackdropColor = 0xFFF1F3F4;
static const CGFloat kRANCardCorner = 20;
static const CGFloat kRANCardMargin = 16;
static const CGFloat kRANCardMaxWidth = 440;
static const CGFloat kRANCardLandscapeMaxWidth = 760;
static const CGFloat kRANCardLandscapeMaxHeight = 420;
// The picture's share of the card's width in landscape, at most, and of the
// card's height in portrait, so the text keeps its room.
static const CGFloat kRANCardLandscapeMediaShare = 0.58f;
static const CGFloat kRANCardPortraitMediaShare = 0.6f;
// The end card's picture, at most: the app is what the page is about.
static const CGFloat kRANEndCardMediaShare = 0.4f;
static const CGFloat kRANCardPadding = 20;
static const CGFloat kRANCardIconSize = 56;
static const CGFloat kRANCardSmallIconSize = 40;
static const CGFloat kRANEndCardIconSize = 88;
static const CGFloat kRANCardCallToActionHeight = 52;
static const CGFloat kRANCardControlInset = 12;
static const CGFloat kRANCardControlTop = 8;
// Where the AdChoices view sits on the card: 6pt in from its corner, where
// the Ads placeholders and the Editor preview draw the mark.
static const CGFloat kRANCardAdChoicesInset = 6;
// The text and the button of a full-screen face, at most, so a tablet's lines
// stay readable while the picture spans the screen.
static const CGFloat kRANBleedTextMaxWidth = 560;
static const NSInteger kRANCardHeadlineMaxLines = 3;
// The card's text follows Dynamic Type up to this, as Android follows its
// font scale: past it the face has nothing left to give.
static const CGFloat kRANCardMaxFontScale = 1.3f;
// The close and the timer are tonal discs: a faint tint of what is behind
// them and a glyph in the theme's secondary ink, so the exit is plain to find
// without outshouting the ad. Over what is dark - a dark theme's surface, or
// the dimmed game around the app open ad's card - the tint turns light.
static const uint32_t kRANCloseDiscColor = 0x14000000;
static const uint32_t kRANCloseGlyphColor = 0xFF444746;
static const uint32_t kRANDarkBackdropDiscColor = 0x24FFFFFF;
static const uint32_t kRANDarkBackdropGlyphColor = 0xFFE3E3E3;
// The app open ad's bar back to the game, as AdMob draws its own: the theme's
// surface, its left end round, running off the screen's right edge.
// The way out, so a full finger target.
static const CGFloat kRANAppBarHeight = 48;
static const CGFloat kRANAppBarGap = 8;
// Between the game's name and Continue to app.
static const CGFloat kRANAppBarNameGap = 24;
static const CGFloat kRANAppBarIconSize = 26;
static NSString *const kRANAppBarContinueText = @"Continue to app";

static UIColor *RANARGB(uint32_t argb) {
    return [UIColor colorWithRed:((argb >> 16) & 0xFF) / 255.0
                           green:((argb >> 8) & 0xFF) / 255.0
                            blue:(argb & 0xFF) / 255.0
                           alpha:((argb >> 24) & 0xFF) / 255.0];
}

// A UILabel with content insets - the attribution badge and the timer and
// close controls all pad their text the way the Android views do.
// The game's own icon, as the home screen shows it: the last of the primary
// icon's files is the largest the bundle carries.
static UIImage *RANApplicationIcon(void) {
    NSDictionary *icons = NSBundle.mainBundle.infoDictionary[@"CFBundleIcons"];
    NSArray *files = icons[@"CFBundlePrimaryIcon"][@"CFBundleIconFiles"];
    NSString *name = files.lastObject;
    return name != nil ? [UIImage imageNamed:name] : nil;
}

// The build runs before the view has a window, so the card is first fitted
// against the key window's insets; the layout pass fits it again in its own.
static UIEdgeInsets RANKeyWindowSafeArea(void) {
    for (UIScene *scene in UIApplication.sharedApplication.connectedScenes) {
        if (![scene isKindOfClass:UIWindowScene.class]) continue;
        for (UIWindow *window in ((UIWindowScene *)scene).windows) {
            if (window.isKeyWindow) return window.safeAreaInsets;
        }
    }
    return UIEdgeInsetsZero;
}

static NSString *RANApplicationName(void) {
    NSDictionary *info = NSBundle.mainBundle.infoDictionary;
    NSString *name = info[@"CFBundleDisplayName"];
    if (name.length == 0) name = info[@"CFBundleName"];
    return name ?: @"";
}

@interface RANPaddedLabel : UILabel
@property (nonatomic) UIEdgeInsets ran_contentInsets;
// VoiceOver's press. A control that has given up being its own touch target
// still has to answer the assistive one - see the close chip under
// RedirectOnClose.
@property (nonatomic, copy) dispatch_block_t ran_onAccessibilityActivate;
@end

@implementation RANPaddedLabel

- (void)drawTextInRect:(CGRect)rect {
    [super drawTextInRect:UIEdgeInsetsInsetRect(rect, self.ran_contentInsets)];
}

- (CGSize)sizeThatFits:(CGSize)size {
    CGSize content = [super sizeThatFits:size];
    UIEdgeInsets insets = self.ran_contentInsets;
    return CGSizeMake(
            content.width + insets.left + insets.right
          , content.height + insets.top + insets.bottom);
}

- (BOOL)accessibilityActivate {
    if (self.ran_onAccessibilityActivate == nil) return NO;

    self.ran_onAccessibilityActivate();
    return YES;
}

@end

// Android's CentreUnderIcon, and the text branch is the whole point of it:
// these labels are laid out at the rail's full width, so moving the VIEW to
// the middle of a space it already fills changes nothing - only the text
// inside it can move. RANAdTextLabel is this pack's TextView, and a plain
// isKindOfClass:UILabel test misses it, which is how the side-media rail
// ended up with a centered icon over left-aligned lines.
// Read from the left, as a listing row.
static void RANAlignStart(UIView *view) {
    if (view == nil) return;

    if ([view isKindOfClass:RANAdTextLabel.class]) {
        ((RANAdTextLabel *)view).textAlignment = NSTextAlignmentLeft;
    } else if ([view isKindOfClass:UILabel.class]) {
        ((UILabel *)view).textAlignment = NSTextAlignmentLeft;
    }
    view.ran_layoutGravity = RANGravityLeft;
}

static void RANCentreUnderIcon(UIView *view) {
    if (view == nil) return;

    if ([view isKindOfClass:RANAdTextLabel.class]) {
        ((RANAdTextLabel *)view).textAlignment = NSTextAlignmentCenter;
    } else if ([view isKindOfClass:UILabel.class]) {
        ((UILabel *)view).textAlignment = NSTextAlignmentCenter;
    }
    view.ran_layoutGravity = RANGravityCenterHorizontal;
}

// Card text at the reader's Dynamic Type size, up to kRANCardMaxFontScale, as
// Android's card follows its font scale.
static UIFont *RANCardFont(CGFloat size, UIFontWeight weight) {
    UIFont *font = [UIFont systemFontOfSize:size weight:weight];
    return [[UIFontMetrics defaultMetrics]
            scaledFontForFont:font
             maximumPointSize:round(size * kRANCardMaxFontScale)];
}


@implementation RANOverlayAdContentView {
    GADNativeAd *_nativeAd;
    int64_t _countDownRemainingMs;
    BOOL _closeOnLeft;
    BOOL _redirectOnClose;
    // Interstitial 0, AppOpen 1, EndCard 2: how the full-screen card is
    // arranged.
    int32_t _layout;
    BOOL _timerOnLeft;
    BOOL _fullScreen;
    int32_t _backgroundColor;
    dispatch_block_t _onClose;
    CGFloat _resolvedPanelHeight;

    GADNativeAdView *_nativeAdView;
    RANLinearLayoutView *_contentColumn;
    GADMediaView *_mediaView;
    RANLinearLayoutView *_identityRow;
    RANLinearLayoutView *_identityText;
    UIImageView *_icon;
    RANAdTextLabel *_headline;
    RANAdTextLabel *_advertiser;
    RANNativeAdStarRatingView *_starRating;
    RANAdTextLabel *_body;
    UIButton *_callToAction;
    // The end card's store figures: the price and the store, beside the
    // rating; and the dimmed, blurred copy of the creative its banner stands
    // on (ran_arrangeEndCardListing, ran_placeEndCardBackdrop).
    RANAdTextLabel *_price;
    RANAdTextLabel *_store;
    RANLinearLayoutView *_endCardFigures;
    UIView *_endCardBackdrop;
    RANPaddedLabel *_attribution;
    GADAdChoicesView *_adChoicesView;
    RANPaddedLabel *_countdown;
    RANPaddedLabel *_close;
    CAShapeLayer *_closeGlyph;

    BOOL _hasDisplayableMedia;
    BOOL _sideMediaLayout;
    // The full-screen card replaces the adaptive pipeline outright: one
    // arrangement, fitted to the screen, nothing negotiated afterwards.
    BOOL _cardLayout;
    BOOL _cardLandscape;
    // The landscape face's height: what it holds, no taller (the fit).
    CGFloat _cardLandscapeFaceHeight;
    // The half panel with its picture stacked over the text: its text and
    // button stand further in (kRANStackedTextInset).
    BOOL _stackedHalf;
    RANLinearLayoutView *_cardInfo;
    // The gaps over and under the full-screen face's picture and pitch.
    NSArray<UIView *> *_cardSpacers;
    UIView *_appBar;
    UIActivityIndicatorView *_appBarSpinner;
    UIView *_appBarReady;
    CAShapeLayer *_appBarChevron;
    UILabel *_appBarName;
    UIImageView *_appBarIcon;
    UILabel *_appBarContinue;
    CGFloat _sideMediaWidthPx;
    // What the media's left edge costs the row: 0 while the picture bleeds,
    // the side padding once something stands under it.
    CGFloat _sideRowLeftInset;
    // What the plain arrangement spends across, and what the three edges of
    // the spilled one share. Kept so the responsive pass can land on the
    // rail's true inner width without re-deriving the split.
    CGFloat _sidePaddingBudget;
    BOOL _mediaAvoidsControlStrip;
    BOOL _mediaAspectReported;
    BOOL _mediaAboveIdentity;
    CGFloat _sideRailHeight;
    BOOL _sideBodySpilled;
    BOOL _chromeTrimmedForMedia;
    BOOL _controlAvoidanceActive;
    BOOL _layoutUnrenderable;
    // The last avoidance pass found no band at the media's floor.
    BOOL _avoidanceFoundNoBand;
    BOOL _scrimLayoutActive;
    // The panel a last-resort layout has to fit inside, kept for the
    // overrun check at the end of the build.
    CGFloat _lastResortPanelHeight;
    // The fit pipeline pinned the icon to pay for the media and certified
    // the fit at that size; the icon-follows-text rule yields to this.
    // Owner's call, 2026-08: the spending decision wins.
    BOOL _iconSpentForFit;
    BOOL _mediaIsVideo;
    UIView *_scrimVeil;
    CGFloat _avoidancePanelHeight;
    RANLinearLayoutView *_sideRail;
    BOOL _iconHero;
    BOOL _tickerLayout;
    CGFloat _minimumMediaSize;
    CGFloat _mediaAspectRatio;
    UIImage *_fallbackMediaImage;
    UIImageView *_fallbackMediaImageView;

    NSTimer *_timer;
    // A close press that has been let through to the ad surface and is
    // waiting for the SDK to report the click it should have produced.
    // Bounded, because a press the SDK never reports must still close.
    NSTimer *_closeReportTimer;
    BOOL _closePressPending;
    // Close presses the SDK never answered. A click arriving for one of them
    // clears the run, so only misses count toward the net.
    NSInteger _unansweredClosePresses;
    // The touch time of the last press counted, so that the repeat asks
    // hitTest: makes for that same touch are not counted again.
    NSTimeInterval _lastClosePressTime;
    // A close press whose click the SDK has taken, waiting for whatever it
    // opened to come to the front. The ad stays up through this: closing on
    // the click alone left a gap where the ad was already gone and the
    // browser had not arrived, and the game showed through it.
    NSTimer *_redirectTimer;
    BOOL _redirectPending;
    BOOL _clickCommitted;
    BOOL _released;
    // One shrink per label, ever - the whole-or-scrolling switch must not
    // compound across layout passes.
    NSMutableSet<NSValue *> *_shrunkTexts;
}

+ (CGFloat)resolveInitialPanelHeightForFullScreen:(BOOL)fullScreen
                                      heightRatio:(float)heightRatio
                                  hasVideoContent:(BOOL)hasVideoContent {
    if (fullScreen) return UIScreen.mainScreen.bounds.size.height;

    return [self resolveHalfScreenPanelHeightForRatio:heightRatio];
}

// The one place a half-screen height is decided, and anything standing behind
// the panel has to take its height from here too - published as
// RANOverlayAd.resolvePanelHeight - rather than measure for itself. A view
// behind the ad once computed its own from the host view's bounds while the ad
// measured UIScreen, which made the two disagree wherever the Unity view is not
// the whole screen, and the surplus showed as an empty band above the ad -
// both sit at the bottom, so only the taller one has anything left over. The
// Android side carries the same pairing.
+ (CGFloat)resolveHalfScreenPanelHeightForRatio:(float)heightRatio {
    CGSize screen = UIScreen.mainScreen.bounds.size;

    float ratio = heightRatio;
    if (isnan(ratio) || isinf(ratio)) {
        NSLog(@"%@: heightRatio is not finite; using 0.5", kRANTag);
        ratio = kRANDefaultHeightRatio;
    }
    ratio = MAX(0.0f, MIN(1.0f, ratio));
    CGFloat requestedHeight = screen.height * ratio;
    requestedHeight = MAX(requestedHeight, kRANMinPanelHeight);
    return MIN(screen.height, requestedHeight);
}

- (instancetype)initWithNativeAd:(GADNativeAd *)nativeAd
            countDownRemainingMs:(int64_t)countDownRemainingMs
                     closeOnLeft:(BOOL)closeOnLeft
                     timerOnLeft:(BOOL)timerOnLeft
                      fullScreen:(BOOL)fullScreen
                 backgroundColor:(int32_t)backgroundColor
                 redirectOnClose:(BOOL)redirectOnClose
            requestedPanelHeight:(CGFloat)requestedPanelHeight
                          layout:(int32_t)layout
                         onClose:(dispatch_block_t)onClose {
    self = [super initWithFrame:CGRectZero];
    if (self == nil) return nil;

    _nativeAd = nativeAd;
    _countDownRemainingMs = MAX(0, countDownRemainingMs);
    _closeOnLeft = closeOnLeft;
    _timerOnLeft = timerOnLeft;
    _fullScreen = fullScreen;
    _backgroundColor = backgroundColor;
    _redirectOnClose = redirectOnClose;
    _layout = layout;
    _onClose = [onClose copy];
    _shrunkTexts = [NSMutableSet set];
    [self ran_buildWithRequestedPanelHeight:requestedPanelHeight];

    [NSNotificationCenter.defaultCenter
            addObserver:self
               selector:@selector(ran_applicationDidBecomeActive)
                   name:UIApplicationDidBecomeActiveNotification
                 object:nil];
    return self;
}

- (void)dealloc {
    [NSNotificationCenter.defaultCenter removeObserver:self];
}

- (CGFloat)resolvedPanelHeight {
    return _resolvedPanelHeight;
}

- (BOOL)layoutUnrenderable {
    return _layoutUnrenderable;
}

- (void)releaseContent {
    if (_released) return;
    _released = YES;

    [_timer invalidate];
    _timer = nil;
    _closePressPending = NO;
    _unansweredClosePresses = 0;
    _lastClosePressTime = 0;
    [_closeReportTimer invalidate];
    _closeReportTimer = nil;
    _redirectPending = NO;
    [_redirectTimer invalidate];
    _redirectTimer = nil;
    _nativeAdView.nativeAd = nil;
    [_nativeAdView removeFromSuperview];
    _nativeAdView = nil;
    for (UIView *child in [self.subviews copy]) [child removeFromSuperview];
    _countdown = nil;
    _close = nil;
}

- (void)onPresented {
    [self onPresentedWithRemainingMs:_countDownRemainingMs];
}

- (void)onPresentedWithRemainingMs:(int64_t)remainingMs {
    if (_released) return;
    _countDownRemainingMs = MAX(0, remainingMs);
    [self ran_startCountdown];
}

// Resigning active while a redirect is pending IS the redirect arriving -
// nothing else takes the screen off an ad the player just sent themselves
// away from. The ad closes underneath it, unseen. The Android side reads the
// same moment off its window losing focus.
//
// The notification center and an incoming call land here too, but only
// matter while _redirectPending, which needs the close button pressed within
// the same breath. Landing in that window by accident would still end in the
// close the player asked for.
- (void)onPaused {
    if (_redirectPending) {
        [self ran_runCloseFromRedirect];
        return;
    }

    [_timer invalidate];
    _timer = nil;
}

- (int64_t)countDownRemainingMs {
    return _countDownRemainingMs;
}

// The SDK has taken a click. Android's CommitAdClick, and it carries the
// same two duties: the latch, always, and the promotion of a waiting close
// press from the report stage to the redirect stage.
- (void)commitAdClick {
    _clickCommitted = YES;

    // Only a click that began on the close chip closes the ad. A genuine tap
    // on the call to action leaves the flag clear, so the player who meant
    // to follow the ad comes back to it still open.
    if (!_closePressPending) return;

    _closePressPending = NO;
    _unansweredClosePresses = 0;
    [_closeReportTimer invalidate];
    _closeReportTimer = nil;
    if (_released) return;

    // Reported, not yet arrived: hand the wait over to whatever takes the
    // screen, with a bound in case nothing ever comes to the front.
    _redirectPending = YES;
    __weak RANOverlayAdContentView *weakSelf = self;
    [_redirectTimer invalidate];
    _redirectTimer =
            [NSTimer scheduledTimerWithTimeInterval:kRANRedirectTimeout
                                            repeats:NO
                                              block:^(NSTimer *timer) {
        [weakSelf ran_runCloseFromRedirect];
    }];
}

// The redirect taking the screen, reported by the SDK itself rather than
// inferred from the app resigning active. It is the more precise signal, and
// the only one that fires at all when the SDK opens its landing page INSIDE
// the app: an SKStoreProductViewController or an SFSafariViewController
// never resigns the app active, so onPaused below is deaf to exactly the
// case that is most common. Android has no counterpart - its windows report
// their own focus and visibility, which is the road it takes instead.
- (void)adWillPresentScreen {
    if (_redirectPending) {
        [self ran_runCloseFromRedirect];
        return;
    }

    // Not a close press, then: a genuine tap on the ad, whose landing page
    // opened inside the app. The ad is still standing but nobody is watching
    // it, and the countdown buys watch time. Android reads this moment off
    // its window going invisible - a signal the in-app case never produces
    // here, so the duty falls to this hook instead.
    [_timer invalidate];
    _timer = nil;
}

// The in-app sheet closing again. Without this the latch would stay shut for
// the rest of the show, because its only other release is the app becoming
// active - which never happened, since it never resigned. This is the iOS
// shape of the window-visibility clear Android had to add for its
// not-focusable half-screen window.
- (void)adDidDismissScreen {
    _clickCommitted = NO;
    if (_released) return;

    // The other half of the pause above, and Android's resume on the window
    // coming back. ran_startCountdown re-anchors on _countDownRemainingMs -
    // which the timer's own ticks left current - and invalidates whatever is
    // running before it starts, so the out-of-app path arriving both here
    // and at didBecomeActive costs nothing.
    [self ran_startCountdown];
}

- (void)ran_applicationDidBecomeActive {
    // The window-focus release of the Android latch: the store sheet or the
    // browser the click opened has gone away.
    _clickCommitted = NO;
}

// Is this touch landing on the close chip? Asked of a POINT rather than of
// the hit-test result, because under RedirectOnClose the chip is deliberately
// not the hit-test result - hitTest: below declines the target so the press
// can reach the ad surface. Hidden is part of the question: the chip stays
// hidden until the countdown ends, and UIKit's own refusal to hit-test a
// hidden view is what used to enforce that.
- (BOOL)ran_isCloseTouchAtPoint:(CGPoint)point withEvent:(UIEvent *)event {
    if (_close == nil || _close.hidden) return NO;
    // hitTest: is also asked questions that are not presses at all - layout
    // and focus bookkeeping ask it with no event whatsoever, and the pointer
    // merely hovering on iPad asks it with one that is not a touch. Arming
    // off either would start the close's clock with no finger behind it, so
    // a real touch event is required rather than merely not contradicted.
    if (event == nil || event.type != UIEventTypeTouches) return NO;

    return CGRectContainsPoint(
            CGRectInset(_close.frame, -kRANControlTouchSlop, -kRANControlTouchSlop)
          , point);
}

// The latch: after a committed click every touch inside the ad is consumed
// by this view instead of any child, so the SDK cannot register another.
// The close chip is the one exception on either road - Android gets that for
// free by keeping its latch on a container wrapping the ad view alone, which
// leaves the close a sibling outside it.
- (UIView *)hitTest:(CGPoint)point withEvent:(UIEvent *)event {
    UIView *hit = [super hitTest:point withEvent:event];

    if (![self ran_isCloseTouchAtPoint:point withEvent:event]) {
        if (_clickCommitted && hit != nil) return self;
        return hit;
    }

    // Plain close: the chip is its own target and its tap recogniser closes,
    // a press just around it included.
    if (!_redirectOnClose) return _close;

    // Let-through close. The arming happens HERE, at touch-down, because
    // this is the only moment a view can decline to be the target and let
    // the press continue to the ad surface beneath it. A gesture recogniser
    // would fire on touch-UP and would consume the touch, and the SDK counts
    // touches, not calls. hitTest: is asked more than once per press, so the
    // touch's own time goes with it and the repeats fall away there.
    [self ran_armCloseFromPressAtTime:event.timestamp];
    // The ad surface is deaf while the latch is shut, so let nothing reach
    // it - one press, one click. The press is armed all the same, and its
    // report bound closes the ad when no second click ever arrives.
    if (_clickCommitted) return self;

    return hit;
}

// ---------------------------------------------------------------------------
// Build
// ---------------------------------------------------------------------------

- (void)ran_buildWithRequestedPanelHeight:(CGFloat)requestedPanelHeight {
    GADMediaContent *mediaContent = _nativeAd.mediaContent;
    BOOL hasVideoContent = mediaContent.hasVideoContent;
    UIImage *mainMediaImage = mediaContent.mainImage;
    _fallbackMediaImage = (!hasVideoContent && mainMediaImage == nil)
            ? [self ran_findFallbackMediaImage]
            : nil;
    _hasDisplayableMedia = hasVideoContent
            || mainMediaImage != nil
            || _fallbackMediaImage != nil;
    _minimumMediaSize = hasVideoContent
            ? kRANMinVideoMediaSize
            : kRANMinImageMediaSize;
    _mediaIsVideo = hasVideoContent;
    _mediaAspectRatio = [self ran_mediaAspectRatio];
    CGSize hbScreen = UIScreen.mainScreen.bounds.size;
    CGFloat sidePanelHeight = _fullScreen
            ? hbScreen.height
            : requestedPanelHeight;
    _tickerLayout = !_fullScreen
            && requestedPanelHeight < kRANTickerMaxPanelHeight;
    BOOL panelHostsMedia = _fullScreen
            || requestedPanelHeight
                    >= _minimumMediaSize + kRANMinMediaLowerContent;
    // With the media dropped - or a creative that never had any - the
    // registered icon stands in for it, the way the rect ad works.
    _iconHero = !_fullScreen
            && !_tickerLayout
            && (!_hasDisplayableMedia || !panelHostsMedia)
            && _nativeAd.icon.image != nil;
    _hasDisplayableMedia = _hasDisplayableMedia
            && panelHostsMedia
            && !_tickerLayout;
    // A video has no way down to the icon or the ticker: Google serves it
    // only into a MediaView of 120pt or more and flags it without one. A
    // panel that cannot hold one drops the creative.
    if (hasVideoContent && !_hasDisplayableMedia) {
        _layoutUnrenderable = YES;
    }
    // A picture directly above the identity row is its own separator; the
    // row's top padding would only widen a seam already there.
    _mediaAboveIdentity = _hasDisplayableMedia;
    _cardLayout = _fullScreen;
    _sideMediaLayout = !_cardLayout
            && _hasDisplayableMedia
            && [self ran_shouldUseSideMediaForPanelHeight:sidePanelHeight];

    // One line per build: every layout decision and its inputs, so a
    // screenshot of a wrong layout always arrives with its numbers.

    [self ran_configureBackground];

    _nativeAdView = [[GADNativeAdView alloc] init];
    _contentColumn = [[RANLinearLayoutView alloc] init];
    _contentColumn.ran_vertical = YES;
    _contentColumn.ran_gravity =
            RANGravityCenterVertical | RANGravityCenterHorizontal;
    _stackedHalf = !_cardLayout && !_sideMediaLayout && !_tickerLayout;
    CGFloat columnPadding = [self ran_columnPadding];
    _contentColumn.ran_padding = UIEdgeInsetsMake(
            [self ran_controlStripHeight]
          , columnPadding
          , _stackedHalf ? kRANHalfBottomPadding : 0
          , columnPadding);

    _mediaView = [[GADMediaView alloc] init];
    // Deliberately black: when a creative reports one ratio but renders
    // less inside it, the black ground makes the shortfall visible.
    _mediaView.backgroundColor = UIColor.blackColor;
    _mediaView.contentMode = UIViewContentModeScaleAspectFit;
    _mediaView.ran_minimumSize =
            CGSizeMake(_minimumMediaSize, _minimumMediaSize);
    _mediaView.ran_layoutWidth = RANLayoutMatchParent;
    _mediaView.ran_layoutHeight = _minimumMediaSize;
    // The media is the one child allowed to ignore the side padding: it
    // bleeds edge to edge through negative margins while every text stays
    // inset.
    _mediaView.ran_layoutMargins = UIEdgeInsetsMake(
            0, -columnPadding, 0, -columnPadding);
    if (hasVideoContent || mainMediaImage != nil) {
        _mediaView.mediaContent = mediaContent;
    } else if (_fallbackMediaImage != nil) {
        _fallbackMediaImageView =
                [[UIImageView alloc] initWithImage:_fallbackMediaImage];
        _fallbackMediaImageView.contentMode = UIViewContentModeScaleAspectFit;
        [_mediaView addSubview:_fallbackMediaImageView];
    }

    _identityRow = [[RANLinearLayoutView alloc] init];
    _identityRow.ran_vertical = NO;
    _identityRow.ran_gravity = RANGravityCenterVertical;
    _identityRow.ran_padding = UIEdgeInsetsMake(
            _fullScreen
                    ? kRANMaxIdentityVerticalPadding
                    : kRANMinIdentityVerticalPadding
          , 0
          , kRANMinIdentityVerticalPadding
          , 0);
    _identityRow.ran_layoutWidth = RANLayoutMatchParent;

    _icon = [[UIImageView alloc] init];
    _icon.contentMode = UIViewContentModeScaleAspectFill;
    _icon.clipsToBounds = YES;
    _icon.ran_layoutWidth = kRANMinIconSize;
    _icon.ran_layoutHeight = kRANMinIconSize;
    _icon.ran_layoutMargins = UIEdgeInsetsMake(0, 0, 0, kRANIconGap);
    if (_iconHero) {
        _icon.contentMode = UIViewContentModeScaleAspectFit;
        _icon.clipsToBounds = NO;
    } else if (!_sideMediaLayout) {
        [_identityRow addSubview:_icon];
    }

    _identityText = [[RANLinearLayoutView alloc] init];
    _identityText.ran_vertical = YES;
    _identityText.ran_gravity = RANGravityCenterVertical;
    _identityText.ran_layoutWidth = 0;
    _identityText.ran_layoutWeight = 1;

    _headline = [[RANAdTextLabel alloc] init];
    _headline.font = [UIFont boldSystemFontOfSize:
            _fullScreen
                    ? kRANFullScreenHeadlineTextSize
                    : kRANMinHeadlineTextSize];
    _headline.textColor = UIColor.whiteColor;
    _headline.maxLines = NSIntegerMax;
    _headline.ran_layoutWidth = RANLayoutMatchParent;

    _advertiser = [[RANAdTextLabel alloc] init];
    _advertiser.font = [UIFont systemFontOfSize:kRANMinAdvertiserTextSize];
    _advertiser.textColor = [UIColor colorWithWhite:1 alpha:0.8];
    // The single line that does not fit scrolls rather than being cut.
    _advertiser.marquee = YES;
    _advertiser.ran_layoutWidth = RANLayoutMatchParent;

    _starRating = [[RANNativeAdStarRatingView alloc] init];
    _starRating.ran_layoutGravity = RANGravityLeft;

    [_identityText addSubview:_headline];
    [_identityText addSubview:_advertiser];
    [_identityText addSubview:_starRating];
    [_identityRow addSubview:_identityText];

    if (_layout == kRANLayoutEndCard) {
        _price = [[RANAdTextLabel alloc] init];
        _store = [[RANAdTextLabel alloc] init];
        // In the ink, a step over the body.
        for (RANAdTextLabel *figure in @[ _price, _store ]) {
            figure.textColor = RANARGB(RANThemeInk());
            figure.font = RANCardFont(15, UIFontWeightMedium);
            figure.maxLines = 1;
            figure.ellipsizes = YES;
            figure.textAlignment = NSTextAlignmentCenter;
            figure.ran_layoutWidth = RANLayoutMatchParent;
        }
    }

    _body = [[RANAdTextLabel alloc] init];
    _body.font = [UIFont systemFontOfSize:kRANMinBodyTextSize];
    _body.textColor = [UIColor colorWithWhite:1 alpha:0.8];
    _body.maxLines = kRANMaxBodyLineCount;
    _body.ran_layoutWidth = RANLayoutMatchParent;
    _body.ran_padding = UIEdgeInsetsMake(0, 0, kRANMinBodyBottomPadding, 0);
    [self ran_colorTextOnSurface:YES];

    _callToAction = [UIButton buttonWithType:UIButtonTypeCustom];
    [self ran_styleCallToAction:_callToAction];
    _callToAction.titleLabel.font =
            [UIFont boldSystemFontOfSize:kRANMinCallToActionTextSize];
    _callToAction.titleLabel.numberOfLines = 0;
    _callToAction.ran_minimumSize = CGSizeMake(0, kRANMinCallToActionHeight);
    _callToAction.ran_layoutWidth = RANLayoutMatchParent;

    if (_cardLayout) {
        [self ran_arrangeCard];
    } else if (_sideMediaLayout) {
        CGFloat sideMediaWidth =
                [self ran_sideMediaWidthForPanelHeight:sidePanelHeight];
        // The media column is exactly as tall as the creative can fill at
        // its own aspect, centered - never a band of dead backfill painted
        // to the panel's height.
        CGFloat sideMediaHeight =
                [self ran_sideMediaHeightForWidth:sideMediaWidth
                                     panelHeight:sidePanelHeight];
        // Height the media leaves unused belongs to the content, not to
        // the column beside the picture: what fits under the row moves
        // there and spans the whole panel instead of being squeezed into a
        // narrow rail. The button goes first, the body follows only when
        // the leftover comfortably holds both.
        CGFloat seam = kRANMediaLowerSeam;
        CGFloat belowCallToActionHeight = kRANAvoidCallToActionHeight;
        CGFloat bodyLineHeight =
                kRANMinBodyTextSize * kRANBodyLineHeightRatio;
        BOOL callToActionBelow = sidePanelHeight - sideMediaHeight
                >= belowCallToActionHeight + seam;
        CGFloat sideLeftover = sidePanelHeight - sideMediaHeight;
        BOOL bodyBelow = callToActionBelow
                && sideLeftover >= belowCallToActionHeight
                        + 2 * bodyLineHeight
                        + 2 * seam;
        _sideMediaWidthPx = sideMediaWidth;

        // Media bleeds to the edge only while nothing stands under it. The
        // moment something spills below, the two read as one column, and a
        // column with two different left edges reads as a mistake.
        //
        // That left edge is CARVED OUT of the budget, never added on top of
        // it. The budget is what the plain arrangement spends across: the
        // gap between picture and rail, plus the panel's right edge. Two
        // shares there, three here - the same pie either way - so the rail
        // measures the same and a creative that fitted plainly cannot fail
        // to fit here. Points divide evenly, so unlike the Android side
        // there is never a spare pixel to hand out.
        CGFloat railOuterPlain = MAX(
                0
              , UIScreen.mainScreen.bounds.size.width - sideMediaWidth);
        CGFloat sidePaddingBudget = 2 * RANSideRailPadding(railOuterPlain);
        _sidePaddingBudget = sidePaddingBudget;
        CGFloat railGap;
        CGFloat sideRowRightPadding;
        if (callToActionBelow) {
            _sideRowLeftInset = sidePaddingBudget / 3;
            sideRowRightPadding = _sideRowLeftInset;
            railGap = _sideRowLeftInset;
        } else {
            _sideRowLeftInset = 0;
            railGap = sidePaddingBudget / 2;
            sideRowRightPadding = railGap;
        }

        _contentColumn.ran_padding = UIEdgeInsetsZero;
        RANLinearLayoutView *sideRow = [[RANLinearLayoutView alloc] init];
        sideRow.ran_vertical = NO;
        sideRow.ran_padding = UIEdgeInsetsMake(0, _sideRowLeftInset, 0, 0);
        _mediaView.ran_layoutWidth = sideMediaWidth;
        _mediaView.ran_layoutHeight = sideMediaHeight;
        _mediaView.ran_layoutGravity = RANGravityCenterVertical;
        _mediaView.ran_layoutMargins = UIEdgeInsetsZero;
        [sideRow addSubview:_mediaView];

        RANLinearLayoutView *rail = [[RANLinearLayoutView alloc] init];
        rail.ran_vertical = YES;
        rail.ran_gravity = RANGravityCenterVertical;
        // The corner controls and AdChoices sit over the rail's top, so
        // only the rail keeps the strip inset; the media needs none. Its two
        // side paddings are the shares taken above: the left one is the seam
        // beside the picture, the right one the panel's edge.
        CGFloat railOuterWidth = MAX(
                0
              , UIScreen.mainScreen.bounds.size.width
                        - sideMediaWidth
                        - _sideRowLeftInset);
        rail.ran_padding = UIEdgeInsetsMake(
                [self ran_controlStripHeight]
              , railGap
              , 0
              , sideRowRightPadding);
        // The rail is narrow, so the icon never shares a line with text
        // here: it stands alone and the identity stack follows below at the
        // rail's full width.
        if (!_iconHero) {
            CGFloat railWidth = MAX(
                    0, railOuterWidth - railGap - sideRowRightPadding);
            CGFloat railIconSize = MAX(
                    kRANMinIconSize
                  , MIN(
                        kRANMaxIconSize
                      , round(railWidth * kRANSideRailIconWidthRatio)));
            _icon.ran_layoutWidth = railIconSize;
            _icon.ran_layoutHeight = railIconSize;
            _icon.ran_layoutGravity = RANGravityCenterHorizontal;
            _icon.ran_layoutMargins =
                    UIEdgeInsetsMake(0, 0, kRANRailIconGap, 0);
            [rail addSubview:_icon];
        }

        [rail addSubview:_identityRow];

        if (!bodyBelow) [rail addSubview:_body];
        if (!callToActionBelow) [rail addSubview:_callToAction];
        rail.ran_layoutWidth = 0;
        rail.ran_layoutHeight = RANLayoutMatchParent;
        rail.ran_layoutWeight = 1;
        [sideRow addSubview:rail];
        sideRow.ran_layoutWidth = RANLayoutMatchParent;
        // With content spilling below, the row hugs the media instead of
        // claiming the whole panel.
        _sideRailHeight = callToActionBelow
                ? sideMediaHeight
                : sidePanelHeight;
        sideRow.ran_layoutHeight = _sideRailHeight;
        [_contentColumn addSubview:sideRow];
        _sideBodySpilled = bodyBelow;
        if (bodyBelow) {
            _body.maxLines = kRANSideBelowBodyMaxLineCount;
            _body.ran_layoutWidth = RANLayoutMatchParent;
            _body.ran_layoutMargins = UIEdgeInsetsMake(
                    seam, _sideRowLeftInset, 0, sideRowRightPadding);
            [_contentColumn addSubview:_body];
        }
        if (callToActionBelow) {
            _callToAction.ran_layoutWidth = RANLayoutMatchParent;
            _callToAction.ran_layoutMargins = UIEdgeInsetsMake(
                    seam, _sideRowLeftInset, 0, sideRowRightPadding);
            [_contentColumn addSubview:_callToAction];
        }
        _sideRail = rail;
    } else if (_tickerLayout) {
        _contentColumn.ran_padding = UIEdgeInsetsZero;
        _body.hidden = YES;
        _advertiser.hidden = YES;
        _starRating.hidden = YES;
        _headline.maxLines = 1;
        _callToAction.ran_minimumSize =
                CGSizeMake(0, kRANTickerCtaMinHeight);
        _callToAction.ran_layoutWidth = RANLayoutWrapContent;
        RANLinearLayoutView *ticker = [[RANLinearLayoutView alloc] init];
        ticker.ran_vertical = NO;
        ticker.ran_gravity = RANGravityCenterVertical;
        CGFloat tickerControlReserve =
                kRANControlStripHeight + kRANRightControlInset;
        ticker.ran_padding = UIEdgeInsetsMake(
                0, tickerControlReserve, 0, tickerControlReserve);
        _identityRow.ran_padding = UIEdgeInsetsZero;
        _identityRow.ran_layoutWidth = 0;
        _identityRow.ran_layoutWeight = 1;
        [ticker addSubview:_identityRow];
        [ticker addSubview:_callToAction];
        ticker.ran_layoutWidth = RANLayoutMatchParent;
        [_contentColumn addSubview:ticker];
    } else {
        if (_hasDisplayableMedia) {
            [_contentColumn addSubview:_mediaView];
        } else if (_iconHero) {
            _icon.ran_layoutWidth = RANLayoutMatchParent;
            _icon.ran_layoutHeight = 0;
            _icon.ran_layoutWeight = 1;
            _icon.ran_layoutMargins = UIEdgeInsetsZero;
            [_contentColumn addSubview:_icon];
        }
        [_contentColumn addSubview:_identityRow];
        [_contentColumn addSubview:_body];
        [_contentColumn addSubview:_callToAction];
    }
    [_nativeAdView addSubview:_contentColumn];

    _attribution = [self ran_createAttributionLabel];
    [_nativeAdView addSubview:_attribution];

    // The SDK's AdChoices mark goes where the template puts this view, the
    // way GADNativeAdView's adChoicesView is meant to be used, rather than at
    // a corner of the ad view the SDK picks.
    _adChoicesView = [[GADAdChoicesView alloc] init];
    [_nativeAdView addSubview:_adChoicesView];
    _nativeAdView.adChoicesView = _adChoicesView;

    _nativeAdView.iconView = _icon;
    _nativeAdView.headlineView = _headline;
    _nativeAdView.advertiserView = _advertiser;
    _nativeAdView.starRatingView = _starRating;
    if (_price != nil) _nativeAdView.priceView = _price;
    if (_store != nil) _nativeAdView.storeView = _store;
    _nativeAdView.bodyView = _body;
    _nativeAdView.callToActionView = _callToAction;
    [self ran_bindAssets];
    if (_cardLayout) {
        // The theme's ink and grey on the card.
        [self ran_colorTextOnSurface:YES];
        // No unrenderable verdict: full screen has no such station on
        // Android, and the card keeps its media floor when it cannot fit.
        [self ran_fitCardInSize:UIScreen.mainScreen.bounds.size
                       safeArea:RANKeyWindowSafeArea()];
    }
    // One anchor per block: where the icon stands alone above the text -
    // the rail beside a side media, or the icon standing in for a missing
    // picture - the text centers under it. A centered icon over left-aligned
    // lines reads as two blocks that never agreed. Text that spilled below
    // the row belongs to the full-width block and keeps its left edge.
    if (_sideMediaLayout || _iconHero) {
        RANCentreUnderIcon(_headline);
        RANCentreUnderIcon(_advertiser);
        RANCentreUnderIcon(_starRating);
        if (!_sideBodySpilled) RANCentreUnderIcon(_body);
    }
    if (!_fullScreen && !_tickerLayout) {
        if (_sideMediaLayout) {
            [self ran_configureResponsiveSideRailWithPanelHeight:
                    _sideRailHeight > 0
                            ? _sideRailHeight
                            : requestedPanelHeight];
        } else {
            [self ran_configureResponsiveCollapsibleContentWithPanelHeight:
                    requestedPanelHeight];
        }
    }
    if (_fullScreen && !_cardLayout
            && _hasDisplayableMedia && !_sideMediaLayout) {
        // The stack hugs the bottom and the media grows toward the corner
        // controls; the exact fit runs in the layout pass, where the true
        // frame is known.
        [self ran_enableControlAvoidanceWithPanelHeight:0];
    }
    // Registered here, after the template is decided.
    if (_hasDisplayableMedia) {
        _nativeAdView.mediaView = _mediaView;
    }
    _nativeAdView.nativeAd = _nativeAd;
    [self addSubview:_nativeAdView];

    _countdown = [self ran_createControlLabelWithText:
                    [NSString stringWithFormat:@"%lld"
                          , (long long)((_countDownRemainingMs + 999) / 1000)]
                                            textSize:15];
    _countdown.font = [UIFont boldSystemFontOfSize:15];
    _close = [self ran_createControlLabelWithText:@""
                                        textSize:20];
    // Drawn, not typed - see the Android CloseGlyphDrawable. The path is
    // laid each time the control's frame is, in ran_layoutCloseGlyph.
    _closeGlyph = [CAShapeLayer layer];
    _closeGlyph.fillColor = UIColor.clearColor.CGColor;
    _closeGlyph.lineWidth = kRANCloseGlyphStroke;
    _closeGlyph.lineCap = kCALineCapRound;
    [_close.layer addSublayer:_closeGlyph];
    _close.hidden = YES;
    _close.isAccessibilityElement = YES;
    _close.accessibilityLabel = @"Close ad";
    // Under RedirectOnClose the chip must NOT be the touch target: the press
    // belongs to the ad surface underneath it, and a view that is its own
    // target is a view the press stops at. It therefore carries neither
    // interactivity nor a recogniser, and hitTest: both arms the press and
    // steps aside for it.
    _close.userInteractionEnabled = !_redirectOnClose;
    if (_redirectOnClose) {
        // VoiceOver cannot press through anything: it activates an element,
        // it does not put a finger somewhere. Give it the plain close, or it
        // is handed an ad it can never dismiss.
        __weak RANOverlayAdContentView *weakSelf = self;
        _close.ran_onAccessibilityActivate = ^{
            RANOverlayAdContentView *strongSelf = weakSelf;
            if (strongSelf == nil || strongSelf->_released) return;
            if (strongSelf->_onClose != nil) strongSelf->_onClose();
        };
    } else {
        [_close addGestureRecognizer:[[UITapGestureRecognizer alloc]
                initWithTarget:self
                        action:@selector(ran_closeTapped)]];
    }
    [self ran_styleControlsOnEndCardBackdrop:NO];
    [self addSubview:_countdown];
    [self addSubview:_close];

    if (_cardLayout) {
        _resolvedPanelHeight = UIScreen.mainScreen.bounds.size.height;
        if (_layout == kRANLayoutAppOpen && !_redirectOnClose) {
            [self ran_buildAppBar];
        }
        return;
    }
    [self ran_resolveContentHeightWithRequestedPanelHeight:requestedPanelHeight];

    // The last resort freed the text from sharing the panel with the
    // picture, but not from the panel itself. The chrome is at its floors
    // by now, so a column that still overruns the panel would clip its
    // top row off screen - a headline half-shown is not shown. The owner
    // reads this verdict at prepare time and drops the creative.
    if (_scrimLayoutActive && _lastResortPanelHeight > 0) {
        [_contentColumn ran_measureWithWidthSpec:
                        RANMeasureSpecMake(
                                RANMeasureSpecExactly
                              , UIScreen.mainScreen.bounds.size.width)
                                     heightSpec:
                        RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
        if (_contentColumn.ran_measuredSize.height
                > _lastResortPanelHeight) {
            _layoutUnrenderable = YES;
        }
    }
}

// Android lets the close press fall through to the NativeAdView, which is
// the SDK's clickable surface across its whole area. iOS has no such
// surface: GADNativeAdView tracks clicks per REGISTERED ASSET VIEW, and the
// close chip sits in the band the content column reserves as padding for the
// control strip - over nothing registered at all in every layout but the
// scrim one. A press let through there reaches a plain container and the SDK
// counts nothing.
//
// So the surface is made rather than found: a view the size of the chip,
// registered on the one asset slot this face never uses. The press lands on
// a genuine registered asset view, and Google does its own accounting and
// its own redirect off it - nothing here fakes either. The road is built
// because there was none, which is the reverse of what the Android comment
// beside this feature used to claim about iOS.
//
// The slot is matched to an asset the creative actually carries: a slot for
// an absent asset is the one the SDK has least reason to track. Registration
// must happen BEFORE the ad is handed to the view - the tracker is built at
// that moment and never looks again.
// The plain close: no redirect wanted, the chip is its own touch target, and
// this is its tap. The let-through road never arrives here.
- (void)ran_closeTapped {
    if (_onClose != nil) _onClose();
}

// A press has been let through to the ad surface. Nothing is faked and
// nothing is asked of the SDK: the finger itself reached a registered asset
// view, and Google does the rest. All this does is start waiting for the
// report.
//
// Android arms from an OnTouchListener returning false on ACTION_DOWN; this
// is the same moment, read off the one UIKit hook that runs at touch-down
// and is allowed to decline the target.
//
// The time is the touch's own, not the clock's, because the only thing being
// told apart here is one finger from two.
- (void)ran_armCloseFromPressAtTime:(NSTimeInterval)touchTime {
    // _redirectPending: a second press while the redirect is on its way must
    // not restart the memory and hand the redirect's own click to a later
    // press. The press is ignored; the redirect, or its own bound, still
    // ends the show.
    if (_released || _redirectPending) return;
    // The same touch-down asking again, which is not a second press. Only
    // this is turned away - a real second press re-arms over the first,
    // counts, and starts its own wait, so pressing again quickly is what it
    // looks like rather than a second of nothing.
    if (_lastClosePressTime > 0
            && touchTime - _lastClosePressTime < kRANClosePressSeparation) {
        return;
    }

    // Below the net the newest press owns the claim and the wait runs from
    // it, so the press that trips the net gets its own full second to earn a
    // click - anchoring the wait to the FIRST press of the run would leave
    // the third with whatever was left of it, often less than the SDK needs,
    // and close the ad bare on a press that was about to work.
    //
    // Once the net has tripped, the deadline it set is a promise and stands.
    // A later press still counts, and can still earn a click - that click
    // cancels the deadline and closes on the redirect instead, the better
    // ending - but it cannot PUSH the deadline away, or someone hammering a
    // button that keeps ignoring them would never reach the close they are
    // asking for.
    BOOL netAlreadyTripped = _closePressPending
            && _unansweredClosePresses >= kRANClosePressesBeforeGivingUp;

    _closePressPending = YES;
    _lastClosePressTime = touchTime;
    _unansweredClosePresses++;
    if (netAlreadyTripped) return;

    __weak RANOverlayAdContentView *weakSelf = self;
    [_closeReportTimer invalidate];
    _closeReportTimer =
            [NSTimer scheduledTimerWithTimeInterval:kRANCloseReportTimeout
                                            repeats:NO
                                              block:^(NSTimer *timer) {
        [weakSelf ran_forgetClosePress];
    }];
}

// The press was let through and no click was ever reported for it - the chip
// was over a part of the ad the SDK does not treat as clickable, or the latch
// was shut. The press was never reported as a click, so it was never a press
// as far as the ad is concerned. Nothing closes: the chip is only
// decoration, and a decoration that was missed does nothing. All that ends
// here is this press's claim on the next click to arrive - unless the net
// has run out of patience.
- (void)ran_forgetClosePress {
    if (!_closePressPending) return;

    _closePressPending = NO;
    [_closeReportTimer invalidate];
    _closeReportTimer = nil;

    // Under the net, the press simply never happened.
    if (_unansweredClosePresses < kRANClosePressesBeforeGivingUp) return;
    if (_released) return;

    if (_onClose != nil) _onClose();
}

// The click was taken but nothing ever came to the front - a slow network,
// or something the SDK opened inside this app. The close button still has to
// close.
- (void)ran_runCloseFromRedirect {
    if (!_redirectPending) return;


    _redirectPending = NO;
    [_redirectTimer invalidate];
    _redirectTimer = nil;
    if (_released) return;

    if (_onClose != nil) _onClose();
}

// The caller names the color outright, alpha included. Nothing is derived
// from the mode any more: a half-screen panel and a full-screen one both
// wear exactly what they were handed, which is why two of them in a row no
// longer flash a different shade between shows.
- (void)ran_configureBackground {
    self.backgroundColor = RANARGB((uint32_t)_backgroundColor);
}

- (RANPaddedLabel *)ran_createAttributionLabel {
    RANPaddedLabel *attribution = [[RANPaddedLabel alloc] init];
    attribution.text = kRANAttributionText;
    attribution.textColor = RANARGB(kRANThemeAttributionText);
    // One size on every face and every device: a mark, not text that grows
    // with the reader's type size.
    attribution.font = [UIFont boldSystemFontOfSize:11];
    attribution.textAlignment = NSTextAlignmentCenter;
    attribution.backgroundColor = RANARGB(kRANThemeAttribution);
    RANShapeBadge(attribution, NO);
    attribution.ran_contentInsets = UIEdgeInsetsMake(0, 6, 0, 6);
    attribution.ran_minimumSize = CGSizeMake(
            MAX(kRANAttributionWidth, kRANMinBadgeSize)
          , MAX(kRANAttributionHeight, kRANMinBadgeSize));
    attribution.userInteractionEnabled = NO;
    return attribution;
}

// The control's colors follow the backdrop, so the caller sets them.
- (RANPaddedLabel *)ran_createControlLabelWithText:(NSString *)text
                                        textSize:(CGFloat)textSize {
    RANPaddedLabel *control = [[RANPaddedLabel alloc] init];
    control.text = text;
    control.font = [UIFont systemFontOfSize:textSize];
    control.textAlignment = NSTextAlignmentCenter;
    // A disc: every control is the strip's height square.
    control.layer.cornerRadius = kRANControlStripHeight / 2;
    control.layer.masksToBounds = YES;
    return control;
}

// A filled pill rather than the platform default; its radius follows its
// height in the layout pass, so the ends stay round however the fit sizes it.
- (void)ran_styleCallToAction:(UIButton *)callToAction {
    callToAction.backgroundColor = RANARGB(RANThemePrimary());
    callToAction.layer.masksToBounds = YES;
    [callToAction setTitleColor:UIColor.whiteColor
                       forState:UIControlStateNormal];
    callToAction.titleLabel.textAlignment = NSTextAlignmentCenter;
    // The native ad view under it takes the touch and reports the click, as
    // Google's native ad guide asks of the call to action.
    callToAction.userInteractionEnabled = NO;
}

- (void)ran_bindAssets {
    // A headline the creative never sent is not a blank line to reserve:
    // an absent asset leaves no trace, exactly as the advertiser, body and
    // rating already do.
    NSString *headlineValue = [_nativeAd.headline
            stringByTrimmingCharactersInSet:
                    NSCharacterSet.whitespaceAndNewlineCharacterSet];
    if (headlineValue.length == 0) {
        _headline.ran_gone = YES;
        _headline.hidden = YES;
    } else {
        _headline.text = _nativeAd.headline;
    }

    UIImage *iconImage = _nativeAd.icon.image;
    if (iconImage == nil) {
        _icon.ran_gone = YES;
        _icon.hidden = YES;
    } else {
        _icon.image = iconImage;
    }

    NSString *advertiserValue = _nativeAd.advertiser;
    if (advertiserValue.length == 0) {
        _advertiser.ran_gone = YES;
        _advertiser.hidden = YES;
    } else {
        _advertiser.text = advertiserValue;
    }

    NSDecimalNumber *starRatingValue = _nativeAd.starRating;
    double stars = starRatingValue.doubleValue;
    if (starRatingValue == nil || stars <= 0 || isnan(stars) || isinf(stars)) {
        _starRating.ran_gone = YES;
        _starRating.hidden = YES;
    } else {
        _starRating.rating = MAX(0, MIN(5, stars));
    }

    NSString *bodyValue = _nativeAd.body;
    if (bodyValue.length == 0) {
        _body.ran_gone = YES;
        _body.hidden = YES;
    } else {
        _body.text = bodyValue;
    }

    for (RANAdTextLabel *figure in @[ _price ?: (id)NSNull.null, _store ?: (id)NSNull.null ]) {
        if (figure == (id)NSNull.null) continue;

        NSString *value = figure == _price ? _nativeAd.price : _nativeAd.store;
        figure.text = value;
        figure.ran_gone = value.length == 0;
        figure.hidden = value.length == 0;
    }

    NSString *callToActionValue = _nativeAd.callToAction;
    if (callToActionValue.length == 0) {
        _callToAction.ran_gone = YES;
        _callToAction.hidden = YES;
    } else {
        [_callToAction setTitle:callToActionValue
                       forState:UIControlStateNormal];
    }
}

- (UIImage *)ran_findFallbackMediaImage {
    for (GADNativeAdImage *image in _nativeAd.images) {
        if (image.image != nil) return image.image;
    }
    return nil;
}

- (CGFloat)ran_mediaAspectRatio {
    _mediaAspectReported = YES;
    if (_fallbackMediaImage != nil
            && _fallbackMediaImage.size.width > 0
            && _fallbackMediaImage.size.height > 0) {
        return _fallbackMediaImage.size.width
                / _fallbackMediaImage.size.height;
    }

    GADMediaContent *mediaContent = _nativeAd.mediaContent;
    if (mediaContent != nil) {
        if (!mediaContent.hasVideoContent) {
            UIImage *mainImage = mediaContent.mainImage;
            if (mainImage != nil
                    && mainImage.size.width > 0
                    && mainImage.size.height > 0) {
                return mainImage.size.width / mainImage.size.height;
            }
        }

        CGFloat aspectRatio = mediaContent.aspectRatio;
        if (aspectRatio > 0
                && !isnan(aspectRatio)
                && !isinf(aspectRatio)) {
            return aspectRatio;
        }
    }

    _mediaAspectReported = NO;
    return kRANDefaultMediaAspectRatio;
}

// ---------------------------------------------------------------------------
// Responsive collapsible ladder - a direct transcription: shed body lines,
// then body, then rating, then advertiser until the minimum media fits, and
// afterwards binary-search the largest uniform scale the natural media
// still tolerates.
// ---------------------------------------------------------------------------

- (void)ran_configureResponsiveCollapsibleContentWithPanelHeight:
        (CGFloat)panelHeight {
    // The strip-avoiding attempt comes first: the media inset like every
    // other element and the control strip's band kept above it, so close and
    // timer overlay nothing. Shrinking spends the call to action, the icon
    // and the paddings before the attempt surrenders; only when even those
    // floors cannot host the content does the layout fall back to bleeding
    // the media edge to edge under the strip.
    if (_hasDisplayableMedia) {
        _mediaView.ran_layoutMargins = UIEdgeInsetsZero;
        _mediaAvoidsControlStrip = YES;
        BOOL contentFits =
                [self ran_runCollapsibleFitPipelineWithPanelHeight:panelHeight];
        if (!contentFits) {
            [self ran_applyStripAvoidingFloors];
            contentFits = [self
                    ran_contentFitsWithMinimumMediaForPanelHeight:panelHeight];
        }
        if (contentFits) {
            // The compact chrome is not a last resort but the standing
            // dress: the height it frees goes straight to the media. The
            // half panel then runs the same maximiser as the full screen.
            [self ran_applyStripAvoidingFloors];
            [self ran_enableControlAvoidanceWithPanelHeight:panelHeight];
            // The plan came back with nowhere to put the picture: the
            // text alone has taken the panel. The panel is fixed, so the
            // layout gives way instead.
            if (_avoidanceFoundNoBand) {
                _controlAvoidanceActive = NO;
                [self ran_adoptLastResortLayoutWithPanelHeight:panelHeight];
            }
            return;
        }

        _mediaAvoidsControlStrip = NO;
        _mediaView.ran_layoutMargins = UIEdgeInsetsMake(
                0, -[self ran_columnPadding], 0, -[self ran_columnPadding]);
        [self ran_restoreOptionalRows];
    }
    if ([self ran_runCollapsibleFitPipelineWithPanelHeight:panelHeight]) {
        return;
    }
    // Every rung is spent: smallest scale, fewest body lines, optional
    // rows dropped, media at its floor - and the column still does not
    // fit. Stacking is what has run out, not room.
    [self ran_adoptLastResortLayoutWithPanelHeight:panelHeight];
}

- (BOOL)ran_runCollapsibleFitPipelineWithPanelHeight:(CGFloat)panelHeight {
    _headline.maxLines = kRANCollapsibleHeadlineMaxLines;
    _body.maxLines = kRANMaxBodyLineCount;
    [self ran_applyResponsiveContentScale:0];

    BOOL contentFits =
            [self ran_contentFitsWithMinimumMediaForPanelHeight:panelHeight];
    while (!_body.ran_gone
            && _body.maxLines > kRANMinBodyLineCount
            && !contentFits) {
        _body.maxLines = _body.maxLines - 1;
        contentFits = [self
                ran_contentFitsWithMinimumMediaForPanelHeight:panelHeight];
    }

    if (!contentFits && !_body.ran_gone) {
        _body.ran_gone = YES;
        _body.hidden = YES;
        contentFits = [self
                ran_contentFitsWithMinimumMediaForPanelHeight:panelHeight];
    }
    if (!contentFits && !_starRating.ran_gone) {
        _starRating.ran_gone = YES;
        _starRating.hidden = YES;
        contentFits = [self
                ran_contentFitsWithMinimumMediaForPanelHeight:panelHeight];
    }
    if (!contentFits && !_advertiser.ran_gone) {
        _advertiser.ran_gone = YES;
        _advertiser.hidden = YES;
        contentFits = [self
                ran_contentFitsWithMinimumMediaForPanelHeight:panelHeight];
    }

    if (!contentFits) return NO;
    if (![self ran_contentFitsWithNaturalMediaForPanelHeight:panelHeight]) {
        return YES;
    }

    [self ran_applyResponsiveContentScale:1];
    if ([self ran_contentFitsWithNaturalMediaForPanelHeight:panelHeight]) {
        return YES;
    }

    CGFloat minimumScale = 0;
    CGFloat maximumScale = 1;
    for (NSInteger iteration = 0;
         iteration < kRANResponsiveScaleSearchIterations;
         ++iteration) {
        CGFloat candidateScale = (minimumScale + maximumScale) / 2;
        [self ran_applyResponsiveContentScale:candidateScale];
        if ([self ran_contentFitsWithNaturalMediaForPanelHeight:panelHeight]) {
            minimumScale = candidateScale;
        } else {
            maximumScale = candidateScale;
        }
    }
    [self ran_applyResponsiveContentScale:minimumScale];
    return YES;
}

// The rail owns a fixed height beside the media, so the fit that matters is
// the rail's own stack against that height. The search mirrors the stacked
// panel's: the largest text that keeps every element inside the rail,
// shedding body lines and then optional rows when even the floor does not
// fit.
- (void)ran_configureResponsiveSideRailWithPanelHeight:(CGFloat)panelHeight {
    if (_sideRail == nil) return;

    _headline.maxLines = kRANCollapsibleHeadlineMaxLines;
    _body.maxLines = kRANMaxBodyLineCount;
    [self ran_applyRailContentScale:0];

    BOOL railFits = [self ran_sideRailFitsWithPanelHeight:panelHeight];
    while (!railFits
            && !_body.ran_gone
            && _body.maxLines > kRANMinBodyLineCount) {
        _body.maxLines = _body.maxLines - 1;
        railFits = [self ran_sideRailFitsWithPanelHeight:panelHeight];
    }
    if (!railFits && !_body.ran_gone) {
        _body.ran_gone = YES;
        _body.hidden = YES;
        railFits = [self ran_sideRailFitsWithPanelHeight:panelHeight];
    }
    if (!railFits && !_starRating.ran_gone) {
        _starRating.ran_gone = YES;
        _starRating.hidden = YES;
        railFits = [self ran_sideRailFitsWithPanelHeight:panelHeight];
    }
    if (!railFits && !_advertiser.ran_gone) {
        _advertiser.ran_gone = YES;
        _advertiser.hidden = YES;
        railFits = [self ran_sideRailFitsWithPanelHeight:panelHeight];
    }
    if (!railFits) return;

    // Three widths, and they are not interchangeable. The outer width is
    // what the rail actually occupies; the scale width is the column the
    // PLAIN arrangement would have had, which the text ceiling reads so the
    // picture's left edge cannot cost a step of text size; the content
    // width is what is left inside the rail's own padding.
    CGFloat railOuterWidth = MAX(
            0
          , UIScreen.mainScreen.bounds.size.width
                    - _sideMediaWidthPx
                    - _sideRowLeftInset);
    CGFloat railScaleWidth = MAX(
            0
          , UIScreen.mainScreen.bounds.size.width - _sideMediaWidthPx);
    CGFloat railScaleCap = MAX(
            0
          , MIN(
                1
              , (railScaleWidth - kRANRailScaleCapMinWidth)
                        / kRANRailScaleCapRange));
    CGFloat railScale = railScaleCap;
    [self ran_applyRailContentScale:railScale];
    if (![self ran_sideRailFitsWithPanelHeight:panelHeight]) {
        CGFloat minimumScale = 0;
        CGFloat maximumScale = railScaleCap;
        for (NSInteger iteration = 0;
             iteration < kRANResponsiveScaleSearchIterations;
             ++iteration) {
            CGFloat candidateScale = (minimumScale + maximumScale) / 2;
            [self ran_applyRailContentScale:candidateScale];
            if ([self ran_sideRailFitsWithPanelHeight:panelHeight]) {
                minimumScale = candidateScale;
            } else {
                maximumScale = candidateScale;
            }
        }
        railScale = minimumScale;
        [self ran_applyRailContentScale:railScale];
    }

    // Whole text before size, size before scrolling: give lines while they
    // fit, and when a text still cannot show itself whole, trade the scale
    // down a step and try again. Only what survives this may ever scroll.
    CGFloat railContentWidth = MAX(
            0, railScaleWidth - _sidePaddingBudget);
    for (NSInteger attempt = 0;
         attempt <= kRANRailFullTextSteps;
         ++attempt) {
        [self ran_growTextLines:_headline
                       maximum:kRANRailHeadlineMaxLineCount
                   panelHeight:panelHeight];
        [self ran_growTextLines:_body
                       maximum:kRANRailBodyMaxLineCount
                   panelHeight:panelHeight];
        BOOL allWhole =
                [self ran_railTextWhole:_headline width:railContentWidth]
                && [self ran_railTextWhole:_body width:railContentWidth]
                && [self ran_railTextWhole:_advertiser
                                    width:railContentWidth];
        if (allWhole) break;
        if (railScale <= 0) break;

        railScale = MAX(0, railScale - kRANRailFullTextScaleStep);
        [self ran_applyRailContentScale:railScale];
    }

    [self ran_growRailIconWithPanelHeight:panelHeight
                          railOuterWidth:railOuterWidth];
}

// The rail's own dress code on top of the shared scale: the button keeps
// the avoidance floor and the rows drop their optional padding - a narrow
// column spends its ground on text and media, not on chrome.
- (void)ran_applyRailContentScale:(CGFloat)scale {
    [self ran_applyResponsiveContentScale:scale];
    _callToAction.ran_minimumSize =
            CGSizeMake(0, kRANAvoidCallToActionHeight);
    _identityRow.ran_padding = UIEdgeInsetsZero;
    _body.ran_padding = UIEdgeInsetsZero;
}

- (void)ran_growTextLines:(RANAdTextLabel *)text
                 maximum:(NSInteger)maximumLineCount
             panelHeight:(CGFloat)panelHeight {
    if (text == nil || text.ran_gone || text.hidden) return;

    while (text.maxLines < maximumLineCount) {
        text.maxLines = text.maxLines + 1;
        if (![self ran_sideRailFitsWithPanelHeight:panelHeight]) {
            text.maxLines = text.maxLines - 1;
            return;
        }
    }
}

- (BOOL)ran_railTextWhole:(RANAdTextLabel *)text
                   width:(CGFloat)width {
    if (text == nil || text.ran_gone || text.hidden || width <= 0) {
        return YES;
    }
    return [text ran_showsEntireTextForWidth:width];
}

// Height that remains after the text has everything belongs to the icon:
// it grows into the slack, never past the rail's proportion.
- (void)ran_growRailIconWithPanelHeight:(CGFloat)panelHeight
                        railOuterWidth:(CGFloat)railOuterWidth {
    if (_iconHero || _icon.ran_gone || _icon.superview != _sideRail) return;
    if (![self ran_sideRailFitsWithPanelHeight:panelHeight]) return;

    CGFloat maximumIconSize = railOuterWidth * kRANRailIconMaxWidthRatio;
    while (_icon.ran_layoutWidth + kRANRailIconGrowthStep
            <= maximumIconSize) {
        _icon.ran_layoutWidth += kRANRailIconGrowthStep;
        _icon.ran_layoutHeight += kRANRailIconGrowthStep;
        if (![self ran_sideRailFitsWithPanelHeight:panelHeight]) {
            _icon.ran_layoutWidth -= kRANRailIconGrowthStep;
            _icon.ran_layoutHeight -= kRANRailIconGrowthStep;
            break;
        }
    }
}

- (BOOL)ran_sideRailFitsWithPanelHeight:(CGFloat)panelHeight {
    [_sideRail ran_measureWithWidthSpec:
                    RANMeasureSpecMake(
                            RANMeasureSpecExactly
                          , MAX(
                                0
                              , UIScreen.mainScreen.bounds.size.width
                                        - _sideMediaWidthPx
                                        - _sideRowLeftInset))
                            heightSpec:
                    RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
    return _sideRail.ran_measuredSize.height <= panelHeight;
}

// The floors the strip-avoiding layout may fall to before it surrenders: a
// shorter call to action, a smaller icon and no optional padding, each a
// price worth paying to keep the corner controls off the media.
- (void)ran_applyStripAvoidingFloors {
    _callToAction.ran_minimumSize =
            CGSizeMake(0, kRANAvoidCallToActionHeight);
    if (!_iconHero && !_sideMediaLayout) {
        _icon.ran_layoutWidth = kRANAvoidIconSize;
        _icon.ran_layoutHeight = kRANAvoidIconSize;
        _iconSpentForFit = YES;
    }
}

- (void)ran_restoreOptionalRows {
    if (_nativeAd.advertiser.length > 0) {
        _advertiser.ran_gone = NO;
        _advertiser.hidden = NO;
    }
    NSDecimalNumber *starRatingValue = _nativeAd.starRating;
    double stars = starRatingValue.doubleValue;
    if (starRatingValue != nil && stars > 0 && !isnan(stars)
            && !isinf(stars)) {
        _starRating.ran_gone = NO;
        _starRating.hidden = NO;
    }
    if (_nativeAd.body.length > 0) {
        _body.ran_gone = NO;
        _body.hidden = NO;
    }
    _body.maxLines = kRANMaxBodyLineCount;
}

static CGFloat RANInterpolate(CGFloat minimum, CGFloat maximum, CGFloat scale) {
    return minimum + (maximum - minimum) * scale;
}

- (void)ran_applyResponsiveContentScale:(CGFloat)scale {
    CGFloat resolvedScale = MAX(0, MIN(1, scale));
    _headline.font = [UIFont boldSystemFontOfSize:
            RANInterpolate(
                    kRANMinHeadlineTextSize
                  , kRANMaxHeadlineTextSize
                  , resolvedScale)];
    _advertiser.font = [UIFont systemFontOfSize:
            RANInterpolate(
                    kRANMinAdvertiserTextSize
                  , kRANMaxAdvertiserTextSize
                  , resolvedScale)];
    _body.font = [UIFont systemFontOfSize:
            RANInterpolate(
                    kRANMinBodyTextSize
                  , kRANMaxBodyTextSize
                  , resolvedScale)];
    _callToAction.titleLabel.font = [UIFont boldSystemFontOfSize:
            RANInterpolate(
                    kRANMinCallToActionTextSize
                  , kRANMaxCallToActionTextSize
                  , resolvedScale)];

    CGFloat identityVerticalPadding = round(
            RANInterpolate(
                    kRANMinIdentityVerticalPadding
                  , kRANMaxIdentityVerticalPadding
                  , resolvedScale));
    _identityRow.ran_padding = UIEdgeInsetsMake(
            _mediaAboveIdentity ? 0 : identityVerticalPadding
          , 0
          , identityVerticalPadding
          , 0);
    _body.ran_padding = UIEdgeInsetsMake(
            0
          , 0
          , round(RANInterpolate(
                    kRANMinBodyBottomPadding
                  , kRANMaxBodyBottomPadding
                  , resolvedScale))
          , 0);

    if (!_iconHero && !_sideMediaLayout) {
        CGFloat iconSize = round(
                RANInterpolate(kRANMinIconSize, kRANMaxIconSize, resolvedScale));
        _icon.ran_layoutWidth = iconSize;
        _icon.ran_layoutHeight = iconSize;
        _icon.ran_layoutMargins = UIEdgeInsetsMake(0, 0, 0, kRANIconGap);
    }

    _callToAction.ran_minimumSize = CGSizeMake(
            0
          , round(RANInterpolate(
                    kRANMinCallToActionHeight
                  , kRANMaxCallToActionHeight
                  , resolvedScale)));
}

- (CGFloat)ran_sideMediaHeightForWidth:(CGFloat)mediaWidth
                          panelHeight:(CGFloat)panelHeight {
    return MIN(panelHeight, round(mediaWidth / _mediaAspectRatio));
}

- (BOOL)ran_shouldUseSideMediaForPanelHeight:(CGFloat)panelHeight {
    if (_mediaAspectRatio >= kRANSideMediaMaxAspect) return NO;
    if (panelHeight > UIScreen.mainScreen.bounds.size.width
            * kRANSideMediaMaxPanelHeightRatio) {
        return NO;
    }

    CGFloat mediaWidth = [self ran_sideMediaWidthForPanelHeight:panelHeight];
    CGFloat railWidth =
            UIScreen.mainScreen.bounds.size.width - mediaWidth;
    // The video floor for every creative, image ones included - the same
    // line SideMediaFloor draws on Android. A picture narrow enough to need
    // the image floor is too narrow to carry a rail beside it, and letting
    // the two platforms disagree here put the same creative in a rail on
    // one and stacked on the other.
    return mediaWidth >= kRANMinVideoMediaSize
            && railWidth >= kRANSideMediaMinRail;
}

- (CGFloat)ran_sideMediaWidthForPanelHeight:(CGFloat)panelHeight {
    return MIN(
            round(panelHeight * _mediaAspectRatio)
          , round(UIScreen.mainScreen.bounds.size.width
                    * kRANSideMediaMaxWidthShare));
}

- (CGFloat)ran_contentWidth {
    return MAX(
            0
          , UIScreen.mainScreen.bounds.size.width - 2 * [self ran_columnPadding]);
}

// The column's side padding: the stacked half panel's text inset, the
// panel's own padding elsewhere.
- (CGFloat)ran_columnPadding {
    return _stackedHalf ? kRANStackedTextInset : kRANHorizontalPadding;
}

- (BOOL)ran_contentFitsWithMinimumMediaForPanelHeight:(CGFloat)panelHeight {
    return [self ran_contentFitsWithMediaHeight:_minimumMediaSize
                                   panelHeight:panelHeight];
}

- (BOOL)ran_contentFitsWithNaturalMediaForPanelHeight:(CGFloat)panelHeight {
    CGFloat contentWidth = [self ran_contentWidth];
    CGFloat naturalMediaHeight = MAX(
            _minimumMediaSize
          , round(contentWidth / _mediaAspectRatio));
    return [self ran_contentFitsWithMediaHeight:naturalMediaHeight
                                   panelHeight:panelHeight];
}

- (BOOL)ran_contentFitsWithMediaHeight:(CGFloat)mediaHeight
                          panelHeight:(CGFloat)panelHeight {
    CGFloat contentWidth = [self ran_contentWidth];
    if (_hasDisplayableMedia && !_sideMediaLayout) {
        CGFloat resolvedMediaHeight = MAX(_minimumMediaSize, mediaHeight);
        CGFloat resolvedMediaWidth = MIN(
                contentWidth
              , MAX(
                    _minimumMediaSize
                  , round(resolvedMediaHeight * _mediaAspectRatio)));
        _mediaView.ran_layoutWidth = resolvedMediaWidth;
        _mediaView.ran_layoutHeight = resolvedMediaHeight;
        _mediaView.ran_layoutGravity = RANGravityCenterHorizontal;
    }

    [_contentColumn ran_measureWithWidthSpec:
                    RANMeasureSpecMake(
                            RANMeasureSpecExactly
                          , contentWidth + 2 * [self ran_columnPadding])
                                 heightSpec:
                    RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
    return _contentColumn.ran_measuredSize.height <= panelHeight;
}

- (void)ran_resolveContentHeightWithRequestedPanelHeight:
        (CGFloat)requestedPanelHeight {
    CGSize screen = UIScreen.mainScreen.bounds.size;
    CGFloat contentWidth = [self ran_contentWidth];
    [_contentColumn ran_measureWithWidthSpec:
                    RANMeasureSpecMake(RANMeasureSpecExactly, screen.width)
                                 heightSpec:
                    RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
    if (_hasDisplayableMedia && !_sideMediaLayout
            && !_controlAvoidanceActive
            // A last-resort layout took the media out of the column;
            // sizing it as a column child again would wreck the layout it
            // was moved into.
            && !_scrimLayoutActive) {
        // Controls and badges draw on top with their own opaque backgrounds,
        // and with media as the first child the strip can only ever cover
        // media, never text - so the inset's height belongs to the media.
        // The strip-avoiding layout keeps the inset instead: nothing of the
        // media sits under the controls there.
        if (!_mediaAvoidsControlStrip) {
            UIEdgeInsets columnInset = _contentColumn.ran_padding;
            columnInset.top = 0;
            _contentColumn.ran_padding = columnInset;
        }
        [_contentColumn ran_measureWithWidthSpec:
                        RANMeasureSpecMake(RANMeasureSpecExactly, screen.width)
                                     heightSpec:
                        RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
        CGFloat lowerContentHeight = MAX(
                0
              , _contentColumn.ran_measuredSize.height - _minimumMediaSize);
        CGFloat panelHeight = _fullScreen
                ? screen.height
                : requestedPanelHeight;
        CGFloat availableMediaHeight = panelHeight - lowerContentHeight;
        // A strip-avoiding media is inset like every other element, so its
        // width basis is the content width, not the bleed's screen width.
        CGFloat mediaWidthBasis = _mediaAvoidsControlStrip
                ? contentWidth
                : screen.width;
        CGFloat naturalMediaHeight = MAX(
                _minimumMediaSize
              , round(mediaWidthBasis / _mediaAspectRatio));
        CGFloat resolvedMediaHeight = MAX(
                _minimumMediaSize
              , MIN(
                    naturalMediaHeight
                  , MAX(_minimumMediaSize, availableMediaHeight)));
        CGFloat resolvedMediaWidth = MIN(
                mediaWidthBasis
              , MAX(
                    _minimumMediaSize
                  , round(resolvedMediaHeight * _mediaAspectRatio)));
        _mediaView.ran_layoutWidth = resolvedMediaWidth;
        _mediaView.ran_layoutHeight = resolvedMediaHeight;
        _mediaView.ran_layoutGravity = RANGravityCenterHorizontal;
        [_contentColumn ran_measureWithWidthSpec:
                        RANMeasureSpecMake(RANMeasureSpecExactly, screen.width)
                                     heightSpec:
                        RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
    }

    // The panel does not grow. The caller asked for a share of the
    // screen and that share is the whole budget: content that will not
    // fit is shrunk, dropped or sent down the last-resort ladder, never
    // answered with a bigger panel. Android removed its widening on the
    // same owner's call - the ad and whatever stands behind it read one
    // number again.
    _resolvedPanelHeight = _fullScreen
            ? screen.height
            : requestedPanelHeight;
}

// ---------------------------------------------------------------------------
// Layout - the explicit pass Android got from its view hierarchy. Runs the
// measure model over the content column, overlays the badges, aligns the
// controls to the attribution's edge, then applies the whole-or-scrolling
// rule to whatever the pass truncated.
// ---------------------------------------------------------------------------

- (void)layoutSubviews {
    [super layoutSubviews];
    if (_released || _nativeAdView == nil) return;
    if (_cardLayout) {
        [self ran_layoutCard];
        return;
    }

    CGRect bounds = self.bounds;
    CGFloat cutoutInset = _fullScreen ? self.safeAreaInsets.top : 0;
    CGRect adFrame = CGRectMake(
            0
          , cutoutInset
          , bounds.size.width
          , bounds.size.height - cutoutInset);
    _nativeAdView.frame = adFrame;

    if (_controlAvoidanceActive) {
        [self ran_recomputeMediaControlAvoidanceWithWidth:adFrame.size.width
                                                height:adFrame.size.height];
    }

    if (_scrimLayoutActive) {
        CGRect innerFrame = CGRectMake(
                0, 0, adFrame.size.width, adFrame.size.height);
        _mediaView.frame = innerFrame;
        _scrimVeil.frame = innerFrame;
        // The column no longer fills the panel: it wraps its text and
        // hugs the bottom edge, over the veil.
        [_contentColumn ran_measureWithWidthSpec:
                        RANMeasureSpecMake(
                                RANMeasureSpecExactly, adFrame.size.width)
                                     heightSpec:
                        RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
        CGFloat columnHeight = MIN(
                _contentColumn.ran_measuredSize.height
              , adFrame.size.height);
        [_contentColumn ran_layoutWithFrame:CGRectMake(
                0
              , adFrame.size.height - columnHeight
              , adFrame.size.width
              , columnHeight)];
    } else {
        [_contentColumn ran_measureWithWidthSpec:
                        RANMeasureSpecMake(
                                RANMeasureSpecExactly, adFrame.size.width)
                                     heightSpec:
                        RANMeasureSpecMake(
                                RANMeasureSpecExactly, adFrame.size.height)];
        [_contentColumn ran_layoutWithFrame:
                CGRectMake(0, 0, adFrame.size.width, adFrame.size.height)];
    }
    if (_fallbackMediaImageView != nil) {
        _fallbackMediaImageView.frame = _mediaView.bounds;
    }

    [_attribution ran_measureWithWidthSpec:
                    RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)
                               heightSpec:
                    RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
    CGSize attributionSize = _attribution.ran_measuredSize;
    BOOL marksOnPicture = _stackedHalf
            && _hasDisplayableMedia
            && !_mediaView.hidden
            && _mediaView.bounds.size.width > 0;
    if (marksOnPicture) {
        // On the picture's top corners, their centres on one line, as on the
        // card: the picture is the ad and the strip above it is left to the
        // controls.
        CGRect picture = [_mediaView convertRect:_mediaView.bounds
                                          toView:_attribution.superview];
        // Flush with the picture's corners, as on the rect ads.
        _attribution.frame = CGRectMake(
                CGRectGetMinX(picture)
              , CGRectGetMinY(picture)
              , attributionSize.width
              , attributionSize.height);
        RANShapeBadge(_attribution, YES);
        _adChoicesView.frame = CGRectMake(
                CGRectGetMaxX(picture) - kRANAdChoicesSize
              , CGRectGetMinY(picture)
              , kRANAdChoicesSize
              , kRANAdChoicesSize);
    } else {
        // In the side-media layout even the badge steps off the picture: it
        // hugs the media's right edge, and the left corner control then
        // lines up beside it.
        CGFloat attributionX = _sideMediaLayout
                ? _sideRowLeftInset + _sideMediaWidthPx + kRANControlGap
                : 0;
        // The badge and the mark off the panel's corners.
        _attribution.frame = CGRectMake(
                attributionX + kRANHalfMarkInset
              , kRANHalfMarkInset
              , attributionSize.width
              , attributionSize.height);
        RANShapeBadge(_attribution, NO);
        _adChoicesView.frame = CGRectMake(
                adFrame.size.width - kRANHalfMarkInset - kRANAdChoicesSize
              , kRANHalfMarkInset
              , kRANAdChoicesSize
              , kRANAdChoicesSize);
    }

    CGFloat controlSize = kRANControlStripHeight;
    // The stacked panel's strip holds the controls alone, both sides in by
    // the panel's inset; elsewhere the left control follows the badge. The
    // disc stands in the strip's middle.
    CGFloat leftControlInset = _stackedHalf
            ? kRANHalfMarkInset
            : CGRectGetMaxX(_attribution.frame) + kRANControlGap;
    CGFloat rightControlInset = _stackedHalf
            ? kRANHalfMarkInset
            : kRANRightControlInset;
    CGFloat controlY = cutoutInset + (_stackedHalf
            ? MAX(0, ([self ran_controlStripHeight] - controlSize) / 2)
            : 0);
    BOOL closeLeft = _closeOnLeft;
    BOOL numberLeft = _timerOnLeft;
    _close.frame = CGRectMake(
            closeLeft
                    ? leftControlInset
                    : bounds.size.width - rightControlInset - controlSize
          , controlY
          , controlSize
          , controlSize);
    [self ran_layoutCloseGlyph];
    _countdown.frame = CGRectMake(
            numberLeft
                    ? leftControlInset
                    : bounds.size.width - rightControlInset - controlSize
          , controlY
          , controlSize
          , controlSize);

    [self ran_matchIconSizeToIdentityText];
    [self ran_roundAssets];
    [self ran_keepTextWholeOrScrolling:_headline];
    [self ran_keepTextWholeOrScrolling:_body];
    [self ran_keepTextWholeOrScrolling:_advertiser];
}

// Radii that follow the sizes the layout settled on.
- (void)ran_roundAssets {
    CGSize button = _callToAction.bounds.size;
    _callToAction.layer.cornerRadius = MIN(button.width, button.height) / 2;
    if (!_iconHero) {
        CGSize icon = _icon.bounds.size;
        _icon.layer.cornerRadius =
                MIN(icon.width, icon.height) * kRANIconCornerRatio;
    }
}

// ---------------------------------------------------------------------------
// Card - the full-screen face. The interstitial over the whole screen: the
// picture as large as it goes in the middle - under the controls' row across
// the width, or up to the screen's top between the two controls' columns,
// whichever is larger - and the app's strip at the foot, read from the left:
// the icon beside the name, the pitch, the button. The end card as the app's
// store page: the picture as a banner over its dimmed, blurred copy, then the
// listing (ran_arrangeEndCardListing) and the button at the foot. The app open
// ad a rounded card in the middle of the dimmed game: the picture across its
// top, then the app, the pitch and the button, centred. In landscape the
// picture takes the left and the rest stands beside it. The picture yields
// first when the screen is short, then the body's lines, then the icon; a face
// that still cannot fit keeps the picture at its floor and is held to its
// slot, its foot cut. The Java side's BuildCard and FitCard.
// ---------------------------------------------------------------------------

// The interstitial and the end card: the card's arrangement spread over the
// whole screen on the theme's surface, as the networks' own full-screen ads;
// only the app open ad keeps the card over the game.
- (BOOL)ran_cardBleeds {
    return _layout != kRANLayoutAppOpen;
}

- (BOOL)ran_cardHasAppBar {
    return _layout == kRANLayoutAppOpen && !_redirectOnClose;
}

// The strip the controls stand in across the panel's top: the full-screen
// faces keep the disc's own height, the half panel gives it room above and
// below.
- (CGFloat)ran_controlStripHeight {
    return _cardLayout ? kRANControlStripHeight : kRANHalfControlStrip;
}

// The controls never take a strip across the top. A landscape full-screen
// face has two gaps of its own beside its picture: one control stands right
// of the picture, the other at the screen's right edge, both over the text's
// column. A landscape card has the screen's sides, so they take a column at
// each side; a full-screen portrait face lets them stand over its picture's
// corners or beside it (the fit). Only the app open card without its bar
// keeps the strip, as the game around it is not the ad's to lay out.
- (CGFloat)ran_cardTopReserve {
    if ([self ran_cardHasAppBar]) return kRANAppBarHeight + 2 * kRANAppBarGap;
    return _cardLandscape || [self ran_cardBleeds]
            ? 0
            : kRANControlStripHeight + 2 * kRANCardControlTop;
}

// The column a control stands in at the side of the face, with its inset on
// both sides.
- (CGFloat)ran_cardControlColumn {
    return kRANControlStripHeight + 2 * kRANCardControlInset;
}

// The columns the landscape card leaves at its sides for the controls.
- (CGFloat)ran_cardSideReserve {
    return _cardLandscape
                    && ![self ran_cardHasAppBar]
                    && ![self ran_cardBleeds]
            ? [self ran_cardControlColumn]
            : 0;
}

- (void)ran_arrangeCard {
    CGSize screen = UIScreen.mainScreen.bounds.size;
    [self ran_arrangeCardLandscape:screen.width > screen.height];
}

// The views go back to their places for this orientation; what giving way
// took is given back, as the fit starts over.
- (void)ran_arrangeCardLandscape:(BOOL)landscape {
    BOOL endCard = _layout == kRANLayoutEndCard;
    _cardLandscape = landscape;
    for (UIView *view in @[ _mediaView, _icon, _identityText, _body
                          , _callToAction ]) {
        [view removeFromSuperview];
    }
    [_cardInfo removeFromSuperview];
    [_endCardFigures removeFromSuperview];
    _endCardFigures = nil;
    [_cardSpacers makeObjectsPerformSelector:@selector(removeFromSuperview)];
    _cardSpacers = nil;
    _body.ran_gone = NO;

    _contentColumn.ran_padding = UIEdgeInsetsZero;
    _contentColumn.ran_vertical = !_cardLandscape;
    _contentColumn.ran_gravity = RANGravityCenterHorizontal | RANGravityTop;
    BOOL bleeds = [self ran_cardBleeds];
    // The end card's face is see-through: its banner's backdrop stands
    // behind it, on the surface the panel paints.
    _contentColumn.backgroundColor = endCard
            ? UIColor.clearColor
            : RANARGB(RANThemeSurface());
    _contentColumn.layer.cornerRadius = bleeds ? 0 : kRANCardCorner;
    _contentColumn.clipsToBounds = YES;
    // The card's rim, drawn over the media: faint white on the dimmed game,
    // where a dark picture or a dark card would lose its edge. The Java
    // side's card edge.
    _contentColumn.layer.borderWidth = bleeds ? 0 : 1;
    _contentColumn.layer.borderColor = RANARGB(0x33FFFFFF).CGColor;

    _headline.font = RANCardFont(endCard ? 22 : 20, UIFontWeightBold);
    _headline.maxLines = kRANCardHeadlineMaxLines;
    _headline.ellipsizes = YES;
    // The advertiser a step under the headline and over the body: its own
    // weight and the ink at 70%, where the body is the grey.
    _advertiser.font = RANCardFont(13, UIFontWeightMedium);
    _advertiser.textColor = [RANARGB(RANThemeInk()) colorWithAlphaComponent:0.7];
    _advertiser.ran_padding = UIEdgeInsetsMake(2, 0, 0, 0);
    _body.font = RANCardFont(14, UIFontWeightRegular);
    _body.maxLines = kRANMaxBodyLineCount;
    _body.lineHeightMultiple = 1.15;
    _body.ellipsizes = YES;
    _body.ran_padding = UIEdgeInsetsMake(12, 0, 0, 0);
    RANCentreUnderIcon(_headline);
    RANCentreUnderIcon(_advertiser);
    RANCentreUnderIcon(_starRating);
    RANCentreUnderIcon(_body);
    _identityText.ran_gravity = RANGravityCenterHorizontal;
    _identityText.ran_layoutWidth = RANLayoutMatchParent;
    _identityText.ran_layoutWeight = 0;
    _identityText.ran_layoutMargins = UIEdgeInsetsZero;

    _callToAction.titleLabel.font = RANCardFont(16, UIFontWeightBold);
    _callToAction.titleLabel.numberOfLines = 1;
    _callToAction.ran_minimumSize = CGSizeZero;
    _callToAction.ran_layoutWidth = RANLayoutMatchParent;
    _callToAction.ran_layoutHeight = kRANCardCallToActionHeight;
    _callToAction.ran_layoutMargins = UIEdgeInsetsMake(16, 0, 0, 0);

    CGFloat iconSize = endCard ? kRANEndCardIconSize : kRANCardIconSize;
    _icon.ran_layoutWidth = iconSize;
    _icon.ran_layoutHeight = iconSize;
    _icon.ran_layoutGravity = RANGravityCenterHorizontal;
    _icon.ran_layoutMargins = UIEdgeInsetsMake(0, 0, 12, 0);
    // A white tile under the icon, as a launcher draws one: an icon with a
    // transparent ground otherwise loses its dark marks on a dark surface.
    _icon.backgroundColor = UIColor.whiteColor;
    if (_hasDisplayableMedia) {
        // Spread over the screen there is no card to cut in two: the
        // letterbox is the surface the rest stands on.
        _mediaView.backgroundColor = RANARGB(
                bleeds ? RANThemeSurface() : kRANCardMediaBackdropColor);
        _mediaView.ran_minimumSize = CGSizeZero;
        _mediaView.ran_layoutMargins = UIEdgeInsetsZero;
        _mediaView.ran_layoutWidth = RANLayoutMatchParent;
        _mediaView.ran_layoutHeight = 0;
    }

    _cardInfo = [[RANLinearLayoutView alloc] init];
    _cardInfo.ran_vertical = YES;
    _cardInfo.ran_gravity =
            RANGravityCenterHorizontal | RANGravityCenterVertical;
    _cardInfo.ran_padding = UIEdgeInsetsMake(
            kRANCardPadding, kRANCardPadding, kRANCardPadding, kRANCardPadding);
    _cardInfo.ran_layoutWidth = RANLayoutMatchParent;

    CGFloat mediaInset = [self ran_cardMediaInset];
    if (_cardLandscape) {
        if (_hasDisplayableMedia) {
            // Sized by the fit to the creative's own height at its width,
            // centred beside the column.
            _mediaView.ran_layoutGravity = RANGravityCenterVertical;
            _mediaView.ran_layoutMargins = UIEdgeInsetsMake(
                    mediaInset, mediaInset, mediaInset, 0);
            [_contentColumn addSubview:_mediaView];
        }
        if (endCard) {
            [self ran_arrangeEndCardListing];
        } else {
            [_cardInfo addSubview:_icon];
            [_cardInfo addSubview:_identityText];
            [_cardInfo addSubview:_body];
        }
        [_cardInfo addSubview:_callToAction];
        if (bleeds) {
            // The text's column keeps the controls' strip at its top.
            _cardInfo.ran_padding = UIEdgeInsetsMake(
                    MAX(kRANCardPadding
                      , kRANControlStripHeight + 2 * kRANCardControlTop)
                  , kRANCardPadding
                  , kRANCardPadding
                  , kRANCardPadding);
        }
        _cardInfo.ran_layoutWidth = 0;
        _cardInfo.ran_layoutHeight = RANLayoutMatchParent;
        _cardInfo.ran_layoutWeight = 1;
        [_contentColumn addSubview:_cardInfo];
        return;
    }

    if (_hasDisplayableMedia) {
        _mediaView.ran_layoutGravity = RANGravityCenterHorizontal;
        _mediaView.ran_layoutMargins = UIEdgeInsetsMake(
                mediaInset, mediaInset, 0, mediaInset);
    }
    if (!bleeds) {
        // The app open card: the picture across its top, then the app, the
        // pitch and the button, centred.
        if (_hasDisplayableMedia) [_contentColumn addSubview:_mediaView];
        [_cardInfo addSubview:_icon];
        [_cardInfo addSubview:_identityText];
        [_cardInfo addSubview:_body];
        [_cardInfo addSubview:_callToAction];
        [_contentColumn addSubview:_cardInfo];
        return;
    }

    UIView *over = [[UIView alloc] init];
    over.ran_layoutWidth = 0;
    over.ran_layoutHeight = 0;
    over.ran_layoutWeight = 1;
    UIView *under = [[UIView alloc] init];
    under.ran_layoutWidth = 0;
    under.ran_layoutHeight = 0;
    under.ran_layoutWeight = 1;
    _cardSpacers = @[ over, under ];
    _cardInfo.ran_layoutHeight = RANLayoutWrapContent;
    _cardInfo.ran_layoutWeight = 0;
    _cardInfo.ran_padding = UIEdgeInsetsMake(
            kRANCardPadding, kRANCardPadding, 0, kRANCardPadding);
    // The button apart from the pitch above it, however tight the face.
    _callToAction.ran_layoutMargins = UIEdgeInsetsMake(
            kRANCardPadding, 0, kRANCardPadding + 8, 0);
    if (endCard) {
        // The end card is the app's store page, as the networks draw the
        // card after a video: the creative as the page's banner across the
        // top, then the listing read from the left - the big icon beside the
        // name and the advertiser, the figures a store shows, the pitch -
        // and the button at the foot.
        if (_hasDisplayableMedia) [_contentColumn addSubview:_mediaView];
        [self ran_arrangeEndCardListing];
        [_contentColumn addSubview:_cardInfo];
        [_contentColumn addSubview:under];
        [_contentColumn addSubview:_callToAction];
        return;
    }

    // The interstitial is the creative's stage, as Google's own full-screen
    // native ads are: the picture as large as it goes in the middle of what
    // the app's strip leaves; the strip at the foot, read from the left as a
    // store's listing row - the icon beside the name, the pitch, then the
    // button.
    [_contentColumn addSubview:over];
    if (_hasDisplayableMedia) [_contentColumn addSubview:_mediaView];
    [_contentColumn addSubview:under];
    for (UIView *text in @[ _headline, _advertiser, _starRating, _body ]) {
        RANAlignStart(text);
    }
    _identityText.ran_gravity = RANGravityLeft;
    _identityText.ran_layoutWidth = 0;
    _identityText.ran_layoutWeight = 1;
    _headline.maxLines = 2;
    _body.maxLines = 2;
    _icon.ran_layoutGravity = RANGravityCenterVertical;
    _icon.ran_layoutMargins = UIEdgeInsetsMake(0, 0, 0, 12);
    RANLinearLayoutView *row = [[RANLinearLayoutView alloc] init];
    row.ran_vertical = NO;
    row.ran_gravity = RANGravityCenterVertical;
    row.ran_layoutWidth = RANLayoutMatchParent;
    [row addSubview:_icon];
    [row addSubview:_identityText];
    _cardInfo.ran_gravity = RANGravityLeft | RANGravityTop;
    [_cardInfo addSubview:row];
    [_cardInfo addSubview:_body];
    [_contentColumn addSubview:_cardInfo];
    [_contentColumn addSubview:_callToAction];
}

// The card's frame inside a panel of this size, before its height is known
// in portrait (the height field is then the most it may take).
- (CGRect)ran_cardSlotInSize:(CGSize)size safeArea:(UIEdgeInsets)safeArea {
    CGFloat side = [self ran_cardSideReserve];
    if ([self ran_cardBleeds]) {
        // To the foot of the screen, edge to edge but for the controls'
        // columns.
        CGFloat top = safeArea.top + [self ran_cardTopReserve];
        return CGRectMake(
                safeArea.left + side
              , top
              , MAX(0, size.width - safeArea.left - safeArea.right - 2 * side)
              , MAX(0, size.height - top - safeArea.bottom));
    }
    CGFloat left = safeArea.left + MAX(kRANCardMargin, side);
    CGFloat right = safeArea.right + MAX(kRANCardMargin, side);
    CGFloat top = safeArea.top + [self ran_cardTopReserve];
    CGFloat bottom = safeArea.bottom + kRANCardMargin;
    CGFloat availableWidth = MAX(0, size.width - left - right);
    CGFloat width = MIN(
            availableWidth
          , _cardLandscape ? kRANCardLandscapeMaxWidth : kRANCardMaxWidth);
    CGFloat height = MAX(0, size.height - top - bottom);
    if (_cardLandscape) height = MIN(height, kRANCardLandscapeMaxHeight);
    return CGRectMake(
            left + (availableWidth - width) / 2, top, width, height);
}

- (CGFloat)ran_measureCardWidth:(CGFloat)width {
    [_contentColumn ran_measureWithWidthSpec:
                    RANMeasureSpecMake(RANMeasureSpecExactly, width)
                                 heightSpec:
                    RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
    return _contentColumn.ran_measuredSize.height;
}

// One step of giving way, in order: the body a line at a time to one, the
// headline to two lines, the headline a size smaller, the body gone, the icon
// smaller. The headline holds its lines before the body; it is the ad's name.
// NO when nothing is left to give.
- (BOOL)ran_cardGiveWay:(NSInteger)step {
    switch (step) {
        case 0:
            _body.maxLines = kRANMaxBodyLineCount - 1;
            return YES;
        case 1:
            _body.maxLines = kRANMinBodyLineCount;
            return YES;
        case 2:
            _headline.maxLines = kRANCardHeadlineMaxLines - 1;
            return YES;
        case 3:
            _headline.font = [_headline.font
                    fontWithSize:round(_headline.font.pointSize * 0.9)];
            return YES;
        case 4:
            _body.ran_gone = YES;
            return YES;
        case 5:
            _icon.ran_layoutWidth = kRANCardSmallIconSize;
            _icon.ran_layoutHeight = kRANCardSmallIconSize;
            return YES;
        default:
            return NO;
    }
}

// Sizes the card for a panel of this size. In portrait the picture takes
// what the text leaves, up to its own proportions and its share of the card;
// the text gives way before the picture goes under its floor. In landscape
// the column beside the picture gives way until it fits the card's height.
// Giving way only ever narrows, so the layout pass may run it again.
- (BOOL)ran_fitCardInSize:(CGSize)size safeArea:(UIEdgeInsets)safeArea {
    CGRect slot = [self ran_cardSlotInSize:size safeArea:safeArea];
    if (slot.size.width <= 0 || slot.size.height <= 0) return NO;

    CGFloat mediaInset = _hasDisplayableMedia ? [self ran_cardMediaInset] : 0;
    if (_cardLandscape) {
        CGFloat mediaWidth = 0;
        CGFloat mediaRoom = slot.size.height - 2 * mediaInset;
        if (_hasDisplayableMedia) {
            mediaWidth = MAX(
                    _minimumMediaSize
                  , MIN(
                        round(slot.size.width * kRANCardLandscapeMediaShare)
                      , round(mediaRoom * _mediaAspectRatio)));
            _mediaView.ran_layoutWidth = mediaWidth;
            if (slot.size.height < _minimumMediaSize) return NO;
        }
        // The creative's own height at that width: a full-height box made
        // the player draw black bands above and below a wide video.
        CGFloat mediaHeight = _hasDisplayableMedia
                ? MIN(mediaRoom
                    , MAX(_minimumMediaSize, round(mediaWidth / _mediaAspectRatio)))
                : 0;
        if (_hasDisplayableMedia) _mediaView.ran_layoutHeight = mediaHeight;
        CGFloat infoWidth = slot.size.width - mediaWidth - mediaInset;
        for (NSInteger step = 0; ; ++step) {
            [_cardInfo ran_measureWithWidthSpec:
                            RANMeasureSpecMake(RANMeasureSpecExactly, infoWidth)
                                     heightSpec:
                            RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
            CGFloat infoHeight = _cardInfo.ran_measuredSize.height;
            if (infoHeight <= slot.size.height) {
                // The face no taller than what it holds, centred in its
                // room: the picture then reaches the face's top, where the
                // badge and the AdChoices mark sit.
                _cardLandscapeFaceHeight = MIN(
                        slot.size.height
                      , MAX(mediaHeight + 2 * mediaInset, infoHeight));
                return YES;
            }
            if (![self ran_cardGiveWay:step]) {
                _cardLandscapeFaceHeight = slot.size.height;
                return NO;
            }
        }
    }

    if ([self ran_cardBleeds]) {
        CGFloat textWidth = MIN(slot.size.width, kRANBleedTextMaxWidth);
        _cardInfo.ran_layoutWidth = textWidth;
        _callToAction.ran_layoutWidth = textWidth - 2 * kRANCardPadding;
    }
    BOOL bleeds = [self ran_cardBleeds];
    // The end card's picture gives the app the middle; the interstitial's is
    // the face.
    CGFloat naturalMediaHeight =
            round((slot.size.width - 2 * mediaInset) / _mediaAspectRatio);
    CGFloat mediaCap = round(slot.size.height
            * (!bleeds
                    ? kRANCardPortraitMediaShare
                    : _layout == kRANLayoutEndCard ? kRANEndCardMediaShare : 1));
    // The controls stand in the face's top corners. The picture either keeps
    // under the strip they stand in, at the face's whole width, or rises to
    // the face's top between the columns they stand in; the larger picture
    // wins.
    CGFloat strip = kRANControlStripHeight + 2 * kRANCardControlTop;
    CGFloat column = [self ran_cardControlColumn];
    if (_hasDisplayableMedia) _mediaView.ran_layoutHeight = 0;
    for (NSInteger step = 0; ; ++step) {
        _contentColumn.ran_padding = UIEdgeInsetsZero;
        CGFloat textHeight = [self ran_measureCardWidth:slot.size.width];
        if (!_hasDisplayableMedia) {
            if (textHeight <= slot.size.height) return YES;
        } else {
            CGFloat room = slot.size.height - textHeight;
            CGFloat mediaHeight = MIN(
                    naturalMediaHeight
                  , MIN(mediaCap, room - (bleeds ? strip : 0)));
            CGFloat top = bleeds ? strip : 0;
            if (bleeds) {
                CGFloat between = MIN(
                        round((slot.size.width - 2 * column) / _mediaAspectRatio)
                      , MIN(mediaCap, room));
                CGFloat underWidth = MIN(
                        slot.size.width, round(mediaHeight * _mediaAspectRatio));
                if (round(between * _mediaAspectRatio) * between
                        > underWidth * mediaHeight) {
                    mediaHeight = between;
                    top = 0;
                }
            }
            if (mediaHeight >= _minimumMediaSize) {
                _contentColumn.ran_padding = UIEdgeInsetsMake(top, 0, 0, 0);
                [self ran_sizeCardMediaToHeight:mediaHeight width:slot.size.width];
                return YES;
            }
        }
        if (![self ran_cardGiveWay:step]) {
            if (_hasDisplayableMedia) {
                _contentColumn.ran_padding =
                        UIEdgeInsetsMake(bleeds ? strip : 0, 0, 0, 0);
                [self ran_sizeCardMediaToHeight:_minimumMediaSize
                                          width:slot.size.width];
            }
            return NO;
        }
    }
}

// The picture at this height, its box no wider than the creative at that
// height: a player letterboxes in black, which a tall video's sides would show
// across the face, and on the app open card would split it.
- (void)ran_sizeCardMediaToHeight:(CGFloat)height width:(CGFloat)width {
    _mediaView.ran_layoutHeight = height;
    _mediaView.ran_layoutWidth = MIN(
            width - 2 * [self ran_cardMediaInset]
          , round(height * _mediaAspectRatio));
}

// How far the app open card's picture stands in from each edge it meets: by
// what the card's rounding takes from a corner, so its own corners touch the
// curve and not a pixel of the creative is cut - the native policy lets a
// picture lose only width, a video nothing. Nothing on the full-screen faces,
// whose corners are square.
- (CGFloat)ran_cardMediaInset {
    return [self ran_cardBleeds] ? 0 : ceil(kRANCardCorner * (1.0 - M_SQRT1_2));
}

// The end card's listing in _cardInfo: the icon beside the name and the
// advertiser, the store's figures - the rating's stars, the price, the store,
// apart by hairlines, only those the ad carries - and the pitch, all from the
// left.
- (void)ran_arrangeEndCardListing {
    // Centred down the column beside a landscape picture.
    _cardInfo.ran_gravity = RANGravityLeft | RANGravityCenterVertical;
    for (UIView *text in @[ _headline, _advertiser, _body ]) {
        RANAlignStart(text);
    }
    _identityText.ran_gravity = RANGravityLeft | RANGravityCenterVertical;
    _identityText.ran_layoutWidth = 0;
    _identityText.ran_layoutWeight = 1;
    _headline.maxLines = 2;
    _body.maxLines = 3;
    _icon.ran_layoutGravity = RANGravityCenterVertical;
    _icon.ran_layoutMargins = UIEdgeInsetsMake(0, 0, 0, 16);
    RANLinearLayoutView *row = [[RANLinearLayoutView alloc] init];
    row.ran_vertical = NO;
    row.ran_gravity = RANGravityCenterVertical;
    row.ran_layoutWidth = RANLayoutMatchParent;
    [row addSubview:_icon];
    [row addSubview:_identityText];
    [_cardInfo addSubview:row];

    [_starRating removeFromSuperview];
    _starRating.textSize = 15;
    // Read from the ad, not from the views: the card is first arranged
    // before the assets are bound (ran_bindAssets).
    double stars = _nativeAd.starRating.doubleValue;
    NSMutableArray<UIView *> *figures = [NSMutableArray array];
    if (_nativeAd.starRating != nil && stars > 0 && !isnan(stars) && !isinf(stars)) {
        [figures addObject:_starRating];
    }
    if (_price != nil && _nativeAd.price.length > 0) [figures addObject:_price];
    if (_store != nil && _nativeAd.store.length > 0) [figures addObject:_store];
    for (UIView *figure in figures) [figure removeFromSuperview];
    if (figures.count > 0) {
        _endCardFigures = [[RANLinearLayoutView alloc] init];
        _endCardFigures.ran_vertical = NO;
        _endCardFigures.ran_gravity = RANGravityCenterVertical;
        _endCardFigures.ran_layoutWidth = RANLayoutMatchParent;
        _endCardFigures.ran_layoutMargins = UIEdgeInsetsMake(16, 0, 0, 0);
        for (NSUInteger index = 0; index < figures.count; ++index) {
            if (index > 0) {
                UIView *hairline = [[UIView alloc] init];
                hairline.backgroundColor = RANARGB(
                        RANThemeIsDark() ? 0xFF444746 : 0xFFDADCE0);
                hairline.ran_layoutWidth = 1;
                hairline.ran_layoutHeight = 20;
                [_endCardFigures addSubview:hairline];
            }
            RANFrameLayoutView *cell = [[RANFrameLayoutView alloc] init];
            cell.ran_layoutWidth = 0;
            cell.ran_layoutWeight = 1;
            UIView *figure = figures[index];
            figure.ran_layoutGravity =
                    RANGravityCenterHorizontal | RANGravityCenterVertical;
            [cell addSubview:figure];
            [_endCardFigures addSubview:cell];
        }
        [_cardInfo addSubview:_endCardFigures];
    }
    [_cardInfo addSubview:_body];
    [self ran_ensureEndCardBackdrop];
}

// The dimmed, blurred copy of the creative's picture the end card's banner
// stands on, behind the ad view; the surface alone when the ad carries no
// picture to copy.
- (void)ran_ensureEndCardBackdrop {
    if (_endCardBackdrop != nil) return;

    UIImage *image = [self ran_findFallbackMediaImage];
    UIView *backdrop = [[UIView alloc] init];
    backdrop.clipsToBounds = YES;
    backdrop.userInteractionEnabled = NO;
    backdrop.backgroundColor = RANARGB(RANThemeIsDark() ? 0xFF1F1F1F : 0xFFE9EEF6);
    if (image != nil) {
        UIImageView *copy = [[UIImageView alloc] initWithImage:image];
        copy.contentMode = UIViewContentModeScaleAspectFill;
        copy.autoresizingMask = UIViewAutoresizingFlexibleWidth
                | UIViewAutoresizingFlexibleHeight;
        [backdrop addSubview:copy];
        UIVisualEffectView *blur = [[UIVisualEffectView alloc]
                initWithEffect:[UIBlurEffect effectWithStyle:UIBlurEffectStyleDark]];
        blur.autoresizingMask = UIViewAutoresizingFlexibleWidth
                | UIViewAutoresizingFlexibleHeight;
        [backdrop addSubview:blur];
    }
    _endCardBackdrop = backdrop;
    [self insertSubview:backdrop belowSubview:_nativeAdView];
}

// Across the screen from its top edge to the foot of the picture, or in
// landscape down the screen from its left edge to the picture's right: the
// controls stand on it, clear of the creative.
// The close and the countdown take the tone of what they stand on: dark over
// the dimmed game around the app open ad's card, over the end card's dimmed
// copy of its creative, and over a dark theme's surface.
- (void)ran_styleControlsOnEndCardBackdrop:(BOOL)onEndCardBackdrop {
    BOOL darkBackdrop = (_cardLayout && ![self ran_cardBleeds])
            || onEndCardBackdrop
            || RANThemeIsDark();
    UIColor *disc = RANARGB(
            darkBackdrop ? kRANDarkBackdropDiscColor : kRANCloseDiscColor);
    UIColor *glyph = RANARGB(
            darkBackdrop ? kRANDarkBackdropGlyphColor : kRANCloseGlyphColor);
    _close.backgroundColor = disc;
    _closeGlyph.strokeColor = glyph.CGColor;
    _countdown.backgroundColor = disc;
    _countdown.textColor = glyph;
}

- (void)ran_placeEndCardBackdrop:(CGRect)card {
    if (_endCardBackdrop == nil) return;

    BOOL showing = _hasDisplayableMedia && !_mediaView.hidden;
    _endCardBackdrop.hidden = !showing;
    // In portrait the controls stand on it; in landscape on the listing.
    [self ran_styleControlsOnEndCardBackdrop:showing && !_cardLandscape];
    if (!showing) return;

    CGRect picture = _mediaView.frame;
    CGRect frame = _cardLandscape
            ? CGRectMake(0, 0, CGRectGetMinX(card) + CGRectGetMaxX(picture), self.bounds.size.height)
            : CGRectMake(0, 0, self.bounds.size.width, CGRectGetMinY(card) + CGRectGetMaxY(picture));
    _endCardBackdrop.frame = frame;
    for (UIView *layer in _endCardBackdrop.subviews) layer.frame = _endCardBackdrop.bounds;
}

- (void)ran_layoutCard {
    CGRect bounds = self.bounds;
    UIEdgeInsets safeArea = self.safeAreaInsets;
    // Built ahead for the screen as it was; turned since, the card takes
    // the other arrangement.
    BOOL landscape = bounds.size.width > bounds.size.height;
    if (bounds.size.width > 0 && landscape != _cardLandscape) {
        [self ran_arrangeCardLandscape:landscape];
    }
    // Fitted again for the real bounds; the build only had the screen.
    [self ran_fitCardInSize:bounds.size safeArea:safeArea];
    CGRect slot = [self ran_cardSlotInSize:bounds.size safeArea:safeArea];
    CGFloat height = _cardLandscape
            ? _cardLandscapeFaceHeight
            : [self ran_cardBleeds]
                    ? slot.size.height
                    : MIN(slot.size.height, [self ran_measureCardWidth:slot.size.width]);
    CGRect card = CGRectMake(
            slot.origin.x
          , slot.origin.y + (slot.size.height - height) / 2
          , slot.size.width
          , height);
    _nativeAdView.frame = card;
    [_contentColumn ran_measureWithWidthSpec:
                    RANMeasureSpecMake(RANMeasureSpecExactly, card.size.width)
                                 heightSpec:
                    RANMeasureSpecMake(RANMeasureSpecExactly, card.size.height)];
    [_contentColumn ran_layoutWithFrame:
            CGRectMake(0, 0, card.size.width, card.size.height)];
    if (_fallbackMediaImageView != nil) {
        _fallbackMediaImageView.frame = _mediaView.bounds;
    }
    [self ran_placeEndCardBackdrop:card];

    // The badges on the card's corners, over its picture: the card is the
    // ad and the panel around it is not.
    [_attribution ran_measureWithWidthSpec:
                    RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)
                               heightSpec:
                    RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
    CGSize attributionSize = _attribution.ran_measuredSize;
    // On the picture's top corners, their centres on one line; on the face's
    // when it has no picture.
    CGRect picture = _hasDisplayableMedia && !_mediaView.hidden
            ? _mediaView.frame
            : CGRectMake(0, 0, card.size.width, card.size.height);
    // Flush on the picture's top corners, as on the rect ads: they are
    // square everywhere, the app open card's picture standing clear of its
    // rounding (ran_cardMediaInset).
    _attribution.frame = CGRectMake(
            CGRectGetMinX(picture)
          , CGRectGetMinY(picture)
          , attributionSize.width
          , attributionSize.height);
    RANShapeBadge(_attribution, YES);
    _adChoicesView.frame = CGRectMake(
            CGRectGetMaxX(picture) - kRANAdChoicesSize
          , CGRectGetMinY(picture)
          , kRANAdChoicesSize
          , kRANAdChoicesSize);

    // The controls at the panel's top corners, clear of the card; on the
    // landscape full-screen face the left one right of the picture.
    CGFloat controlSize = kRANControlStripHeight;
    CGFloat controlY = safeArea.top + kRANCardControlTop;
    CGFloat leftX = (_cardLandscape
                            && [self ran_cardBleeds]
                            && _hasDisplayableMedia
                            && !_mediaView.hidden
                    ? CGRectGetMinX(card) + CGRectGetMaxX(_mediaView.frame)
                    : safeArea.left)
            + kRANCardControlInset;
    CGFloat rightX = bounds.size.width - safeArea.right
            - kRANCardControlInset - controlSize;
    _close.frame = CGRectMake(
            _closeOnLeft ? leftX : rightX, controlY, controlSize, controlSize);
    [self ran_layoutCloseGlyph];
    _countdown.frame = CGRectMake(
            _timerOnLeft ? leftX : rightX, controlY, controlSize, controlSize);
    [self ran_layoutAppBarInBounds:bounds safeArea:safeArea];

    [self ran_roundAssets];
    [self ran_keepTextWholeOrScrolling:_headline];
    [self ran_keepTextWholeOrScrolling:_body];
    [self ran_keepTextWholeOrScrolling:_advertiser];
}

// The bar back to the game, at the top of an app open ad: the game's icon and
// name, a spinner while the close is held, then Continue to app and a
// chevron. It is the way out, so the cross and the countdown stand down.
- (void)ran_buildAppBar {
    UIView *bar = [[UIView alloc] init];
    bar.backgroundColor = RANARGB(RANThemeSurface());
    bar.layer.cornerRadius = kRANAppBarHeight / 2;
    bar.layer.maskedCorners =
            kCALayerMinXMinYCorner | kCALayerMinXMaxYCorner;
    bar.layer.shadowColor = UIColor.blackColor.CGColor;
    bar.layer.shadowOpacity = 0.18f;
    bar.layer.shadowRadius = 2;
    bar.layer.shadowOffset = CGSizeMake(0, 1);

    _appBarIcon = [[UIImageView alloc] initWithImage:RANApplicationIcon()];
    _appBarIcon.contentMode = UIViewContentModeScaleAspectFill;
    _appBarIcon.clipsToBounds = YES;
    _appBarIcon.layer.cornerRadius = kRANAppBarIconSize * kRANIconCornerRatio;
    [bar addSubview:_appBarIcon];

    _appBarName = [[UILabel alloc] init];
    _appBarName.text = RANApplicationName();
    _appBarName.font = [UIFont systemFontOfSize:15];
    _appBarName.textColor = RANARGB(RANThemeInk());
    _appBarName.lineBreakMode = NSLineBreakByTruncatingTail;
    [bar addSubview:_appBarName];

    _appBarSpinner = [[UIActivityIndicatorView alloc]
            initWithActivityIndicatorStyle:UIActivityIndicatorViewStyleMedium];
    _appBarSpinner.color = RANARGB(RANThemePrimary());
    [_appBarSpinner startAnimating];
    [bar addSubview:_appBarSpinner];

    UIView *ready = [[UIView alloc] init];
    _appBarContinue = [[UILabel alloc] init];
    _appBarContinue.text = kRANAppBarContinueText;
    // The action, so it weighs as much as the name beside it.
    _appBarContinue.font = [UIFont systemFontOfSize:15 weight:UIFontWeightMedium];
    _appBarContinue.textColor = RANARGB(RANThemeInk());
    [ready addSubview:_appBarContinue];
    _appBarChevron = [CAShapeLayer layer];
    _appBarChevron.strokeColor = RANARGB(RANThemePrimary()).CGColor;
    _appBarChevron.fillColor = UIColor.clearColor.CGColor;
    _appBarChevron.lineWidth = 2.5;
    _appBarChevron.lineCap = kCALineCapRound;
    _appBarChevron.lineJoin = kCALineJoinRound;
    [ready.layer addSublayer:_appBarChevron];
    [bar addSubview:ready];
    _appBarReady = ready;

    [bar addGestureRecognizer:[[UITapGestureRecognizer alloc]
            initWithTarget:self
                    action:@selector(ran_appBarTapped)]];
    bar.isAccessibilityElement = YES;
    bar.accessibilityLabel = kRANAppBarContinueText;
    bar.accessibilityTraits = UIAccessibilityTraitButton;
    [self addSubview:bar];
    _appBar = bar;
    [self ran_syncAppBar];
}

- (void)ran_appBarTapped {
    if (_countDownRemainingMs > 0 || _onClose == nil) return;
    _onClose();
}

- (void)ran_layoutAppBarInBounds:(CGRect)bounds safeArea:(UIEdgeInsets)safeArea {
    if (_appBar == nil) return;

    CGFloat height = kRANAppBarHeight;
    CGFloat overhang = height / 2;
    CGFloat chevronSize = 22;
    CGSize continueSize = [_appBarContinue sizeThatFits:
            CGSizeMake(CGFLOAT_MAX, height)];
    CGFloat readyWidth = continueSize.width + 6 + chevronSize;
    // The bar hugs what it holds: icon, name, the gap, then the slot the
    // spinner and Continue to app share, as wide as the wider, so it keeps
    // its length when one replaces the other. A long name gives way first.
    CGFloat nameWidth = MIN(
            ceil([_appBarName sizeThatFits:CGSizeMake(CGFLOAT_MAX, height)].width)
          , round(bounds.size.width * 0.4));
    CGFloat slotWidth = MAX(readyWidth, 20);
    // The right end runs off the screen, as AdMob's bar does.
    CGFloat width = 14 + kRANAppBarIconSize + 10 + nameWidth + kRANAppBarNameGap
            + slotWidth + 8 + overhang;
    _appBar.frame = CGRectMake(
            bounds.size.width - safeArea.right + overhang - width
          , safeArea.top + kRANAppBarGap
          , width
          , height);
    CGFloat x = 14;
    _appBarIcon.frame = CGRectMake(
            x, (height - kRANAppBarIconSize) / 2
          , kRANAppBarIconSize, kRANAppBarIconSize);
    x += kRANAppBarIconSize + 10;

    // What stands at the right, inside the visible part of the bar.
    CGFloat visibleRight = width - overhang - 8;
    CGFloat trailing = slotWidth;
    _appBarReady.frame = CGRectMake(
            visibleRight - readyWidth, 0, readyWidth, height);
    _appBarContinue.frame = CGRectMake(
            0, (height - continueSize.height) / 2
          , continueSize.width, continueSize.height);
    CGRect chevronBox = CGRectMake(
            continueSize.width + 6, (height - chevronSize) / 2
          , chevronSize, chevronSize);
    _appBarChevron.frame = chevronBox;
    UIBezierPath *chevron = [UIBezierPath bezierPath];
    CGFloat tipX = chevronSize / 2 + chevronSize * 0.14f;
    CGFloat centreY = chevronSize / 2;
    [chevron moveToPoint:CGPointMake(
            tipX - chevronSize * 0.26f, centreY - chevronSize * 0.28f)];
    [chevron addLineToPoint:CGPointMake(tipX, centreY)];
    [chevron addLineToPoint:CGPointMake(
            tipX - chevronSize * 0.26f, centreY + chevronSize * 0.28f)];
    _appBarChevron.path = chevron.CGPath;
    _appBarSpinner.frame = CGRectMake(
            visibleRight - 20, (height - 20) / 2, 20, 20);

    _appBarName.frame = CGRectMake(
            x, 0, MAX(0, visibleRight - trailing - 8 - x), height);
}

// Where the app bar is the way out, it shows the countdown's state and the
// corner controls stay hidden.
- (void)ran_syncAppBar {
    if (_appBar == nil) return;

    BOOL done = _countDownRemainingMs <= 0;
    _countdown.hidden = YES;
    _close.hidden = YES;
    _appBarSpinner.hidden = done;
    if (done) {
        [_appBarSpinner stopAnimating];
    } else {
        [_appBarSpinner startAnimating];
    }
    _appBarReady.hidden = !done;
    [self setNeedsLayout];
}

// A media that ran out of height before it reached the sides is starving,
// and the chrome below it is what it is starving for: the button drops to
// its floor, the icon to its own, and the rows give up the paddings they
// only had to look comfortable. Once, and only for a media that would grow
// by it - a picture already touching the sides needs nothing.
- (BOOL)ran_trimChromeForStarvedMediaWithBoxWidth:(CGFloat)boxWidth
                                   intervalWidth:(CGFloat)intervalWidth {
    if (_chromeTrimmedForMedia
            || !_mediaAspectReported
            || boxWidth >= intervalWidth - 2) {
        return NO;
    }

    _chromeTrimmedForMedia = YES;
    [self ran_applyStripAvoidingFloors];
    _identityRow.ran_padding = UIEdgeInsetsZero;
    _body.ran_padding = UIEdgeInsetsZero;
    return YES;
}

// A probe: the column measured with the media collapsed, giving the
// height of everything below it. The media's layout values are restored
// before returning, and the caller owns re-measuring for real.
- (CGFloat)ran_measureLowerContentWithPanelWidth:(CGFloat)panelWidth {
    CGFloat previousWidth = _mediaView.ran_layoutWidth;
    CGFloat previousHeight = _mediaView.ran_layoutHeight;
    CGFloat previousWeight = _mediaView.ran_layoutWeight;
    UIEdgeInsets previousMargins = _mediaView.ran_layoutMargins;
    _mediaView.ran_layoutWidth = 0;
    _mediaView.ran_layoutHeight = 0;
    _mediaView.ran_layoutWeight = 0;
    // The margins collapse with the view - the seam under the media is
    // accounted for explicitly in the band arithmetic, and leaving it
    // here counted it twice from the second measure on. The Android side
    // carries the on-device attribution of this exact drift.
    _mediaView.ran_layoutMargins = UIEdgeInsetsZero;
    [_contentColumn ran_measureWithWidthSpec:
                    RANMeasureSpecMake(RANMeasureSpecExactly, panelWidth)
                                 heightSpec:
                    RANMeasureSpecMake(RANMeasureSpecUnspecified, 0)];
    CGFloat lowerContentHeight = _contentColumn.ran_measuredSize.height;
    _mediaView.ran_layoutWidth = previousWidth;
    _mediaView.ran_layoutHeight = previousHeight;
    _mediaView.ran_layoutWeight = previousWeight;
    _mediaView.ran_layoutMargins = previousMargins;
    return lowerContentHeight;
}

- (void)ran_enableControlAvoidanceWithPanelHeight:(CGFloat)panelHeight {
    _controlAvoidanceActive = YES;
    _avoidancePanelHeight = panelHeight;
    _contentColumn.ran_gravity =
            RANGravityBottom | RANGravityCenterHorizontal;
    _mediaView.ran_layoutMargins = UIEdgeInsetsZero;
    // The collapsible path computes its first plan here and now, so the
    // NO BAND verdict exists while the last-resort ladder can still act
    // on it. The fullScreen path passes 0 and keeps deferring to the
    // layout pass, where its true frame is known.
    if (panelHeight > 0) {
        [self ran_recomputeMediaControlAvoidanceWithWidth:
                        UIScreen.mainScreen.bounds.size.width
                                                  height:panelHeight];
    }
}

// Control avoidance, the Android transcription: the stack hugs the bottom,
// four candidates compete - the media rising through the gap of the top
// control row, sitting below that row, rising between edge-hugging controls
// below the badges, or sitting below those - and the placement that leaves
// the media largest wins. The chosen band then becomes the media's box:
// the creative fits centered inside it and the ambient backdrop carries its
// colors to the edges. Badges may sit over media; the close and timer
// controls avoid it.
- (void)ran_recomputeMediaControlAvoidanceWithWidth:(CGFloat)panelWidth
                                            height:(CGFloat)panelHeight {
    if (panelWidth <= 0 || panelHeight <= 0) return;

    // The lower stack's height, measured with the media collapsed. The
    // button keeps its gap above the screen's bottom edge.
    CGFloat bottomPadding = kRANHalfBottomPadding;
    UIEdgeInsets columnPadding = _contentColumn.ran_padding;
    columnPadding.top = 0;
    columnPadding.bottom = bottomPadding;
    _contentColumn.ran_padding = columnPadding;
    CGFloat lowerContentHeight =
            [self ran_measureLowerContentWithPanelWidth:panelWidth];
    CGFloat availableHeight =
            MAX(0, panelHeight - lowerContentHeight - bottomPadding);

    // The media's field. The controls stand in the panel's top corners;
    // the picture either starts under the strip they stand in, at the
    // panel's whole width, or rises to the panel's top between the two
    // columns they stand in. The larger picture wins, so the controls only
    // ever claim room the picture could not have used. It grows on the
    // panel's center line until it touches the field's sides or top.
    CGFloat candidateTops[2] = { kRANHalfControlStrip, 0 };
    CGFloat candidateLefts[2] = { 0, kRANHalfControlStrip };
    CGFloat candidateRights[2] = {
            panelWidth, panelWidth - kRANHalfControlStrip };
    // A reported ratio sizes the box to the creative itself: it grows
    // until it touches whichever limit binds, and the band's slack splits
    // so the block sits centered between the controls and the bottom edge.
    // Only a creative that never reported its proportions is handed the
    // whole band - no number exists to size or validate it - and renders
    // inside as it pleases on the black ground.
    // The largest box wins; ties go to the placement that starts higher,
    // because a picture reaching the panel's top edge reads as filling the
    // panel.
    CGFloat bestScore = -1;
    // A box of nothing, not a box the size of the panel: the defaults are
    // what survives when no placement is found, and a panel-sized default
    // overflows the very panel this is trying to fit into.
    CGFloat bestBoxWidth = 0;
    CGFloat bestBoxHeight = 0;
    CGFloat bestTop = 0;
    CGFloat bestIntervalWidth = panelWidth;
    for (NSInteger index = 0; index < 2; ++index) {
        CGFloat top = candidateTops[index];
        CGFloat intervalLeft = MAX(0, candidateLefts[index]);
        CGFloat intervalRight = MIN(panelWidth, candidateRights[index]);
        // The picture stays on the panel's center line, so what a candidate
        // really offers is twice its narrower half: growing past that would
        // push the media off center, not make it bigger.
        CGFloat panelCentre = panelWidth / 2;
        CGFloat intervalWidth = 2 * MIN(
                panelCentre - intervalLeft
              , intervalRight - panelCentre);
        if (intervalWidth < _minimumMediaSize) continue;

        CGFloat bandHeight = availableHeight - top - kRANMediaLowerSeam;
        if (bandHeight < _minimumMediaSize) continue;

        CGFloat boxWidth;
        CGFloat boxHeight;
        if (_mediaAspectReported) {
            boxHeight = MIN(
                    bandHeight
                  , round(intervalWidth / _mediaAspectRatio));
            boxWidth = MIN(
                    intervalWidth
                  , round(boxHeight * _mediaAspectRatio));
            if (boxWidth < _minimumMediaSize
                    || boxHeight < _minimumMediaSize) {
                continue;
            }
        } else {
            boxWidth = intervalWidth;
            boxHeight = bandHeight;
        }

        CGFloat score = boxWidth * boxHeight;
        if (score > bestScore
                || (score == bestScore && top < bestTop)) {
            bestScore = score;
            bestBoxWidth = boxWidth;
            bestBoxHeight = boxHeight;
            bestTop = top;
            bestIntervalWidth = intervalWidth;
        }
    }

    // The sweep found nothing at the media's floor. The panel cannot
    // grow, so the layout gives way instead: the caller reads this and
    // reaches for the last-resort ladder.
    _avoidanceFoundNoBand = bestScore < 0;

    if ([self ran_trimChromeForStarvedMediaWithBoxWidth:bestBoxWidth
                                        intervalWidth:bestIntervalWidth]) {
        [self ran_recomputeMediaControlAvoidanceWithWidth:panelWidth
                                                 height:panelHeight];
        return;
    }

    CGFloat slack = _mediaAspectReported
            ? MAX(0, availableHeight
                    - bestTop
                    - kRANMediaLowerSeam
                    - bestBoxHeight)
            : 0;
    // Slack feeds the text before it pads the void: the body takes more
    // whole lines while slack remains, so a clipped line never sits beside
    // empty space. A line already scrolling is left alone.
    if (!_body.ran_gone && !_body.hidden && !_body.marquee) {
        while (slack > 0
                && _body.maxLines < kRANRailBodyMaxLineCount
                && ![_body ran_showsEntireTextForWidth:
                        [self ran_contentWidth]]) {
            _body.maxLines = _body.maxLines + 1;
            CGFloat grownLower =
                    [self ran_measureLowerContentWithPanelWidth:panelWidth];
            CGFloat grownAvailable =
                    MAX(0, panelHeight - grownLower - bottomPadding);
            CGFloat grownSlack =
                    grownAvailable - bestTop - bestBoxHeight;
            if (grownSlack < 0) {
                _body.maxLines = _body.maxLines - 1;
                [self ran_measureLowerContentWithPanelWidth:panelWidth];
                break;
            }
            lowerContentHeight = grownLower;
            availableHeight = grownAvailable;
            slack = grownSlack;
        }
    }
    columnPadding.top = bestTop;
    columnPadding.bottom = bottomPadding + slack / 2;
    _contentColumn.ran_padding = columnPadding;
    _mediaView.ran_layoutWidth = bestBoxWidth;
    _mediaView.ran_layoutHeight = bestBoxHeight;
    _mediaView.ran_layoutWeight = 0;
    // Centered on the panel, never on the gap between the controls: the eye
    // measures the picture against the panel's edges, and a box centered in an
    // off-center interval reads as a mistake. The margin is measured from the
    // panel edge, so the column's own left padding is subtracted - negative
    // means the media bleeds through it, as it may.
    _mediaView.ran_layoutGravity = RANGravityLeft;
    UIEdgeInsets mediaMargins = UIEdgeInsetsZero;
    mediaMargins.left = (panelWidth - bestBoxWidth) / 2
            - _contentColumn.ran_padding.left;
    mediaMargins.bottom = kRANMediaLowerSeam;
    _mediaView.ran_layoutMargins = mediaMargins;
}

// One creative, one verdict: an image goes to the scrim, and a creative
// that cannot - a video, which Google serves only into a MediaView of 120pt
// or more and flags without one - is declared unshowable: the owner drops
// it and requests another, never shows it badly.
- (void)ran_adoptLastResortLayoutWithPanelHeight:(CGFloat)panelHeight {
    BOOL adopted = _hasDisplayableMedia
            && [self ran_applyScrimLayoutWithPanelHeight:panelHeight];
    _layoutUnrenderable = !adopted;
}

// The scrim spends the panel twice: the picture takes the whole of it, a
// veil goes over the picture, and the text sits on the veil instead of
// underneath the picture - so the height the text needs is no longer
// subtracted from the height the picture needs. The template the rect ad
// reaches for in a squarish cell, with the same veil. Never a video: a
// player behind text it does not know about cannot keep its frame clear
// of it, and the veil dims every frame besides.
- (BOOL)ran_applyScrimLayoutWithPanelHeight:(CGFloat)panelHeight {
    if (_mediaIsVideo) {
        return NO;
    }
    if (_scrimLayoutActive
            || _nativeAdView == nil
            || _mediaView.superview == nil) {
        return NO;
    }

    _scrimLayoutActive = YES;
    _lastResortPanelHeight = panelHeight;
    // The text moves onto the veil, which is dark whatever the theme.
    [self ran_colorTextOnSurface:NO];
    [_mediaView removeFromSuperview];
    [_nativeAdView insertSubview:_mediaView atIndex:0];
    _mediaView.ran_layoutMargins = UIEdgeInsetsZero;

    _scrimVeil = [[UIView alloc] init];
    // The same gray the rect ad's veil wears: black at 0xB3 alpha.
    _scrimVeil.backgroundColor = [UIColor colorWithWhite:0 alpha:0.7];
    _scrimVeil.userInteractionEnabled = NO;
    [_nativeAdView insertSubview:_scrimVeil atIndex:1];

    // The veil already separates the text from the picture; the block
    // spends only a hair on each side and hugs the bottom. The top strip
    // inset goes - a block at the bottom has no strip above it.
    _contentColumn.ran_padding = UIEdgeInsetsMake(
            kRANHorizontalPadding
          , kRANHorizontalPadding
          , kRANHorizontalPadding
          , kRANHorizontalPadding);
    _contentColumn.ran_gravity =
            RANGravityBottom | RANGravityCenterHorizontal;
    return YES;
}

// On the theme's surface, or white on a picture's veil.
- (void)ran_colorTextOnSurface:(BOOL)onSurface {
    _headline.textColor = onSurface
            ? RANARGB(RANThemeInk())
            : UIColor.whiteColor;
    UIColor *secondary = onSurface
            ? RANARGB(RANThemeGrey())
            : [UIColor colorWithWhite:1 alpha:0.8];
    // On the card the advertiser is a step over the body: the ink at 70%.
    _advertiser.textColor = onSurface && _cardLayout
            ? [RANARGB(RANThemeInk()) colorWithAlphaComponent:0.7]
            : secondary;
    _body.textColor = secondary;
    _starRating.textColor = secondary;
}

- (void)ran_matchIconSizeToIdentityText {
    // The pipeline pinned this icon to pay for the media and certified
    // the fit at that size; growing it back here is what invalidated the
    // certificate behind the pipeline's back on Android. Same rule here.
    if (_icon.ran_gone || _sideMediaLayout || _iconSpentForFit) return;

    CGFloat textHeight = _identityText.ran_measuredSize.height;
    CGFloat rowWidth = _identityRow.ran_measuredSize.width;
    if (textHeight <= 0 || rowWidth <= 0) return;

    CGFloat minimumIconSize = MAX(
            kRANMinIconSize
          , MAX(_icon.ran_layoutWidth, _icon.ran_layoutHeight));
    CGFloat maximumIconSize = MAX(
            minimumIconSize
          , MIN(kRANMaxIconSize, round(rowWidth * kRANMaxIconRowWidthRatio)));
    CGFloat resolvedIconSize = MAX(
            minimumIconSize
          , MIN(textHeight, maximumIconSize));
    if (_icon.ran_layoutWidth == resolvedIconSize
            && _icon.ran_layoutHeight == resolvedIconSize) {
        return;
    }

    _icon.ran_layoutWidth = resolvedIconSize;
    _icon.ran_layoutHeight = resolvedIconSize;
    [self setNeedsLayout];
}

// The same rule the rect ad layout follows: a value is shown whole, or on
// one line that scrolls at a smaller size - never cut.
- (void)ran_keepTextWholeOrScrolling:(RANAdTextLabel *)text {
    if (text == nil || text.ran_gone || text.hidden) return;

    NSValue *identity = [NSValue valueWithNonretainedObject:text];
    CGFloat width = text.bounds.size.width;
    if (width <= 0) return;

    if (text.marquee) {
        if ([_shrunkTexts containsObject:identity]) return;
        if ([text ran_showsEntireTextForWidth:width]
                && text.bounds.size.width > 0
                && [text sizeThatFits:
                        CGSizeMake(CGFLOAT_MAX, CGFLOAT_MAX)].width <= width) {
            return;
        }
        [_shrunkTexts addObject:identity];
        text.font = [text.font fontWithSize:
                text.font.pointSize * kRANMarqueeTextShrink];
        [self setNeedsLayout];
        return;
    }

    if ([text ran_showsEntireTextForWidth:width]) return;

    [_shrunkTexts addObject:identity];
    CGFloat shrunkSize = text.font.pointSize * kRANMarqueeTextShrink;
    text.marquee = YES;
    text.font = [text.font fontWithSize:shrunkSize];
    [self setNeedsLayout];
}

// ---------------------------------------------------------------------------
// Countdown
// ---------------------------------------------------------------------------

- (void)ran_layoutCloseGlyph {
    if (_closeGlyph == nil) return;

    CGRect bounds = _close.bounds;
    _closeGlyph.frame = bounds;
    CGFloat half = MIN(bounds.size.width, bounds.size.height)
            * kRANCloseGlyphSpanRatio / 2;
    CGFloat centreX = CGRectGetMidX(bounds);
    CGFloat centreY = CGRectGetMidY(bounds);
    UIBezierPath *path = [UIBezierPath bezierPath];
    [path moveToPoint:CGPointMake(centreX - half, centreY - half)];
    [path addLineToPoint:CGPointMake(centreX + half, centreY + half)];
    [path moveToPoint:CGPointMake(centreX - half, centreY + half)];
    [path addLineToPoint:CGPointMake(centreX + half, centreY - half)];
    _closeGlyph.path = path.CGPath;
}

- (void)ran_startCountdown {
    if (_countdown == nil || _close == nil) return;
    [_timer invalidate];
    _timer = nil;

    int64_t remainingMs = MAX(0, _countDownRemainingMs);
    _countdown.text = [NSString stringWithFormat:@"%lld"
          , (long long)((remainingMs + 999) / 1000)];
    _countdown.hidden = NO;
    _close.hidden = YES;
    if (remainingMs <= 0) {
        _countdown.hidden = YES;
        _close.hidden = NO;
        [self ran_syncAppBar];
        return;
    }
    [self ran_syncAppBar];

    // Monotonic, like the CountDownTimer the Java side runs this on. The
    // manual route to a clock change cannot reach a running countdown - the
    // Settings trip resigns active, which pauses it, and the return builds a
    // fresh anchor - but an NTP correction needs no trip, and a deadline
    // measured on the calendar was never the right instrument anyway.
    NSTimeInterval finishAt = RANNow() + remainingMs / 1000.0;
    __weak RANOverlayAdContentView *weakSelf = self;
    _timer = [NSTimer scheduledTimerWithTimeInterval:kRANCountdownInterval
                                             repeats:YES
                                               block:^(NSTimer *timer) {
        RANOverlayAdContentView *strongSelf = weakSelf;
        if (strongSelf == nil) {
            [timer invalidate];
            return;
        }
        NSTimeInterval remaining = finishAt - RANNow();
        strongSelf->_countDownRemainingMs =
                MAX(0, (int64_t)(remaining * 1000));
        if (remaining > 0) {
            strongSelf->_countdown.text =
                    [NSString stringWithFormat:@"%ld"
                          , (long)ceil(remaining)];
            return;
        }
        [timer invalidate];
        strongSelf->_timer = nil;
        strongSelf->_countDownRemainingMs = 0;
        strongSelf->_countdown.hidden = YES;
        strongSelf->_close.hidden = NO;
        [strongSelf ran_syncAppBar];
    }];
}

@end