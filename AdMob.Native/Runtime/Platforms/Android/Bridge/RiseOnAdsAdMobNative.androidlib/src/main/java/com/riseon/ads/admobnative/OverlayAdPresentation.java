package com.riseon.ads.admobnative;

import android.app.Activity;
import android.app.Dialog;
import android.content.Context;
import android.graphics.Color;
import android.graphics.drawable.ColorDrawable;
import android.os.Build;
import android.os.IBinder;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.view.WindowInsets;
import android.view.WindowManager;
import android.view.animation.AccelerateInterpolator;
import android.view.animation.DecelerateInterpolator;
import com.google.android.gms.ads.nativead.NativeAd;

public final class OverlayAdPresentation
        extends Dialog
        implements NativeAdPresentation {
    // Both the same layer, and it has to stay that way.
    //
    // Android orders windows by TYPE first and by the order they were added
    // second; there is no z-order API and no insert-at-index. Anything else
    // that wants to sit right under this panel - a ground of its own color
    // while one ad hands over to the next - has to be a SUB_PANEL window too,
    // and the order between the two is a CALLER CONTRACT: add that window
    // first, open the ad second. Added the other way round it lands ON TOP
    // of the ad. Hiding and showing a window keeps its place; only removing
    // and adding it again moves it to the top.
    //
    // TYPE_APPLICATION_ATTACHED_DIALOG (1003) was tried as a way to make the
    // ladder hold by itself, on the assumption that a bigger type number
    // draws higher. IT DOES NOT. Measured with `adb shell dumpsys window
    // windows`, which lists top first:
    //
    //     ty=APPLICATION_SUB_PANEL          <- the window meant to be under
    //     ty=APPLICATION_ATTACHED_DIALOG    <- ad, buried
    //     ty=APPLICATION_PANEL              <- rect ad
    //     ty=BASE_APPLICATION               <- Unity
    //
    // The collapsible showed as a black band with nothing in it. Sub-window
    // types are mapped to layers inside WindowManagerService and the mapping
    // is not the numeric order; the only type that sorts above SUB_PANEL is
    // ABOVE_SUB_PANEL (1005), which is @hide and cannot be named. So this is
    // the top rung available, and the contract carries the rest.
    private static final int NON_FULLSCREEN_WINDOW_TYPE =
            WindowManager.LayoutParams.TYPE_APPLICATION_SUB_PANEL;
    private static final int FULLSCREEN_WINDOW_TYPE =
            WindowManager.LayoutParams.TYPE_APPLICATION_SUB_PANEL;
    // The slide, both ways. The iOS side keeps the same number.
    private static final long SLIDE_DURATION_MS = 250L;

    private final Activity hostActivity;
    private final NativeAd nativeAd;
    private final int countDownSec;
    private final boolean closeOnLeft;
    private final boolean timerOnLeft;
    private final boolean redirectOnClose;
    private final boolean fullScreen;
    private final float heightRatio;
    private final int backgroundColor;
    private OverlayAdContentView contentView;
    private int panelHeightPx;
    // How this show arrives and leaves. Set by the owner right before Show,
    // because the face may have been built long before anyone knew.
    private boolean slideIn;
    private boolean slideOutOnClose;
    private Runnable onDisplayed;
    private boolean displayedNotified;
    // The panel is leaving: nothing may bring it back or announce it.
    private boolean leaving;

    public OverlayAdPresentation(
            Context context
          , NativeAd nativeAd
          , int countDownSec
          , boolean closeOnLeft
          , boolean timerOnLeft
          , boolean fullScreen
          , float heightRatio
          , int backgroundColor
          , boolean redirectOnClose) {
        super(
                context
              , android.R.style.Theme_Black_NoTitleBar_Fullscreen);
        this.hostActivity =
                context instanceof Activity
                        ? (Activity) context
                        : null;
        this.nativeAd = nativeAd;
        this.countDownSec = countDownSec;
        this.closeOnLeft = closeOnLeft;
        this.timerOnLeft = timerOnLeft;
        this.redirectOnClose = redirectOnClose;
        this.fullScreen = fullScreen;
        this.heightRatio = heightRatio;
        this.backgroundColor = backgroundColor;
        // The show message, not onStart: onStart runs before the window is
        // added, and an ad is only on screen once its window is.
        super.setOnShowListener(ignored -> OnWindowShown());
    }

    void SetArrival(
            boolean slideIn
          , boolean slideOutOnClose
          , Runnable onDisplayed) {
        // Only the bottom slice slides: the full-screen face is an Activity
        // and never comes through here at show time.
        this.slideIn = slideIn && !fullScreen;
        this.slideOutOnClose = slideOutOnClose && !fullScreen;
        this.onDisplayed = onDisplayed;
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setCancelable(false);

        Context context = getContext();
        int requestedPanelHeight =
                OverlayAdContentView.ResolveInitialPanelHeight(
                        context
                      , fullScreen
                      , heightRatio);
        panelHeightPx = requestedPanelHeight;
        Window window = getWindow();
        ConfigureWindow(window, requestedPanelHeight);

        contentView = new OverlayAdContentView(
                context
              , nativeAd
              , Math.max(0, countDownSec) * 1000L
              , closeOnLeft
              , timerOnLeft
              , fullScreen
              , backgroundColor
              , redirectOnClose
              , requestedPanelHeight
              , this::CloseByPlayer);
        setContentView(contentView);
        // The window keeps the height ConfigureWindow already gave it. It
        // used to be re-sized here from a height the content had negotiated
        // upward, which is how the ad ended up taller than a window built
        // behind it from the same request. There is one panel height now,
        // the one ResolvePanelHeight publishes, and the content fits inside
        // it.
    }

    @Override
    public void dismiss() {
        leaving = true;
        if (contentView != null) {
            // An animation outlives the window it was drawn in, and its end
            // action would announce or dismiss an ad already gone.
            contentView.animate().cancel();
            contentView.Release();
        }
        super.dismiss();
    }

    // The player's own close. A close under RedirectOnClose never slides: the
    // redirect is already taking the screen, and a panel still on its way
    // down would be the one thing left in sight.
    private void CloseByPlayer() {
        Dismiss(slideOutOnClose && !redirectOnClose);
    }

    void Dismiss(boolean slideOut) {
        if (!slideOut || fullScreen || contentView == null || !isShowing()) {
            dismiss();
            return;
        }
        if (leaving) return;

        leaving = true;
        contentView.SetTouchLocked(true);
        contentView.animate().cancel();
        // A panel caught on its way in turns round where it stands, so the
        // way out is only as long as the distance it has come.
        float remaining = panelHeightPx <= 0
                ? 1f
                : 1f - contentView.getTranslationY() / panelHeightPx;
        contentView.animate()
                .translationY(panelHeightPx)
                .setDuration(
                        Math.max(0L, (long) (SLIDE_DURATION_MS * remaining)))
                .setInterpolator(new AccelerateInterpolator())
                .withEndAction(this::dismiss)
                .start();
    }

    private void OnWindowShown() {
        if (contentView == null || leaving) return;

        if (!slideIn) {
            NotifyDisplayed();
            return;
        }

        contentView.animate()
                .translationY(0f)
                .setDuration(SLIDE_DURATION_MS)
                .setInterpolator(new DecelerateInterpolator())
                // Not run when canceled, which is what a slide out does.
                .withEndAction(() -> {
                    if (contentView == null || leaving) return;

                    contentView.SetTouchLocked(false);
                    // The countdown buys watch time, and a panel still on its
                    // way in is not being watched yet.
                    contentView.OnPresented();
                    NotifyDisplayed();
                })
                .start();
    }

    private void NotifyDisplayed() {
        if (displayedNotified) return;

        displayedNotified = true;
        if (onDisplayed != null) onDisplayed.run();
    }

    @Override
    public boolean Show() {
        show();
        return isShowing();
    }

    public boolean Prepare() {
        create();
        return contentView != null;
    }

    // Prepare builds the whole content view, so by the time it returns the
    // layout has already been attempted and either found or not. Asking here
    // is what lets an unrenderable creative be dropped before it is ever
    // shown, instead of being discovered with the ad already on screen.
    public boolean IsLayoutUnrenderable() {
        return contentView != null && contentView.IsLayoutUnrenderable();
    }

    @Override
    public boolean IsShowing() {
        return isShowing();
    }

    @Override
    public void OnAdClicked() {
        if (contentView != null) contentView.CommitAdClick();
    }

    @Override
    public void Dismiss() {
        dismiss();
    }

    @Override
    public void Release() {
        setOnDismissListener(null);
        dismiss();
    }

    @Override
    protected void onStart() {
        super.onStart();
        ConfigureWindowOrder(getWindow());
        if (contentView == null) return;

        if (slideIn) {
            // Off the bottom edge before the window's first frame, so the
            // panel is never seen at rest before it moves. It is translated,
            // never resized: the window keeps the panel's height throughout,
            // and the edge clips it.
            contentView.setTranslationY(panelHeightPx);
            contentView.SetTouchLocked(true);
            return;
        }
        contentView.OnPresented();
    }

    private void ConfigureWindow(
            Window window
          , int requestedPanelHeight) {
        if (window == null) return;

        window.setFlags(
                WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED
              , WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
        ConfigureWindowOrder(window);
        window.setBackgroundDrawable(
                new ColorDrawable(Color.TRANSPARENT));
        window.setWindowAnimations(0);
        if (fullScreen) {
            ConfigureFullScreenCutout(window);
            window.setLayout(
                    ViewGroup.LayoutParams.MATCH_PARENT
                  , ViewGroup.LayoutParams.MATCH_PARENT);
            return;
        }

        window.setGravity(Gravity.BOTTOM);
        window.setLayout(
                ViewGroup.LayoutParams.MATCH_PARENT
              , requestedPanelHeight);
        window.setDimAmount(0f);
        window.addFlags(
                WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL
                        | WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE);
        // The same system-UI contract the game's surface keeps: lay out
        // UNDER the navigation bar and the cutout, and hide the bar the
        // sticky way. Without it
        // this window was the one object on screen that respected the bar -
        // swipe it out of auto-hide, return from Recents, and the window
        // manager parked the panel ABOVE the bar: a white strip where the
        // buttons sit and the whole ad shoved up, until the next touch let
        // the bar hide again and the panel fell back. Measured on a
        // three-button Samsung, 2026-08-21.
        window.getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                        | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                        | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                        | View.SYSTEM_UI_FLAG_FULLSCREEN
                        | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                        | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            WindowManager.LayoutParams attributes = window.getAttributes();
            attributes.layoutInDisplayCutoutMode =
                    WindowManager.LayoutParams
                            .LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
            window.setAttributes(attributes);
        }
    }

    private void ConfigureWindowOrder(Window window) {
        if (hostActivity == null
                || hostActivity.getWindow() == null) {
            return;
        }

        View hostDecorView =
                hostActivity.getWindow().getDecorView();
        IBinder hostWindowToken = hostDecorView.getWindowToken();
        if (hostWindowToken == null) {
            hostWindowToken = hostDecorView.getApplicationWindowToken();
        }
        if (hostWindowToken == null) return;

        WindowManager.LayoutParams attributes = window.getAttributes();
        attributes.token = hostWindowToken;
        attributes.type = fullScreen
                ? FULLSCREEN_WINDOW_TYPE
                : NON_FULLSCREEN_WINDOW_TYPE;
        window.setAttributes(attributes);
    }

    private void ConfigureFullScreenCutout(Window window) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            Api30Impl.ConfigureFullScreenInsets(window);
        } else {
            ConfigureLegacyFullScreenInsets(window);
        }
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.P) return;

        WindowManager.LayoutParams attributes = window.getAttributes();
        attributes.layoutInDisplayCutoutMode =
                WindowManager.LayoutParams
                        .LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
        window.setAttributes(attributes);
    }

    @SuppressWarnings("deprecation")
    private static void ConfigureLegacyFullScreenInsets(Window window) {
        window.getDecorView().setSystemUiVisibility(
                View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                        | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN);
    }

    private static final class Api30Impl {
        private Api30Impl() {}

        static void ConfigureFullScreenInsets(Window window) {
            WindowManager.LayoutParams attributes = window.getAttributes();
            int fitInsetTypes =
                    WindowInsets.Type.systemBars()
                            & ~WindowInsets.Type.statusBars();
            attributes.setFitInsetsTypes(fitInsetTypes);
            window.setAttributes(attributes);
        }
    }
}