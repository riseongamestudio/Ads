package com.riseon.ads;

import android.app.Activity;
import android.app.Dialog;
import android.graphics.Color;
import android.graphics.drawable.ColorDrawable;
import android.os.Build;
import android.view.View;
import android.view.ViewGroup;
import android.view.Window;
import android.view.WindowManager;
import android.widget.FrameLayout;

// The half-screen cover: a WINDOW on the Unity Activity, not an Activity of
// its own. It changes nothing about the Activity's lifecycle, so the game
// keeps running behind it - which is what a cover over part of the screen has
// to do anyway - and every call is always reachable.
//
// WHERE IT SITS, AND WHY IT IS NEVER ADDED TWICE. Android orders windows by
// type first and by the order they were added second; there is no z-order
// API and no insert-at-index. This window is TYPE_APPLICATION_SUB_PANEL, the
// same type as the half-screen ad it stands under, so it is under that ad
// only because it was added FIRST. Prepare adds it - hidden - before the ad
// opens; Reveal and Conceal only flip its visibility, which keeps its place;
// Remove takes it away. Adding it again, for any reason, would put it on top
// of every window added since, the ad among them.
public final class HalfScreenCover {
    private static CoverDialog dialog;

    private HalfScreenCover() {}

    // panelHeight is the ad's own panel height in pixels, as the ad publishes
    // it, so the two windows end at the same line. Anything at or below 0
    // means the whole screen.
    public static void Prepare(
            Activity currentActivity
          , int color
          , int panelHeight) {
        BaseCover.RunOnMainThread(() -> {
            CoverDialog current = dialog;
            if (current != null
                    && current.IsAddedTo(currentActivity)) {
                // Already in place: repainted and resized where it stands.
                current.Apply(color, panelHeight);
                return;
            }

            Dismiss();
            if (!BaseCover.IsActivityUsable(currentActivity)) return;

            CoverDialog created =
                    new CoverDialog(currentActivity, color, panelHeight);
            dialog = created;
            try {
                // Added, then hidden in the same message: the first frame is
                // drawn after both, so nothing ever sees it before Reveal.
                created.show();
                created.added = true;
                created.SetRevealed(false);
            } catch (RuntimeException ignored) {
                if (dialog == created) Dismiss();
            }
        });
    }

    public static void Reveal() {
        BaseCover.RunOnMainThread(() -> {
            if (dialog != null) dialog.SetRevealed(true);
        });
    }

    public static void Conceal() {
        BaseCover.RunOnMainThread(() -> {
            if (dialog != null) dialog.SetRevealed(false);
        });
    }

    public static void Remove() {
        BaseCover.RunOnMainThread(HalfScreenCover::Dismiss);
    }

    private static void Dismiss() {
        CoverDialog currentDialog = dialog;
        dialog = null;
        if (currentDialog == null) return;

        try {
            currentDialog.dismiss();
        } catch (RuntimeException ignored) {
        }
    }

    private static final class CoverDialog extends Dialog {
        private final Activity hostActivity;
        private int color;
        private int panelHeight;
        private FrameLayout overlayView;
        // Cleared by the window going away for any reason, so a dead window
        // is never mistaken for one still holding its place.
        private boolean added;

        CoverDialog(Activity activity, int dialogColor, int height) {
            super(
                    activity
                  , android.R.style.Theme_Translucent_NoTitleBar_Fullscreen);
            hostActivity = activity;
            color = dialogColor;
            panelHeight = height;
            setOnDismissListener(ignored -> added = false);
        }

        boolean IsAddedTo(Activity activity) {
            return added
                    && hostActivity == activity
                    && BaseCover.IsActivityUsable(activity);
        }

