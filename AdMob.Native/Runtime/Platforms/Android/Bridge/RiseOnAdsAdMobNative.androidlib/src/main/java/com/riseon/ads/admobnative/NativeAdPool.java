package com.riseon.ads.admobnative;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.util.Log;

import com.google.android.gms.ads.AdListener;
import com.google.android.gms.ads.AdLoader;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.VideoOptions;
import com.google.android.gms.ads.nativead.NativeAd;
import com.google.android.gms.ads.nativead.NativeAdOptions;

import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.IdentityHashMap;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import java.util.Set;

// The warm native ads of one ad unit id, shared by every format drawing from
// that unit: a rect ad, a collapsible panel and a full-screen ad all
// show the same Native advanced creative, only laid out differently. The
// unit id stays with the ad because the request went to it: revenue,
// reports and the unit's mediation are counted against that unit.
//
// Split by the mute policy too: whether a video starts muted is a request
// option, and VideoController.mute works only with custom controls, which
// are only available for Ad Manager reservations (developers.google.com/
// android/reference/com/google/android/gms/ads/VideoController). A format
// that pauses the game loads with sound, one beside the game loads muted.
//
// A consumer takes an ad, and gives back one it never displayed: one that did
// not fit is marked with the consumer's size key, and the others still get
// it. A full pool whose ads fit nobody who asks would stop loading, so a
// consumer finding nothing for its size drops one: an ad that fits no
// consumer, else the oldest that does not fit it.
//
// Main thread only.
final class NativeAdPool {
    static final long ANY_SIZE = -1L;
    static final long NO_RETRY_SCHEDULED_MS = -1L;
    // developers.google.com/admob/android/native/start, Request ads: "Since
    // ads expire after an hour, you should clear this cache and reload with
    // new ads every hour."
    static final long MAX_CACHED_AD_AGE_MS = 3_600_000L;
    private static final int MAX_RETRY_EXPONENT = 5;
    private static final long RETRY_BASE_DELAY_MS = 1_000L;
    private static final String TAG = "NativeAdPool";

    private static final Map<String, NativeAdPool> pools = new HashMap<>();

    interface Consumer {
        // How many warm ads it would like on hand.
        int WantedCount();
        // Whether it could lay the ad out somewhere it takes ads for now.
        boolean CouldUse(CachedAd cached);
        // Ads arrived or came back; it may take one.
        void OnPoolChanged();
        void OnLoadStarted();
        void OnLoadCompleted(int code, String message, long retryDelayMs);
        // A click on an ad it holds.
        void OnAdClicked(NativeAd ad);
    }

    static final class CachedAd {
        final NativeAd ad;
        final long loadedAtMs;
        // The size keys it failed to lay out in.
        final Set<Long> unfitSizes = new HashSet<>();

        CachedAd(
                NativeAd ad
              , long loadedAtMs) {
            this.ad = ad;
            this.loadedAtMs = loadedAtMs;
        }
    }

    private final Handler main = new Handler(Looper.getMainLooper());
    private final Runnable retryRunnable = this::HandleRetry;
    private final Runnable cacheExpiryRunnable = this::HandleCacheExpiry;
    private final String key;
    private final String adUnitId;
    private final boolean startMuted;
    private final List<Consumer> consumers = new ArrayList<>();
    // Oldest first: the head is the first to go stale.
    private final ArrayDeque<CachedAd> cachedAds = new ArrayDeque<>();
    // Who holds each ad taken out, for its clicks.
    private final Map<NativeAd, Consumer> holders = new IdentityHashMap<>();
    private Activity activity;
    private boolean released;
    private boolean isAdLoading;
    private boolean retryScheduled;
    private int noFillStreak;
    private int generation;

    private NativeAdPool(
            String key
          , String adUnitId
          , boolean startMuted) {
        this.key = key;
        this.adUnitId = adUnitId;
        this.startMuted = startMuted;
    }

