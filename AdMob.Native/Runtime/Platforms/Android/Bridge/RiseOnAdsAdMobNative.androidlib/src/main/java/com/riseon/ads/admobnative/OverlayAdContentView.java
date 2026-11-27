package com.riseon.ads.admobnative;

import android.content.Context;
import android.content.res.ColorStateList;
import android.content.res.Configuration;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.ColorFilter;
import android.graphics.Outline;
import android.graphics.Paint;
import android.graphics.PixelFormat;
import android.graphics.Point;
import android.graphics.Rect;
import android.graphics.RenderEffect;
import android.graphics.Shader;
import android.graphics.Typeface;
import android.graphics.drawable.ColorDrawable;
import android.graphics.drawable.Drawable;
import android.graphics.drawable.LayerDrawable;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.RippleDrawable;
import android.os.Build;
import android.os.CountDownTimer;
import android.text.Layout;
import android.text.TextUtils;
import android.util.DisplayMetrics;
import android.util.Log;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewOutlineProvider;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.Space;
import android.widget.TextView;

import com.google.android.gms.ads.nativead.MediaView;
import com.google.android.gms.ads.nativead.NativeAdView;
import com.google.android.gms.ads.nativead.NativeAd;

import java.util.ArrayList;
import java.util.List;

final class OverlayAdContentView extends FrameLayout {
    private static final class SingleClickNativeAdContainer
            extends FrameLayout {
        private boolean clickCommitted;

        SingleClickNativeAdContainer(Context context) {
            super(context);
        }

        @Override
        public boolean dispatchTouchEvent(MotionEvent event) {
            if (clickCommitted) return true;
            return super.dispatchTouchEvent(event);
        }

        void CommitAdClick() {
            clickCommitted = true;
        }

        @Override
        public void onWindowFocusChanged(boolean hasWindowFocus) {
            super.onWindowFocusChanged(hasWindowFocus);
            if (hasWindowFocus) clickCommitted = false;
        }

        // The half-screen window is FLAG_NOT_FOCUSABLE, so the focus hook
        // above is deaf there - the same deafness the redirect close had -
        // and one genuine click left the call to action dead for the rest
        // of the show. Visibility does not need focus: coming back from
        // the browser makes this window visible again on either path.
        @Override
        protected void onWindowVisibilityChanged(int visibility) {
            super.onWindowVisibilityChanged(visibility);
            if (visibility == View.VISIBLE) clickCommitted = false;
        }
    }

    private static final String TAG = "OverlayAd";
    private static final String ATTRIBUTION_TEXT = "Ad";
    // Over a picture's veil, whatever the theme.
    private static final String SECONDARY_TEXT_COLOR = "#CCFFFFFF";
    // The countdown and the close share one tonal disc, as the placeholders
    // draw. The controls only ever stand on the panel or the dimmed game,
    // never on the creative, so a faint wash of the ink is enough to show
    // the target without outweighing the call to action.
    private static final String CLOSE_BACKGROUND_COLOR = "#14000000";
    private static final String CLOSE_GLYPH_COLOR = "#FF444746";
    // The same over what is dark: a dark theme's panel or the dimmed game.
    private static final String DARK_BACKDROP_DISC_COLOR = "#24FFFFFF";
    private static final String DARK_BACKDROP_GLYPH_COLOR = "#FFE3E3E3";
    // Sized to fill the chip: a mark lost in the middle of its box leaves
    // the box reading as empty space.
    private static final float CLOSE_TEXT_SIZE_SP = 20f;
    // The close mark's geometry: it spans this share of its box's shorter
    // side, in strokes this thick. Numbers, not a glyph - see
    // CloseGlyphDrawable for why.
    // One mark for every close of every format, the Ads placeholders' and
    // Google's own: ink across 44% of the disc in strokes a tenth of it, so
    // the stroke ends' centres span 34%.
    private static final float CLOSE_GLYPH_SPAN_RATIO = 0.34f;
    private static final float CLOSE_GLYPH_STROKE_DP = 3f;
    private static final float COUNTDOWN_TEXT_SIZE_SP = 15f;
    private static final String CTA_TEXT_COLOR = "#FFFFFFFF";
    private static final String CTA_RIPPLE_COLOR = "#55FFFFFF";
    // An icon's corners, as a launcher rounds them.
    private static final float ICON_CORNER_RATIO = 0.22f;

    // The full-screen arrangements, ordinals shared with the C#
    // FullScreenLayout.
    static final int LAYOUT_INTERSTITIAL = 0;
    static final int LAYOUT_APP_OPEN = 1;
    static final int LAYOUT_END_CARD = 2;
    // Behind media narrower or shorter than its box, a picture's or a
    // video's: light, so the letterbox reads as part of the card. Black bars
    // beside a tall video merged into a dark panel and cut the card in two.
    private static final String CARD_MEDIA_BACKDROP_COLOR = "#FFF1F3F4";
    private static final int CARD_CORNER_DP = 20;
    // The card's rim on the dark ground behind it, where a dark picture or a
    // dark card would merge into it and lose its edge.
    private static final String CARD_EDGE_COLOR = "#33FFFFFF";
    private static final int CARD_MARGIN_DP = 16;
    private static final int CARD_MAX_WIDTH_DP = 440;
    private static final int CARD_LANDSCAPE_MAX_WIDTH_DP = 760;
    private static final int CARD_LANDSCAPE_MAX_HEIGHT_DP = 420;
    // The picture's share of the card's width in landscape, at most.
    private static final float CARD_LANDSCAPE_MEDIA_SHARE = 0.58f;
    // And of the card's height in portrait, so the text keeps its room.
    private static final float CARD_PORTRAIT_MEDIA_SHARE = 0.6f;
    // The end card's picture, at most: the app is what the page is about.
    private static final float END_CARD_MEDIA_SHARE = 0.4f;
    private static final int CARD_PADDING_DP = 20;
    private static final int CARD_ICON_DP = 56;
    private static final int CARD_SMALL_ICON_DP = 40;
    private static final int END_CARD_ICON_DP = 88;
    private static final int CARD_CTA_HEIGHT_DP = 52;
    private static final int CARD_CONTROL_INSET_DP = 12;
    private static final int CARD_CONTROL_TOP_DP = 8;
    // Where the SDK's AdChoices mark sits on the card: 6dp in from its
    // corner, where the Ads placeholders and the Editor preview draw it.
    private static final int CARD_AD_CHOICES_INSET_DP = 6;
    // The card's text follows the device's font scale up to this, as iOS
    // caps Dynamic Type: past it the face has nothing left to give.
    private static final float CARD_MAX_FONT_SCALE = 1.3f;
    private static final int CARD_HEADLINE_MAX_LINE_COUNT = 3;
    // The SDK's AdChoices mark, measured on a device (15dp square).
    private static final int CARD_AD_CHOICES_MARK_DP = 15;
    // The text and the button of a full-screen face, at most, so a tablet's
    // lines stay readable while the picture spans the screen.
    private static final int BLEED_TEXT_MAX_WIDTH_DP = 560;
    // The app open ad's bar back to the game, as AdMob draws its own: white,
    // its left end round, running off the screen's right edge.
    // The way out, so a full finger target.
    private static final int APP_BAR_HEIGHT_DP = 48;
    private static final int APP_BAR_GAP_DP = 8;
    // Between the game's name and Continue to app.
    private static final int APP_BAR_NAME_GAP_DP = 24;
    private static final int APP_BAR_ICON_DP = 26;
    private static final String APP_BAR_CONTINUE_TEXT = "Continue to app";
    private static final String CLOSE_CONTENT_DESCRIPTION = "Close ad";

    // The 120dp floor holds for every creative, a still picture's as well as
    // a video's: the SDK flags a smaller MediaView as an implementation
    // problem and warns that such slots will be demonetized. A slot too small
    // for it takes a layout without the picture.
    private static final float MIN_VIDEO_MEDIA_SIZE_DP = 120f;
    private static final float MIN_IMAGE_MEDIA_SIZE_DP = 120f;
    // A probe value only, for the fit checks that need a number. It never
    // frames the media: a creative with an unreported ratio is handed the
    // whole band and renders inside it as it pleases.
    private static final float DEFAULT_MEDIA_ASPECT_RATIO = 1f;
    private static final float DEFAULT_HEIGHT_RATIO = 0.5f;
    // 20dp a side spent 40dp of every screen on nothing the ad needed.
    // Five rather than eight because the side layout with content below
    // wears this padding three times across - one panel edge, the media's
    // left, the other panel edge - and it exists to make the plain side
    // layout prettier. Three fives are less than the two eights the plain
    // one used to spend, so the nicer arrangement can never be the one
    // that runs out of width first.
    private static final int HORIZONTAL_PADDING_DP = 5;
    // The stacked half panel's text and button, in from the screen's edges
    // as a page's text is; the picture still runs edge to edge. The side
    // layouts keep HORIZONTAL_PADDING_DP (B8).
    private static final int STACKED_TEXT_INSET_DP = 16;
    // Under the half panel's button, off the screen's bottom edge.
    private static final int HALF_BOTTOM_PADDING_DP = 8;
    // The half panel's controls, in from its sides, and the badge and
    // AdChoices mark off its corners where there is no picture to carry them.
    private static final int HALF_MARK_INSET_DP = 6;
    private static final int CONTROL_STRIP_HEIGHT_DP = 30;
    // The half panel's strip: the 30dp disc with 6dp above and below, so a
    // control neither meets the panel's edge nor touches the picture.
    private static final int HALF_CONTROL_STRIP_DP = 42;
    private static final int CONTROL_GAP_DP = 2;
    // Clear of the AdChoices mark (15dp, 6dp in from the corner) by the
    // close's own reach past its disc and 3dp more, so a press meant for
    // one never lands on the other.
    private static final int RIGHT_CONTROL_INSET_DP = 33;
    // A control's disc is 30dp; a press counts this far around it, so it
    // answers a 48dp finger target as the platform asks.
    private static final int CONTROL_TOUCH_SLOP_DP = 9;
    // How long a let-through close press waits for the SDK's click report.
    // When it runs out the press is forgotten, not acted on: the SDK did not
    // accept the touch, so as far as the ad is concerned nothing was pressed,
    // and a button that missed should do nothing. A second of silence
    // followed by the ad vanishing by itself reads as a glitch instead.
    //
    // The flag still has to expire, or a genuine call to action tap much
    // later would inherit it and close the ad on someone who meant to follow
    // it.
    private static final long CLOSE_REPORT_TIMEOUT_MS = 1000L;
    // A missed press doing nothing is right until it is every press. The
    // unaccepted area is small enough that landing in it three times running
    // is close to impossible - and a player pressing a third time is saying
    // plainly that they want out, whatever the SDK thinks. That press still
    // waits its bound, so a click that does arrive is not thrown away, and
    // only then closes. The button can never become a trap.
    private static final int CLOSE_PRESSES_BEFORE_GIVING_UP = 3;
    // And how long a REPORTED click then waits for the redirect to actually
    // take the screen. Longer than the first, because by this point a click
    // is certain and only the browser is late. It ends the wait rather than
    // deciding it: whatever opened either arrived or is not coming.
    private static final long REDIRECT_TIMEOUT_MS = 3000L;
    // The veil between the picture and the text in the scrim layout. The
    // same value the rect ad uses, because it is the same idea and a reader
    // moving between the two formats should not see two different greys.
    private static final String SCRIM_VEIL_COLOR = "#B3000000";
    // How many measure-and-adjust rounds the pre-layout settle may take.
    // It converges in two on every ad measured so far; the cap is here so a
    // creative nobody anticipated cannot spin.
    private static final int SETTLE_MAX_PASSES = 4;
    private static final int MIN_BADGE_SIZE_PX = 15;
    private static final int ATTRIBUTION_WIDTH_DP = 26;
    private static final int ATTRIBUTION_HEIGHT_DP = 18;
    private static final int AD_CHOICES_WIDTH_PX = 76;
    private static final int AD_CHOICES_SIZE_DP = 24;
    private static final int MIN_ICON_SIZE_DP = 36;
    private static final int MAX_ICON_SIZE_DP = 64;
    private static final int ICON_GAP_DP = 8;
    // The floor gives ground in a tight panel; the ceiling is what a roomy
    // panel is allowed to spend, and lowering it would take from exactly the
    // case that has room to spare.
    private static final int MIN_CALL_TO_ACTION_HEIGHT_DP = 44;
    private static final int MAX_CALL_TO_ACTION_HEIGHT_DP = 56;
    private static final int MIN_IDENTITY_VERTICAL_PADDING_DP = 2;
    private static final int MAX_IDENTITY_VERTICAL_PADDING_DP = 6;
    private static final int MIN_BODY_BOTTOM_PADDING_DP = 4;
    private static final int MAX_BODY_BOTTOM_PADDING_DP = 8;
    private static final int MIN_HEADLINE_TEXT_SIZE_SP = 15;
    private static final int MAX_HEADLINE_TEXT_SIZE_SP = 20;
    private static final int MIN_ADVERTISER_TEXT_SIZE_SP = 12;
    private static final int MAX_ADVERTISER_TEXT_SIZE_SP = 14;
    private static final int MIN_BODY_TEXT_SIZE_SP = 13;
    private static final int MAX_BODY_TEXT_SIZE_SP = 16;
    private static final int MIN_CALL_TO_ACTION_TEXT_SIZE_SP = 14;
    private static final int MAX_CALL_TO_ACTION_TEXT_SIZE_SP = 17;
    private static final int COLLAPSIBLE_HEADLINE_MAX_LINE_COUNT = 2;
    private static final int MIN_BODY_LINE_COUNT = 1;
    private static final int MAX_BODY_LINE_COUNT = 3;
    private static final int RESPONSIVE_SCALE_SEARCH_ITERATIONS = 8;
    private static final float MAX_ICON_ROW_WIDTH_RATIO = 0.33f;
    // A line that moves is harder to read than one that sits still, so text
    // that has to scroll never does it at the size that failed to fit whole.
    private static final float MARQUEE_TEXT_SHRINK = 0.8f;
    private static final long MILLIS_PER_SECOND = 1000L;
    private static final long COUNTDOWN_INTERVAL_MS = 250L;
    // A portrait creative in a centered column leaves dead gutters both sides.
    // Below this aspect the media takes the panel's full height on the left
    // and everything else moves into a rail beside it - provided the media
    // still meets its policy minimum and the rail keeps enough width to read.
    // Strictly portrait only: a square creative belongs stacked on top -
    // MEDIA_TOP wins there - and only media clearly taller than wide earns
    // the rail beside it.
    private static final float SIDE_MEDIA_MAX_ASPECT = 0.85f;
    // Just over half: enough width for a portrait creative to stay
    // imposing, while the rail keeps room for whole text and a real button.
    private static final float SIDE_MEDIA_MAX_WIDTH_SHARE = 0.56f;
    private static final int SIDE_MEDIA_MIN_RAIL_DP = 120;
    // The rail's side padding follows the rail's width; a narrow column
    // cannot afford the full HORIZONTAL_PADDING_DP on each side.
    private static final float RAIL_SIDE_PADDING_RATIO = 0.03f;
    private static final int RAIL_MIN_SIDE_PADDING_DP = 2;
    private static final int RAIL_ICON_GAP_DP = 4;
    // Text no larger than the rail can wear: the scale ceiling follows the
    // rail's width, reaching full size only in a genuinely wide rail.
    private static final int RAIL_SCALE_CAP_MIN_WIDTH_DP = 160;
    private static final int RAIL_SCALE_CAP_RANGE_DP = 240;
    // Whole text before size, size before scrolling: how many size steps
    // the rail trades away before a line is allowed to scroll.
    private static final int RAIL_FULL_TEXT_STEPS = 3;
    private static final float RAIL_FULL_TEXT_SCALE_STEP = 0.34f;
    private static final int RAIL_HEADLINE_MAX_LINE_COUNT = 4;
    private static final int SIDE_BELOW_BODY_MAX_LINE_COUNT = 2;
    private static final float BODY_LINE_HEIGHT_RATIO = 1.5f;
    // The seam between the media and the identity row below it - a hair,
    // not a margin.
    private static final int MEDIA_LOWER_SEAM_DP = 8;
    // A panel meaningfully taller than wide reads as a page: media belongs
    // stacked on top of it, not beside it. Side media only suits panels near
    // screen proportions, where a portrait creative would otherwise sit in a
    // centered column between dead gutters.
    private static final float SIDE_MEDIA_MAX_PANEL_HEIGHT_RATIO = 1.3f;
    // The rail is narrow by construction, so the icon never shares a line
    // with text there: it stands alone and the identity stack follows below
    // at the rail's full width.
    private static final float SIDE_RAIL_ICON_WIDTH_RATIO = 0.3f;
    // Slack absorption in the rail: how many whole body lines it may take,
    // and how far the icon may grow, before free height is left alone.
    private static final int RAIL_BODY_MAX_LINE_COUNT = 6;
    private static final int RAIL_ICON_GROWTH_STEP_DP = 8;
    private static final float RAIL_ICON_MAX_WIDTH_RATIO = 0.6f;
    // Extra ground the strip-avoiding layout may give before it surrenders
    // and lets the corner controls overlay the media instead.
    private static final int AVOID_CALL_TO_ACTION_HEIGHT_DP = 36;
    // Ratio first: the panel is the height the game asked for. Media needs its
    // policy minimum plus a usable row of content under it; a panel that
    // cannot host that drops the media rather than growing past the request,
    // and only a panel under the absolute floor is ever grown - minimally.
    private static final int AVOID_ICON_SIZE_DP = 28;
    private static final int MIN_MEDIA_LOWER_CONTENT_DP = 88;
    private static final int MIN_PANEL_HEIGHT_DP = 48;
    // Below this the panel is a strip, and the strip is one row: icon and
    // headline sharing the line with the call to action, the corner controls
    // overlaid at its ends, the headline scrolling when long.
    private static final int TICKER_MAX_PANEL_DP = 120;
    private static final int TICKER_CTA_MIN_HEIGHT_DP = 32;

    private final NativeAd nativeAd;
    private long countDownRemainingMs;
    private boolean sideMediaLayout;
    private int sideMediaWidthPx;
    // What the media's left edge costs the row: 0 while the picture bleeds,
    // the side padding once something stands under it.
    private int sideRowLeftInsetPx;
    private boolean mediaAvoidsControlStrip;
    private boolean mediaAspectReported;
    private boolean mediaAboveIdentity;
    private int sideRailHeightPx;
    private boolean sideBodySpilled;
    private boolean controlAvoidanceActive;
    private LinearLayout avoidanceColumn;
    private MediaView avoidanceMediaView;
    private TextView avoidanceBodyView;
    private ImageView avoidanceIconView;
    private Button avoidanceCallToAction;
    private LinearLayout avoidanceIdentityRow;
    private boolean chromeTrimmedForMedia;
    // The fit pipeline SPENT the icon - pinned it to the avoidance floor to
    // buy room for the media - and certified the fit with that size. The
    // settle's icon-follows-text rule yields to this: growing the icon back
    // is what used to invalidate the certificate behind the pipeline's back
    // and demote creatives that had already fit. Owner's call, 2026-08:
    // the spending decision wins.
    private boolean iconSpentForFit;
    private boolean scrimLayoutActive;
    // The panel the last-resort layout - the scrim - has to fit
    // inside, kept for the check the settle runs after the text has taken
    // its final shape.
    private int lastResortPanelHeight;
    private boolean mediaIsVideo;
    // Every layout was tried and none can show this creative in this panel.
    // The ad is not renderable here and the only answer is a different ad.
    private boolean layoutUnrenderable;
    // The last avoidance pass could not place the picture anywhere inside
    // the panel. The panel cannot grow, so the layout changes instead.
    private boolean avoidanceFoundNoBand;
    // What the pre-layout settle has to reach. These used to be captured by
    // layout-change listeners, which is exactly what made the panel move
    // after it was already on screen; the settle needs them from outside.
    private LinearLayout settleColumn;
    private LinearLayout settleIdentityRow;
    private LinearLayout settleIdentityText;
    private ImageView settleIcon;
    private TextView settleHeadline;
    private TextView settleBody;
    private TextView settleAdvertiser;
    private NativeAdStarRatingView settleStarRating;
    private int settleColumnWidth;
    private float settleDensity;
    // Headline, body, advertiser. Each is shrunk at most once, the same
    // bound the listener carried, and the reason the settle loop ends.
    private final boolean[] textShrunk = new boolean[3];
    private float avoidanceMediaAspect;
    private int avoidanceMinimumMediaSize;
    private int avoidanceHorizontalPadding;
    private int avoidancePanelHeight;
    private final boolean closeOnLeft;
    private final boolean redirectOnClose;
    private final boolean timerOnLeft;
    private final boolean fullScreen;
    private final int backgroundColor;
    private final int layout;
    // The full-screen card replaces the adaptive pipeline outright: one
    // arrangement fitted here, nothing negotiated after the first layout.
    private boolean cardLayout;
    // The interstitial and the end card: the card's arrangement spread over
    // the whole screen on the theme's surface, as the networks' own
    // full-screen ads; only the app open ad keeps the card over the game.
    private boolean bleedLayout;
    private int cardWidthPx;
    private int cardHeightPx;
    private int cardTopReservePx;
    // The column each control stands in at the side of a landscape face.
    private int cardControlColumnPx;
    private View appBar;
    private View appBarSpinner;
    private View appBarReady;
    // What FitCard needs once the assets are bound: the arrangement is
    // decided before, the sizes only after, as they depend on the text.
    private LinearLayout cardFitColumn;
    private MediaView cardMedia;
    private TextView cardBody;
    private ImageView cardIcon;
    private int cardFitWidthPx;
    private int cardMaxHeightPx;
    private int cardMinimumMediaPx;
    private int cardNaturalMediaHeightPx;
    // The portrait picture's inset from the card's sides (CardMediaInsetPx).
    private int cardMediaInsetPx;
    private boolean cardLandscape;
    // The landscape picture's height: the creative's own at its width, so a
    // player never letterboxes it in black.
    private int cardLandscapeMediaHeightPx;
    // The landscape full-screen face's picture width: the near control
    // stands right of it (FollowCardMedia).
    private int cardLandscapeMediaWidthPx;
    // The badge, kept on the picture's top-left corner (FollowCardMedia).
    private TextView cardAttribution;
    // The stacked half panel's column and picture, which FollowCardMedia
    // follows as it follows the card's.
    private LinearLayout halfColumn;
    private MediaView halfMedia;
    private final Rect cardMarkPadding = new Rect();
    // What BuildCard arranged the card from, kept so a window that changes
    // size while the ad shows - a foldable opening, split screen, a turn -
    // gets the card arranged and fitted again (RefitCard).
    private int cardBuiltWidthPx;
    private int cardBuiltHeightPx;
    // The cutout's bands the card was arranged inside: insets that arrive
    // later with other bands arrange it again.
    private final Rect cardBuiltCutout = new Rect();
    private LinearLayout cardColumn;
    private float cardMediaAspectRatio;
    private LinearLayout cardIdentityText;
    private TextView cardHeadline;
    private TextView cardAdvertiser;
    private NativeAdStarRatingView cardStarRating;
    private Button cardCallToAction;
    // The end card's store figures beside the rating - the price and the
    // store - and the dimmed, blurred copy of the creative its banner stands
    // on (ArrangeEndCardListing, PlaceEndCardBackdrop).
    private TextView cardPrice;
    private TextView cardStore;
    private View cardBackdrop;
    private boolean cardBodyBound;
    private final Runnable onClose;
    private CountDownTimer timer;
    // Whether the visibility hook stopped a running countdown; only then
    // does becoming visible restart one.
    private boolean countdownPausedWhileHidden;
    private SingleClickNativeAdContainer nativeAdContainer;
    private NativeAdView nativeAdView;
    // Every touch on the panel is swallowed while it slides, the close
    // button's included: a panel in motion is not yet - or no longer - there.
    private boolean touchLocked;
    private TextView countdown;
    private TextView close;
    // The tone the controls were last given (StyleControls).
    private Boolean controlsDark;
    private boolean closeSlopTouch;
    private Button callToActionView;
    private boolean released;
    // A press on the close button that has been let through to the ad view
    // and is waiting for the SDK to say it counted.
    private boolean closePressPending;
    // The SDK has counted that press and something is opening on top of us.
    // The ad stays up through this: closing on the click report alone left a
    // gap where the ad was already gone and the browser had not arrived, and
    // the game showed through it. Closing when the redirect actually takes
    // the screen means nobody ever sees the swap.
    private boolean redirectPending;
    private final Runnable closeFallbackRunnable = this::ForgetClosePress;
    // Close presses the SDK never answered. A click arriving for one of them
    // clears the run, so only misses count toward the net.
    private int unansweredClosePresses;
    private final Runnable redirectFallbackRunnable =
            this::RunCloseFromRedirect;

