package com.riseon.ads.admobnative;

import android.content.Context;
import android.content.res.Configuration;
import android.graphics.drawable.GradientDrawable;
import android.view.View;

// The colors every ad of the pack draws with, in the device's light or dark
// appearance: the Material 3 numbers the Ads pack's Editor placeholders use,
// so a format looks the same in the Editor and on a device.
final class AdTheme {
    private static final int LIGHT_SURFACE = 0xFFFFFFFF;
    private static final int LIGHT_INK = 0xFF1F1F1F;
    private static final int LIGHT_GREY = 0xFF454745;
    private static final int LIGHT_PRIMARY = 0xFF0A57D1;
    private static final int DARK_SURFACE = 0xFF29292B;
    private static final int DARK_INK = 0xFFE3E3E3;
    private static final int DARK_GREY = 0xFFC4C7C4;
    // The call to action stays a saturated fill on a dark surface too: a
    // pale one reads as the weakest thing there.
    private static final int DARK_PRIMARY = 0xFF1A73E8;

    // The Ad tag: a grey pill, white text, solid so it reads the same over
    // any picture - Google's grey 700, 6:1 against the white.
    static final int ATTRIBUTION = 0xFF5F6368;

    // The Ad tag's corners: a small rounding, not round ends.
    private static final float BADGE_CORNER_DP = 4f;

    // The Ad tag's shape: corners slightly round; flush in a square corner,
    // a tab - square along the corner's edges, round where it reaches into
    // the ad.
    static void ShapeBadge(View badge, boolean tab) {
        if (!(badge.getBackground() instanceof GradientDrawable)) return;

        GradientDrawable background = (GradientDrawable) badge.getBackground();
        float radius = BADGE_CORNER_DP
                * badge.getResources().getDisplayMetrics().density;
        if (tab) {
            background.setCornerRadii(
                    new float[] { 0, 0, 0, 0, radius, radius, 0, 0 });
        } else {
            background.setCornerRadius(radius);
        }
    }
    static final int ATTRIBUTION_TEXT = 0xFFFFFFFF;

    // The rating's stars: Google's yellow filled on both appearances; the
    // empty ones a grey that reads on the surface - Google's grey 300 on
    // light, 700 on dark.
    private static final int STAR_FILLED = 0xFFFBBC04;
    private static final int LIGHT_STAR_EMPTY = 0xFFDADCE0;
    private static final int DARK_STAR_EMPTY = 0xFF5F6368;

    // Behind the app open ad's card: the game dimmed, as AdMob's own.
    private static final int APP_OPEN_BACKDROP = 0xB8000000;

    private AdTheme() {}

    static boolean IsDark(Context context) {
        int night = context.getResources().getConfiguration().uiMode
                & Configuration.UI_MODE_NIGHT_MASK;
        return night == Configuration.UI_MODE_NIGHT_YES;
    }

    static int Surface(Context context) {
        return IsDark(context) ? DARK_SURFACE : LIGHT_SURFACE;
    }

    static int Ink(Context context) {
        return IsDark(context) ? DARK_INK : LIGHT_INK;
    }

    static int Grey(Context context) {
        return IsDark(context) ? DARK_GREY : LIGHT_GREY;
    }

    static int StarFilled() {
        return STAR_FILLED;
    }

    static int StarEmpty(boolean onDark) {
        return onDark ? DARK_STAR_EMPTY : LIGHT_STAR_EMPTY;
    }

    static int Primary(Context context) {
        return IsDark(context) ? DARK_PRIMARY : LIGHT_PRIMARY;
    }

    // What shows where the creative does not: the dimmed game around the
    // app open ad's card, the theme's surface for every other panel - the
    // interstitial and the end card fill the screen with it.
    static int Panel(Context context, boolean fullScreen, int layout) {
        return fullScreen && layout == OverlayAdContentView.LAYOUT_APP_OPEN
                ? APP_OPEN_BACKDROP
                : Surface(context);
    }
}