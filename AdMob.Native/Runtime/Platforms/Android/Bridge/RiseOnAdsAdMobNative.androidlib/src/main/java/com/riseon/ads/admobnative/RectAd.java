package com.riseon.ads.admobnative;

import android.app.Activity;
import android.util.Log;

import java.util.Collections;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

import com.google.android.gms.ads.nativead.NativeAd;

// A native ad in a rect the game places: a feed cell or a block such as an
// MRec. The ads come from the unit's NativeAdPool, muted, shared with every
// other format drawing from the same unit; the rect takes one while shown,
// and gives back one it never displayed. Everything rect-shaped (layout,
// dwell, rotation, watchdogs) lives in RectAdSlot.
//
// One that is never configured still joins the pool with its cache size, so
// a game can keep a unit warm before it places any rect.
public final class RectAd extends BaseAd implements NativeAdPool.Consumer {
    static final String TAG = "RectAd";
    private static final int LOAD_SUCCESS_CODE = 0;
    static final long NO_RETRY_SCHEDULED_MS = NativeAdPool.NO_RETRY_SCHEDULED_MS;
    static final long MAX_CACHED_AD_AGE_MS = NativeAdPool.MAX_CACHED_AD_AGE_MS;

    private final String adUnitId;
    private final int cacheSize;
    private final int backgroundColor;
    private final RectAdSlot slot;
    private Activity activity;
    // Main thread only; joined on the first message, left on release.
    private NativeAdPool pool;
    // The ads the rect holds. The paid event asks from whatever thread the
    // SDK chose, so the set is a concurrent one.
    private final Set<NativeAd> ownedAds =
            Collections.newSetFromMap(new ConcurrentHashMap<>());
    private volatile RectAdListener listener;

    public RectAd(
            Activity currentActivity
          , String adUnitId
          , int cacheSize
          , int backgroundColor) {
        if (adUnitId == null || adUnitId.trim().isEmpty()) {
            throw new IllegalArgumentException(
                    "RectAd requires a non-empty adUnitId");
        }

        this.adUnitId = adUnitId;
        this.cacheSize = Math.max(1, cacheSize);
        this.backgroundColor = backgroundColor;
        this.activity = currentActivity;
        this.slot = new RectAdSlot(this);
        main.post(() -> {
            if (released) return;

            pool = NativeAdPool.Join(adUnitId, true, this);
            if (!IsActivityUsable(activity)) {
                Log.e(
                        TAG
                      , "RectAd created without a usable Activity; "
                                + "loading will start when Configure or "
                                + "Show receives one");
                return;
            }
            pool.AdoptActivity(activity);
        });
    }

    public void SetListener(RectAdListener newListener) {
        if (released) return;
        listener = newListener;
    }

    // The corner radius travels with the rect: the rect is measured on screen
    // by the caller, which only then knows both.
    public void Configure(
            Activity currentActivity
          , int xPx
          , int yPx
          , int widthPx
          , int heightPx
          , int roundCornerPx) {
        main.post(() -> {
            if (released) {
                Log.e(
                        TAG
                      , "Configure ignored after Release; create a new instance");
                return;
            }
            if (!IsActivityUsable(currentActivity)) {
                Log.e(TAG, "Configure requires a usable Activity");
                return;
            }

            AdoptActivity(currentActivity);
            slot.Configure(xPx, yPx, widthPx, heightPx, roundCornerPx);
        });
    }

    public void Show(Activity currentActivity) {
        if (released) return;
        main.post(() -> {
            if (released) return;
            if (!IsActivityUsable(currentActivity)) {
                Log.e(TAG, "Show ignored because Activity is not usable");
                return;
            }

            AdoptActivity(currentActivity);
            slot.Show();
        });
    }

    public void Hide() {
        if (released) return;
        main.post(() -> {
            if (!released) slot.Hide();
        });
    }

    public void SetPosition(int xPx, int yPx) {
        if (released) return;
        main.post(() -> {
            if (!released) slot.SetPosition(xPx, yPx);
        });
    }

    public void Release() {
        if (released) return;
        released = true;
        main.removeCallbacksAndMessages(null);
        main.post(() -> {
            slot.ReleaseOnMain();
            if (pool != null) pool.Leave(this);
            pool = null;
            activity = null;
            listener = null;
        });
    }

    // Every call hands over the current Unity activity; a change means the old
    // windows are gone, so the rect starts over on the new one.
    private void AdoptActivity(Activity currentActivity) {
        if (activity != null && activity != currentActivity) {
            slot.HandleActivityChanged();
        }
        activity = currentActivity;
        if (pool != null) pool.AdoptActivity(currentActivity);
    }

    static long BackoffDelayMs(int streak, int immediateAttempts) {
        return NativeAdPool.BackoffDelayMs(streak, immediateAttempts);
    }

    // ---- the pool, for the slot

    boolean HasCachedAdFor(long sizeKey) {
        return pool != null && pool.HasAdFor(sizeKey);
    }

    // The oldest ad that may fit the size, now held by this rect.
    NativeAdPool.CachedAd TakeCachedAd(long sizeKey) {
        if (pool == null) return null;

        NativeAdPool.CachedAd taken = pool.Take(sizeKey, this);
        if (taken == null) return null;

        ownedAds.add(taken.ad);
        BindPaidEvent(taken.ad, adUnitId, () -> OwnsAd(taken.ad));
        return taken;
    }

