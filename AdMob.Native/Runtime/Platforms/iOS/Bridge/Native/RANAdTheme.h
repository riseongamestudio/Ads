// The colors every ad of the pack draws with, in the device's light or dark
// appearance: the Material 3 numbers the Ads pack's Editor placeholders and
// the Android side (AdTheme.java) use, so a format looks the same everywhere.

#ifndef RAN_AD_THEME_H
#define RAN_AD_THEME_H

#import <UIKit/UIKit.h>

// The Ad tag Google draws: a grey pill, white text.
// Solid, Google's grey 700: 6:1 against the white text over any picture.
static const uint32_t kRANThemeAttribution = 0xFF5F6368;
static const uint32_t kRANThemeAttributionText = 0xFFFFFFFF;

// The Ad tag's corners: a small rounding, not round ends.
static const CGFloat kRANThemeBadgeCorner = 4;

// The Ad tag's shape: corners slightly round; flush in a square corner, a
// tab - square along the corner's edges, round where it reaches into the ad.
static inline void RANShapeBadge(UIView *badge, BOOL tab) {
    badge.layer.cornerRadius = kRANThemeBadgeCorner;
    badge.layer.masksToBounds = YES;
    badge.layer.maskedCorners = tab
            ? kCALayerMaxXMaxYCorner
            : kCALayerMinXMinYCorner | kCALayerMaxXMinYCorner
                    | kCALayerMinXMaxYCorner | kCALayerMaxXMaxYCorner;
}

// The rating's stars: Google's yellow filled on both appearances; the empty
// ones a grey that reads on the ground - Google's grey 300 on a light one,
// 700 on a dark one.
static const uint32_t kRANThemeStarFilled = 0xFFFBBC04;

static inline uint32_t RANThemeStarEmpty(BOOL onDark) {
    return onDark ? 0xFF5F6368 : 0xFFDADCE0;
}

static inline BOOL RANThemeIsDark(void) {
    return UIScreen.mainScreen.traitCollection.userInterfaceStyle
            == UIUserInterfaceStyleDark;
}

static inline uint32_t RANThemeSurface(void) {
    return RANThemeIsDark() ? 0xFF29292B : 0xFFFFFFFF;
}

static inline uint32_t RANThemeInk(void) {
    return RANThemeIsDark() ? 0xFFE3E3E3 : 0xFF1F1F1F;
}

static inline uint32_t RANThemeGrey(void) {
    return RANThemeIsDark() ? 0xFFC4C7C4 : 0xFF454745;
}

// The call to action stays a saturated fill on a dark surface too: a pale
// one reads as the weakest thing there.
static inline uint32_t RANThemePrimary(void) {
    return RANThemeIsDark() ? 0xFF1A73E8 : 0xFF0A57D1;
}

// What shows where the creative does not: the game dimmed around an app open
// ad's card (layout 1), as AdMob's own; the theme's surface for every other
// panel - the interstitial and the end card fill the screen with it.
static inline uint32_t RANThemePanel(BOOL fullScreen, int32_t layout) {
    return fullScreen && layout == 1 ? 0xB8000000 : RANThemeSurface();
}

#endif