    // The pool of the unit and mute policy, made on first use; the consumer
    // joins it and loading starts once an Activity is known.
    static NativeAdPool Join(
            String adUnitId
          , boolean startMuted
          , Consumer consumer) {
        String key = adUnitId + "|" + startMuted;
        NativeAdPool pool = pools.get(key);
        if (pool == null) {
            pool = new NativeAdPool(key, adUnitId, startMuted);
            pools.put(key, pool);
        }
        pool.consumers.add(consumer);
        return pool;
    }

    // The last consumer to leave takes the pool with it.
    void Leave(Consumer consumer) {
        consumers.remove(consumer);
        for (Iterator<Map.Entry<NativeAd, Consumer>> it =
                holders.entrySet().iterator(); it.hasNext(); ) {
            if (it.next().getValue() == consumer) it.remove();
        }
        if (!consumers.isEmpty() || released) return;

        released = true;
        ++generation;
        main.removeCallbacks(retryRunnable);
        main.removeCallbacks(cacheExpiryRunnable);
        for (CachedAd cached : cachedAds) cached.ad.destroy();
        cachedAds.clear();
        holders.clear();
        activity = null;
        pools.remove(key);
    }

    String AdUnitId() {
        return adUnitId;
    }

    int NoFillStreak() {
        return noFillStreak;
    }

    int CachedCount() {
        return cachedAds.size();
    }

    boolean IsLoading() {
        return isAdLoading;
    }

    Activity CurrentActivity() {
        return activity;
    }

    // Every consumer call hands over the current Unity activity: requests are
    // built on it.
    void AdoptActivity(Activity currentActivity) {
        if (released || !BaseAd.IsActivityUsable(currentActivity)) return;

        activity = currentActivity;
        StartLoad();
    }

    boolean HasAdFor(long sizeKey) {
        RemoveExpiredAds();
        for (CachedAd cached : cachedAds) {
            if (Fits(cached, sizeKey)) return true;
        }
        return false;
    }

    // The oldest ad that may fit, taken out for the consumer; its seat is
    // refilled at once.
    CachedAd Take(
            long sizeKey
          , Consumer consumer) {
        RemoveExpiredAds();
        for (Iterator<CachedAd> it = cachedAds.iterator(); it.hasNext(); ) {
            CachedAd cached = it.next();
            if (!Fits(cached, sizeKey)) continue;

            it.remove();
            holders.put(cached.ad, consumer);
            ScheduleCacheExpiry();
            StartLoad();
            return cached;
        }
        return null;
    }

    // An ad taken and never displayed comes back in age order. With a size
    // key, it did not fit there. The other consumers hear of it on the next
    // message, after the one giving it back is done.
    void GiveBack(
            NativeAd ad
          , long loadedAtMs
          , long unfitSizeKey) {
        if (ad == null) return;

        holders.remove(ad);
        if (released) {
            ad.destroy();
            return;
        }

        CachedAd returned = new CachedAd(ad, loadedAtMs);
        if (unfitSizeKey != ANY_SIZE) returned.unfitSizes.add(unfitSizeKey);
        ArrayDeque<CachedAd> ordered = new ArrayDeque<>();
        boolean placed = false;
        for (CachedAd cached : cachedAds) {
            if (!placed && cached.loadedAtMs > loadedAtMs) {
                ordered.addLast(returned);
                placed = true;
            }
            ordered.addLast(cached);
        }
        if (!placed) ordered.addLast(returned);
        cachedAds.clear();
        cachedAds.addAll(ordered);
        ScheduleCacheExpiry();
        if (unfitSizeKey != ANY_SIZE) {
            Log.i(TAG, "Took back an ad unfit for size " + unfitSizeKey + ": " + Describe());
        }
        main.post(this::NotifyPoolChanged);
    }

    // An ad that was displayed, or that its holder is done with.
    void Destroy(NativeAd ad) {
        if (ad == null) return;

        holders.remove(ad);
        ad.destroy();
    }