    // From a shown rect that found nothing for its size.
    void RequestLoadFor(long sizeKey) {
        if (pool != null) pool.RequestFor(sizeKey);
    }

    void RequestLoad() {
        RequestLoadFor(NativeAdPool.ANY_SIZE);
    }

    // An ad the rect took and never displayed.
    void ReturnCachedAd(
            NativeAd ad
          , long loadedAtMs
          , long unfitSizeKey) {
        if (ad == null) return;

        ownedAds.remove(ad);
        if (pool == null) {
            ad.destroy();
            return;
        }

        pool.GiveBack(ad, loadedAtMs, unfitSizeKey);
    }

    void DestroyAd(NativeAd ad) {
        if (ad == null) return;

        ownedAds.remove(ad);
        if (pool == null) {
            ad.destroy();
            return;
        }

        pool.Destroy(ad);
    }

    private boolean OwnsAd(NativeAd ad) {
        return ownedAds.contains(ad);
    }

    // ---- NativeAdPool.Consumer

    @Override
    public int WantedCount() {
        return cacheSize;
    }

    @Override
    public boolean CouldUse(NativeAdPool.CachedAd cached) {
        long sizeKey = slot.SizeKey();
        return sizeKey != RectAdSlot.NO_SIZE
                && !cached.unfitSizes.contains(sizeKey);
    }

    // A freshly loaded or returned ad goes to the rect if it is shown with
    // nothing on screen and nothing being built, and the ad may fit it.
    @Override
    public void OnPoolChanged() {
        if (released) return;

        if (slot.WantsCachedAd() && HasCachedAdFor(slot.SizeKey())) {
            slot.PresentCachedAd();
        }
    }

    @Override
    public void OnLoadStarted() {
        NotifyLoadingStarted();
    }

    @Override
    public void OnLoadCompleted(
            int code
          , String message
          , long retryDelayMs) {
        NotifyLoadingCompleted(
                code
              , code == LOAD_SUCCESS_CODE
                        ? ""
                        : BuildFailureMessage(message, retryDelayMs));
    }

    @Override
    public void OnAdClicked(NativeAd ad) {
        slot.CommitAdClick(ad);
    }

    Activity CurrentActivity() {
        return activity;
    }

    int BackgroundColor() {
        return backgroundColor;
    }

    String AdUnitId() {
        return adUnitId;
    }

    int NoFillStreak() {
        return pool == null ? 0 : pool.NoFillStreak();
    }

    private String BuildFailureMessage(
            String reason
          , long retryDelayMs) {
        String retryDescription = retryDelayMs == NO_RETRY_SCHEDULED_MS
                ? "not scheduled"
                : retryDelayMs + "ms";
        return String.valueOf(reason)
                + " | " + (pool == null ? "no pool" : pool.Describe())
                + " | retry=" + retryDescription;
    }

    // Every Notify* below hands its event to AdEventDispatcher instead of
    // crossing into C# on the calling thread - see the note on that class.
    // Arguments read from mutable state are snapshotted into locals BEFORE
    // the post, so each event carries the values of the moment it left.
    private void NotifyLoadingStarted() {
        RectAdListener current = listener;
        if (current == null) return;
        AdEventDispatcher.Post(() -> {
            try {
                current.OnLoadingStarted();
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnLoadingStarted callback failed", exception);
            }
        });
    }

    private void NotifyLoadingCompleted(
            int errorCode
          , String errorMessage) {
        RectAdListener current = listener;
        if (current == null) return;
        // The unit's pool, as every format on the unit reports it.
        int cachedCountNow = pool == null ? 0 : pool.CachedCount();
        int cacheSizeNow = pool == null ? cacheSize : pool.Capacity();
        AdEventDispatcher.Post(() -> {
            try {
                current.OnLoadingCompleted(
                        errorCode
                      , errorMessage
                      , cachedCountNow
                      , cacheSizeNow);
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnLoadingCompleted callback failed", exception);
            }
        });
    }

    @Override
    protected void NotifyAdPaid(
            String source
          , String paidAdUnitId
          , double value
          , String currencyCode
          , int precision) {
        RectAdListener current = listener;
        if (current == null) return;
        AdEventDispatcher.Post(() -> {
            try {
                current.OnAdPaid(
                        source
                      , paidAdUnitId
                      , value
                      , currencyCode
                      , precision);
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnAdPaid callback failed", exception);
            }
        });
    }

    void NotifyDisplayed() {
        RectAdListener current = listener;
        if (current == null) return;
        AdEventDispatcher.Post(() -> {
            try {
                current.OnDisplayed();
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnDisplayed callback failed", exception);
            }
        });
    }

    void NotifyShowNotReady() {
        RectAdListener current = listener;
        if (current == null) return;
        AdEventDispatcher.Post(() -> {
            try {
                current.OnShowNotReady();
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnShowNotReady callback failed", exception);
            }
        });
    }

    void NotifyPresentationFailed(
            int errorCode
          , String errorMessage) {
        RectAdListener current = listener;
        if (current == null) return;
        AdEventDispatcher.Post(() -> {
            try {
                current.OnPresentationFailed(errorCode, errorMessage);
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnPresentationFailed callback failed", exception);
            }
        });
    }
}