        @Override
        protected void onCreate(android.os.Bundle savedInstanceState) {
            super.onCreate(savedInstanceState);
            setCancelable(false);

            ConfigureWindow(getWindow(), hostActivity, panelHeight);

            overlayView = new FrameLayout(hostActivity);
            overlayView.setBackgroundColor(color);
            overlayView.setClickable(true);
            overlayView.setFocusable(false);
            overlayView.setImportantForAccessibility(
                    View.IMPORTANT_FOR_ACCESSIBILITY_NO);
            setContentView(
                    overlayView
                  , new ViewGroup.LayoutParams(
                            ViewGroup.LayoutParams.MATCH_PARENT
                          , ViewGroup.LayoutParams.MATCH_PARENT));
        }

        @Override
        protected void onStart() {
            super.onStart();
            ConfigureWindow(getWindow(), hostActivity, panelHeight);
        }

        void Apply(int newColor, int newPanelHeight) {
            color = newColor;
            if (overlayView != null) overlayView.setBackgroundColor(newColor);
            if (newPanelHeight == panelHeight) return;

            panelHeight = newPanelHeight;
            // Window.setLayout updates the window in place; it never removes
            // and adds it, so its place in the stack is kept.
            Window window = getWindow();
            if (window != null) {
                window.setLayout(
                        ViewGroup.LayoutParams.MATCH_PARENT
                      , ResolveHeight(newPanelHeight));
            }
        }

        // The decor view's visibility, set directly rather than through
        // Dialog.show and Dialog.hide. Those two do the same thing on a
        // window already added - and show() would ADD it again on one that
        // is not, which is the one thing this window must never do.
        void SetRevealed(boolean revealed) {
            Window window = getWindow();
            View decor = window == null ? null : window.peekDecorView();
            if (decor == null) return;

            decor.setVisibility(revealed ? View.VISIBLE : View.GONE);
        }

        private static int ResolveHeight(int height) {
            return height > 0
                    ? height
                    : ViewGroup.LayoutParams.MATCH_PARENT;
        }

        private static void ConfigureWindow(
                Window window
              , Activity activity
              , int panelHeight) {
            if (window == null
                    || activity == null
                    || activity.getWindow() == null) {
                return;
            }

            View hostDecorView = activity.getWindow().getDecorView();
            android.os.IBinder hostWindowToken =
                    hostDecorView.getWindowToken();
            if (hostWindowToken == null) {
                hostWindowToken =
                        hostDecorView.getApplicationWindowToken();
            }
            if (hostWindowToken == null) return;

            // A partial cover is a SHORT WINDOW, not a full one with a
            // transparent top: the strip above it never enters this window's
            // input region, so the game keeps receiving touches there.
            //
            // The height comes from the ad, not from a formula of this
            // window's own. A cover that measured for itself once read
            // decorView.getHeight(), which matches the display height on a
            // plain emulator and does NOT on a device with a cutout or a
            // gesture bar - and the difference showed as a black band above
            // the ad, since both windows sit at Gravity.BOTTOM.
            int coverHeight = ResolveHeight(panelHeight);

            WindowManager.LayoutParams attributes =
                    window.getAttributes();
            attributes.token = hostWindowToken;
            attributes.type =
                    WindowManager.LayoutParams.TYPE_APPLICATION_SUB_PANEL;
            attributes.width = ViewGroup.LayoutParams.MATCH_PARENT;
            attributes.height = coverHeight;
            attributes.gravity = android.view.Gravity.BOTTOM;
            window.setAttributes(attributes);

            window.setFlags(
                    WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED
                  , WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
            window.addFlags(
                    WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE);
            window.clearFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);
            window.setDimAmount(0f);
            window.setBackgroundDrawable(
                    new ColorDrawable(Color.TRANSPARENT));
            window.setWindowAnimations(0);
            window.setLayout(
                    ViewGroup.LayoutParams.MATCH_PARENT
                  , coverHeight);

            window.getDecorView().setSystemUiVisibility(
                    View.SYSTEM_UI_FLAG_LAYOUT_STABLE
                            | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                            | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                            | View.SYSTEM_UI_FLAG_FULLSCREEN
                            | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                            | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                attributes = window.getAttributes();
                attributes.layoutInDisplayCutoutMode =
                        WindowManager.LayoutParams
                                .LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES;
                window.setAttributes(attributes);
            }
        }
    }
}