    // From a consumer that found nothing for its size: makes room in a full
    // pool, then loads when it may.
    void RequestFor(long sizeKey) {
        if (released) return;
        if (cachedAds.size() >= Capacity()) DropUnfitAd(sizeKey);
        if (StartLoad()) return;
        if (!isAdLoading && !retryScheduled && HasFreeSeat()) {
            PostRetry(BackoffDelayMs(Math.max(1, noFillStreak), 0));
        }
    }

    static long BackoffDelayMs(int streak, int immediateAttempts) {
        if (streak <= immediateAttempts) return 0L;

        int exponent = Math.min(
                MAX_RETRY_EXPONENT
              , streak - immediateAttempts);
        return RETRY_BASE_DELAY_MS * (1L << exponent);
    }

    private static boolean Fits(CachedAd cached, long sizeKey) {
        return sizeKey == ANY_SIZE || !cached.unfitSizes.contains(sizeKey);
    }

    // The ads it keeps warm when full: the most any consumer wants. The
    // consumers share the ads and seldom all show at once, so the largest
    // want covers them; each ad taken is reloaded, so consumers that find the
    // pool empty get one in turn.
    int Capacity() {
        int wanted = 1;
        for (Consumer consumer : consumers) {
            wanted = Math.max(wanted, consumer.WantedCount());
        }
        return wanted;
    }

    private boolean HasFreeSeat() {
        return cachedAds.size() < Capacity();
    }

    private void DropUnfitAd(long sizeKey) {
        CachedAd dropped = null;
        for (CachedAd cached : cachedAds) {
            if (FitsNoConsumer(cached)) {
                dropped = cached;
                break;
            }
            if (dropped == null && !Fits(cached, sizeKey)) dropped = cached;
        }
        if (dropped == null) return;

        cachedAds.remove(dropped);
        dropped.ad.destroy();
        ScheduleCacheExpiry();
        Log.i(TAG, "Dropped an ad unfit for size " + sizeKey + " to make room: "
                + Describe());
    }

    private boolean FitsNoConsumer(CachedAd cached) {
        for (Consumer consumer : consumers) {
            if (consumer.CouldUse(cached)) return false;
        }
        return true;
    }

    private boolean StartLoad() {
        if (released
                || isAdLoading
                || retryScheduled
                || !HasFreeSeat()
                || !BaseAd.IsActivityUsable(activity)) {
            return false;
        }

        isAdLoading = true;
        final int requestGeneration = ++generation;
        final NativeAd[] loaded = new NativeAd[1];
        for (Consumer consumer : new ArrayList<>(consumers)) {
            consumer.OnLoadStarted();
        }

        try {
            VideoOptions videoOptions = new VideoOptions.Builder()
                    .setStartMuted(startMuted)
                    .build();
            NativeAdOptions nativeAdOptions = new NativeAdOptions.Builder()
                    .setMediaAspectRatio(
                            NativeAdOptions.NATIVE_MEDIA_ASPECT_RATIO_ANY)
                    .setAdChoicesPlacement(
                            NativeAdOptions.ADCHOICES_TOP_RIGHT)
                    .setVideoOptions(videoOptions)
                    .build();

            AdLoader adLoader = new AdLoader.Builder(activity, adUnitId)
                    .forNativeAd(ad -> {
                        if (released || requestGeneration != generation) {
                            ad.destroy();
                            return;
                        }
                        loaded[0] = ad;
                        cachedAds.addLast(new CachedAd(
                                ad
                              , SystemClock.elapsedRealtime()));
                        Log.i(TAG, "Loaded: " + Describe());
                        ScheduleCacheExpiry();
                    })
                    .withNativeAdOptions(nativeAdOptions)
                    .withAdListener(new AdListener() {
                        @Override
                        public void onAdFailedToLoad(LoadAdError error) {
                            if (released || requestGeneration != generation) {
                                return;
                            }

                            isAdLoading = false;
                            long retryDelayMs = ScheduleNoFillRetry();
                            NotifyLoadCompleted(
                                    error.getCode()
                                  , error.getMessage()
                                  , retryDelayMs);
                        }

                        @Override
                        public void onAdLoaded() {
                            if (released || requestGeneration != generation) {
                                return;
                            }

                            isAdLoading = false;
                            noFillStreak = 0;
                            NotifyLoadCompleted(0, "", NO_RETRY_SCHEDULED_MS);
                            NotifyPoolChanged();
                            StartLoad();
                        }

                        @Override
                        public void onAdClicked() {
                            // Not gated on the generation: the ad clicked may
                            // come from an earlier load.
                            NativeAd ad = loaded[0];
                            Consumer holder = ad == null ? null : holders.get(ad);
                            if (holder != null) holder.OnAdClicked(ad);
                        }
                    })
                    .build();

            adLoader.loadAd(new AdRequest.Builder().build());
            return true;
        } catch (RuntimeException exception) {
            isAdLoading = false;
            Log.e(TAG, "Native ad load failed before callback", exception);
            long retryDelayMs = ScheduleNoFillRetry();
            NotifyLoadCompleted(
                    BaseAd.INTERNAL_LOAD_ERROR
                  , "Failed to load ad: "
                            + String.valueOf(exception.getMessage())
                  , retryDelayMs);
            return false;
        }
    }

