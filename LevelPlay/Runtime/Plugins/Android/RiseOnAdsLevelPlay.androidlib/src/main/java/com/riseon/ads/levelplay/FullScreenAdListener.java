package com.riseon.ads.levelplay;

// A full-screen ad's events, called on the pack's own event thread, never on Android's main thread nor through Unity's
// player loop. Implemented in C# by an AndroidJavaProxy, which dispatches by name: keep these in step with it.
public interface FullScreenAdListener {
    void onLoaded();

    void onLoadFailed(int code, String message);

    void onDisplayed();

    void onDisplayFailed(int code, String message);

    // Before onClosed, also for a network that reports the reward after the close.
    void onRewarded();

    void onClosed();

    // Revenue in US dollars.
    void onPaid(String adNetwork, String adUnitId, double revenue);
}