    // One constructor only, and it takes milliseconds. A seconds-taking
    // twin used to sit here and roll the close button's side itself; an
    // int argument then bound to it instead of to this one and re-rolled a
    // side that had already been decided, which is how the collapsible
    // ended up 25/75 instead of 50/50. The side arrives resolved now, from
    // OverlayAdStyle, and there is nothing left to pick by accident.
    OverlayAdContentView(
            Context context
          , NativeAd nativeAd
          , long countDownRemainingMs
          , boolean closeOnLeft
          , boolean timerOnLeft
          , boolean fullScreen
          , int backgroundColor
          , boolean redirectOnClose
          , int requestedPanelHeight
          , int layout
          , Runnable onClose) {
        super(context);
        this.nativeAd = nativeAd;
        this.countDownRemainingMs = Math.max(0L, countDownRemainingMs);
        this.closeOnLeft = closeOnLeft;
        this.timerOnLeft = timerOnLeft;
        this.fullScreen = fullScreen;
        this.backgroundColor = backgroundColor;
        this.redirectOnClose = redirectOnClose;
        this.layout = layout;
        this.onClose = onClose;
        Build(requestedPanelHeight);
        // Settle first, inset second - the order the device validated.
        // The settle plans against displayMetrics.heightPixels, which on a
        // cutout device already excludes the inset, so the plan and the
        // first real layout agree without either knowing about the other.
        SettleContentBeforeLayout();
        ApplyFullScreenContentInset();
    }

    static int ResolveInitialPanelHeight(
            Context context
          , boolean fullScreen
          , float heightRatio) {
        if (fullScreen) {
            return context.getResources()
                    .getDisplayMetrics().heightPixels;
        }
        return ResolveHalfScreenPanelHeight(context, heightRatio);
    }

    // The one place a half-screen height is decided, and anything standing
    // behind the panel has to take its height from here too - published as
    // OverlayAd.ResolvePanelHeight - rather than measure the screen itself.
    //
    // A window behind the ad once computed its own from decorView.getHeight()
    // while the ad used displayMetrics.heightPixels. Those are the same number
    // on a plain 1080x1920 emulator, which is why it looked fine there - and
    // they differ on a real device with a cutout or a gesture bar. The
    // surplus showed up as an empty black band above the ad, because both
    // windows sit at Gravity.BOTTOM and only the taller one has anything left
    // over.
    static int ResolveHalfScreenPanelHeight(
            Context context
          , float heightRatio) {
        DisplayMetrics displayMetrics =
                context.getResources().getDisplayMetrics();
        float density = displayMetrics.density;

        float ratio = heightRatio;
        if (Float.isNaN(ratio) || Float.isInfinite(ratio)) {
            Log.w(TAG, "heightRatio is not finite; using 0.5");
            ratio = DEFAULT_HEIGHT_RATIO;
        }
        ratio = Math.max(0f, Math.min(1f, ratio));

        int requestedHeight =
                (int) (displayMetrics.heightPixels * ratio);
        requestedHeight = Math.max(
                requestedHeight
              , (int) (MIN_PANEL_HEIGHT_DP * density));
        return Math.min(displayMetrics.heightPixels, requestedHeight);
    }

    void Release() {
        if (released) return;
        released = true;

        closePressPending = false;
        unansweredClosePresses = 0;
        redirectPending = false;
        if (close != null) {
            close.removeCallbacks(closeFallbackRunnable);
            close.removeCallbacks(redirectFallbackRunnable);
        }

        if (timer != null) {
            timer.cancel();
            timer = null;
        }

        NativeAdView currentNativeAdView = nativeAdView;
        nativeAdView = null;
        if (currentNativeAdView != null) {
            try {
                currentNativeAdView.destroy();
            } catch (RuntimeException exception) {
                Log.e(TAG, "Failed to release native ad view", exception);
            }
        }

        removeAllViews();
        nativeAdContainer = null;
        countdown = null;
        close = null;
    }

    void SetTouchLocked(boolean locked) {
        touchLocked = locked;
    }

    @Override
    public boolean dispatchTouchEvent(MotionEvent event) {
        if (touchLocked) return true;

        // A press just around the close disc is the close's: the whole
        // gesture goes to it, landing on its centre.
        int action = event.getActionMasked();
        if (action == MotionEvent.ACTION_DOWN) {
            closeSlopTouch = close != null
                    && close.getVisibility() == View.VISIBLE
                    && close.getParent() == this
                    && !InsideControl(close, event, 0)
                    && InsideControl(
                            close
                          , event
                          , Math.round(
                                CONTROL_TOUCH_SLOP_DP
                                        * getResources().getDisplayMetrics().density));
        }
        if (closeSlopTouch && close != null) {
            MotionEvent forwarded = MotionEvent.obtain(event);
            forwarded.setLocation(close.getWidth() / 2f, close.getHeight() / 2f);
            boolean handled = close.dispatchTouchEvent(forwarded);
            forwarded.recycle();
            if (action == MotionEvent.ACTION_UP
                    || action == MotionEvent.ACTION_CANCEL) {
                closeSlopTouch = false;
            }
            return handled;
        }
        return super.dispatchTouchEvent(event);
    }

    private static boolean InsideControl(View control, MotionEvent event, int slop) {
        float x = event.getX();
        float y = event.getY();
        return x >= control.getLeft() + control.getTranslationX() - slop
                && x < control.getRight() + control.getTranslationX() + slop
                && y >= control.getTop() + control.getTranslationY() - slop
                && y < control.getBottom() + control.getTranslationY() + slop;
    }

    void OnPresented() {
        OnPresented(countDownRemainingMs);
    }

    void OnPresented(long remainingMs) {
        if (released) return;
        countDownRemainingMs = Math.max(0L, remainingMs);
        StartCountdown();
    }

    // Losing the window's focus while a redirect is pending IS the redirect
    // arriving - nothing else takes the screen off an ad the player just
    // sent themselves away from. The ad closes underneath it, unseen.
    //
    // This hook covers the ACTIVITY path only. It was written believing it
    // also covered the Dialog path - "a view gets window focus changes
    // whichever window it sits in" - and that is false for the one window
    // that matters: OverlayAdPresentation adds FLAG_NOT_FOCUSABLE to the
    // half-screen window, and a window that never takes focus never reports
    // losing it. Measured: across a whole session of half-screen shows this
    // hook fired exactly once, for a full-screen ad. The half-screen path is
    // covered by onWindowVisibilityChanged below.
    //
    // Home, a call or the notification shade land here too, but only matter
    // while redirectPending, which needs the close button pressed AND the
    // SDK's click report within the same breath. Landing in that window by
    // accident would still end in the close the player asked for.
    @Override
    public void onWindowFocusChanged(boolean hasWindowFocus) {
        super.onWindowFocusChanged(hasWindowFocus);
        if (!hasWindowFocus && redirectPending) RunCloseFromRedirect();
    }

    // The half-screen path's signal, and the reason the redirect used to
    // leave the ad standing: that window is not focusable, so the hook above
    // is deaf there and only the 3s fallback ever closed it - long enough
    // for the player to come back from the browser and still find the ad.
    //
    // Window VISIBILITY does not need focus. When the browser takes the
    // screen the host Activity stops, every window on its token goes
    // invisible, and that reaches here whichever window this view sits in -
    // which is what the focus hook was wrongly believed to do.
    @Override
    protected void onWindowVisibilityChanged(int visibility) {
        super.onWindowVisibilityChanged(visibility);
        if (visibility != View.VISIBLE && redirectPending) {
            RunCloseFromRedirect();
        }
        // The countdown buys watch time, and a backgrounded ad is not being
        // watched. iOS has held the remaining time across resign-active
        // from the start; the Dialog window here never gets an Activity
        // pause of its own, so visibility - which every window gets -
        // carries the duty for both paths. countDownRemainingMs is kept
        // current by the timer's own ticks, so canceling freezes it.
        if (visibility != View.VISIBLE) {
            if (timer != null) {
                timer.cancel();
                timer = null;
                countdownPausedWhileHidden = true;
            }
        } else if (countdownPausedWhileHidden) {
            countdownPausedWhileHidden = false;
            if (!released) StartCountdown();
        }
    }

    // The Activity path's own signal. Redundant with the focus change above
    // rather than alternative to it - RunCloseFromRedirect is idempotent -
    // and it stays because a full-screen ad could be given RedirectOnClose
    // tomorrow without anyone rechecking which hook fires.
    void OnPaused() {
        if (redirectPending) {
            RunCloseFromRedirect();
            return;
        }

        if (timer == null) return;
        timer.cancel();
        timer = null;
    }

    void CommitAdClick() {
        if (nativeAdContainer != null) {
            nativeAdContainer.CommitAdClick();
        }
        // Only a click that began on the close button closes the ad. A
        // genuine tap on the call to action leaves the flag false, so the
        // player who meant to follow the ad comes back to it still open.
        if (!closePressPending) return;

        closePressPending = false;
        unansweredClosePresses = 0;
        if (close != null) close.removeCallbacks(closeFallbackRunnable);

        // With no button left to post on there is no way to bound the wait,
        // so close now rather than arm a state nothing can clear.
        if (released || close == null) {
            if (!released && onClose != null) onClose.run();
            return;
        }

        // Reported, not yet arrived: hand the wait over to the window losing
        // focus, with a bound in case nothing ever comes to the front.
        redirectPending = true;
        close.postDelayed(redirectFallbackRunnable, REDIRECT_TIMEOUT_MS);
    }

    private void ArmCloseFromPress() {
        // redirectPending: a second press while the redirect is on its way
        // must not restart the memory and hand the redirect's own click to a
        // later press. The press is ignored; the redirect, or its own bound,
        // still ends the show.
        //
        // A press while an earlier one is merely WAITING is a different
        // thing, and it re-arms: it counts toward the net and starts its own
        // wait. Turning it away instead would have made the net ask for
        // three presses a second apart, and the tap-tap-tap anyone does at a
        // button that ignored them counts as one.
        if (released || redirectPending) return;

        // Below the net the newest press owns the claim and the wait runs
        // from it, so the press that trips the net gets its own full second
        // to earn a click - anchoring the wait to the FIRST press of the run
        // would leave the third with whatever was left of it, often less
        // than the SDK needs, and close the ad bare on a press that was
        // about to work.
        //
        // Once the net has tripped, the deadline it set is a promise and
        // stands. A later press still counts, and can still earn a click -
        // that click cancels the deadline and closes on the redirect
        // instead, the better ending - but it cannot PUSH the deadline away,
        // or someone hammering a button that keeps ignoring them would never
        // reach the close they are asking for.
        boolean netAlreadyTripped = closePressPending
                && unansweredClosePresses >= CLOSE_PRESSES_BEFORE_GIVING_UP;

        closePressPending = true;
        unansweredClosePresses++;
        if (netAlreadyTripped) return;

        close.removeCallbacks(closeFallbackRunnable);
        close.postDelayed(closeFallbackRunnable, CLOSE_REPORT_TIMEOUT_MS);
    }

    // The press was never reported as a click, so it was never a press as
    // far as the ad is concerned. Nothing closes: the chip is only
    // decoration, and a decoration that was missed does nothing. All that
    // ends here is this press's claim on the next click to arrive - unless
    // the net below has run out of patience.
    private void ForgetClosePress() {
        if (!closePressPending) return;

        closePressPending = false;
        if (close != null) close.removeCallbacks(closeFallbackRunnable);

        if (unansweredClosePresses < CLOSE_PRESSES_BEFORE_GIVING_UP) return;
        if (released) return;

        if (onClose != null) onClose.run();
    }

    // The click was counted but nothing ever came to the front - a slow
    // network, or something the SDK opened inside this app. The close button
    // still has to close.
    private void RunCloseFromRedirect() {
        if (!redirectPending) return;

        redirectPending = false;
        if (close != null) close.removeCallbacks(redirectFallbackRunnable);
        if (released) return;

        if (onClose != null) onClose.run();
    }

