package com.riseon.ads.levelplay;

import android.app.Activity;
import android.os.Handler;
import android.os.HandlerThread;

import com.unity3d.mediation.LevelPlayAdError;
import com.unity3d.mediation.LevelPlayAdInfo;
import com.unity3d.mediation.impression.LevelPlayImpressionData;
import com.unity3d.mediation.impression.LevelPlayImpressionDataListener;
import com.unity3d.mediation.interstitial.LevelPlayInterstitialAd;
import com.unity3d.mediation.interstitial.LevelPlayInterstitialAdListener;
import com.unity3d.mediation.rewarded.LevelPlayReward;
import com.unity3d.mediation.rewarded.LevelPlayRewardedAd;
import com.unity3d.mediation.rewarded.LevelPlayRewardedAdListener;

// An interstitial or rewarded ad of LevelPlay's own Android API, whose events reach C# without Unity's player loop.
//
// Unity's bridge hands every event to the player loop, which a pause under a full-screen ad stops, so a cover could never
// come down on a LevelPlay close. Here the SDK's events, which come on the main thread, are posted to one event thread of
// the pack's, which calls C#: entering the scripting runtime can stall for as long as Unity stays paused, and the main
// thread must never be the one stalled (Android's ANR). The one thread also keeps the events in order.
public final class FullScreenAd {
    // How long a close waits for the reward of a network that reports it after the close.
    private static final long REWARD_GRACE_MS = 1000;

    private static final Object LOCK = new Object();
    private static Handler events;

    private final FullScreenAdListener listener;
    private final LevelPlayInterstitialAd interstitial;
    private final LevelPlayRewardedAd rewarded;

    // The event thread's only.
    private boolean rewardSeen;
    private Runnable pendingClose;

    private volatile boolean destroyed;

    public FullScreenAd(String adUnitId, boolean rewarding, FullScreenAdListener listener) {
        this.listener = listener;
        if (rewarding) {
            interstitial = null;
            rewarded = new LevelPlayRewardedAd(adUnitId);
            rewarded.setListener(new RewardedListener());
            rewarded.setImpressionDataListener(new PaidListener());
        } else {
            rewarded = null;
            interstitial = new LevelPlayInterstitialAd(adUnitId);
            interstitial.setListener(new InterstitialListener());
            interstitial.setImpressionDataListener(new PaidListener());
        }
    }

    public void load() {
        if (rewarded != null) rewarded.loadAd();
        else interstitial.loadAd();
    }

    public boolean isReady() {
        return rewarded != null ? rewarded.isAdReady() : interstitial.isAdReady();
    }

    // Any thread: a chain shows its next step from the last one's close, on the event thread. The activity comes from
    // C#, as an androidlib does not compile against Unity's classes.
    public void show(final Activity activity, final String placement) {
        post(() -> rewardSeen = false);
        activity.runOnUiThread(() -> {
            if (rewarded != null) rewarded.showAd(activity, placement);
            else interstitial.showAd(activity, placement);
        });
    }

    // LevelPlay's Android ads have no destroy of their own, and its Kotlin setters take no null listener: the events still
    // come, and stop here.
    public void destroy() {
        destroyed = true;
    }

    // An event for C#, unless the ad was destroyed by the time the event thread runs it.
    private void emit(Runnable event) {
        post(() -> {
            if (!destroyed) event.run();
        });
    }

    private static void post(Runnable event) {
        Handler current;
        synchronized (LOCK) {
            if (events == null) {
                HandlerThread thread = new HandlerThread("RiseOnLevelPlayEvents");
                thread.start();
                events = new Handler(thread.getLooper());
            }
            current = events;
        }
        current.post(event);
    }

    private void loaded() {
        emit(listener::onLoaded);
    }

    private void loadFailed(LevelPlayAdError error) {
        final int code = error.getErrorCode();
        final String message = error.getErrorMessage();
        emit(() -> listener.onLoadFailed(code, message));
    }

    private void displayed() {
        emit(listener::onDisplayed);
    }

    private void displayFailed(LevelPlayAdError error) {
        final int code = error.getErrorCode();
        final String message = error.getErrorMessage();
        emit(() -> listener.onDisplayFailed(code, message));
    }

    private void closed(boolean waitsForReward) {
        emit(() -> {
            if (!waitsForReward || rewardSeen) {
                listener.onClosed();
                return;
            }

            pendingClose = () -> {
                pendingClose = null;
                if (!destroyed) listener.onClosed();
            };
            events.postDelayed(pendingClose, REWARD_GRACE_MS);
        });
    }

    private void rewardedNow() {
        emit(() -> {
            if (rewardSeen) return;

            rewardSeen = true;
            listener.onRewarded();
            if (pendingClose == null) return;

            events.removeCallbacks(pendingClose);
            pendingClose = null;
            listener.onClosed();
        });
    }

    private final class InterstitialListener implements LevelPlayInterstitialAdListener {
        @Override public void onAdLoaded(LevelPlayAdInfo info) { loaded(); }
        @Override public void onAdLoadFailed(LevelPlayAdError error) { loadFailed(error); }
        @Override public void onAdDisplayed(LevelPlayAdInfo info) { displayed(); }
        @Override public void onAdDisplayFailed(LevelPlayAdError error, LevelPlayAdInfo info) { displayFailed(error); }
        @Override public void onAdClosed(LevelPlayAdInfo info) { closed(false); }
    }

    private final class RewardedListener implements LevelPlayRewardedAdListener {
        @Override public void onAdLoaded(LevelPlayAdInfo info) { loaded(); }
        @Override public void onAdLoadFailed(LevelPlayAdError error) { loadFailed(error); }
        @Override public void onAdDisplayed(LevelPlayAdInfo info) { displayed(); }
        @Override public void onAdDisplayFailed(LevelPlayAdError error, LevelPlayAdInfo info) { displayFailed(error); }
        @Override public void onAdRewarded(LevelPlayReward reward, LevelPlayAdInfo info) { rewardedNow(); }
        @Override public void onAdClosed(LevelPlayAdInfo info) { closed(true); }
    }

    private final class PaidListener implements LevelPlayImpressionDataListener {
        @Override
        public void onImpressionSuccess(LevelPlayImpressionData data) {
            final String network = data.getAdNetwork();
            final String unit = data.getMediationAdUnitId();
            final Double revenue = data.getRevenue();
            emit(() -> listener.onPaid(network, unit, revenue == null ? 0 : revenue));
        }
    }
}