    private void NotifyLoadCompleted(
            int code
          , String message
          , long retryDelayMs) {
        for (Consumer consumer : new ArrayList<>(consumers)) {
            consumer.OnLoadCompleted(code, message, retryDelayMs);
        }
    }

    private void NotifyPoolChanged() {
        if (released) return;

        for (Consumer consumer : new ArrayList<>(consumers)) {
            if (released || cachedAds.isEmpty()) return;
            consumer.OnPoolChanged();
        }
    }

    // No fill is a property of inventory: waiting is the only thing that
    // can help, so back off from the first miss.
    private long ScheduleNoFillRetry() {
        ++noFillStreak;
        return PostRetry(BackoffDelayMs(noFillStreak, 0));
    }

    private long PostRetry(long delayMs) {
        main.removeCallbacks(retryRunnable);
        if (released) return NO_RETRY_SCHEDULED_MS;

        retryScheduled = true;
        main.postDelayed(retryRunnable, Math.max(0L, delayMs));
        return delayMs;
    }

    private void HandleRetry() {
        retryScheduled = false;
        if (released) return;

        NotifyPoolChanged();
        if (StartLoad()) return;
        // A missing Activity ends nothing: one arrives with the next call.
        if (!isAdLoading && HasFreeSeat()) {
            PostRetry(BackoffDelayMs(Math.max(1, noFillStreak), 0));
        }
    }

    private void HandleCacheExpiry() {
        if (released) return;

        RemoveExpiredAds();
        StartLoad();
        ScheduleCacheExpiry();
    }

    private void ScheduleCacheExpiry() {
        main.removeCallbacks(cacheExpiryRunnable);
        if (released) return;

        long nextExpiryAtMs = Long.MAX_VALUE;
        for (CachedAd cached : cachedAds) {
            nextExpiryAtMs = Math.min(
                    nextExpiryAtMs
                  , cached.loadedAtMs + MAX_CACHED_AD_AGE_MS);
        }
        if (nextExpiryAtMs == Long.MAX_VALUE) return;

        main.postDelayed(
                cacheExpiryRunnable
              , Math.max(0L, nextExpiryAtMs - SystemClock.elapsedRealtime()));
    }

    // Also swept on every take: the timer runs on uptime, which stops while
    // the device sleeps.
    private void RemoveExpiredAds() {
        long nowMs = SystemClock.elapsedRealtime();
        for (Iterator<CachedAd> it = cachedAds.iterator(); it.hasNext(); ) {
            CachedAd cached = it.next();
            if (nowMs - cached.loadedAtMs < MAX_CACHED_AD_AGE_MS) continue;

            cached.ad.destroy();
            it.remove();
        }
    }

    String Describe() {
        return "unit=" + adUnitId
                + " muted=" + startMuted
                + " cache " + cachedAds.size() + "/" + Capacity()
                + " consumers=" + consumers.size();
    }
}