    private void Build(int requestedPanelHeight) {
        Context context = getContext();
        DisplayMetrics displayMetrics =
                context.getResources().getDisplayMetrics();
        float density = displayMetrics.density;
        com.google.android.gms.ads.MediaContent mediaContent =
                nativeAd.getMediaContent();
        boolean hasVideoContent =
                mediaContent != null
                        && mediaContent.hasVideoContent();
        mediaIsVideo = hasVideoContent;
        Drawable mainMediaImage =
                mediaContent != null
                        ? mediaContent.getMainImage()
                        : null;
        Drawable fallbackMediaImage =
                !hasVideoContent
                        && mainMediaImage == null
                        ? FindFallbackMediaImage()
                        : null;
        boolean hasDisplayableMedia =
                hasVideoContent
                        || mainMediaImage != null
                        || fallbackMediaImage != null;
        int minimumMediaSize =
                (int) Math.ceil(
                        (hasVideoContent
                                ? MIN_VIDEO_MEDIA_SIZE_DP
                                : MIN_IMAGE_MEDIA_SIZE_DP)
                                * density);
        float mediaAspectRatio =
                GetMediaAspectRatio(fallbackMediaImage);
        int controlStripHeight = (int) ((fullScreen
                        ? CONTROL_STRIP_HEIGHT_DP
                        : HALF_CONTROL_STRIP_DP)
                * density);
        boolean panelHostsMedia = fullScreen
                || requestedPanelHeight
                        >= minimumMediaSize
                                + (int) (MIN_MEDIA_LOWER_CONTENT_DP
                                        * density);
        // With the media dropped - or a creative that never had any - the
        // registered icon stands in for it, the way the rect ad already
        // works: it leaves the identity row and takes the media's band.
        boolean tickerLayout = !fullScreen
                && requestedPanelHeight
                        < (int) (TICKER_MAX_PANEL_DP * density);
        boolean iconHero = !fullScreen
                && !tickerLayout
                && (!hasDisplayableMedia || !panelHostsMedia)
                && nativeAd.getIcon() != null
                && nativeAd.getIcon().getDrawable() != null;
        hasDisplayableMedia = hasDisplayableMedia
                && panelHostsMedia
                && !tickerLayout;
        // A video has no way down to the icon or the ticker: Google serves
        // it only into a MediaView of 120dp or more and flags it without
        // one. A panel that cannot hold one drops the creative.
        if (hasVideoContent && !hasDisplayableMedia) {
            layoutUnrenderable = true;
        }
        // A picture directly above the identity row is its own separator;
        // the row's top padding would only widen a seam already there.
        mediaAboveIdentity = hasDisplayableMedia;
        int sidePanelHeight = fullScreen
                ? displayMetrics.heightPixels
                : requestedPanelHeight;
        cardLayout = fullScreen;
        bleedLayout = fullScreen && layout != LAYOUT_APP_OPEN;
        sideMediaLayout = !cardLayout
                && hasDisplayableMedia
                && ShouldUseSideMedia(
                        displayMetrics
                      , mediaAspectRatio
                      , sidePanelHeight);
        boolean stackedHalf = !cardLayout && !sideMediaLayout && !tickerLayout;
        int horizontalPadding = (int) ((stackedHalf
                        ? STACKED_TEXT_INSET_DP
                        : HORIZONTAL_PADDING_DP)
                * density);

        ConfigureBackground();

        nativeAdView = new NativeAdView(context);
        LinearLayout contentColumn = new LinearLayout(context);
        contentColumn.setOrientation(LinearLayout.VERTICAL);
        contentColumn.setGravity(Gravity.CENTER);
        contentColumn.setPadding(
                horizontalPadding
              , controlStripHeight
              , horizontalPadding
              , stackedHalf ? (int) (HALF_BOTTOM_PADDING_DP * density) : 0);
        // The media is the one child allowed to ignore the side padding: it
        // bleeds edge to edge through negative margins while every text stays
        // inset, so the padding cannot cost the picture anything.
        contentColumn.setClipToPadding(false);

        MediaView mediaView = new MediaView(context);
        mediaView.setMinimumWidth(minimumMediaSize);
        mediaView.setMinimumHeight(minimumMediaSize);
        // Deliberately black, not the panel color: when a creative reports
        // one ratio but renders less inside it, the black ground makes the
        // shortfall visible instead of hiding it.
        mediaView.setBackgroundColor(Color.BLACK);
        mediaView.setImageScaleType(ImageView.ScaleType.FIT_CENTER);
        LinearLayout.LayoutParams mediaCreateParams =
                new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , minimumMediaSize);
        mediaCreateParams.leftMargin = -horizontalPadding;
        mediaCreateParams.rightMargin = -horizontalPadding;
        mediaView.setLayoutParams(mediaCreateParams);
        if (hasVideoContent || mainMediaImage != null) {
            mediaView.setMediaContent(mediaContent);
        } else if (fallbackMediaImage != null) {
            ImageView fallbackImageView = new ImageView(context);
            fallbackImageView.setScaleType(ImageView.ScaleType.FIT_CENTER);
            fallbackImageView.setImageDrawable(fallbackMediaImage);
            mediaView.addView(
                    fallbackImageView
                  , new FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , ViewGroup.LayoutParams.MATCH_PARENT));
        }

        LinearLayout identityRow = new LinearLayout(context);
        identityRow.setOrientation(LinearLayout.HORIZONTAL);
        identityRow.setGravity(Gravity.CENTER_VERTICAL);
        identityRow.setPadding(
                0
              , (int) ((fullScreen
                        ? MAX_IDENTITY_VERTICAL_PADDING_DP
                        : MIN_IDENTITY_VERTICAL_PADDING_DP)
                        * density)
              , 0
              , (int) (MIN_IDENTITY_VERTICAL_PADDING_DP * density));
        identityRow.setLayoutParams(
                new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , ViewGroup.LayoutParams.WRAP_CONTENT));

        ImageView icon = new ImageView(context);
        icon.setScaleType(ImageView.ScaleType.CENTER_CROP);
        RoundCorners(icon, ICON_CORNER_RATIO);
        LinearLayout.LayoutParams iconLayoutParams =
                new LinearLayout.LayoutParams(
                        (int) (MIN_ICON_SIZE_DP * density)
                      , (int) (MIN_ICON_SIZE_DP * density));
        iconLayoutParams.setMarginEnd((int) (ICON_GAP_DP * density));
        // A white tile under the icon, as a launcher draws one: an icon with
        // a transparent ground otherwise loses its dark marks on a dark
        // surface.
        if (!iconHero) icon.setBackgroundColor(Color.WHITE);
        if (iconHero) {
            icon.setScaleType(ImageView.ScaleType.FIT_CENTER);
        } else if (!sideMediaLayout) {
            identityRow.addView(icon, iconLayoutParams);
        }

        LinearLayout identityText = new LinearLayout(context);
        identityText.setOrientation(LinearLayout.VERTICAL);
        identityText.setGravity(Gravity.CENTER_VERTICAL);
        LinearLayout.LayoutParams identityTextLayoutParams =
                new LinearLayout.LayoutParams(
                        0
                      , ViewGroup.LayoutParams.WRAP_CONTENT
                      , 1f);

        TextView headline = new TextView(context);
        headline.setTextColor(Color.WHITE);
        headline.setTextSize(
                fullScreen
                        ? 18f
                        : MIN_HEADLINE_TEXT_SIZE_SP);
        headline.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        headline.setSingleLine(false);

        TextView advertiser = new TextView(context);
        advertiser.setTextColor(AdTheme.Grey(context));
        advertiser.setTextSize(MIN_ADVERTISER_TEXT_SIZE_SP);
        advertiser.setMaxLines(1);
        MarqueeWhenTooLong(advertiser);

        NativeAdStarRatingView starRating =
                new NativeAdStarRatingView(context);
        LinearLayout.LayoutParams starRatingLayoutParams =
                new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.WRAP_CONTENT
                      , ViewGroup.LayoutParams.WRAP_CONTENT);
        starRatingLayoutParams.gravity = Gravity.START;

        identityText.addView(headline);
        identityText.addView(advertiser);
        identityText.addView(starRating, starRatingLayoutParams);
        identityRow.addView(identityText, identityTextLayoutParams);

        TextView body = new TextView(context);
        body.setTextColor(AdTheme.Grey(context));
        body.setTextSize(MIN_BODY_TEXT_SIZE_SP);
        body.setMaxLines(MAX_BODY_LINE_COUNT);
        body.setPadding(
                0
              , 0
              , 0
              , (int) (MIN_BODY_BOTTOM_PADDING_DP * density));

        Button callToAction = new Button(context);
        StyleCallToAction(callToAction, density);
        callToAction.setTextSize(MIN_CALL_TO_ACTION_TEXT_SIZE_SP);
        callToAction.setSingleLine(false);
        callToAction.setMinHeight(
                (int) (MIN_CALL_TO_ACTION_HEIGHT_DP * density));
        callToAction.setMinimumHeight(
                (int) (MIN_CALL_TO_ACTION_HEIGHT_DP * density));

        LinearLayout sideRail = null;
        if (cardLayout) {
            BuildCard(
                    context
                  , displayMetrics
                  , density
                  , contentColumn
                  , hasDisplayableMedia ? mediaView : null
                  , mediaAspectRatio
                  , minimumMediaSize
                  , identityText
                  , icon
                  , headline
                  , advertiser
                  , starRating
                  , body
                  , callToAction);
        } else if (sideMediaLayout) {
            int sideMediaWidth = SideMediaWidth(
                    displayMetrics
                  , mediaAspectRatio
                  , sidePanelHeight);
            // The media column is exactly as tall as the creative can fill
            // at its own aspect, centered - never a band of dead backfill
            // painted to the panel's height.
            int sideMediaHeight = SideMediaHeight(
                    sideMediaWidth
                  , mediaAspectRatio
                  , sidePanelHeight);
            // Height the media leaves unused belongs to the content, not
            // to the column beside the picture: what fits under the row
            // moves there and spans the whole panel instead of being
            // squeezed into a narrow rail. The button goes first, the body
            // follows only when the leftover comfortably holds both.
            int seam = Math.round(MEDIA_LOWER_SEAM_DP * density);
            int belowCallToActionHeight =
                    Math.round(AVOID_CALL_TO_ACTION_HEIGHT_DP * density);
            int bodyLineHeight = Math.round(
                    MIN_BODY_TEXT_SIZE_SP
                            * BODY_LINE_HEIGHT_RATIO
                            * density);
            boolean callToActionBelow =
                    sidePanelHeight - sideMediaHeight
                            >= belowCallToActionHeight + seam;
            int sideLeftover = sidePanelHeight - sideMediaHeight;
            boolean bodyBelow = callToActionBelow
                    && sideLeftover >= belowCallToActionHeight
                            + 2 * bodyLineHeight
                            + 2 * seam;
            sideMediaWidthPx = sideMediaWidth;

            // Media bleeds to the edge only while nothing stands under it.
            // The moment something spills below, the two read as one column,
            // and a column with two different left edges reads as a mistake.
            //
            // That left edge is CARVED OUT of the budget, never added on top
            // of it. The budget is what the plain arrangement spends across:
            // the gap between picture and rail, plus the panel's right edge.
            // Two shares there, three here - the same pie either way - so
            // the rail measures the same to the pixel and a creative that
            // fitted plainly cannot fail to fit here.
            int railOuterPlain = Math.max(
                    0
                  , displayMetrics.widthPixels - sideMediaWidth);
            int sidePaddingBudget = 2 * SideRailPadding(
                    railOuterPlain
                  , horizontalPadding
                  , density);
            int railGap;
            int sideRowRightPadding;
            if (callToActionBelow) {
                int share = sidePaddingBudget / 3;
                int spare = sidePaddingBudget - 3 * share;
                // One pixel over goes to the seam between picture and rail,
                // where a closed gap reads worst; two go to the outer edges,
                // which must stay a matched pair.
                sideRowLeftInsetPx = share + (spare == 2 ? 1 : 0);
                sideRowRightPadding = sideRowLeftInsetPx;
                railGap = share + (spare == 1 ? 1 : 0);
            } else {
                sideRowLeftInsetPx = 0;
                railGap = sidePaddingBudget / 2;
                sideRowRightPadding = sidePaddingBudget - railGap;
            }

            contentColumn.setPadding(0, 0, 0, 0);
            LinearLayout sideRow = new LinearLayout(context);
            sideRow.setOrientation(LinearLayout.HORIZONTAL);
            sideRow.setPadding(sideRowLeftInsetPx, 0, 0, 0);
            LinearLayout.LayoutParams sideMediaParams =
                    new LinearLayout.LayoutParams(
                            sideMediaWidth
                          , sideMediaHeight);
            sideMediaParams.gravity = Gravity.CENTER_VERTICAL;
            sideRow.addView(mediaView, sideMediaParams);
            LinearLayout rail = new LinearLayout(context);
            rail.setOrientation(LinearLayout.VERTICAL);
            rail.setGravity(Gravity.CENTER_VERTICAL);
            // The corner controls and AdChoices live over the rail's top,
            // so the rail alone keeps the strip inset; the media needs none.
            // Its two side paddings are the shares taken above: the left one
            // is the seam beside the picture, the right one the panel's edge.
            int railOuterWidth = Math.max(
                    0
                  , displayMetrics.widthPixels
                            - sideMediaWidth
                            - sideRowLeftInsetPx);
            rail.setPadding(
                    railGap
                  , controlStripHeight
                  , sideRowRightPadding
                  , 0);
            // The rail is narrow, so the icon never shares a line with text
            // here: it stands alone and the identity stack follows below at
            // the rail's full width.
            if (!iconHero) {
                int railWidth = Math.max(
                        0
                      , railOuterWidth - railGap - sideRowRightPadding);
                int railIconSize = Math.max(
                        Math.round(MIN_ICON_SIZE_DP * density)
                      , Math.min(
                            Math.round(MAX_ICON_SIZE_DP * density)
                          , Math.round(
                                railWidth * SIDE_RAIL_ICON_WIDTH_RATIO)));
                LinearLayout.LayoutParams railIconParams =
                        new LinearLayout.LayoutParams(
                                railIconSize
                              , railIconSize);
                railIconParams.gravity = Gravity.CENTER_HORIZONTAL;
                railIconParams.bottomMargin =
                        Math.round(RAIL_ICON_GAP_DP * density);
                rail.addView(icon, railIconParams);
            }
            rail.addView(identityRow);
            if (!bodyBelow) rail.addView(body);
            if (!callToActionBelow) {
                rail.addView(
                        callToAction
                      , new LinearLayout.LayoutParams(
                            ViewGroup.LayoutParams.MATCH_PARENT
                          , ViewGroup.LayoutParams.WRAP_CONTENT));
            }
            sideRow.addView(
                    rail
                  , new LinearLayout.LayoutParams(
                        0
                      , ViewGroup.LayoutParams.MATCH_PARENT
                      , 1f));
            // With content spilling below, the row hugs the media instead
            // of claiming the whole panel.
            sideRailHeightPx = callToActionBelow
                    ? sideMediaHeight
                    : sidePanelHeight;
            contentColumn.addView(
                    sideRow
                  , new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , sideRailHeightPx));
            sideBodySpilled = bodyBelow;
            if (bodyBelow) {
                body.setMaxLines(SIDE_BELOW_BODY_MAX_LINE_COUNT);
                LinearLayout.LayoutParams belowBodyParams =
                        new LinearLayout.LayoutParams(
                                ViewGroup.LayoutParams.MATCH_PARENT
                              , ViewGroup.LayoutParams.WRAP_CONTENT);
                belowBodyParams.topMargin = seam;
                belowBodyParams.leftMargin = sideRowLeftInsetPx;
                belowBodyParams.rightMargin = sideRowRightPadding;
                contentColumn.addView(body, belowBodyParams);
            }
            if (callToActionBelow) {
                LinearLayout.LayoutParams belowCallToActionParams =
                        new LinearLayout.LayoutParams(
                                ViewGroup.LayoutParams.MATCH_PARENT
                              , ViewGroup.LayoutParams.WRAP_CONTENT);
                belowCallToActionParams.topMargin = seam;
                belowCallToActionParams.leftMargin = sideRowLeftInsetPx;
                belowCallToActionParams.rightMargin = sideRowRightPadding;
                contentColumn.addView(
                        callToAction
                      , belowCallToActionParams);
            }
            sideRail = rail;
        } else if (tickerLayout) {
            contentColumn.setPadding(0, 0, 0, 0);
            body.setVisibility(View.GONE);
            advertiser.setVisibility(View.GONE);
            starRating.setVisibility(View.GONE);
            headline.setSingleLine(true);
            headline.setMaxLines(1);
            callToAction.setMinHeight(
                    (int) (TICKER_CTA_MIN_HEIGHT_DP * density));
            callToAction.setMinimumHeight(
                    (int) (TICKER_CTA_MIN_HEIGHT_DP * density));

            LinearLayout ticker = new LinearLayout(context);
            ticker.setOrientation(LinearLayout.HORIZONTAL);
            ticker.setGravity(Gravity.CENTER_VERTICAL);
            int tickerControlReserve =
                    (int) ((CONTROL_STRIP_HEIGHT_DP
                                    + RIGHT_CONTROL_INSET_DP)
                            * density);
            ticker.setPadding(
                    tickerControlReserve
                  , 0
                  , tickerControlReserve
                  , 0);
            identityRow.setPadding(0, 0, 0, 0);
            ticker.addView(
                    identityRow
                  , new LinearLayout.LayoutParams(
                        0
                      , ViewGroup.LayoutParams.WRAP_CONTENT
                      , 1f));
            ticker.addView(
                    callToAction
                  , new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.WRAP_CONTENT
                      , ViewGroup.LayoutParams.WRAP_CONTENT));
            contentColumn.addView(
                    ticker
                  , new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , ViewGroup.LayoutParams.WRAP_CONTENT));
        } else {
            if (hasDisplayableMedia) {
                contentColumn.addView(mediaView);
            } else if (iconHero) {
                contentColumn.addView(
                        icon
                      , new LinearLayout.LayoutParams(
                            ViewGroup.LayoutParams.MATCH_PARENT
                          , 0
                          , 1f));
            }
            contentColumn.addView(identityRow);
            contentColumn.addView(body);
            contentColumn.addView(
                    callToAction
                  , new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , ViewGroup.LayoutParams.WRAP_CONTENT));
        }
        nativeAdView.addView(
                contentColumn
              , cardLayout
                        ? new FrameLayout.LayoutParams(
                              ViewGroup.LayoutParams.MATCH_PARENT
                            , cardHeightPx)
                        : new FrameLayout.LayoutParams(
                              ViewGroup.LayoutParams.MATCH_PARENT
                            , ViewGroup.LayoutParams.MATCH_PARENT));

        TextView attribution = CreateAttributionView(context, density);
        FrameLayout.LayoutParams attributionLayoutParams =
                new FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.WRAP_CONTENT
                      , ViewGroup.LayoutParams.WRAP_CONTENT
                      , Gravity.TOP | Gravity.START);
        // In the side-media layout even the badge steps off the picture:
        // it hugs the media's right edge, and the left corner control then
        // lines up beside it.
        if (cardLayout || stackedHalf) {
            // On the picture's corner, as the picture is the ad and the
            // panel around it is not (FollowCardMedia keeps it there).
            // Placed for the picture's top left until the picture is laid
            // out (FollowCardMedia).
            int badgeInset = CardMediaInsetPx(density);
            attributionLayoutParams.setMargins(badgeInset, badgeInset, 0, 0);
            AdTheme.ShapeBadge(attribution, true);
        } else if (sideMediaLayout) {
            attributionLayoutParams.leftMargin =
                    sideRowLeftInsetPx
                            + sideMediaWidthPx
                            + Math.max(1, (int) (CONTROL_GAP_DP * density));
        }
        nativeAdView.addView(attribution, attributionLayoutParams);

        int adChoicesReserveWidth = Math.max(
                (int) Math.ceil(AD_CHOICES_SIZE_DP * density)
              , AD_CHOICES_WIDTH_PX);
        int adChoicesReserveHeight = Math.max(
                (int) Math.ceil(AD_CHOICES_SIZE_DP * density)
              , MIN_BADGE_SIZE_PX);
        View adChoicesReserve = new View(context);
        adChoicesReserve.setClickable(false);
        adChoicesReserve.setFocusable(false);
        nativeAdView.addView(
                adChoicesReserve
              , new FrameLayout.LayoutParams(
                    adChoicesReserveWidth
                  , adChoicesReserveHeight
                  , Gravity.TOP | Gravity.END));
        if (cardLayout) {
            InsetCardAdChoices(
                    nativeAdView
                  , Math.round(CARD_AD_CHOICES_INSET_DP * density)
                  , contentColumn
                  , attribution);
            cardAttribution = attribution;
            cardMarkPadding.setEmpty();
            contentColumn.addOnLayoutChangeListener(
                    (view, left, top, right, bottom
                          , oldLeft, oldTop, oldRight, oldBottom) ->
                            post(this::FollowCardMedia));
        } else if (stackedHalf) {
            // The badge and the mark on the picture's corners, as on the
            // card; the strip above it is left to the controls.
            InsetCardAdChoices(
                    nativeAdView
                  , Math.round(CARD_AD_CHOICES_INSET_DP * density)
                  , contentColumn
                  , attribution);
            cardAttribution = attribution;
            halfColumn = contentColumn;
            halfMedia = mediaView;
            cardMarkPadding.setEmpty();
            contentColumn.addOnLayoutChangeListener(
                    (view, left, top, right, bottom
                          , oldLeft, oldTop, oldRight, oldBottom) ->
                            post(this::FollowCardMedia));
        } else {
            // The badge and the mark off the panel's corners; the content
            // reaches back out and keeps its place.
            InsetCardAdChoices(
                    nativeAdView
                  , (int) (HALF_MARK_INSET_DP * density)
                  , contentColumn);
        }

        nativeAdView.setIconView(icon);
        nativeAdView.setHeadlineView(headline);
        nativeAdView.setAdvertiserView(advertiser);
        nativeAdView.setStarRatingView(starRating);
        if (layout == LAYOUT_END_CARD) {
            cardPrice = CreateEndCardFigure(context, nativeAd.getPrice());
            cardStore = CreateEndCardFigure(context, nativeAd.getStore());
            nativeAdView.setPriceView(cardPrice);
            nativeAdView.setStoreView(cardStore);
        }
        nativeAdView.setBodyView(body);
        nativeAdView.setCallToActionView(callToAction);
        callToActionView = callToAction;
        BindAssets(
                headline
              , advertiser
              , starRating
              , body
              , icon
              , callToAction);
        if (cardLayout) {
            cardBodyBound = body.getVisibility() == View.VISIBLE;
            FitCard(density);
        }
        settleColumn = contentColumn;
        settleColumnWidth = displayMetrics.widthPixels;
        settleDensity = density;
        settleHeadline = headline;
        settleBody = body;
        settleAdvertiser = advertiser;
        settleStarRating = starRating;
        ColorText(true);
        // The card is fitted once in BuildCard; the settle's text and icon
        // rules belong to the adaptive pipeline.
        if (cardLayout) settleColumn = null;
        // One anchor per block: where the icon stands alone above the text
        // - the rail beside a side media, or the icon standing in for a
        // missing picture - the text centers under it. A centered icon over
        // left-aligned lines reads as two blocks that never agreed. Text
        // that spilled below the row belongs to the full-width block and
        // keeps its left edge.
        if (sideMediaLayout || iconHero) {
            CentreUnderIcon(headline);
            CentreUnderIcon(advertiser);
            CentreUnderIcon(starRating);
            if (!sideBodySpilled) CentreUnderIcon(body);
        }
        if (!fullScreen && !tickerLayout) {
            if (sideMediaLayout) {
                ConfigureResponsiveSideRail(
                        sideRail
                      , displayMetrics.widthPixels
                                - sideMediaWidthPx
                                - sideRowLeftInsetPx
                      , displayMetrics.widthPixels - sideMediaWidthPx
                      , sideRailHeightPx > 0
                                ? sideRailHeightPx
                                : requestedPanelHeight
                      , density
                      , identityRow
                      , headline
                      , advertiser
                      , starRating
                      , body
                      , iconHero ? null : icon
                      , callToAction);
            } else {
                ConfigureResponsiveCollapsibleContent(
                        displayMetrics
                      , horizontalPadding
                      , minimumMediaSize
                      , mediaAspectRatio
                      , hasDisplayableMedia
                      , requestedPanelHeight
                      , density
                      , contentColumn
                      , mediaView
                      , identityRow
                      , headline
                      , advertiser
                      , starRating
                      , body
                      , iconHero ? null : icon
                      , callToAction);
            }
        }
        // Same condition the listener carried: the icon only follows the
        // text where it actually stands beside it.
        if (!cardLayout && !iconHero && !sideMediaLayout) {
            settleIcon = icon;
            settleIdentityText = identityText;
            settleIdentityRow = identityRow;
        }
        if (fullScreen && !cardLayout
                && hasDisplayableMedia && !sideMediaLayout) {
            EnableControlAvoidance(
                    displayMetrics
                  , horizontalPadding
                  , minimumMediaSize
                  , mediaAspectRatio
                  , contentColumn
                  , mediaView
                  , body
                  , identityRow
                  , iconHero ? null : icon
                  , callToAction
                  , displayMetrics.heightPixels);
        }
        // Registered here, after the template is decided.
        if (hasDisplayableMedia) {
            nativeAdView.setMediaView(mediaView);
        }
        nativeAdView.setNativeAd(nativeAd);
        nativeAdContainer = new SingleClickNativeAdContainer(context);
        if (cardLayout) {
            // The card in the middle of what the controls leave free.
            nativeAdContainer.setPadding(
                    cardControlColumnPx
                  , cardTopReservePx
                  , cardControlColumnPx
                  , bleedLayout ? 0 : Math.round(CARD_MARGIN_DP * density));
        }
        nativeAdContainer.addView(
                nativeAdView
               // The card's own width, not its content's: the SDK's
               // AdChoices overlay inside it asks for the whole parent.
               , cardLayout
                        ? CardParams()
                        : new FrameLayout.LayoutParams(
                              ViewGroup.LayoutParams.MATCH_PARENT
                            , ViewGroup.LayoutParams.MATCH_PARENT));
        addView(
                nativeAdContainer
              , new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT
                  , ViewGroup.LayoutParams.MATCH_PARENT));

        int closeGravity = Gravity.TOP
                | (closeOnLeft ? Gravity.START : Gravity.END);
        int numberGravity = Gravity.TOP
                | (timerOnLeft ? Gravity.START : Gravity.END);
        int controlSize = (int) (CONTROL_STRIP_HEIGHT_DP * density);
        int controlGap = Math.max(
                1
              , (int) (CONTROL_GAP_DP * density));
        // The stacked half panel's strip holds the controls alone, the marks
        // being on the picture: both sides in by the panel's inset, the disc
        // in the strip's middle.
        int leftControlInset = stackedHalf
                ? (int) (HALF_MARK_INSET_DP * density)
                : attribution.getMinWidth() + controlGap;
        int rightControlInset = (int) ((stackedHalf
                        ? HALF_MARK_INSET_DP
                        : RIGHT_CONTROL_INSET_DP)
                * density);
        int controlTop = cardLayout
                ? 0
                : Math.max(0, (controlStripHeight - controlSize) / 2);

        countdown = CreateControlView(
                context
              , String.valueOf(
                        (int) Math.ceil(
                                countDownRemainingMs
                                        / (double) MILLIS_PER_SECOND))
              , COUNTDOWN_TEXT_SIZE_SP
              , CLOSE_BACKGROUND_COLOR);
        countdown.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        FrameLayout.LayoutParams countdownLayoutParams =
                new FrameLayout.LayoutParams(
                        controlSize
                      , controlSize
                      , numberGravity);
        countdownLayoutParams.setMargins(
                leftControlInset
              , controlTop
              , rightControlInset
              , 0);
        countdown.setLayoutParams(countdownLayoutParams);

        close = CreateControlView(
                context
              , ""
              , CLOSE_TEXT_SIZE_SP
              , CLOSE_BACKGROUND_COLOR);
        StyleControls(false);
        close.setVisibility(View.GONE);
        close.setContentDescription(CLOSE_CONTENT_DESCRIPTION);
        FrameLayout.LayoutParams closeLayoutParams =
                new FrameLayout.LayoutParams(
                        controlSize
                      , controlSize
                      , closeGravity);
        closeLayoutParams.setMargins(
                leftControlInset
              , controlTop
              , rightControlInset
              , 0);
        close.setLayoutParams(closeLayoutParams);
        if (redirectOnClose) {
            // The press is LET THROUGH rather than performed. A touch
            // listener sees the DOWN and returns false, so the view never
            // becomes the touch target and the whole gesture continues down
            // to the NativeAdView underneath - which fills this panel and is
            // the SDK's own clickable surface. Google therefore sees a real
            // finger on a real ad view and does its own accounting and its
            // own redirect; nothing here fakes either.
            //
            // performClick() used to sit here and did nothing at all: it
            // fires an OnClickListener without ever producing a MotionEvent,
            // and the SDK counts touches, not calls.
            //
            // iOS lets the press through the same way and for the same
            // reason, but has to do it from hitTest: - the one hook there
            // that runs at touch-down and is allowed to decline the target.
            // The API this comment used to name does not exist:
            // performClickOnAssetWithKey: belongs to GADCustomNativeAd,
            // never to GADNativeAd.
            close.setOnTouchListener((view, event) -> {
                if (event.getActionMasked() == MotionEvent.ACTION_DOWN) {
                    ArmCloseFromPress();
                }
                return false;
            });
        } else {
            close.setOnClickListener(view -> {
                if (onClose != null) onClose.run();
            });
        }

        if (cardLayout) {
            int cardInset = Math.round(CARD_CONTROL_INSET_DP * density);
            int cardTop = Math.round(CARD_CONTROL_TOP_DP * density);
            int nearInset = cardInset + CardNearControlOffsetPx();
            UpdateControlInsets(countdown, nearInset, cardInset, cardTop);
            UpdateControlInsets(close, nearInset, cardInset, cardTop);
        }
        addView(countdown);
        addView(close);
        if (cardLayout
                && layout == LAYOUT_APP_OPEN
                && !redirectOnClose) {
            BuildAppBar(context, displayMetrics, density);
            return;
        }
        if (cardLayout) return;
        ResolveContentHeight(
                displayMetrics
              , horizontalPadding
              , minimumMediaSize
              , mediaAspectRatio
              , hasDisplayableMedia
              , requestedPanelHeight
              , contentColumn
              , mediaView);
        if (hasDisplayableMedia && !sideMediaLayout
                && !controlAvoidanceActive
                && !scrimLayoutActive) {
            ObserveMediaSize(
                    minimumMediaSize
                  , contentColumn
                  , mediaView
                  , fallbackMediaImage);
        }
        // The stacked half panel's controls keep the strip's own insets:
        // its badge is on the picture, not beside them.
        if (!stackedHalf) {
            ObserveBadgePositions(
                    attribution
                  , countdown
                  , close
                  , controlGap
                  , rightControlInset);
        }
    }

    private void ConfigureResponsiveCollapsibleContent(
            DisplayMetrics displayMetrics
          , int horizontalPadding
          , int minimumMediaSize
          , float mediaAspectRatio
          , boolean hasDisplayableMedia
          , int requestedPanelHeight
          , float density
          , LinearLayout contentColumn
          , MediaView mediaView
          , LinearLayout identityRow
          , TextView headline
          , TextView advertiser
          , NativeAdStarRatingView starRating
          , TextView body
          , ImageView icon
          , Button callToAction) {
        // The strip-avoiding attempt comes first: the media inset like every
        // other element and the control strip's band kept above it, so close
        // and timer overlay nothing. Shrinking spends the call to action, the
        // icon and the paddings before the attempt surrenders; only when even
        // those floors cannot host the content does the layout fall back to
        // bleeding the media edge to edge under the strip.
        if (hasDisplayableMedia) {
            SetMediaSideBleed(mediaView, 0);
            mediaAvoidsControlStrip = true;
            boolean contentFits = RunCollapsibleFitPipeline(
                    displayMetrics
                  , horizontalPadding
                  , minimumMediaSize
                  , mediaAspectRatio
                  , true
                  , requestedPanelHeight
                  , density
                  , contentColumn
                  , mediaView
                  , identityRow
                  , headline
                  , advertiser
                  , starRating
                  , body
                  , icon
                  , callToAction);
            if (!contentFits) {
                ApplyStripAvoidingFloors(density, icon, callToAction);
                contentFits = ContentFitsWithMinimumMedia(
                        displayMetrics
                      , horizontalPadding
                      , minimumMediaSize
                      , mediaAspectRatio
                      , true
                      , requestedPanelHeight
                      , contentColumn
                      , mediaView);
            }
            if (contentFits) {
                // The compact chrome is not a last resort but the standing
                // dress: the height it frees goes straight to the media.
                ApplyStripAvoidingFloors(density, icon, callToAction);
                // The half panel runs the same maximiser as the full
                // screen: the media's band grows toward the controls and
                // the controls move where the band grows largest.
                EnableControlAvoidance(
                        displayMetrics
                      , horizontalPadding
                      , minimumMediaSize
                      , mediaAspectRatio
                      , contentColumn
                      , mediaView
                      , body
                      , identityRow
                      , icon
                      , callToAction
                      , requestedPanelHeight);
                // The plan came back with nowhere to put the picture: the
                // text alone has taken the panel. The panel is fixed, so the
                // layout gives way instead - the same scrim, for the same
                // reason. Done here rather than after the settle because the
                // media may only be reparented before setNativeAd binds it.
                if (avoidanceFoundNoBand) {
                    controlAvoidanceActive = false;
                    layoutUnrenderable = mediaIsVideo
                            || !ApplyScrimLayout(
                                    contentColumn
                                  , mediaView
                                  , density
                                  , horizontalPadding
                                  , requestedPanelHeight);
                }
                return;
            }

            mediaAvoidsControlStrip = false;
            SetMediaSideBleed(mediaView, horizontalPadding);
            RestoreOptionalRows(advertiser, starRating, body);
        }
        if (RunCollapsibleFitPipeline(
                displayMetrics
              , horizontalPadding
              , minimumMediaSize
              , mediaAspectRatio
              , hasDisplayableMedia
              , requestedPanelHeight
              , density
              , contentColumn
              , mediaView
              , identityRow
              , headline
              , advertiser
              , starRating
              , body
              , icon
              , callToAction)) {
            return;
        }

        // Every rung is spent: smallest scale, fewest body lines, body and
        // rating and advertiser all dropped, media at its floor - and the
        // column still does not fit. Stacking is what has run out, not room.
        //
        // The scrim spends the panel twice. The picture takes the whole of
        // it, a veil goes over the picture, and the text sits on the veil
        // instead of underneath the picture - so the height the text needs
        // is no longer subtracted from the height the picture needs. This
        // is the template the rect ad reaches for in a squarish cell, for the
        // same reason and with the same veil.
        //
        // A video has no rung here: Google serves it only into a MediaView of
        // 120dp or more, unveiled, and flags it without one. The creative is
        // dropped and another requested.
        layoutUnrenderable = mediaIsVideo
                || !ApplyScrimLayout(
                        contentColumn
                      , mediaView
                      , density
                      , horizontalPadding
                      , requestedPanelHeight);
    }

    // Turns the built column into the scrim template in place. The column is
    // already parented to nativeAdView, which is a FrameLayout, so the whole
    // change is a reparent and three layout params: the media moves out of
    // the column to sit behind everything, a veil goes over it, and the
    // column stops filling the panel and hugs its bottom edge.
    private boolean ApplyScrimLayout(
            LinearLayout contentColumn
          , MediaView mediaView
          , float density
          , int horizontalPadding
          , int panelHeight) {
        // Never a video. A player behind text it does not know about cannot
        // keep its controls or its frame clear of them, and the veil dims
        // every frame of it besides. The rect ad draws the same line: the scrim
        // template is only ever evaluated for a still main image.
        if (mediaIsVideo) {
                return false;
        }
        if (scrimLayoutActive
                || nativeAdView == null
                || mediaView == null
                || !(mediaView.getParent() instanceof ViewGroup)) {
            return false;
        }

        scrimLayoutActive = true;
        lastResortPanelHeight = panelHeight;
        // The text moves onto the veil, which is dark whatever the theme.
        ColorText(false);
        mediaAvoidsControlStrip = false;
        ((ViewGroup) mediaView.getParent()).removeView(mediaView);
        nativeAdView.addView(
                mediaView
              , 0
              , new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT
                  , ViewGroup.LayoutParams.MATCH_PARENT));

        View veil = new View(getContext());
        veil.setBackgroundColor(Color.parseColor(SCRIM_VEIL_COLOR));
        veil.setClickable(false);
        veil.setFocusable(false);
        veil.setImportantForAccessibility(
                View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        nativeAdView.addView(
                veil
              , 1
              , new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT
                  , ViewGroup.LayoutParams.MATCH_PARENT));

        ViewGroup.LayoutParams columnParams = contentColumn.getLayoutParams();
        if (columnParams instanceof FrameLayout.LayoutParams) {
            FrameLayout.LayoutParams frameParams =
                    (FrameLayout.LayoutParams) columnParams;
            frameParams.height = ViewGroup.LayoutParams.WRAP_CONTENT;
            frameParams.gravity = Gravity.BOTTOM;
            contentColumn.setLayoutParams(frameParams);
        }
        // The veil already separates the text from the picture, so the block
        // spends only a hair on each side - enough that nothing is printed
        // on the very edge, never enough to read as a border. The top strip
        // inset goes: a block hugging the bottom has no strip above it.
        int scrimPad = Math.max(1, horizontalPadding);
        contentColumn.setPadding(scrimPad, scrimPad, scrimPad, scrimPad);
        contentColumn.setGravity(Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL);
        contentColumn.setClipToPadding(false);
        return true;
    }

    // Read by the presentation once the view is built: true means no
    // arrangement of this creative fits this panel, so the ad must be
    // dropped and another requested rather than shown badly.
    boolean IsLayoutUnrenderable() {
        return layoutUnrenderable;
    }

    private boolean RunCollapsibleFitPipeline(
            DisplayMetrics displayMetrics
          , int horizontalPadding
          , int minimumMediaSize
          , float mediaAspectRatio
          , boolean hasDisplayableMedia
          , int requestedPanelHeight
          , float density
          , LinearLayout contentColumn
          , MediaView mediaView
          , LinearLayout identityRow
          , TextView headline
          , TextView advertiser
          , NativeAdStarRatingView starRating
          , TextView body
          , ImageView icon
          , Button callToAction) {
        headline.setMaxLines(COLLAPSIBLE_HEADLINE_MAX_LINE_COUNT);
        headline.setEllipsize(TextUtils.TruncateAt.END);
        body.setMaxLines(MAX_BODY_LINE_COUNT);
        body.setEllipsize(TextUtils.TruncateAt.END);
        ApplyResponsiveContentScale(
                0f
              , density
              , identityRow
              , headline
              , advertiser
              , body
              , icon
              , callToAction);

        boolean contentFits = ContentFitsWithMinimumMedia(
                displayMetrics
              , horizontalPadding
              , minimumMediaSize
              , mediaAspectRatio
              , hasDisplayableMedia
              , requestedPanelHeight
              , contentColumn
              , mediaView);
        while (body.getVisibility() == View.VISIBLE
                && body.getMaxLines() > MIN_BODY_LINE_COUNT
                && !contentFits) {
            body.setMaxLines(body.getMaxLines() - 1);
            contentFits = ContentFitsWithMinimumMedia(
                    displayMetrics
                  , horizontalPadding
                  , minimumMediaSize
                  , mediaAspectRatio
                  , hasDisplayableMedia
                  , requestedPanelHeight
                  , contentColumn
                  , mediaView);
        }

        if (!contentFits && body.getVisibility() == View.VISIBLE) {
            body.setVisibility(View.GONE);
            contentFits = ContentFitsWithMinimumMedia(
                    displayMetrics
                  , horizontalPadding
                  , minimumMediaSize
                  , mediaAspectRatio
                  , hasDisplayableMedia
                  , requestedPanelHeight
                  , contentColumn
                  , mediaView);
        }
        if (!contentFits && starRating.getVisibility() == View.VISIBLE) {
            starRating.setVisibility(View.GONE);
            contentFits = ContentFitsWithMinimumMedia(
                    displayMetrics
                  , horizontalPadding
                  , minimumMediaSize
                  , mediaAspectRatio
                  , hasDisplayableMedia
                  , requestedPanelHeight
                  , contentColumn
                  , mediaView);
        }
        if (!contentFits && advertiser.getVisibility() == View.VISIBLE) {
            advertiser.setVisibility(View.GONE);
            contentFits = ContentFitsWithMinimumMedia(
                    displayMetrics
                  , horizontalPadding
                  , minimumMediaSize
                  , mediaAspectRatio
                  , hasDisplayableMedia
                  , requestedPanelHeight
                  , contentColumn
                  , mediaView);
        }

        if (!contentFits) return false;
        if (!ContentFitsWithNaturalMedia(
                displayMetrics
              , horizontalPadding
              , minimumMediaSize
              , mediaAspectRatio
              , hasDisplayableMedia
              , requestedPanelHeight
              , contentColumn
              , mediaView)) {
            return true;
        }

        ApplyResponsiveContentScale(
                1f
              , density
              , identityRow
              , headline
              , advertiser
              , body
              , icon
              , callToAction);
        if (ContentFitsWithNaturalMedia(
                displayMetrics
              , horizontalPadding
              , minimumMediaSize
              , mediaAspectRatio
              , hasDisplayableMedia
              , requestedPanelHeight
              , contentColumn
              , mediaView)) {
            return true;
        }

        float minimumScale = 0f;
        float maximumScale = 1f;
        for (int iteration = 0;
                iteration < RESPONSIVE_SCALE_SEARCH_ITERATIONS;
                iteration++) {
            float candidateScale =
                    (minimumScale + maximumScale) / 2f;
            ApplyResponsiveContentScale(
                    candidateScale
                  , density
                  , identityRow
                  , headline
                  , advertiser
                  , body
                  , icon
                  , callToAction);
            if (ContentFitsWithNaturalMedia(
                    displayMetrics
                  , horizontalPadding
                  , minimumMediaSize
                  , mediaAspectRatio
                  , hasDisplayableMedia
                  , requestedPanelHeight
                  , contentColumn
                  , mediaView)) {
                minimumScale = candidateScale;
            } else {
                maximumScale = candidateScale;
            }
        }
        ApplyResponsiveContentScale(
                minimumScale
              , density
              , identityRow
              , headline
              , advertiser
              , body
              , icon
              , callToAction);
        return true;
    }

    // The rail owns a fixed height beside the media, so the fit that matters
    // is the rail's own stack against that height. The search mirrors the
    // stacked panel's: the largest text that keeps every element inside the
    // rail, shedding body lines and then optional rows when even the floor
    // does not fit.
    private void ConfigureResponsiveSideRail(
            LinearLayout rail
          , int railOuterWidth
          , int railScaleWidth
          , int railHeight
          , float density
          , LinearLayout identityRow
          , TextView headline
          , TextView advertiser
          , NativeAdStarRatingView starRating
          , TextView body
          , ImageView railIcon
          , Button callToAction) {
        if (rail == null) return;

        headline.setMaxLines(COLLAPSIBLE_HEADLINE_MAX_LINE_COUNT);
        headline.setEllipsize(TextUtils.TruncateAt.END);
        body.setMaxLines(MAX_BODY_LINE_COUNT);
        body.setEllipsize(TextUtils.TruncateAt.END);

        ApplyRailContentScale(
                0f
              , density
              , identityRow
              , headline
              , advertiser
              , body
              , callToAction);
        boolean railFits = RailFits(rail, railOuterWidth, railHeight);
        while (!railFits
                && body.getVisibility() == View.VISIBLE
                && body.getMaxLines() > MIN_BODY_LINE_COUNT) {
            body.setMaxLines(body.getMaxLines() - 1);
            railFits = RailFits(rail, railOuterWidth, railHeight);
        }
        if (!railFits && body.getVisibility() == View.VISIBLE) {
            body.setVisibility(View.GONE);
            railFits = RailFits(rail, railOuterWidth, railHeight);
        }
        if (!railFits && starRating.getVisibility() == View.VISIBLE) {
            starRating.setVisibility(View.GONE);
            railFits = RailFits(rail, railOuterWidth, railHeight);
        }
        if (!railFits && advertiser.getVisibility() == View.VISIBLE) {
            advertiser.setVisibility(View.GONE);
            railFits = RailFits(rail, railOuterWidth, railHeight);
        }
        if (!railFits) return;

        // Text no larger than the rail can wear: the ceiling follows the
        // rail's width, so a narrow column keeps modest sizes even with
        // height to spare. It reads the column the PLAIN arrangement would
        // have had, so the picture's left edge cannot cost a step of text
        // size either.
        float railScaleCap = Math.max(
                0f
              , Math.min(
                    1f
                  , (railScaleWidth
                            - RAIL_SCALE_CAP_MIN_WIDTH_DP * density)
                            / (RAIL_SCALE_CAP_RANGE_DP * density)));
        float railScale = railScaleCap;
        ApplyRailContentScale(
                railScale
              , density
              , identityRow
              , headline
              , advertiser
              , body
              , callToAction);
        if (!RailFits(rail, railOuterWidth, railHeight)) {
            float minimumScale = 0f;
            float maximumScale = railScaleCap;
            for (int iteration = 0;
                    iteration < RESPONSIVE_SCALE_SEARCH_ITERATIONS;
                    iteration++) {
                float candidateScale =
                        (minimumScale + maximumScale) / 2f;
                ApplyRailContentScale(
                        candidateScale
                      , density
                      , identityRow
                      , headline
                      , advertiser
                      , body
                      , callToAction);
                if (RailFits(rail, railOuterWidth, railHeight)) {
                    minimumScale = candidateScale;
                } else {
                    maximumScale = candidateScale;
                }
            }
            railScale = minimumScale;
            ApplyRailContentScale(
                    railScale
                  , density
                  , identityRow
                  , headline
                  , advertiser
                  , body
                  , callToAction);
        }

        // Whole text before size, size before scrolling: give lines while
        // they fit, and when a text still cannot show itself whole, trade
        // the scale down a step and try again. Only what survives this may
        // ever scroll.
        for (int attempt = 0; attempt <= RAIL_FULL_TEXT_STEPS; ++attempt) {
            RailFits(rail, railOuterWidth, railHeight);
            GrowTextLines(
                    headline
                  , RAIL_HEADLINE_MAX_LINE_COUNT
                  , rail
                  , railOuterWidth
                  , railHeight);
            GrowTextLines(
                    body
                  , RAIL_BODY_MAX_LINE_COUNT
                  , rail
                  , railOuterWidth
                  , railHeight);
            RailFits(rail, railOuterWidth, railHeight);
            if (!RailTextTruncated(headline)
                    && !RailTextTruncated(body)
                    && !RailTextTruncated(advertiser)) {
                break;
            }
            if (railScale <= 0f) break;

            railScale = Math.max(
                    0f
                  , railScale - RAIL_FULL_TEXT_SCALE_STEP);
            ApplyRailContentScale(
                    railScale
                  , density
                  , identityRow
                  , headline
                  , advertiser
                  , body
                  , callToAction);
        }

        GrowRailIcon(railIcon, rail, railOuterWidth, railHeight, density);
    }

    // The rail's own dress code on top of the shared scale: the button keeps
    // the avoidance floor and the rows drop their optional padding - a
    // narrow column spends its ground on text and media, not on chrome.
    private void ApplyRailContentScale(
            float scale
          , float density
          , LinearLayout identityRow
          , TextView headline
          , TextView advertiser
          , TextView body
          , Button callToAction) {
        ApplyResponsiveContentScale(
                scale
              , density
              , identityRow
              , headline
              , advertiser
              , body
              , null
              , callToAction);
        int railCallToActionHeight =
                Math.round(AVOID_CALL_TO_ACTION_HEIGHT_DP * density);
        callToAction.setMinHeight(railCallToActionHeight);
        callToAction.setMinimumHeight(railCallToActionHeight);
        identityRow.setPadding(0, 0, 0, 0);
        body.setPadding(0, 0, 0, 0);
    }

    private void GrowTextLines(
            TextView text
          , int maximumLineCount
          , LinearLayout rail
          , int railOuterWidth
          , int railHeight) {
        if (text == null || text.getVisibility() != View.VISIBLE) return;

        while (text.getMaxLines() < maximumLineCount) {
            text.setMaxLines(text.getMaxLines() + 1);
            if (!RailFits(rail, railOuterWidth, railHeight)) {
                text.setMaxLines(text.getMaxLines() - 1);
                return;
            }
        }
    }

    // Valid right after a measure pass: the text's layout tells whether it
    // shows everything it holds within the lines it was given.
    private static boolean RailTextTruncated(TextView text) {
        if (text == null || text.getVisibility() != View.VISIBLE) {
            return false;
        }
        Layout layout = text.getLayout();
        if (layout == null || layout.getLineCount() == 0) return false;

        int lineCount = layout.getLineCount();
        for (int line = 0; line < lineCount; ++line) {
            if (layout.getEllipsisCount(line) > 0) return true;
        }
        return layout.getLineEnd(lineCount - 1)
                < text.getText().length();
    }

    // Height that remains after the text has everything belongs to the
    // icon: it grows into the slack, never past the rail's proportion.
    private void GrowRailIcon(
            ImageView railIcon
          , LinearLayout rail
          , int railOuterWidth
          , int railHeight
          , float density) {
        if (railIcon == null || railIcon.getParent() == null) return;
        if (!RailFits(rail, railOuterWidth, railHeight)) return;

        LinearLayout.LayoutParams iconParams =
                (LinearLayout.LayoutParams) railIcon.getLayoutParams();
        int growthStep = Math.round(RAIL_ICON_GROWTH_STEP_DP * density);
        int maximumIconSize = Math.round(
                railOuterWidth * RAIL_ICON_MAX_WIDTH_RATIO);
        while (iconParams.width + growthStep <= maximumIconSize) {
            iconParams.width += growthStep;
            iconParams.height += growthStep;
            railIcon.setLayoutParams(iconParams);
            if (!RailFits(rail, railOuterWidth, railHeight)) {
                iconParams.width -= growthStep;
                iconParams.height -= growthStep;
                railIcon.setLayoutParams(iconParams);
                break;
            }
        }
    }

    private static boolean RailFits(
            LinearLayout rail
          , int railOuterWidth
          , int railHeight) {
        rail.measure(
                View.MeasureSpec.makeMeasureSpec(
                        Math.max(0, railOuterWidth)
                      , View.MeasureSpec.EXACTLY)
              , View.MeasureSpec.makeMeasureSpec(
                        0
                      , View.MeasureSpec.UNSPECIFIED));
        return rail.getMeasuredHeight() <= railHeight;
    }

    // The floors the strip-avoiding layout stands on: a shorter call to
    // action and a smaller icon. The separating paddings between rows are
    // never touched - they are the seams of the layout, not chrome.
    private void ApplyStripAvoidingFloors(
            float density
          , ImageView icon
          , Button callToAction) {
        int callToActionHeight =
                Math.round(AVOID_CALL_TO_ACTION_HEIGHT_DP * density);
        callToAction.setMinHeight(callToActionHeight);
        callToAction.setMinimumHeight(callToActionHeight);
        if (icon != null) {
            LinearLayout.LayoutParams iconLayoutParams =
                    (LinearLayout.LayoutParams) icon.getLayoutParams();
            int iconSize = Math.round(AVOID_ICON_SIZE_DP * density);
            iconLayoutParams.width = iconSize;
            iconLayoutParams.height = iconSize;
            icon.setLayoutParams(iconLayoutParams);
            iconSpentForFit = true;
        }
    }

    private void RestoreOptionalRows(
            TextView advertiser
          , NativeAdStarRatingView starRating
          , TextView body) {
        advertiser.setVisibility(
                TextUtils.isEmpty(nativeAd.getAdvertiser())
                        ? View.GONE
                        : View.VISIBLE);
        Double starRatingValue = nativeAd.getStarRating();
        boolean hasStarRating = starRatingValue != null
                && starRatingValue > 0d
                && !starRatingValue.isNaN()
                && !starRatingValue.isInfinite();
        starRating.setVisibility(
                hasStarRating ? View.VISIBLE : View.GONE);
        body.setVisibility(
                TextUtils.isEmpty(nativeAd.getBody())
                        ? View.GONE
                        : View.VISIBLE);
        body.setMaxLines(MAX_BODY_LINE_COUNT);
    }

    private static void SetMediaSideBleed(
            MediaView mediaView
          , int bleedPx) {
        LinearLayout.LayoutParams mediaLayoutParams =
                (LinearLayout.LayoutParams) mediaView.getLayoutParams();
        mediaLayoutParams.leftMargin = -bleedPx;
        mediaLayoutParams.rightMargin = -bleedPx;
        mediaView.setLayoutParams(mediaLayoutParams);
    }

    private void ApplyResponsiveContentScale(
            float scale
          , float density
          , LinearLayout identityRow
          , TextView headline
          , TextView advertiser
          , TextView body
          , ImageView icon
          , Button callToAction) {
        float resolvedScale = Math.max(0f, Math.min(1f, scale));
        headline.setTextSize(
                Interpolate(
                        MIN_HEADLINE_TEXT_SIZE_SP
                      , MAX_HEADLINE_TEXT_SIZE_SP
                      , resolvedScale));
        advertiser.setTextSize(
                Interpolate(
                        MIN_ADVERTISER_TEXT_SIZE_SP
                      , MAX_ADVERTISER_TEXT_SIZE_SP
                      , resolvedScale));
        body.setTextSize(
                Interpolate(
                        MIN_BODY_TEXT_SIZE_SP
                      , MAX_BODY_TEXT_SIZE_SP
                      , resolvedScale));
        callToAction.setTextSize(
                Interpolate(
                        MIN_CALL_TO_ACTION_TEXT_SIZE_SP
                      , MAX_CALL_TO_ACTION_TEXT_SIZE_SP
                      , resolvedScale));

        int identityVerticalPadding = Math.round(
                Interpolate(
                        MIN_IDENTITY_VERTICAL_PADDING_DP
                      , MAX_IDENTITY_VERTICAL_PADDING_DP
                      , resolvedScale)
                        * density);
        identityRow.setPadding(
                0
              , mediaAboveIdentity ? 0 : identityVerticalPadding
              , 0
              , identityVerticalPadding);
        body.setPadding(
                0
              , 0
              , 0
              , Math.round(
                    Interpolate(
                            MIN_BODY_BOTTOM_PADDING_DP
                          , MAX_BODY_BOTTOM_PADDING_DP
                          , resolvedScale)
                            * density));

        if (icon != null) {
            int iconSize = Math.round(
                    Interpolate(
                            MIN_ICON_SIZE_DP
                          , MAX_ICON_SIZE_DP
                          , resolvedScale)
                            * density);
            LinearLayout.LayoutParams iconLayoutParams =
                    (LinearLayout.LayoutParams) icon.getLayoutParams();
            iconLayoutParams.width = iconSize;
            iconLayoutParams.height = iconSize;
            iconLayoutParams.setMarginEnd(
                    Math.round(ICON_GAP_DP * density));
            icon.setLayoutParams(iconLayoutParams);
        }

        int callToActionHeight = Math.round(
                Interpolate(
                        MIN_CALL_TO_ACTION_HEIGHT_DP
                      , MAX_CALL_TO_ACTION_HEIGHT_DP
                      , resolvedScale)
                        * density);
        callToAction.setMinHeight(callToActionHeight);
        callToAction.setMinimumHeight(callToActionHeight);
    }

    private static boolean ContentFitsWithMinimumMedia(
            DisplayMetrics displayMetrics
          , int horizontalPadding
          , int minimumMediaSize
          , float mediaAspectRatio
          , boolean hasDisplayableMedia
          , int panelHeight
          , LinearLayout contentColumn
          , MediaView mediaView) {
        int contentWidth = Math.max(
                0
              , displayMetrics.widthPixels - 2 * horizontalPadding);
        return ContentFitsWithMediaHeight(
                contentWidth
              , displayMetrics.widthPixels
              , minimumMediaSize
              , mediaAspectRatio
              , hasDisplayableMedia
              , minimumMediaSize
              , panelHeight
              , contentColumn
              , mediaView);
    }

    private static boolean ContentFitsWithNaturalMedia(
            DisplayMetrics displayMetrics
          , int horizontalPadding
          , int minimumMediaSize
          , float mediaAspectRatio
          , boolean hasDisplayableMedia
          , int panelHeight
          , LinearLayout contentColumn
          , MediaView mediaView) {
        int contentWidth = Math.max(
                0
              , displayMetrics.widthPixels - 2 * horizontalPadding);
        int naturalMediaHeight = Math.max(
                minimumMediaSize
              , Math.round(contentWidth / mediaAspectRatio));
        return ContentFitsWithMediaHeight(
                contentWidth
              , displayMetrics.widthPixels
              , minimumMediaSize
              , mediaAspectRatio
              , hasDisplayableMedia
              , naturalMediaHeight
              , panelHeight
              , contentColumn
              , mediaView);
    }

    // The column is laid out MATCH_PARENT inside a panel as wide as the
    // screen, and it carries the side padding ITSELF. Measuring it at the
    // already-padded width subtracts that padding a second time: the text is
    // measured narrower than it will ever be, wraps to more lines, and the
    // column reports needing height it does not need. contentWidth is the
    // basis for what goes INSIDE the column - the media - and columnWidth is
    // what the column itself gets. They are not the same number and this is
    // the only place that ever confused them.
    private static int MeasureColumnHeight(
            LinearLayout contentColumn
          , int columnWidth) {
        contentColumn.measure(
                View.MeasureSpec.makeMeasureSpec(
                        columnWidth
                      , View.MeasureSpec.EXACTLY)
              , View.MeasureSpec.makeMeasureSpec(
                        0
                      , View.MeasureSpec.UNSPECIFIED));
        return contentColumn.getMeasuredHeight();
    }

    private static boolean ContentFitsWithMediaHeight(
            int contentWidth
          , int columnWidth
          , int minimumMediaSize
          , float mediaAspectRatio
          , boolean hasDisplayableMedia
          , int mediaHeight
          , int panelHeight
          , LinearLayout contentColumn
          , MediaView mediaView) {
        if (hasDisplayableMedia) {
            int resolvedMediaHeight = Math.max(
                    minimumMediaSize
                  , mediaHeight);
            int resolvedMediaWidth = Math.min(
                    contentWidth
                  , Math.max(
                        minimumMediaSize
                      , Math.round(
                            resolvedMediaHeight * mediaAspectRatio)));
            LinearLayout.LayoutParams mediaLayoutParams =
                    (LinearLayout.LayoutParams) mediaView.getLayoutParams();
            mediaLayoutParams.width = resolvedMediaWidth;
            mediaLayoutParams.height = resolvedMediaHeight;
            mediaLayoutParams.gravity = Gravity.CENTER_HORIZONTAL;
            mediaView.setLayoutParams(mediaLayoutParams);
        }

        return MeasureColumnHeight(contentColumn, columnWidth) <= panelHeight;
    }

    private static float Interpolate(
            float minimum
          , float maximum
          , float scale) {
        return minimum + (maximum - minimum) * scale;
    }

    // Everything that used to be decided AFTER layout is decided here,
    // before anything is painted.
    //
    // The icon and the identity text are a cycle: identityText carries
    // weight=1 so it takes whatever width the icon leaves, its lines then
    // set its height, and that height sets the icon. Running one leg in a
    // layout-change listener does not break the cycle - it only moves it to
    // where layout has already happened, so layout ends up changing its own
    // input and a second pass no longer agrees with the first. That
    // disagreement is what re-settled the panel a frame after it appeared:
    // measured on device, the icon went 84px -> 192px between the two
    // passes and carried the whole column with it.
    //
    // A cycle is resolved the way a cycle has to be: measure, adjust,
    // measure again, until nothing moves - all of it before the first
    // paint. It terminates because the icon only ever grows (its floor is
    // its current size) and is capped, and each text is shrunk at most once.
    private void SettleContentBeforeLayout() {
        if (settleColumn == null || settleColumnWidth <= 0) return;

        for (int pass = 0; pass < SETTLE_MAX_PASSES; ++pass) {
            settleColumn.measure(
                    View.MeasureSpec.makeMeasureSpec(
                            settleColumnWidth
                          , View.MeasureSpec.EXACTLY)
                  , View.MeasureSpec.makeMeasureSpec(
                            0
                          , View.MeasureSpec.UNSPECIFIED));

            boolean changed = SettleIconSize();
            changed |= SettleTextFitting(settleHeadline, 0);
            changed |= SettleTextFitting(settleBody, 1);
            changed |= SettleTextFitting(settleAdvertiser, 2);
            if (!changed) break;
            avoidancePlanDirty = true;
        }
        // Unconditional, OUTSIDE the loop: the plan the player first sees
        // must be computed from the content as it finally stands. When the
        // loop changed nothing - a pinned icon exits it on the first pass -
        // it used to leave without ever re-planning, and onSizeChanged
        // then corrected the plan on screen: the exact jolt this whole
        // pass exists to prevent. The drift that made the passes disagree
        // was the media seam double-count, since fixed at its source in
        // MeasureLowerContent; this recompute stays regardless, because
        // the invariant must not depend on every future drift having
        // already been found.
        //
        // avoidancePanelHeight as-is, NOT minus getPaddingTop(). Measured
        // on a cutout device: displayMetrics.heightPixels (2290) already
        // excludes the 110px inset of a 2400px window, so it equals
        // onSizeChanged's currentHeight minus paddingTop by itself.
        // Subtracting it here again - tried once - made the settle plan on
        // 2180 and the first real layout replan on 2290, a 110px step on
        // every fullScreen ad.
        if (controlAvoidanceActive) {
            RecomputeMediaControlAvoidance(
                    settleColumnWidth
                  , avoidancePanelHeight);
        }
        // A plan that ended with nowhere to put the picture is not a plan.
        // The scrim is out of reach by now - the ad is already bound - so
        // the creative is declared unshowable and travels the same road as
        // the fit pipeline's failures: dropped by whoever prepared it,
        // never a 0x0 media in front of the player.
        if (controlAvoidanceActive && avoidanceFoundNoBand) {
            layoutUnrenderable = true;
        }
        // The scrim freed the text from sharing the panel with the picture,
        // but not from the panel itself. By now the pipeline has already
        // shrunk the chrome to its floors, so a column that still overruns
        // the panel would simply clip its top row off screen - a headline
        // half-shown is not shown. The creative is unshowable here.
        if (scrimLayoutActive && lastResortPanelHeight > 0) {
            int lastResortColumnHeight =
                    MeasureColumnHeight(settleColumn, settleColumnWidth);
            if (lastResortColumnHeight > lastResortPanelHeight) {
                layoutUnrenderable = true;
            }
        }
    }

    private boolean SettleIconSize() {
        if (settleIcon == null
                || settleIdentityText == null
                || settleIdentityRow == null
                // The pipeline pinned this icon to pay for the media and
                // certified the fit at that size; the beauty rule yields.
                || iconSpentForFit) {
            return false;
        }

        int textHeight = settleIdentityText.getMeasuredHeight();
        int rowWidth = settleIdentityRow.getMeasuredWidth();
        if (textHeight <= 0 || rowWidth <= 0) return false;

        LinearLayout.LayoutParams layoutParams =
                (LinearLayout.LayoutParams) settleIcon.getLayoutParams();
        int minimumIconSize = Math.max(
                Math.round(MIN_ICON_SIZE_DP * settleDensity)
              , Math.max(layoutParams.width, layoutParams.height));
        int maximumIconSize = Math.max(
                minimumIconSize
              , Math.min(
                    Math.round(MAX_ICON_SIZE_DP * settleDensity)
                  , Math.round(rowWidth * MAX_ICON_ROW_WIDTH_RATIO)));
        int resolvedIconSize = Math.max(
                minimumIconSize
              , Math.min(textHeight, maximumIconSize));
        if (layoutParams.width == resolvedIconSize
                && layoutParams.height == resolvedIconSize) {
            return false;
        }

        layoutParams.width = resolvedIconSize;
        layoutParams.height = resolvedIconSize;
        layoutParams.setMarginEnd(
                Math.round(ICON_GAP_DP * settleDensity));
        settleIcon.setLayoutParams(layoutParams);
        return true;
    }

    // The measure-time twin of the rule that used to ride a layout-change
    // listener: a value is on screen whole, or on one line that scrolls -
    // never cut. Measured width stands in for laid-out width, and the
    // TextView already owns a Layout by now because measuring built one.
    private boolean SettleTextFitting(TextView text, int index) {
        if (text == null
                || textShrunk[index]
                || text.getVisibility() != View.VISIBLE) {
            return false;
        }

        Layout layout = text.getLayout();
        if (layout == null || layout.getLineCount() == 0) return false;

        if (text.getEllipsize() == TextUtils.TruncateAt.MARQUEE) {
            int window = text.getMeasuredWidth()
                    - text.getPaddingLeft()
                    - text.getPaddingRight();
            if (window <= 0 || layout.getLineWidth(0) <= window) {
                return false;
            }

            textShrunk[index] = true;
            text.setTextSize(
                    TypedValue.COMPLEX_UNIT_PX
                  , text.getTextSize() * MARQUEE_TEXT_SHRINK);
            return true;
        }

        if (!RailTextTruncated(text)) return false;

        textShrunk[index] = true;
        float shrunkSize = text.getTextSize() * MARQUEE_TEXT_SHRINK;
        MarqueeWhenTooLong(text);
        text.setTextSize(TypedValue.COMPLEX_UNIT_PX, shrunkSize);
        return true;
    }

    // ------------------------------------------------------------------
    // Full-screen control avoidance. The stack hugs the bottom and the
    // media grows until it touches the close and timer controls - through
    // the gap between them when it is narrow enough to fit. Two control
    // placements compete: the top row beside the badges, and the panel's
    // edges just below the badges; whichever placement lets the media end
    // up largest wins. Badges may sit over media; controls avoid it.
    // ------------------------------------------------------------------

    private void EnableControlAvoidance(
            DisplayMetrics displayMetrics
          , int horizontalPadding
          , int minimumMediaSize
          , float mediaAspectRatio
          , LinearLayout contentColumn
          , MediaView mediaView
          , TextView body
          , LinearLayout identityRow
          , ImageView icon
          , Button callToAction
          , int panelHeight) {
        controlAvoidanceActive = true;
        avoidancePlanDirty = true;
        avoidanceColumn = contentColumn;
        avoidanceMediaView = mediaView;
        avoidanceBodyView = body;
        avoidanceIdentityRow = identityRow;
        avoidanceIconView = icon;
        avoidanceCallToAction = callToAction;
        avoidanceMediaAspect = mediaAspectRatio;
        avoidanceMinimumMediaSize = minimumMediaSize;
        // The picture runs edge to edge, as on the full-screen faces; the
        // text stands in by STACKED_TEXT_INSET_DP.
        avoidanceHorizontalPadding = 0;
        avoidancePanelHeight = panelHeight;
        contentColumn.setGravity(
                Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL);
        SetMediaSideBleed(mediaView, 0);
        RecomputeMediaControlAvoidance(
                displayMetrics.widthPixels
              , panelHeight);
    }

    // True while onMeasure is drawing the plan: the recompute's own
    // requestLayout is pointless then - the measure that follows it is the
    // layout it would be asking for.
    private boolean planningInMeasure;
    // The panel the current plan was drawn for, and whether content has
    // moved since. onMeasure re-plans only when one of them says so: a
    // plan recomputed on EVERY measure mutates children, every mutation
    // asks for a layout, and a layout asked for from inside measure is a
    // traversal next frame that measures again - a loop that never ends
    // while the ad is up. Idempotence is what ends it.
    private int plannedPanelWidth = -1;
    private int plannedPanelHeight = -1;
    private boolean avoidancePlanDirty;

    // The plan is drawn from the panel this view is actually being given,
    // inside the measure pass that precedes the first draw - never from a
    // number guessed before layout and corrected on screen a frame later.
    //
    // The guess used to be displayMetrics.heightPixels, and it matched the
    // real window on one device only because that OEM happens to report
    // the height with the cutout already taken out; a second device
    // reported it differently and every fullScreen ad stepped right after
    // its first paint. The post()ed onSizeChanged repair that stood here
    // was the step itself. Measure is the one place the real size exists
    // before anything is drawn, and planning here means the children are
    // measured, laid out and painted against the final plan the first time.
    @Override
    protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
        int width = View.MeasureSpec.getSize(widthMeasureSpec);
        int height = View.MeasureSpec.getSize(heightMeasureSpec);
        int panelHeight = height - getPaddingTop();
        if (controlAvoidanceActive
                && !released
                && View.MeasureSpec.getMode(widthMeasureSpec)
                        != View.MeasureSpec.UNSPECIFIED
                && View.MeasureSpec.getMode(heightMeasureSpec)
                        != View.MeasureSpec.UNSPECIFIED
                && width > 0
                && panelHeight > 0
                && (avoidancePlanDirty
                        || width != plannedPanelWidth
                        || panelHeight != plannedPanelHeight)) {
            planningInMeasure = true;
            try {
                RecomputeMediaControlAvoidance(width, panelHeight);
            } finally {
                planningInMeasure = false;
            }
        }
        super.onMeasure(widthMeasureSpec, heightMeasureSpec);
    }

    private void RecomputeMediaControlAvoidance(
            int panelWidth
          , int panelHeight) {
        if (!controlAvoidanceActive
                || avoidanceColumn == null
                || avoidanceMediaView == null
                || panelWidth <= 0
                || panelHeight <= 0) {
            return;
        }

        float density = getResources().getDisplayMetrics().density;
        // The lower stack's height, measured with the media collapsed. The
        // button keeps its gap above the screen's bottom edge.
        int bottomPadding = (int) (HALF_BOTTOM_PADDING_DP * density);
        LinearLayout.LayoutParams mediaLayoutParams =
                (LinearLayout.LayoutParams)
                        avoidanceMediaView.getLayoutParams();
        avoidanceColumn.setPadding(
                avoidanceColumn.getPaddingLeft()
              , 0
              , avoidanceColumn.getPaddingRight()
              , bottomPadding);
        int lowerContentHeight =
                MeasureLowerContent(panelWidth, mediaLayoutParams);
        int availableHeight = Math.max(
                0
              , panelHeight - lowerContentHeight - bottomPadding);

        // The media's field: the panel's width, under the control strip.
        // The picture grows on the panel's center line until it touches the
        // field's sides or the strip.
        int syncLeft = avoidanceHorizontalPadding;
        int syncRight = panelWidth - avoidanceHorizontalPadding;

        // {media top, interval left, interval right}.
        // The controls stand in the panel's top corners. The picture either
        // starts under the strip they stand in, at the panel's whole width,
        // or rises to the panel's top between the two columns they stand
        // in; the larger picture wins, so the controls only ever claim room
        // the picture could not have used. The badge and the AdChoices mark
        // ride the picture either way.
        int controlColumn = (int) (HALF_CONTROL_STRIP_DP * density);
        int[][] candidates = {
                { controlColumn, syncLeft, syncRight }
              , { 0, controlColumn, panelWidth - controlColumn }
        };
        // A reported ratio sizes the box to the creative itself: it grows
        // until it touches whichever limit binds, and the band's slack
        // splits so the block sits centered between the controls and the
        // bottom edge. Only a creative that never reported its proportions
        // is handed the whole band - no number exists to size or validate
        // it - and renders inside as it pleases on the black ground.
        // The largest box wins; ties go to the placement that starts
        // higher, because a picture reaching the panel's top edge reads as
        // filling the panel.
        int mediaSeam = Math.round(MEDIA_LOWER_SEAM_DP * density);
        long bestScore = -1L;
        // A box of nothing, not a box the size of the panel. The defaults
        // are what survives when no placement is found, and a panel-sized
        // default overflows the very panel this is trying to fit into.
        int bestBoxWidth = 0;
        int bestBoxHeight = 0;
        int bestTop = 0;
        int bestIntervalLeft = 0;
        int bestIntervalWidth = panelWidth;
        // One sweep, at the media's honest floor. The floor IS the
        // definition of a picture worth showing: 120dp is the SDK's
        // line below which video does not render, 48dp is this pack's
        // line below which an image reads as debris. A second sweep
        // used to lower the floor to a single pixel - which ranked a
        // sliver of picture ABOVE the scrim, where the same picture is
        // panel-sized. Below the floor the answer is a better layout
        // or a better creative, never a smaller picture: NO BAND sends
        // an image to the scrim and a video to the drop.
        int mediaFloor = avoidanceMinimumMediaSize;
        for (int[] candidate : candidates) {
            int top = candidate[0];
            int intervalLeft = Math.max(0, candidate[1]);
            int intervalRight = Math.min(panelWidth, candidate[2]);
            // The picture stays on the panel's center line, so what a
            // candidate really offers is twice its narrower half:
            // growing past that would push the media off center, not
            // make it bigger.
            int panelCentre = panelWidth / 2;
            int intervalWidth = 2 * Math.min(
                    panelCentre - intervalLeft
                  , intervalRight - panelCentre);
            if (intervalWidth < mediaFloor) continue;

            int bandHeight = availableHeight - top - mediaSeam;
            if (bandHeight < mediaFloor) continue;

            int boxWidth;
            int boxHeight;
            if (mediaAspectReported) {
                boxHeight = Math.min(
                        bandHeight
                      , Math.round(
                            intervalWidth / avoidanceMediaAspect));
                boxWidth = Math.min(
                        intervalWidth
                      , Math.round(boxHeight * avoidanceMediaAspect));
                if (boxWidth < mediaFloor || boxHeight < mediaFloor) {
                    continue;
                }
            } else {
                boxWidth = intervalWidth;
                boxHeight = bandHeight;
            }

            long score = (long) boxWidth * boxHeight;
            if (score > bestScore
                    || (score == bestScore && top < bestTop)) {
                bestScore = score;
                bestBoxWidth = boxWidth;
                bestBoxHeight = boxHeight;
                bestTop = top;
                bestIntervalLeft = intervalLeft;
                bestIntervalWidth = intervalWidth;
            }
        }
        // The sweep found nothing: the lower stack alone has eaten the
        // panel and no band at or above the floor is left. The media collapses to
        // nothing and the text keeps the room - which is the whole contract
        // now, the same one the rect ad lives under. Nothing overflows and
        // nothing asks for a bigger panel.
        avoidanceFoundNoBand = bestScore < 0L;

        if (TrimChromeForStarvedMedia(
                bestBoxWidth
              , bestIntervalWidth
              , density)) {
            RecomputeMediaControlAvoidance(panelWidth, panelHeight);
            return;
        }

        int slack = mediaAspectReported
                ? Math.max(
                        0
                      , availableHeight
                                - bestTop
                                - mediaSeam
                                - bestBoxHeight)
                : 0;
        // Slack feeds the text before it pads the void: the body takes more
        // whole lines while slack remains, so a clipped line never sits
        // beside empty space. A line already scrolling is left alone.
        if (avoidanceBodyView != null
                && avoidanceBodyView.getVisibility() == View.VISIBLE
                && avoidanceBodyView.getEllipsize()
                        != TextUtils.TruncateAt.MARQUEE) {
            while (slack > 0
                    && avoidanceBodyView.getMaxLines()
                            < RAIL_BODY_MAX_LINE_COUNT
                    && RailTextTruncated(avoidanceBodyView)) {
                avoidanceBodyView.setMaxLines(
                        avoidanceBodyView.getMaxLines() + 1);
                int grownLower = MeasureLowerContent(
                        panelWidth
                      , mediaLayoutParams);
                int grownAvailable = Math.max(
                        0
                      , panelHeight - grownLower - bottomPadding);
                int grownSlack =
                        grownAvailable - bestTop - bestBoxHeight;
                if (grownSlack < 0) {
                    avoidanceBodyView.setMaxLines(
                            avoidanceBodyView.getMaxLines() - 1);
                    MeasureLowerContent(panelWidth, mediaLayoutParams);
                    break;
                }
                lowerContentHeight = grownLower;
                availableHeight = grownAvailable;
                slack = grownSlack;
            }
        }
        avoidanceColumn.setPadding(
                avoidanceColumn.getPaddingLeft()
              , bestTop
              , avoidanceColumn.getPaddingRight()
              , bottomPadding + slack / 2);
        mediaLayoutParams.width = bestBoxWidth;
        mediaLayoutParams.height = bestBoxHeight;
        mediaLayoutParams.bottomMargin = mediaSeam;
        // Centered on the panel, never on the gap between the controls: the
        // eye measures the picture against the panel's edges, and a box
        // centered in an off-center interval reads as a mistake. The margin
        // is measured from the panel edge, so the column's own left padding
        // is subtracted - negative means the media bleeds through it.
        mediaLayoutParams.gravity = Gravity.START;
        mediaLayoutParams.leftMargin =
                (panelWidth - bestBoxWidth) / 2
                        - avoidanceColumn.getPaddingLeft();
        mediaLayoutParams.rightMargin = 0;
        plannedPanelWidth = panelWidth;
        plannedPanelHeight = panelHeight;
        avoidancePlanDirty = false;
        // Inside measure the fields above are already what the measure
        // that follows will read; asking for a layout there would only
        // schedule the next traversal of the loop this guards against.
        if (!planningInMeasure) {
            avoidanceMediaView.setLayoutParams(mediaLayoutParams);
            requestLayout();
        }
    }

    // A media that ran out of height before it reached the sides is
    // starving, and the chrome below it is what it is starving for: the
    // button drops to its floor, the icon to its own, and the rows give up
    // the paddings they only had to look comfortable. Once, and only for a
    // media that would grow by it - a picture already touching the sides
    // needs nothing.
    private boolean TrimChromeForStarvedMedia(
            int boxWidth
          , int intervalWidth
          , float density) {
        if (chromeTrimmedForMedia
                || !mediaAspectReported
                || boxWidth >= intervalWidth - 2) {
            return false;
        }

        chromeTrimmedForMedia = true;
        ApplyStripAvoidingFloors(
                density
              , avoidanceIconView
              , avoidanceCallToAction);
        if (avoidanceIdentityRow != null) {
            avoidanceIdentityRow.setPadding(0, 0, 0, 0);
        }
        if (avoidanceBodyView != null) {
            avoidanceBodyView.setPadding(0, 0, 0, 0);
        }
        return true;
    }

    // A probe: the column measured with the media collapsed, giving the
    // height of everything below it. The media's params are restored before
    // returning, and the caller owns re-measuring for real.
    private int MeasureLowerContent(
            int panelWidth
          , LinearLayout.LayoutParams mediaLayoutParams) {
        int previousWidth = mediaLayoutParams.width;
        int previousHeight = mediaLayoutParams.height;
        int previousBottomMargin = mediaLayoutParams.bottomMargin;
        mediaLayoutParams.width = 0;
        mediaLayoutParams.height = 0;
        // The margin collapses with the view. The seam the plan hangs
        // under the media is already accounted for explicitly in the band
        // and slack arithmetic; once the first plan had installed it as
        // this margin, every later measure counted it a SECOND time inside
        // the lower stack - the +6px drift between first plan and every
        // replan, named by the replan log on device, 2026-08-21.
        mediaLayoutParams.bottomMargin = 0;
        // Written in place, never through setLayoutParams: that call asks
        // for a layout unconditionally, and the measure below reads the
        // child's live LayoutParams fields directly.
        avoidanceColumn.measure(
                View.MeasureSpec.makeMeasureSpec(
                        panelWidth
                      , View.MeasureSpec.EXACTLY)
              , View.MeasureSpec.makeMeasureSpec(
                        0
                      , View.MeasureSpec.UNSPECIFIED));
        int lowerContentHeight = avoidanceColumn.getMeasuredHeight();
        mediaLayoutParams.width = previousWidth;
        mediaLayoutParams.height = previousHeight;
        mediaLayoutParams.bottomMargin = previousBottomMargin;
        return lowerContentHeight;
    }

    // The full-screen face. The interstitial over the whole screen: the
    // picture as large as it goes in the middle - under the controls' row
    // across the width, or up to the screen's top between the two controls'
    // columns, whichever is larger - and the app's strip at the foot, read
    // from the left: the icon beside the name, the pitch, the button. The end
    // card as the app's store page: the picture as a banner over its dimmed,
    // blurred copy, then the listing (ArrangeEndCardListing) and the button at
    // the foot. The app open ad a rounded card in the middle of the dimmed
    // game: the picture across its top, then the app, the pitch and the
    // button, centred. In landscape the picture takes the left and the rest
    // stands beside it. The picture yields first when the screen is short,
    // then the body's lines, then the icon; a face that still cannot fit keeps
    // the picture at its floor (FitCard).
    private void BuildCard(
            Context context
          , DisplayMetrics displayMetrics
          , float density
          , LinearLayout card
          , MediaView mediaView
          , float mediaAspectRatio
          , int minimumMediaSize
          , LinearLayout identityText
          , ImageView icon
          , TextView headline
          , TextView advertiser
          , NativeAdStarRatingView starRating
          , TextView body
          , Button callToAction) {
        Point window = CardWindowSize(context);
        // The window counts the cutout's bands, which this view's padding
        // keeps clear (ApplyFullScreenContentInset): on the top edge upright,
        // on a side edge turned. The card is arranged before that padding is
        // set, so the bands are read from the display.
        Rect cutout = CutoutInsets();
        int width = window.x - cutout.left - cutout.right;
        int height = window.y;
        boolean landscape = width > height;
        boolean endCard = layout == LAYOUT_END_CARD;
        boolean appBarShown = layout == LAYOUT_APP_OPEN && !redirectOnClose;
        int margin = bleedLayout ? 0 : Math.round(CARD_MARGIN_DP * density);
        int padding = Math.round(CARD_PADDING_DP * density);
        int textWidth = Math.min(
                width
              , Math.round(BLEED_TEXT_MAX_WIDTH_DP * density));
        // The controls never take a strip across the top. A landscape
        // full-screen face has two gaps of its own beside its picture: one
        // controls stands right of the picture, the other at the screen's
        // right edge, both over the text's column (BuildCard). A landscape
        // card has the screen's sides, so they take a column at each side; a
        // full-screen portrait face lets them stand over its picture's
        // corners or beside it (FitCard). Only the app open card without its
        // bar keeps the strip, as the game around it is not the ad's to lay
        // out.
        int controlColumn = Math.round(
                (CONTROL_STRIP_HEIGHT_DP + 2 * CARD_CONTROL_INSET_DP) * density);
        boolean controlColumns = landscape && !appBarShown && !bleedLayout;
        cardControlColumnPx = controlColumns ? controlColumn : 0;
        cardTopReservePx = Math.round(
                (appBarShown
                        ? APP_BAR_HEIGHT_DP + 2 * APP_BAR_GAP_DP
                        : controlColumns || bleedLayout
                                ? 0
                                : CONTROL_STRIP_HEIGHT_DP + 2 * CARD_CONTROL_TOP_DP)
                        * density);
        cardWidthPx = bleedLayout
                ? width - 2 * cardControlColumnPx
                : Math.min(
                      width - 2 * Math.max(margin, cardControlColumnPx)
                    , Math.round(
                          (landscape
                                  ? CARD_LANDSCAPE_MAX_WIDTH_DP
                                  : CARD_MAX_WIDTH_DP)
                                  * density));
        int cardMaxHeight = height
                - cutout.top
                - cutout.bottom
                - cardTopReservePx
                - margin;
        cardBuiltWidthPx = window.x;
        cardBuiltHeightPx = window.y;
        cardBuiltCutout.set(cutout);
        cardColumn = card;
        cardMediaAspectRatio = mediaAspectRatio;
        cardIdentityText = identityText;
        cardHeadline = headline;
        cardAdvertiser = advertiser;
        cardStarRating = starRating;
        cardCallToAction = callToAction;

        // Everything but the media: arranged again, the card keeps it where
        // it stands (PlaceCardMedia).
        for (int index = card.getChildCount() - 1; index >= 0; --index) {
            if (card.getChildAt(index) != mediaView) card.removeViewAt(index);
        }
        card.setPadding(0, 0, 0, 0);
        card.setClipToPadding(true);
        card.setOrientation(
                landscape ? LinearLayout.HORIZONTAL : LinearLayout.VERTICAL);
        card.setGravity(Gravity.CENTER_HORIZONTAL);
        GradientDrawable shape = new GradientDrawable();
        shape.setShape(GradientDrawable.RECTANGLE);
        shape.setColor(AdTheme.Surface(context));
        if (bleedLayout) {
            // The end card's face is see-through: its banner's backdrop
            // stands behind it, on the surface the panel paints.
            card.setBackground(endCard ? null : shape);
            card.setClipToOutline(false);
            card.setForeground(null);
        } else {
            shape.setCornerRadius(CARD_CORNER_DP * density);
            card.setBackground(shape);
            card.setClipToOutline(true);
            // Drawn over the media, so it holds the card's shape where the
            // picture runs to the edge.
            GradientDrawable edge = new GradientDrawable();
            edge.setShape(GradientDrawable.RECTANGLE);
            edge.setColor(Color.TRANSPARENT);
            edge.setCornerRadius(CARD_CORNER_DP * density);
            edge.setStroke(
                    Math.max(1, Math.round(density))
                  , Color.parseColor(CARD_EDGE_COLOR));
            card.setForeground(edge);
        }

        Detach(icon);
        Detach(identityText);
        Detach(body);
        Detach(callToAction);
        identityText.setGravity(Gravity.CENTER_HORIZONTAL);
        CentreUnderIcon(headline);
        CentreUnderIcon(advertiser);
        CentreUnderIcon(starRating);
        CentreUnderIcon(body);
        SetCardTextSize(headline, endCard ? 22f : 20f);
        headline.setMaxLines(CARD_HEADLINE_MAX_LINE_COUNT);
        headline.setEllipsize(TextUtils.TruncateAt.END);
        // Centred lines of even length, no word left alone on the last.
        headline.setBreakStrategy(Layout.BREAK_STRATEGY_BALANCED);
        // The advertiser a step under the headline and over the body: its
        // own weight and the ink at 70%, where the body is the grey.
        SetCardTextSize(advertiser, 13f);
        advertiser.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        advertiser.setTextColor(
                (AdTheme.Ink(context) & 0x00FFFFFF) | 0xB3000000);
        advertiser.setPadding(0, Math.round(2 * density), 0, 0);
        SetCardTextSize(body, 14f);
        body.setMaxLines(MAX_BODY_LINE_COUNT);
        body.setLineSpacing(0f, 1.15f);
        body.setEllipsize(TextUtils.TruncateAt.END);
        body.setBreakStrategy(Layout.BREAK_STRATEGY_BALANCED);
        body.setPadding(0, Math.round(12 * density), 0, 0);
        SetCardTextSize(callToAction, 16f);
        callToAction.setSingleLine(true);
        callToAction.setMinHeight(0);
        callToAction.setMinimumHeight(0);

        int iconSize = Math.round(
                (endCard ? END_CARD_ICON_DP : CARD_ICON_DP) * density);
        LinearLayout.LayoutParams iconParams =
                new LinearLayout.LayoutParams(iconSize, iconSize);
        iconParams.gravity = Gravity.CENTER_HORIZONTAL;
        iconParams.bottomMargin = Math.round(12 * density);
        LinearLayout.LayoutParams textParams =
                new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , ViewGroup.LayoutParams.WRAP_CONTENT);
        LinearLayout.LayoutParams callToActionParams =
                new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , Math.round(CARD_CTA_HEIGHT_DP * density));
        callToActionParams.topMargin = Math.round(16 * density);

        if (mediaView != null) {
            // Spread over the screen there is no card to cut in two: the
            // letterbox is the surface the rest stands on.
            mediaView.setBackgroundColor(
                    bleedLayout
                            ? AdTheme.Surface(context)
                            : Color.parseColor(CARD_MEDIA_BACKDROP_COLOR));
            mediaView.setMinimumWidth(0);
            mediaView.setMinimumHeight(0);
        }

        LinearLayout info = new LinearLayout(context);
        info.setOrientation(LinearLayout.VERTICAL);
        info.setGravity(Gravity.CENTER);
        info.setPadding(padding, padding, padding, padding);

        if (landscape && bleedLayout) {
            // The text's column keeps the controls' strip at its top.
            info.setPadding(
                    padding
                  , Math.max(
                        padding
                      , Math.round(
                            (CONTROL_STRIP_HEIGHT_DP + 2 * CARD_CONTROL_TOP_DP)
                                    * density))
                  , padding
                  , padding);
        }
        if (landscape) {
            cardHeightPx = bleedLayout
                    ? cardMaxHeight
                    : Math.min(
                          cardMaxHeight
                        , Math.round(CARD_LANDSCAPE_MAX_HEIGHT_DP * density));
            int mediaWidth = 0;
            cardLandscapeMediaHeightPx = 0;
            cardLandscapeMediaWidthPx = 0;
            // In from the card's left, top and bottom (CardMediaInsetPx).
            int mediaInset = CardMediaInsetPx(density);
            if (mediaView != null) {
                int mediaRoom = cardHeightPx - 2 * mediaInset;
                mediaWidth = Math.max(
                        minimumMediaSize
                      , Math.min(
                            Math.round(cardWidthPx * CARD_LANDSCAPE_MEDIA_SHARE)
                          , Math.round(mediaRoom * mediaAspectRatio)));
                // The creative's own height at that width, centred beside
                // the column: a full-height box made the player draw black
                // bands above and below a wide video.
                cardLandscapeMediaHeightPx = Math.min(
                        mediaRoom
                      , Math.max(
                            minimumMediaSize
                          , Math.round(mediaWidth / mediaAspectRatio)));
                LinearLayout.LayoutParams mediaParams =
                        new LinearLayout.LayoutParams(
                                mediaWidth
                              , cardLandscapeMediaHeightPx);
                mediaParams.gravity = Gravity.CENTER_VERTICAL;
                mediaParams.setMargins(mediaInset, mediaInset, 0, mediaInset);
                PlaceCardMedia(card, mediaView, mediaParams);
                cardLandscapeMediaWidthPx = mediaWidth;
            }
            if (endCard) {
                ArrangeEndCardListing(
                        context
                      , density
                      , info
                      , icon
                      , iconParams
                      , identityText
                      , headline
                      , advertiser
                      , starRating
                      , body);
            } else {
                info.addView(icon, iconParams);
                info.addView(identityText, textParams);
                info.addView(body, new LinearLayout.LayoutParams(textParams));
            }
            info.addView(callToAction, callToActionParams);
            card.addView(
                    info
                  , new LinearLayout.LayoutParams(
                        0
                      , ViewGroup.LayoutParams.MATCH_PARENT
                      , 1f));
            cardLandscape = true;
            cardMediaInsetPx = 0;
            cardFitColumn = info;
            cardFitWidthPx = cardWidthPx
                    - mediaWidth
                    - (mediaView != null ? mediaInset : 0);
            cardMaxHeightPx = cardHeightPx;
            cardMedia = mediaView;
            cardBody = body;
            cardIcon = icon;
            cardMinimumMediaPx = minimumMediaSize;
            return;
        }

        cardHeightPx = bleedLayout
                ? cardMaxHeight
                : ViewGroup.LayoutParams.WRAP_CONTENT;
        LinearLayout.LayoutParams mediaParams =
                new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , 0);
        mediaParams.gravity = Gravity.CENTER_HORIZONTAL;
        // In from the card's top and sides (CardMediaInsetPx).
        int mediaInset = CardMediaInsetPx(density);
        mediaParams.setMargins(mediaInset, mediaInset, mediaInset, 0);
        cardMediaInsetPx = mediaInset;
        LinearLayout.LayoutParams footParams =
                new LinearLayout.LayoutParams(
                        textWidth - 2 * padding
                      , Math.round(CARD_CTA_HEIGHT_DP * density));
        // The button apart from the pitch above it, however tight the face.
        footParams.topMargin = padding;
        footParams.bottomMargin = padding + Math.round(8 * density);
        if (!bleedLayout) {
            // The app open card: the picture across its top, then the app,
            // the pitch and the button, centred.
            if (mediaView != null) PlaceCardMedia(card, mediaView, mediaParams);
            info.addView(icon, iconParams);
            info.addView(identityText, textParams);
            info.addView(body, new LinearLayout.LayoutParams(textParams));
            info.addView(callToAction, callToActionParams);
            card.addView(
                    info
                  , new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , ViewGroup.LayoutParams.WRAP_CONTENT));
        } else if (endCard) {
            // The end card is the app's store page, as the networks draw the
            // card after a video: the creative as the page's banner across
            // the top, then the listing read from the left - the big icon
            // beside the name and the advertiser, the figures a store shows,
            // the pitch - and the button at the foot.
            if (mediaView != null) PlaceCardMedia(card, mediaView, mediaParams);
            info.setPadding(padding, padding, padding, 0);
            ArrangeEndCardListing(
                    context
                  , density
                  , info
                  , icon
                  , iconParams
                  , identityText
                  , headline
                  , advertiser
                  , starRating
                  , body);
            card.addView(
                    info
                  , new LinearLayout.LayoutParams(
                        textWidth
                      , ViewGroup.LayoutParams.WRAP_CONTENT));
            card.addView(new Space(context), new LinearLayout.LayoutParams(0, 0, 1f));
            card.addView(callToAction, footParams);
        } else {
            // The interstitial is the creative's stage, as Google's own
            // full-screen native ads are: the picture as large as it goes in
            // the middle of what the app's strip leaves; the strip at the
            // foot, read from the left as a store's listing row - the icon
            // beside the name, the pitch, then the button.
            if (mediaView != null) PlaceCardMedia(card, mediaView, mediaParams);
            card.addView(new Space(context), 0, new LinearLayout.LayoutParams(0, 0, 1f));
            card.addView(new Space(context), new LinearLayout.LayoutParams(0, 0, 1f));
            for (View text : new View[] {
                    identityText, headline, advertiser, starRating, body }) {
                AlignStart(text);
            }
            identityText.setGravity(Gravity.START);
            headline.setMaxLines(2);
            headline.setBreakStrategy(Layout.BREAK_STRATEGY_SIMPLE);
            body.setMaxLines(2);
            body.setBreakStrategy(Layout.BREAK_STRATEGY_SIMPLE);
            LinearLayout row = new LinearLayout(context);
            row.setOrientation(LinearLayout.HORIZONTAL);
            row.setGravity(Gravity.CENTER_VERTICAL);
            iconParams.gravity = Gravity.CENTER_VERTICAL;
            iconParams.bottomMargin = 0;
            iconParams.rightMargin = Math.round(12 * density);
            row.addView(icon, iconParams);
            row.addView(
                    identityText
                  , new LinearLayout.LayoutParams(
                        0
                      , ViewGroup.LayoutParams.WRAP_CONTENT
                      , 1f));
            info.setGravity(Gravity.START);
            info.setPadding(padding, padding, padding, 0);
            info.addView(
                    row
                  , new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , ViewGroup.LayoutParams.WRAP_CONTENT));
            info.addView(body, new LinearLayout.LayoutParams(textParams));
            card.addView(
                    info
                  , new LinearLayout.LayoutParams(
                        textWidth
                      , ViewGroup.LayoutParams.WRAP_CONTENT));
            card.addView(callToAction, footParams);
        }

        cardLandscape = false;
        cardFitColumn = card;
        cardFitWidthPx = cardWidthPx;
        cardMaxHeightPx = cardMaxHeight;
        cardMedia = mediaView;
        cardBody = body;
        cardIcon = icon;
        cardMinimumMediaPx = minimumMediaSize;
        cardNaturalMediaHeightPx =
                Math.round((cardWidthPx - 2 * mediaInset) / mediaAspectRatio);
    }

    // Sizes the card BuildCard arranged, now that its text is bound. In
    // portrait the picture takes what the text leaves, up to its own
    // proportions and its share of the card; the text gives way before the
    // picture goes under its floor. In landscape the column beside the
    // picture gives way until it fits the card's height. Full screen has no
    // unrenderable station (see OverlayAd.Show): a card that still does not
    // fit keeps the picture at its floor and runs past the screen's edge.
    private void FitCard(float density) {
        if (cardLandscape) {
            ShrinkCardText(
                    cardFitColumn
                  , cardFitWidthPx
                  , cardMaxHeightPx
                  , cardHeadline
                  , cardBody
                  , cardIcon
                  , density);
            // The face no taller than what it holds, centred in its room:
            // the picture then reaches the face's top, where the badge and
            // the AdChoices mark sit.
            int textHeight = MeasureColumnHeight(cardFitColumn, cardFitWidthPx);
            cardHeightPx = Math.min(
                    cardMaxHeightPx
                  , Math.max(
                        cardLandscapeMediaHeightPx
                                + 2 * CardMediaInsetPx(density)
                      , textHeight));
            ApplyCardHeight();
            return;
        }

        boolean endCard = layout == LAYOUT_END_CARD;
        // The end card's picture gives the app the middle; the
        // interstitial's is the face.
        int mediaCap = Math.round(
                cardMaxHeightPx
                        * (!bleedLayout
                                ? CARD_PORTRAIT_MEDIA_SHARE
                                : endCard ? END_CARD_MEDIA_SHARE : 1f));
        // The controls stand in the face's top corners. The picture either
        // keeps under the strip they stand in, at the face's whole width,
        // or rises to the face's top between the columns they stand in; the
        // larger picture wins.
        int strip = Math.round(
                (CONTROL_STRIP_HEIGHT_DP + 2 * CARD_CONTROL_TOP_DP) * density);
        int column = Math.round(
                (CONTROL_STRIP_HEIGHT_DP + 2 * CARD_CONTROL_INSET_DP) * density);
        for (int step = 0; ; ++step) {
            cardFitColumn.setPadding(0, 0, 0, 0);
            // The picture's row measures at zero, so this is the text alone.
            int textHeight = MeasureColumnHeight(cardFitColumn, cardFitWidthPx);
            int room = cardMaxHeightPx - textHeight;
            int mediaHeight = cardMedia == null
                    ? 0
                    : Math.min(
                          cardNaturalMediaHeightPx
                        , Math.min(mediaCap, room - (bleedLayout ? strip : 0)));
            int top = bleedLayout && cardMedia != null ? strip : 0;
            if (bleedLayout && cardMedia != null) {
                int between = Math.min(
                        Math.round((cardFitWidthPx - 2 * column) / cardMediaAspectRatio)
                      , Math.min(mediaCap, room));
                int underWidth = Math.min(
                        cardFitWidthPx
                      , Math.round(mediaHeight * cardMediaAspectRatio));
                int betweenWidth = Math.round(between * cardMediaAspectRatio);
                if ((long) betweenWidth * between > (long) underWidth * mediaHeight) {
                    mediaHeight = between;
                    top = 0;
                }
            }
            boolean fits = cardMedia == null
                    ? textHeight <= cardMaxHeightPx
                    : mediaHeight >= cardMinimumMediaPx;
            if (fits) {
                cardFitColumn.setPadding(0, top, 0, 0);
                if (cardMedia != null) SizeCardMedia(mediaHeight);
                return;
            }
            if (!GiveWay(step, cardHeadline, cardBody, cardIcon, density)) {
                if (cardMedia != null) {
                    cardFitColumn.setPadding(0, bleedLayout ? strip : 0, 0, 0);
                    SizeCardMedia(cardMinimumMediaPx);
                }
                return;
            }
        }
    }

    // The picture at this height, its box no wider than the creative at that
    // height: a player letterboxes in black, which a tall video's sides would
    // show across the face, and on the app open card would split it.
    private void SizeCardMedia(int height) {
        ViewGroup.LayoutParams params = cardMedia.getLayoutParams();
        params.height = height;
        params.width = Math.min(
                cardFitWidthPx - 2 * cardMediaInsetPx
              , Math.round(height * cardMediaAspectRatio));
        cardMedia.requestLayout();
    }

    // The card's column and view at the height FitCard settled on.
    private void ApplyCardHeight() {
        if (cardColumn != null
                && cardColumn.getLayoutParams() != null) {
            cardColumn.getLayoutParams().height = cardHeightPx;
            cardColumn.requestLayout();
        }
        if (nativeAdView != null
                && nativeAdView.getParent() != null) {
            nativeAdView.setLayoutParams(CardParams());
        }
    }

    // The badge and the SDK's AdChoices mark on the picture's top corners,
    // their centres on one line. The SDK pins its mark to the NativeAdView's
    // padded corner (InsetCardAdChoices), so the padding follows the
    // picture and our own views reach back out by as much.
    private void FollowCardMedia() {
        LinearLayout column = cardLayout ? cardColumn : halfColumn;
        View media = cardLayout ? cardMedia : halfMedia;
        if (released
                || nativeAdView == null
                || column == null
                || cardAttribution == null) {
            return;
        }

        float density = getResources().getDisplayMetrics().density;
        int inset = Math.round(CARD_CONTROL_INSET_DP * density);
        if (cardLayout && countdown != null && close != null) {
            int nearInset = inset + CardNearControlOffsetPx();
            int top = Math.round(CARD_CONTROL_TOP_DP * density);
            UpdateControlInsets(countdown, nearInset, inset, top);
            UpdateControlInsets(close, nearInset, inset, top);
        }
        boolean onMedia = media != null
                && media.getVisibility() == View.VISIBLE
                && media.getWidth() > 0;
        int mediaLeft = onMedia ? media.getLeft() : 0;
        int mediaTop = onMedia ? media.getTop() : 0;
        int mediaRight = onMedia ? media.getRight() : column.getWidth();
        PlaceEndCardBackdrop(media, onMedia);
        // Flush on the picture's top corners, as on the rect ads: they are
        // square everywhere, the app open card's picture standing clear of
        // its rounding (CardMediaInsetPx). A half panel with no picture keeps
        // them off its corners instead.
        int markInset = !cardLayout && !onMedia
                ? Math.round(HALF_MARK_INSET_DP * density)
                : 0;
        AdTheme.ShapeBadge(cardAttribution, markInset == 0);
        Rect padding = new Rect(
                0
              , mediaTop + markInset
              , Math.max(0, column.getWidth() - mediaRight) + markInset
              , 0);
        if (padding.equals(cardMarkPadding)) return;

        cardMarkPadding.set(padding);
        nativeAdView.setPadding(
                padding.left
              , padding.top
              , padding.right
              , padding.bottom);
        nativeAdView.setClipToPadding(false);
        FrameLayout.LayoutParams columnParams =
                (FrameLayout.LayoutParams) column.getLayoutParams();
        columnParams.setMargins(
                -padding.left
              , -padding.top
              , -padding.right
              , -padding.bottom);
        column.setLayoutParams(columnParams);
        FrameLayout.LayoutParams badgeParams =
                (FrameLayout.LayoutParams) cardAttribution.getLayoutParams();
        badgeParams.setMargins(
                mediaLeft + markInset - padding.left
              , mediaTop + markInset - padding.top
              , 0
              , 0);
        cardAttribution.setLayoutParams(badgeParams);
    }

    // How far the app open card's picture stands in from each edge it
    // meets: by what the card's rounding takes from a corner, so its own
    // corners touch the curve and not a pixel of the creative is cut - the
    // native policy lets a picture lose only width, a video nothing.
    // Nothing on the full-screen faces, whose corners are square.
    private int CardMediaInsetPx(float density) {
        return cardLayout && !bleedLayout
                ? RectAdViewFactory.CornerInsetForRadius(
                        Math.round(CARD_CORNER_DP * density))
                : 0;
    }

    // How far right of the screen's left edge the left control's gap is: on
    // the landscape full-screen face, the picture's right edge as laid out,
    // or as arranged before that.
    private int CardNearControlOffsetPx() {
        if (!cardLandscape || !bleedLayout) return 0;

        View media = cardMedia;
        if (media == null
                || media.getVisibility() != View.VISIBLE
                || media.getWidth() <= 0) {
            return cardLandscapeMediaWidthPx;
        }
        int[] mediaAt = new int[2];
        int[] selfAt = new int[2];
        media.getLocationInWindow(mediaAt);
        getLocationInWindow(selfAt);
        // From the padding's edge, where the controls' margins count from.
        return Math.max(
                0, mediaAt[0] - selfAt[0] - getPaddingLeft() + media.getWidth());
    }

    // The card's window: the whole screen, as the full-screen activity hides
    // the system bars and lays out into the cutout. displayMetrics leaves the
    // navigation bar's side out in landscape (2307 of 2400px on a Redmi Note
    // 11, 2026-11-27), which pushed the face off centre.
    @SuppressWarnings("deprecation")
    private static Point CardWindowSize(Context context) {
        WindowManager windowManager =
                (WindowManager) context.getSystemService(Context.WINDOW_SERVICE);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            Rect bounds = windowManager.getMaximumWindowMetrics().getBounds();
            return new Point(bounds.width(), bounds.height());
        }
        DisplayMetrics real = new DisplayMetrics();
        windowManager.getDefaultDisplay().getRealMetrics(real);
        return new Point(real.widthPixels, real.heightPixels);
    }

    // One step of giving way, in order: the body a line at a time to one,
    // the headline to two lines, the headline a size smaller, the body gone,
    // the icon smaller. The headline holds its lines before the body; it is
    // the ad's name. False when nothing is left to give.
    private static boolean GiveWay(
            int step
          , TextView headline
          , TextView body
          , ImageView icon
          , float density) {
        switch (step) {
            case 0:
                body.setMaxLines(MAX_BODY_LINE_COUNT - 1);
                return true;
            case 1:
                body.setMaxLines(MIN_BODY_LINE_COUNT);
                return true;
            case 2:
                headline.setMaxLines(CARD_HEADLINE_MAX_LINE_COUNT - 1);
                return true;
            case 3:
                headline.setTextSize(
                        TypedValue.COMPLEX_UNIT_PX
                      , headline.getTextSize() * 0.9f);
                return true;
            case 4:
                body.setVisibility(View.GONE);
                return true;
            case 5:
                int small = Math.round(CARD_SMALL_ICON_DP * density);
                ViewGroup.LayoutParams params = icon.getLayoutParams();
                params.width = small;
                params.height = small;
                icon.setLayoutParams(params);
                return true;
            default:
                return false;
        }
    }

    // The landscape column gives way the same way until it fits its height.
    private static boolean ShrinkCardText(
            LinearLayout info
          , int width
          , int height
          , TextView headline
          , TextView body
          , ImageView icon
          , float density) {
        for (int step = 0; ; ++step) {
            if (MeasureColumnHeight(info, width) <= height) return true;
            if (!GiveWay(step, headline, body, icon, density)) return false;
        }
    }

    // Card text at the device's font scale, up to CARD_MAX_FONT_SCALE.
    private static void SetCardTextSize(TextView view, float sp) {
        float scale = Math.min(
                view.getResources().getConfiguration().fontScale
              , CARD_MAX_FONT_SCALE);
        view.setTextSize(
                TypedValue.COMPLEX_UNIT_PX
              , sp * view.getResources().getDisplayMetrics().density * scale);
    }

    // Where the card stands: in the middle of what the controls leave, or
    // from under them to the foot of the screen when it spreads over it.
    private FrameLayout.LayoutParams CardParams() {
        return new FrameLayout.LayoutParams(
                cardWidthPx
              , cardHeightPx
              , bleedLayout && !cardLandscape ? Gravity.TOP : Gravity.CENTER);
    }

    // A bound MediaView may not leave its parent, so a card arranged again
    // only resizes it where it stands; the rest is placed around it.
    private static void PlaceCardMedia(
            LinearLayout card
          , View media
          , LinearLayout.LayoutParams params) {
        if (media.getParent() == card) {
            media.setLayoutParams(params);
        } else {
            card.addView(media, params);
        }
    }

    @Override
    protected void onConfigurationChanged(Configuration configuration) {
        super.onConfigurationChanged(configuration);
        if (cardLayout) post(this::RefitCard);
    }

    // The card arranged and fitted again for a window that changed size
    // while the ad shows, as iOS does in ran_layoutCard.
    private void RefitCard() {
        if (released || cardColumn == null || nativeAdView == null) return;

        DisplayMetrics metrics = getContext().getResources().getDisplayMetrics();
        Point window = CardWindowSize(getContext());
        if (window.x == cardBuiltWidthPx && window.y == cardBuiltHeightPx) {
            return;
        }
        float density = metrics.density;
        BuildCard(
                getContext()
              , metrics
              , density
              , cardColumn
              , cardMedia
              , cardMediaAspectRatio
              , cardMinimumMediaPx
              , cardIdentityText
              , cardIcon
              , cardHeadline
              , cardAdvertiser
              , cardStarRating
              , cardBody
              , cardCallToAction);
        cardBody.setVisibility(cardBodyBound ? View.VISIBLE : View.GONE);
        FitCard(density);
        cardColumn.setLayoutParams(
                new FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , cardHeightPx));
        nativeAdView.setLayoutParams(CardParams());
        // The new column has lost the reach-back margins: placed again.
        cardMarkPadding.setEmpty();
        if (nativeAdContainer != null) {
            nativeAdContainer.setPadding(
                    cardControlColumnPx
                  , cardTopReservePx
                  , cardControlColumnPx
                  , bleedLayout ? 0 : Math.round(CARD_MARGIN_DP * density));
        }
    }

    // The end card's listing in its info column, read from the left: the
    // icon beside the name and the advertiser, the store's figures - the
    // rating's stars, the price, the store, apart by hairlines, only those
    // the ad carries - and the pitch.
    private void ArrangeEndCardListing(
            Context context
          , float density
          , LinearLayout info
          , ImageView icon
          , LinearLayout.LayoutParams iconParams
          , LinearLayout identityText
          , TextView headline
          , TextView advertiser
          , NativeAdStarRatingView starRating
          , TextView body) {
        for (View text : new View[] { identityText, headline, advertiser, body }) {
            AlignStart(text);
        }
        identityText.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
        headline.setMaxLines(2);
        headline.setBreakStrategy(Layout.BREAK_STRATEGY_SIMPLE);
        body.setMaxLines(3);
        body.setBreakStrategy(Layout.BREAK_STRATEGY_SIMPLE);
        body.setPadding(0, Math.round(16 * density), 0, 0);
        LinearLayout row = new LinearLayout(context);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        iconParams.gravity = Gravity.CENTER_VERTICAL;
        iconParams.bottomMargin = 0;
        iconParams.rightMargin = Math.round(16 * density);
        row.addView(icon, iconParams);
        row.addView(
                identityText
              , new LinearLayout.LayoutParams(
                    0
                  , ViewGroup.LayoutParams.WRAP_CONTENT
                  , 1f));
        // Centred down the column beside a landscape picture.
        info.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
        info.addView(
                row
              , new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT
                  , ViewGroup.LayoutParams.WRAP_CONTENT));

        Detach(starRating);
        SetCardTextSize(starRating, 15f);
        List<View> figures = new ArrayList<>();
        for (View figure : new View[] { starRating, cardPrice, cardStore }) {
            if (figure == null) continue;

            Detach(figure);
            if (figure.getVisibility() != View.GONE) figures.add(figure);
        }
        if (!figures.isEmpty()) {
            LinearLayout figuresRow = new LinearLayout(context);
            figuresRow.setOrientation(LinearLayout.HORIZONTAL);
            figuresRow.setGravity(Gravity.CENTER_VERTICAL);
            for (int index = 0; index < figures.size(); ++index) {
                if (index > 0) {
                    View hairline = new View(context);
                    hairline.setBackgroundColor(
                            AdTheme.IsDark(context) ? 0xFF444746 : 0xFFDADCE0);
                    figuresRow.addView(
                            hairline
                          , new LinearLayout.LayoutParams(
                                Math.max(1, Math.round(density))
                              , Math.round(20 * density)));
                }
                FrameLayout cell = new FrameLayout(context);
                cell.addView(
                        figures.get(index)
                      , new FrameLayout.LayoutParams(
                            ViewGroup.LayoutParams.WRAP_CONTENT
                          , ViewGroup.LayoutParams.WRAP_CONTENT
                          , Gravity.CENTER));
                figuresRow.addView(
                        cell
                      , new LinearLayout.LayoutParams(
                            0
                          , ViewGroup.LayoutParams.WRAP_CONTENT
                          , 1f));
            }
            LinearLayout.LayoutParams figuresParams =
                    new LinearLayout.LayoutParams(
                            ViewGroup.LayoutParams.MATCH_PARENT
                          , ViewGroup.LayoutParams.WRAP_CONTENT);
            figuresParams.topMargin = Math.round(16 * density);
            info.addView(figuresRow, figuresParams);
        }
        info.addView(
                body
              , new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT
                  , ViewGroup.LayoutParams.WRAP_CONTENT));
        EnsureEndCardBackdrop(context);
    }

    // A store figure of the end card - the price or the store - in the ink,
    // a step over the body; gone when the ad does not carry it.
    private static TextView CreateEndCardFigure(Context context, String value) {
        TextView figure = new TextView(context);
        figure.setTextColor(AdTheme.Ink(context));
        figure.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        SetCardTextSize(figure, 15f);
        figure.setSingleLine(true);
        figure.setEllipsize(TextUtils.TruncateAt.END);
        figure.setIncludeFontPadding(false);
        if (TextUtils.isEmpty(value)) {
            figure.setVisibility(View.GONE);
        } else {
            figure.setText(value);
        }
        return figure;
    }

    // The dimmed, blurred copy of the creative's picture the end card's
    // banner stands on, behind the ad view; a plain tone when the ad carries
    // no picture to copy. Blurred where the platform can (Android 12 on).
    private void EnsureEndCardBackdrop(Context context) {
        if (cardBackdrop != null) return;

        FrameLayout backdrop = new FrameLayout(context);
        backdrop.setBackgroundColor(AdTheme.IsDark(context) ? 0xFF1F1F1F : 0xFFE9EEF6);
        backdrop.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);
        Drawable image = FindFallbackMediaImage();
        com.google.android.gms.ads.MediaContent mediaContent = nativeAd.getMediaContent();
        if (image == null && mediaContent != null) image = mediaContent.getMainImage();
        if (image != null) {
            Drawable.ConstantState state = image.getConstantState();
            ImageView copy = new ImageView(context);
            copy.setScaleType(ImageView.ScaleType.CENTER_CROP);
            copy.setImageDrawable(state != null ? state.newDrawable().mutate() : image);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                float radius = 24 * context.getResources().getDisplayMetrics().density;
                copy.setRenderEffect(
                        RenderEffect.createBlurEffect(radius, radius, Shader.TileMode.CLAMP));
            }
            backdrop.addView(
                    copy
                  , new FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , ViewGroup.LayoutParams.MATCH_PARENT));
            View dim = new View(context);
            dim.setBackgroundColor(0x8C000000);
            backdrop.addView(
                    dim
                  , new FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , ViewGroup.LayoutParams.MATCH_PARENT));
        }
        // Drawn from the screen's edge, through this view's cutout padding.
        setClipToPadding(false);
        addView(backdrop, 0, new FrameLayout.LayoutParams(0, 0));
        cardBackdrop = backdrop;
    }

    // Across the screen from its top edge to the foot of the picture, or in
    // landscape down the screen from its left edge to the picture's right:
    // the controls stand on it, clear of the creative.
    private void PlaceEndCardBackdrop(View media, boolean onMedia) {
        if (cardBackdrop == null) return;

        cardBackdrop.setVisibility(onMedia ? View.VISIBLE : View.GONE);
        // In portrait the controls stand on it; in landscape on the listing.
        StyleControls(onMedia && !cardLandscape);
        if (!onMedia) return;

        int[] mediaAt = new int[2];
        int[] selfAt = new int[2];
        media.getLocationInWindow(mediaAt);
        getLocationInWindow(selfAt);
        int width = cardLandscape
                ? mediaAt[0] - selfAt[0] + media.getWidth()
                : getWidth();
        int height = cardLandscape
                ? getHeight()
                : mediaAt[1] - selfAt[1] + media.getHeight();
        FrameLayout.LayoutParams params =
                (FrameLayout.LayoutParams) cardBackdrop.getLayoutParams();
        if (params.width == width
                && params.height == height
                && params.leftMargin == -getPaddingLeft()
                && params.topMargin == -getPaddingTop()) {
            return;
        }
        params.width = width;
        params.height = height;
        params.leftMargin = -getPaddingLeft();
        params.topMargin = -getPaddingTop();
        cardBackdrop.setLayoutParams(params);
    }

    private static void Detach(View view) {
        if (view.getParent() instanceof ViewGroup) {
            ((ViewGroup) view.getParent()).removeView(view);
        }
    }

    // The bar back to the game, at the top of an app open ad: the game's icon
    // and name, a spinner while the close is held, then Continue to app and a
    // chevron. It is the way out, so the cross and the countdown stand down.
    private void BuildAppBar(
            Context context
          , DisplayMetrics displayMetrics
          , float density) {
        int barHeight = Math.round(APP_BAR_HEIGHT_DP * density);
        LinearLayout bar = new LinearLayout(context);
        bar.setOrientation(LinearLayout.HORIZONTAL);
        bar.setGravity(Gravity.CENTER_VERTICAL);
        int side = Math.round(14 * density);
        bar.setPadding(side, 0, side, 0);
        GradientDrawable shape = new GradientDrawable();
        shape.setShape(GradientDrawable.RECTANGLE);
        shape.setColor(AdTheme.Surface(context));
        float round = barHeight / 2f;
        shape.setCornerRadii(new float[] {
                round, round, 0f, 0f, 0f, 0f, round, round });
        bar.setBackground(shape);
        bar.setElevation(2 * density);

        android.content.pm.PackageManager packages =
                context.getPackageManager();
        android.content.pm.ApplicationInfo application =
                context.getApplicationInfo();
        ImageView gameIcon = new ImageView(context);
        gameIcon.setScaleType(ImageView.ScaleType.CENTER_CROP);
        gameIcon.setImageDrawable(packages.getApplicationIcon(application));
        RoundCorners(gameIcon, ICON_CORNER_RATIO);
        int iconSize = Math.round(APP_BAR_ICON_DP * density);
        LinearLayout.LayoutParams iconParams =
                new LinearLayout.LayoutParams(iconSize, iconSize);
        iconParams.setMarginEnd(Math.round(10 * density));
        bar.addView(gameIcon, iconParams);

        TextView name = new TextView(context);
        name.setText(packages.getApplicationLabel(application));
        name.setTextSize(15f);
        name.setTextColor(AdTheme.Ink(context));
        name.setSingleLine(true);
        name.setEllipsize(TextUtils.TruncateAt.END);
        // The bar hugs what it holds; a long name gives way first.
        name.setMaxWidth(Math.round(CardWindowSize(context).x * 0.4f));
        bar.addView(
                name
              , new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT
                  , ViewGroup.LayoutParams.WRAP_CONTENT));

        // The spinner and Continue to app share one slot as wide as the
        // wider, so the bar keeps its length when one replaces the other.
        FrameLayout state = new FrameLayout(context);
        LinearLayout.LayoutParams stateParams =
                new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.WRAP_CONTENT
                      , ViewGroup.LayoutParams.WRAP_CONTENT);
        stateParams.setMarginStart(Math.round(APP_BAR_NAME_GAP_DP * density));
        bar.addView(state, stateParams);

        ProgressBar spinner = new ProgressBar(
                context
              , null
              , android.R.attr.progressBarStyleSmall);
        spinner.setIndeterminateTintList(
                ColorStateList.valueOf(
                        AdTheme.Primary(context)));
        int spinnerSize = Math.round(20 * density);
        FrameLayout.LayoutParams spinnerParams =
                new FrameLayout.LayoutParams(
                        spinnerSize
                      , spinnerSize
                      , Gravity.END | Gravity.CENTER_VERTICAL);
        spinnerParams.setMarginEnd(Math.round(barHeight / 2f));
        state.addView(spinner, spinnerParams);

        LinearLayout ready = new LinearLayout(context);
        ready.setOrientation(LinearLayout.HORIZONTAL);
        ready.setGravity(Gravity.CENTER_VERTICAL);
        TextView go = new TextView(context);
        go.setText(APP_BAR_CONTINUE_TEXT);
        // The action, so it weighs as much as the name beside it.
        go.setTextSize(15f);
        go.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        go.setTextColor(AdTheme.Ink(context));
        go.setSingleLine(true);
        ready.addView(go);
        View chevron = new View(context);
        chevron.setBackground(new ChevronDrawable(
                density
              , AdTheme.Primary(context)));
        int chevronSize = Math.round(22 * density);
        LinearLayout.LayoutParams chevronParams =
                new LinearLayout.LayoutParams(chevronSize, chevronSize);
        chevronParams.setMarginStart(Math.round(6 * density));
        chevronParams.setMarginEnd(Math.round(barHeight / 2f - 6 * density));
        ready.addView(chevron, chevronParams);
        state.addView(
                ready
              , new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT
                  , ViewGroup.LayoutParams.WRAP_CONTENT
                  , Gravity.END | Gravity.CENTER_VERTICAL));

        bar.setOnClickListener(view -> {
            if (countDownRemainingMs <= 0L && onClose != null) onClose.run();
        });
        FrameLayout.LayoutParams barParams = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT
              , barHeight
              , Gravity.TOP | Gravity.END);
        barParams.topMargin = Math.round(APP_BAR_GAP_DP * density);
        // The right end runs off the screen, as AdMob's bar does.
        barParams.rightMargin = -Math.round(barHeight / 2f);
        addView(bar, barParams);
        appBar = bar;
        appBarSpinner = spinner;
        appBarReady = ready;
        SyncAppBar();
    }

    // Where the app bar is the way out, it shows the countdown's state and
    // the corner controls stay hidden.
    private void SyncAppBar() {
        if (appBar == null) return;

        boolean done = countDownRemainingMs <= 0L;
        if (countdown != null) countdown.setVisibility(View.GONE);
        if (close != null) close.setVisibility(View.GONE);
        // Invisible, not gone: the slot keeps the wider one's width.
        appBarSpinner.setVisibility(done ? View.INVISIBLE : View.VISIBLE);
        appBarReady.setVisibility(done ? View.VISIBLE : View.INVISIBLE);
    }

    // A chevron pointing right, drawn as the close cross is: two round-ended
    // strokes, the same on every device.
    private static final class ChevronDrawable extends Drawable {
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);

        ChevronDrawable(float density, int color) {
            paint.setColor(color);
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeCap(Paint.Cap.ROUND);
            paint.setStrokeJoin(Paint.Join.ROUND);
            paint.setStrokeWidth(2.5f * density);
        }

        @Override
        public void draw(Canvas canvas) {
            Rect bounds = getBounds();
            float side = Math.min(bounds.width(), bounds.height());
            float tipX = bounds.exactCenterX() + side * 0.14f;
            float centreY = bounds.exactCenterY();
            canvas.drawLine(
                    tipX - side * 0.26f, centreY - side * 0.28f
                  , tipX, centreY
                  , paint);
            canvas.drawLine(
                    tipX - side * 0.26f, centreY + side * 0.28f
                  , tipX, centreY
                  , paint);
        }

        @Override
        public void setAlpha(int alpha) {
            paint.setAlpha(alpha);
        }

        @Override
        public void setColorFilter(ColorFilter colorFilter) {
            paint.setColorFilter(colorFilter);
        }

        @Override
        public int getOpacity() {
            return PixelFormat.TRANSLUCENT;
        }
    }

    // Read from the left, as a listing row.
    private static void AlignStart(View view) {
        if (view == null) return;

        if (view instanceof TextView) {
            ((TextView) view).setGravity(Gravity.START);
        }
        ViewGroup.LayoutParams params = view.getLayoutParams();
        if (params instanceof LinearLayout.LayoutParams) {
            ((LinearLayout.LayoutParams) params).gravity = Gravity.START;
            view.setLayoutParams(params);
        }
    }

    private static void CentreUnderIcon(View view) {
        if (view == null) return;

        if (view instanceof TextView) {
            ((TextView) view).setGravity(Gravity.CENTER_HORIZONTAL);
        }
        ViewGroup.LayoutParams params = view.getLayoutParams();
        if (params instanceof LinearLayout.LayoutParams) {
            ((LinearLayout.LayoutParams) params).gravity =
                    Gravity.CENTER_HORIZONTAL;
            view.setLayoutParams(params);
        }
    }

    // On the theme's surface, or white on a picture's veil.
    private void ColorText(boolean onSurface) {
        Context context = getContext();
        int primary = onSurface ? AdTheme.Ink(context) : Color.WHITE;
        int secondary = onSurface
                ? AdTheme.Grey(context)
                : Color.parseColor(SECONDARY_TEXT_COLOR);
        if (settleHeadline != null) {
            settleHeadline.setTextColor(primary);
        }
        if (settleAdvertiser != null) {
            settleAdvertiser.setTextColor(secondary);
        }
        if (settleBody != null) {
            settleBody.setTextColor(secondary);
        }
        if (settleStarRating != null) {
            settleStarRating.setTextColor(secondary);
        }
    }

    // AdTheme.Panel's color: the dark ground behind a full-screen card, the
    // theme's surface for a half-screen panel.
    private void ConfigureBackground() {
        GradientDrawable background = new GradientDrawable();
        background.setShape(GradientDrawable.RECTANGLE);
        background.setColor(backgroundColor);
        setBackground(background);
        setClipToOutline(false);
    }

    private void ApplyFullScreenContentInset() {
        if (!fullScreen
                || Build.VERSION.SDK_INT < Build.VERSION_CODES.P) {
            return;
        }

        setOnApplyWindowInsetsListener((view, windowInsets) -> {
            Rect insets = SafeInsets(windowInsets.getDisplayCutout());
            view.setPadding(insets.left, insets.top, insets.right, insets.bottom);
            if (cardLayout && !insets.equals(cardBuiltCutout)) {
                cardBuiltHeightPx = 0;
                post(this::RefitCard);
            }
            return windowInsets;
        });
        // Ask the host now instead of waiting to be told. Insets are
        // delivered after the window is attached, and on a cutout device
        // that arrived 116ms AFTER the ad was already on screen - the whole
        // panel then dropped 110px in front of the player. The host window
        // knows its own cutout before any of this, so the padding is right
        // on the first paint and the listener above only ever confirms it.
        ApplyCutoutInsetFromHost();
        requestApplyInsets();
    }

    // The full-screen face's cutout bands: the display's own, known before
    // this view is attached, or the padding the insets set.
    private Rect CutoutInsets() {
        Rect insets = new Rect(
                getPaddingLeft()
              , getPaddingTop()
              , getPaddingRight()
              , getPaddingBottom());
        if (fullScreen && Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            WindowManager windowManager = (WindowManager)
                    getContext().getSystemService(Context.WINDOW_SERVICE);
            Rect display = SafeInsets(
                    windowManager.getDefaultDisplay().getCutout());
            insets.set(
                    Math.max(insets.left, display.left)
                  , Math.max(insets.top, display.top)
                  , Math.max(insets.right, display.right)
                  , Math.max(insets.bottom, display.bottom));
        }
        return insets;
    }

    private static Rect SafeInsets(android.view.DisplayCutout displayCutout) {
        if (displayCutout == null
                || Build.VERSION.SDK_INT < Build.VERSION_CODES.P) {
            return new Rect();
        }
        return new Rect(
                displayCutout.getSafeInsetLeft()
              , displayCutout.getSafeInsetTop()
              , displayCutout.getSafeInsetRight()
              , displayCutout.getSafeInsetBottom());
    }

    private void ApplyCutoutInsetFromHost() {
        if (!(getContext() instanceof android.app.Activity)) return;

        android.view.Window hostWindow =
                ((android.app.Activity) getContext()).getWindow();
        if (hostWindow == null) return;

        android.view.WindowInsets hostInsets =
                hostWindow.getDecorView().getRootWindowInsets();
        if (hostInsets == null) return;

        Rect insets = SafeInsets(hostInsets.getDisplayCutout());
        setPadding(insets.left, insets.top, insets.right, insets.bottom);
    }

    private TextView CreateAttributionView(
            Context context
          , float density) {
        TextView attribution = new TextView(context);
        attribution.setText(ATTRIBUTION_TEXT);
        attribution.setTextColor(AdTheme.ATTRIBUTION_TEXT);
        // One size on every face and every device: a mark, not text that
        // grows with the reader's font scale.
        attribution.setTextSize(TypedValue.COMPLEX_UNIT_DIP, 11f);
        attribution.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        attribution.setIncludeFontPadding(false);
        attribution.setGravity(Gravity.CENTER);
        attribution.setMinWidth(
                Math.max(
                        (int) (ATTRIBUTION_WIDTH_DP * density)
                      , MIN_BADGE_SIZE_PX));
        attribution.setMinHeight(
                Math.max(
                        (int) (ATTRIBUTION_HEIGHT_DP * density)
                      , MIN_BADGE_SIZE_PX));
        attribution.setPadding(
                (int) (6 * density)
              , 0
              , (int) (6 * density)
              , 0);
        attribution.setClickable(false);
        attribution.setFocusable(false);
        GradientDrawable background = new GradientDrawable();
        background.setShape(GradientDrawable.RECTANGLE);
        background.setColor(AdTheme.ATTRIBUTION);
        attribution.setBackground(background);
        AdTheme.ShapeBadge(attribution, false);
        return attribution;
    }

    // The close mark drawn, not typed. U+2715 is not in Roboto, so every
    // OEM resolves it from whatever symbol font it ships - each with its
    // own em-box, advance and weight - and the same sp came out a different
    // size on every device, and a different size RELATIVE TO THE DIGITS
    // beside it, which come from yet another font the OEM chose. Two
    // strokes on a canvas have no font to fall back to: the mark spans the
    // same share of its box, at the same stroke, on every device there is.
    private static final class CloseGlyphDrawable extends Drawable {
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);

        CloseGlyphDrawable(float density, int color) {
            paint.setColor(color);
            paint.setStyle(Paint.Style.STROKE);
            // Round ends: half a stroke of ink past each endpoint. The
            // Editor preview mirrors it with a dot on every bar end - the
            // device is never dressed down to what the preview finds easy.
            paint.setStrokeCap(Paint.Cap.ROUND);
            paint.setStrokeWidth(CLOSE_GLYPH_STROKE_DP * density);
        }

        @Override
        public void draw(Canvas canvas) {
            Rect bounds = getBounds();
            float centreX = bounds.exactCenterX();
            float centreY = bounds.exactCenterY();
            float half = Math.min(bounds.width(), bounds.height())
                    * CLOSE_GLYPH_SPAN_RATIO / 2f;
            canvas.drawLine(
                    centreX - half, centreY - half
                  , centreX + half, centreY + half
                  , paint);
            canvas.drawLine(
                    centreX - half, centreY + half
                  , centreX + half, centreY - half
                  , paint);
        }

        @Override
        public void setAlpha(int alpha) {
            paint.setAlpha(alpha);
        }

        @Override
        public void setColorFilter(ColorFilter colorFilter) {
            paint.setColorFilter(colorFilter);
        }

        @Override
        public int getOpacity() {
            return PixelFormat.TRANSLUCENT;
        }
    }

    // The close and the countdown take the tone of what they stand on: dark
    // over the dimmed game around the app open ad's card, over the end card's
    // dimmed copy of its creative, and over a dark theme's surface.
    private void StyleControls(boolean onEndCardBackdrop) {
        boolean darkBackdrop = (cardLayout && !bleedLayout)
                || onEndCardBackdrop
                || AdTheme.IsDark(getContext());
        if (controlsDark != null && controlsDark == darkBackdrop) return;

        controlsDark = darkBackdrop;
        ApplyCloseGlyph(close, getResources().getDisplayMetrics().density, darkBackdrop);
        countdown.setBackground(
                Disc(darkBackdrop ? DARK_BACKDROP_DISC_COLOR : CLOSE_BACKGROUND_COLOR));
        countdown.setTextColor(
                Color.parseColor(darkBackdrop ? DARK_BACKDROP_GLYPH_COLOR : CLOSE_GLYPH_COLOR));
    }

    // The tonal disc and its cross, for a light or a dark backdrop.
    private static void ApplyCloseGlyph(
            TextView close
          , float density
          , boolean darkBackdrop) {
        close.setBackground(new LayerDrawable(new Drawable[] {
                Disc(darkBackdrop ? DARK_BACKDROP_DISC_COLOR : CLOSE_BACKGROUND_COLOR)
              , new CloseGlyphDrawable(
                    density
                  , Color.parseColor(
                        darkBackdrop
                                ? DARK_BACKDROP_GLYPH_COLOR
                                : CLOSE_GLYPH_COLOR)) }));
    }

    private static Drawable Disc(String color) {
        GradientDrawable disc = new GradientDrawable();
        disc.setShape(GradientDrawable.OVAL);
        disc.setColor(Color.parseColor(color));
        return disc;
    }

    // Clips the view to rounded corners, the radius a share of its shorter
    // side, so it holds at whatever size the layout settles on.
    private static void RoundCorners(View view, final float ratio) {
        view.setOutlineProvider(new ViewOutlineProvider() {
            @Override
            public void getOutline(View target, Outline outline) {
                int width = target.getWidth();
                int height = target.getHeight();
                outline.setRoundRect(
                        0
                      , 0
                      , width
                      , height
                      , Math.min(width, height) * ratio);
            }
        });
        view.setClipToOutline(true);
    }

    // The SDK lays its AdChoices layer over the NativeAdView's padded area,
    // as the rect ad's InsetSdkOverlay leans on, so padding moves the mark in
    // from the card's corner; the AdChoices reserve, left at the corner of
    // that area, stands on it again. Our own views reach back out by as much
    // and keep their places.
    private static void InsetCardAdChoices(
            NativeAdView nativeAdView
          , int inset
          , View... ownViews) {
        nativeAdView.setPadding(inset, inset, inset, inset);
        nativeAdView.setClipToPadding(false);
        for (View view : ownViews) {
            ViewGroup.MarginLayoutParams params =
                    (ViewGroup.MarginLayoutParams) view.getLayoutParams();
            params.setMargins(
                    params.leftMargin - inset
                  , params.topMargin - inset
                  , params.rightMargin - inset
                  , params.bottomMargin - inset);
            view.setLayoutParams(params);
        }
    }

    private TextView CreateControlView(
            Context context
          , String text
          , float textSize
          , String backgroundColor) {
        TextView control = new TextView(context);
        control.setText(text);
        control.setTextColor(Color.WHITE);
        control.setTextSize(textSize);
        control.setGravity(Gravity.CENTER);
        // Font padding is not symmetric around a glyph, so a centered mark
        // still sits off center while it is included.
        control.setIncludeFontPadding(false);
        control.setBackground(Disc(backgroundColor));
        return control;
    }

    // A filled pill rather than the platform default, with a ripple so it
    // still answers a finger. The radius is past any height it can take, so
    // the ends stay round however the fit sizes it.
    private static void StyleCallToAction(
            Button callToAction
          , float density) {
        GradientDrawable background = new GradientDrawable();
        background.setShape(GradientDrawable.RECTANGLE);
        background.setColor(AdTheme.Primary(callToAction.getContext()));
        background.setCornerRadius(1000f * density);
        callToAction.setStateListAnimator(null);
        callToAction.setBackground(
                new RippleDrawable(
                        ColorStateList.valueOf(
                                Color.parseColor(CTA_RIPPLE_COLOR))
                      , background
                      , background));
        callToAction.setTextColor(Color.parseColor(CTA_TEXT_COLOR));
        callToAction.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        callToAction.setAllCaps(false);
        callToAction.setGravity(Gravity.CENTER);
    }

    // A single line that does not fit scrolls rather than being cut off, so the
    // whole value stays readable. Platform timings: 1200ms still, 30dp per
    // second, 1200ms before it repeats.
    private static void MarqueeWhenTooLong(TextView text) {
        if (text == null) return;

        text.setSingleLine(true);
        text.setMaxLines(1);
        text.setHorizontallyScrolling(true);
        text.setEllipsize(TextUtils.TruncateAt.MARQUEE);
        text.setMarqueeRepeatLimit(-1);
        text.setSelected(true);
        // The scrolling line fades at its ends rather than cutting a letter in
        // half at the edge.
        text.setHorizontalFadingEdgeEnabled(true);
        text.setFadingEdgeLength(Math.round(12 * text.getResources().getDisplayMetrics().density));
    }

    private void BindAssets(
            TextView headline
          , TextView advertiser
          , NativeAdStarRatingView starRating
          , TextView body
          , ImageView icon
          , Button callToAction) {
        // A headline the creative never sent is not a blank line to
        // reserve: an absent asset leaves no trace, exactly as the
        // advertiser, body and rating already do.
        String headlineValue = nativeAd.getHeadline();
        if (headlineValue == null || headlineValue.trim().isEmpty()) {
            headline.setVisibility(View.GONE);
        } else {
            headline.setText(headlineValue);
        }

        NativeAd.Image iconAsset =
                nativeAd.getIcon();
        if (iconAsset == null || iconAsset.getDrawable() == null) {
            icon.setVisibility(View.GONE);
        } else {
            icon.setImageDrawable(iconAsset.getDrawable());
        }

        String advertiserValue = nativeAd.getAdvertiser();
        if (TextUtils.isEmpty(advertiserValue)) {
            advertiser.setVisibility(View.GONE);
        } else {
            advertiser.setText(advertiserValue);
        }

        Double starRatingValue = nativeAd.getStarRating();
        if (starRatingValue == null
                || starRatingValue <= 0d
                || starRatingValue.isNaN()
                || starRatingValue.isInfinite()) {
            starRating.setVisibility(View.GONE);
        } else {
            starRating.SetRating(
                    Math.max(
                            0f
                          , Math.min(
                                5f
                              , starRatingValue.floatValue())));
        }

        String bodyValue = nativeAd.getBody();
        if (TextUtils.isEmpty(bodyValue)) {
            body.setVisibility(View.GONE);
        } else {
            body.setText(bodyValue);
        }

        String callToActionValue = nativeAd.getCallToAction();
        if (TextUtils.isEmpty(callToActionValue)) {
            callToAction.setVisibility(View.GONE);
        } else {
            callToAction.setText(callToActionValue);
        }
    }

    private void ResolveContentHeight(
            DisplayMetrics displayMetrics
          , int horizontalPadding
          , int minimumMediaSize
          , float mediaAspectRatio
          , boolean hasDisplayableMedia
          , int requestedPanelHeight
          , LinearLayout contentColumn
          , MediaView mediaView) {
        // The strip inset exists so nothing that matters can sit under the
        // corner controls - but the controls and badges draw on top with their
        // own opaque backgrounds, exactly how Google's interstitial overlays
        // its close button on the creative. With media as the first child the
        // strip can only ever cover media, never text: a portrait column is
        // too narrow to reach the corners, a landscape one simply runs
        // underneath them. Either way the inset's height belongs to the media.
        // Only a media-free layout keeps it, because there the top row is
        // text.
        if (hasDisplayableMedia && !sideMediaLayout
                && !controlAvoidanceActive
                // A last-resort layout took the media out of the column;
                // sizing it as a column child again would cast its
                // FrameLayout params to LinearLayout ones and crash the
                // build - measured nowhere, found by review.
                && !scrimLayoutActive) {
            if (!mediaAvoidsControlStrip) SetTopInset(contentColumn, 0);
            SizeMediaForBudget(
                    displayMetrics
                  , horizontalPadding
                  , minimumMediaSize
                  , mediaAspectRatio
                  , requestedPanelHeight
                  , contentColumn
                  , mediaView);
        }

        // The panel does not grow. It never did have the right to: the
        // caller asked for a share of the screen and that share is the whole
        // budget, exactly as the rect ad lives inside the cell it is handed.
        // Content that will not fit is shrunk, dropped or collapsed by the
        // pipeline above - it is never answered with a bigger panel.
    }

    private int SizeMediaForBudget(
            DisplayMetrics displayMetrics
          , int horizontalPadding
          , int minimumMediaSize
          , float mediaAspectRatio
          , int requestedPanelHeight
          , LinearLayout contentColumn
          , MediaView mediaView) {
        int contentWidth = Math.max(
                0
              , displayMetrics.widthPixels - 2 * horizontalPadding);
        int lowerContentHeight = Math.max(
                0
              , MeasureColumnHeight(
                    contentColumn
                  , displayMetrics.widthPixels)
                        - minimumMediaSize);
        int panelHeight = fullScreen
                ? displayMetrics.heightPixels
                : requestedPanelHeight;
        int availableMediaHeight = panelHeight - lowerContentHeight;
        // A strip-avoiding media is inset like every other element, so its
        // width basis is the content width, not the bleed's screen width.
        int mediaWidthBasis = mediaAvoidsControlStrip
                ? contentWidth
                : displayMetrics.widthPixels;
        int naturalMediaHeight = Math.max(
                minimumMediaSize
              , Math.round(
                    mediaWidthBasis / mediaAspectRatio));
        int resolvedMediaHeight = Math.max(
                minimumMediaSize
              , Math.min(
                    naturalMediaHeight
                  , Math.max(minimumMediaSize, availableMediaHeight)));
        int resolvedMediaWidth = Math.min(
                mediaWidthBasis
              , Math.max(
                    minimumMediaSize
                  , Math.round(
                        resolvedMediaHeight * mediaAspectRatio)));
        LinearLayout.LayoutParams mediaLayoutParams =
                (LinearLayout.LayoutParams)
                        mediaView.getLayoutParams();
        mediaLayoutParams.width = resolvedMediaWidth;
        mediaLayoutParams.height = resolvedMediaHeight;
        mediaLayoutParams.gravity = Gravity.CENTER_HORIZONTAL;
        mediaView.setLayoutParams(mediaLayoutParams);
        return resolvedMediaWidth;
    }

    private boolean ShouldUseSideMedia(
            DisplayMetrics displayMetrics
          , float mediaAspectRatio
          , int panelHeight) {
        if (mediaAspectRatio >= SIDE_MEDIA_MAX_ASPECT) return false;
        if (panelHeight > displayMetrics.widthPixels
                * SIDE_MEDIA_MAX_PANEL_HEIGHT_RATIO) {
            return false;
        }

        float density = displayMetrics.density;
        int mediaWidth = SideMediaWidth(
                displayMetrics
              , mediaAspectRatio
              , panelHeight);
        int railWidth = displayMetrics.widthPixels - mediaWidth;
        return mediaWidth >= SideMediaFloor(density)
                && railWidth >= (int) (SIDE_MEDIA_MIN_RAIL_DP * density);
    }

    private static int SideMediaWidth(
            DisplayMetrics displayMetrics
          , float mediaAspectRatio
          , int panelHeight) {
        return Math.min(
                Math.round(panelHeight * mediaAspectRatio)
              , Math.round(
                    displayMetrics.widthPixels
                            * SIDE_MEDIA_MAX_WIDTH_SHARE));
    }

    // The side padding one edge of the rail wears on its own: the panel's
    // inset, or 3% of the rail's width where that is narrower - a slim
    // column cannot afford a fixed gutter.
    private static int SideRailPadding(
            int railOuterWidth
          , int horizontalPadding
          , float density) {
        return Math.max(
                Math.round(RAIL_MIN_SIDE_PADDING_DP * density)
              , Math.min(
                    horizontalPadding
                  , Math.round(
                        railOuterWidth * RAIL_SIDE_PADDING_RATIO)));
    }

    private static int SideMediaHeight(
            int mediaWidth
          , float mediaAspectRatio
          , int panelHeight) {
        return Math.min(
                panelHeight
              , Math.round(mediaWidth / mediaAspectRatio));
    }

    // The width a picture must keep to earn the rail beside it.
    private static int SideMediaFloor(float density) {
        return (int) Math.ceil(MIN_VIDEO_MEDIA_SIZE_DP * density);
    }

    private static void SetTopInset(LinearLayout column, int topInset) {
        column.setPadding(
                column.getPaddingLeft()
              , topInset
              , column.getPaddingRight()
              , column.getPaddingBottom());
    }


    private void ObserveMediaSize(
            int minimumMediaSize
          , LinearLayout contentColumn
          , MediaView mediaView
          , Drawable fallbackMediaImage) {
        Runnable matchMediaSize = () -> MatchMediaSizeToAvailableSpace(
                minimumMediaSize
              , contentColumn
              , mediaView
              , fallbackMediaImage);
        contentColumn.getViewTreeObserver().addOnGlobalLayoutListener(
                matchMediaSize::run);
        contentColumn.post(matchMediaSize);
    }

    private void MatchMediaSizeToAvailableSpace(
            int minimumMediaSize
          , LinearLayout contentColumn
          , MediaView mediaView
          , Drawable fallbackMediaImage) {
        // Full column width, padding included: the media bleeds through the
        // side padding by negative margins, so capping it at the padded width
        // here would shrink it right back after layout. A strip-avoiding
        // media keeps the inset instead, so its cap is the padded width.
        int availableWidth = mediaAvoidsControlStrip
                ? contentColumn.getWidth()
                        - contentColumn.getPaddingLeft()
                        - contentColumn.getPaddingRight()
                : contentColumn.getWidth();
        int availableHeight =
                contentColumn.getHeight()
                        - ResolveNonMediaContentHeight(
                                contentColumn
                              , mediaView);
        if (availableWidth <= 0 || availableHeight <= 0) return;

        float mediaAspectRatio =
                GetMediaAspectRatio(fallbackMediaImage);
        int resolvedMediaWidth = availableWidth;
        int resolvedMediaHeight =
                Math.round(resolvedMediaWidth / mediaAspectRatio);
        if (resolvedMediaHeight > availableHeight) {
            resolvedMediaHeight = availableHeight;
            resolvedMediaWidth =
                    Math.round(
                            resolvedMediaHeight * mediaAspectRatio);
        }

        int resolvedMinimumWidth =
                Math.min(minimumMediaSize, availableWidth);
        int resolvedMinimumHeight =
                Math.min(minimumMediaSize, availableHeight);
        resolvedMediaWidth = Math.min(
                availableWidth
              , Math.max(resolvedMinimumWidth, resolvedMediaWidth));
        resolvedMediaHeight = Math.min(
                availableHeight
              , Math.max(resolvedMinimumHeight, resolvedMediaHeight));

        LinearLayout.LayoutParams mediaLayoutParams =
                (LinearLayout.LayoutParams) mediaView.getLayoutParams();
        if (mediaLayoutParams.width == resolvedMediaWidth
                && mediaLayoutParams.height == resolvedMediaHeight) {
            return;
        }

        mediaLayoutParams.width = resolvedMediaWidth;
        mediaLayoutParams.height = resolvedMediaHeight;
        mediaLayoutParams.gravity = Gravity.CENTER_HORIZONTAL;
        mediaView.setLayoutParams(mediaLayoutParams);
    }

    private static int ResolveNonMediaContentHeight(
            LinearLayout contentColumn
          , MediaView mediaView) {
        int resolvedHeight =
                contentColumn.getPaddingTop()
                        + contentColumn.getPaddingBottom();
        for (int index = 0;
                index < contentColumn.getChildCount();
                index++) {
            View child = contentColumn.getChildAt(index);
            if (child == mediaView || child.getVisibility() == View.GONE) {
                continue;
            }

            resolvedHeight += Math.max(
                    child.getHeight()
                  , child.getMeasuredHeight());
            ViewGroup.LayoutParams layoutParams = child.getLayoutParams();
            if (layoutParams instanceof ViewGroup.MarginLayoutParams) {
                ViewGroup.MarginLayoutParams marginLayoutParams =
                        (ViewGroup.MarginLayoutParams) layoutParams;
                resolvedHeight +=
                        marginLayoutParams.topMargin
                                + marginLayoutParams.bottomMargin;
            }
        }
        return resolvedHeight;
    }

    private void ObserveBadgePositions(
            TextView attribution
          , TextView countdown
          , TextView close
          , int controlGap
          , int rightControlInset) {
        Runnable alignControlsToBadges = () -> {
            // The left control follows the attribution badge - which in the
            // side-media layout already stepped off the picture to the
            // media's edge.
            int measuredLeftInset = attribution.getRight() + controlGap;
            int measuredRightInset = rightControlInset;
            int measuredTopInset = 0;
            UpdateControlInsets(
                    countdown
                  , measuredLeftInset
                  , measuredRightInset
                  , measuredTopInset);
            UpdateControlInsets(
                    close
                  , measuredLeftInset
                  , measuredRightInset
                  , measuredTopInset);
        };
        addOnLayoutChangeListener(
                (view
               , left
               , top
               , right
               , bottom
               , oldLeft
               , oldTop
               , oldRight
               , oldBottom) -> alignControlsToBadges.run());
        getViewTreeObserver().addOnGlobalLayoutListener(
                alignControlsToBadges::run);
        post(alignControlsToBadges);
    }

    private void StartCountdown() {
        if (countdown == null || close == null) return;
        if (timer != null) timer.cancel();
        timer = null;

        long remainingMs = Math.max(0L, countDownRemainingMs);
        countdown.setText(
                String.valueOf(
                        (int) Math.ceil(
                                remainingMs / (double) MILLIS_PER_SECOND)));
        countdown.setVisibility(View.VISIBLE);
        close.setVisibility(View.GONE);
        if (remainingMs <= 0L) {
            countdown.setVisibility(View.GONE);
            close.setVisibility(View.VISIBLE);
            SyncAppBar();
            return;
        }
        SyncAppBar();

        timer = new CountDownTimer(
                remainingMs
              , COUNTDOWN_INTERVAL_MS) {
            @Override
            public void onTick(long millisecondsUntilFinished) {
                countDownRemainingMs = Math.max(
                        0L
                      , millisecondsUntilFinished);
                countdown.setText(
                        String.valueOf(
                                (int) Math.ceil(
                                        millisecondsUntilFinished
                                                / (double) MILLIS_PER_SECOND)));
            }

            @Override
            public void onFinish() {
                countDownRemainingMs = 0L;
                timer = null;
                countdown.setVisibility(View.GONE);
                close.setVisibility(View.VISIBLE);
                SyncAppBar();
            }
        }.start();
    }

    private Drawable FindFallbackMediaImage() {
        List<NativeAd.Image> images =
                nativeAd.getImages();
        if (images == null) return null;

        for (NativeAd.Image image
                : images) {
            if (image != null && image.getDrawable() != null) {
                return image.getDrawable();
            }
        }
        return null;
    }

    private float GetMediaAspectRatio(Drawable fallbackMediaImage) {
        mediaAspectReported = true;
        if (fallbackMediaImage != null
                && fallbackMediaImage.getIntrinsicWidth() > 0
                && fallbackMediaImage.getIntrinsicHeight() > 0) {
            return fallbackMediaImage.getIntrinsicWidth()
                    / (float) fallbackMediaImage.getIntrinsicHeight();
        }

        com.google.android.gms.ads.MediaContent mediaContent =
                nativeAd.getMediaContent();
        if (mediaContent != null) {
            if (!mediaContent.hasVideoContent()) {
                Drawable mainImage = mediaContent.getMainImage();
                if (mainImage != null
                        && mainImage.getIntrinsicWidth() > 0
                        && mainImage.getIntrinsicHeight() > 0) {
                    return mainImage.getIntrinsicWidth()
                            / (float) mainImage.getIntrinsicHeight();
                }
            }

            float aspectRatio = mediaContent.getAspectRatio();
            if (aspectRatio > 0f
                    && !Float.isNaN(aspectRatio)
                    && !Float.isInfinite(aspectRatio)) {
                return aspectRatio;
            }
        }

        mediaAspectReported = false;
        return DEFAULT_MEDIA_ASPECT_RATIO;
    }

    private static float GetDrawableAspectRatio(Drawable image) {
        if (image == null
                || image.getIntrinsicWidth() <= 0
                || image.getIntrinsicHeight() <= 0) {
            return DEFAULT_MEDIA_ASPECT_RATIO;
        }
        return image.getIntrinsicWidth()
                / (float) image.getIntrinsicHeight();
    }

    private static void UpdateControlInsets(
            View control
          , int leftInset
          , int rightInset
          , int topInset) {
        FrameLayout.LayoutParams layoutParams =
                (FrameLayout.LayoutParams) control.getLayoutParams();
        if (layoutParams.leftMargin == leftInset
                && layoutParams.rightMargin == rightInset
                && layoutParams.topMargin == topInset) {
            return;
        }
        layoutParams.setMargins(leftInset, topInset, rightInset, 0);
        control.setLayoutParams(layoutParams);
    }
}