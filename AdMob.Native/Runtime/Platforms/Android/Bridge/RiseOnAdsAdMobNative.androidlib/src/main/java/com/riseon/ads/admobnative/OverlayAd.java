package com.riseon.ads.admobnative;

import android.app.Activity;
import android.os.SystemClock;
import android.util.DisplayMetrics;
import android.util.Log;

import com.google.android.gms.ads.nativead.NativeAd;

import java.util.ArrayDeque;
import java.util.Collections;
import java.util.Iterator;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

public final class OverlayAd extends BaseAd implements NativeAdPool.Consumer {
    // BaseAd's tag is the base class's name, which files every line this
    // class logs under the wrong heading - OverlayAdActivity and
    // OverlayAdContentView already use this one, and so does the iOS port.
    private static final String TAG = "OverlayAd";


    private static final long MAX_CACHED_AD_AGE_MS =
            NativeAdPool.MAX_CACHED_AD_AGE_MS;

    private static final int SIDE_LEFT           = 0;
    private static final int SIDE_RIGHT          = 1;
    private static final int TIMER_SIDE_OPPOSITE = 3;
    private static final int TIMER_SIDE_SAME     = 4;

    static final class OverlayAdStyle {
        final boolean fullScreen;
        final int cooldown;
        // Ordinals shared with the C# enums: Left 0, Right 1, Random 2, and
        // for the timer OppositeOfClose 3, SameAsClose 4.
        final int closeSide;
        final int timerSide;
        final float heightRatio;
        final int backgroundColor;
        // The close button commits the ad's click on its way out.
        final boolean redirectOnClose;
        // How a full-screen ad arranges itself, ordinals shared with the C#
        // FullScreenLayout: Interstitial 0, AppOpen 1, EndCard 2.
        final int layout;

        OverlayAdStyle(
                boolean fullScreen
              , int cooldown
              , int closeSide
              , int timerSide
              , float heightRatio
              , int backgroundColor
              , boolean redirectOnClose
              , int layout) {
            this.fullScreen = fullScreen;
            this.cooldown = Math.max(0, cooldown);
            this.closeSide = closeSide;
            this.timerSide = timerSide;
            this.heightRatio = heightRatio;
            this.backgroundColor = backgroundColor;
            this.redirectOnClose = redirectOnClose;
            this.layout = layout;
        }

        OverlayAdStyle WithClose(
                int newCooldown
              , int newCloseSide
              , int newTimerSide
              , boolean newRedirectOnClose) {
            return new OverlayAdStyle(
                    fullScreen
                  , newCooldown
                  , newCloseSide
                  , newTimerSide
                  , heightRatio
                  , backgroundColor
                  , newRedirectOnClose
                  , layout);
        }

        OverlayAdStyle WithLayout(int newLayout) {
            return new OverlayAdStyle(
                    fullScreen
                  , cooldown
                  , closeSide
                  , timerSide
                  , heightRatio
                  , backgroundColor
                  , redirectOnClose
                  , newLayout);
        }

        // Random is answered once per presentation; the relative timer modes
        // then read that answer rather than rolling again.
        boolean ResolveCloseOnLeft() {
            if (closeSide == SIDE_LEFT) return true;
            if (closeSide == SIDE_RIGHT) return false;
            return Math.random() < 0.5d;
        }

        boolean ResolveTimerOnLeft(boolean closeOnLeft) {
            switch (timerSide) {
                case SIDE_LEFT:               return true;
                case SIDE_RIGHT:              return false;
                case TIMER_SIDE_OPPOSITE:     return !closeOnLeft;
                case TIMER_SIDE_SAME:         return closeOnLeft;
                default:                      return Math.random() < 0.5d;
            }
        }

        boolean PausesGame() {
            return fullScreen;
        }
    }

    private final String adUnitId;
    private volatile OverlayAdStyle configuredStyle;
    private volatile int cacheSize = 1;

    // The head it holds from the pool, at most one: the ad the next Show
    // takes, and the only one worth a prepared face.
    private final ArrayDeque<CachedAd> cachedAds = new ArrayDeque<>();
    private final Runnable cacheExpiryRunnable = this::HandleCacheExpiry;
    private final Runnable layoutRetryRunnable = this::HandleLayoutRetry;
    private boolean layoutRetryScheduled;
    // Main thread only; joined at Configure, left at Release.
    private NativeAdPool pool;
    // A creative that will not lay out is one creative's fault, and the
    // next one usually fits - so this streak gets its first attempts free.
    private int layoutFailStreak;
    // Whether anything has ever laid out in this placement. Until something
    // has, a run of failures is more likely the panel than the creatives.
    private boolean layoutProven;
    // The stage a timer-driven refill builds on. Every Load and Show hands
    // one in; the sweep that fires an hour later has none of its own.
    private Activity cacheActivity;
    // The deque lives on the main thread; readiness is asked for from
    // Unity's. One volatile count is the crossing point.
    private volatile int cachedCount;
    // The ads it holds, the head and the one on screen. The paid event fires
    // on whatever thread the SDK picked, so membership is a concurrent set.
    private final Set<NativeAd> ownedAds =
            Collections.newSetFromMap(new ConcurrentHashMap<>());
    private volatile NativeAd activeNativeAd;
    // When the ad on screen loaded, for its age if it goes back to the pool.
    private long activeLoadedAtMs;
    private volatile boolean configured;
    private volatile boolean isAdLoading;
    private OverlayAdPresentation presentation;
    private OverlayAdPresentation preparedPresentation;
    private Activity preparedActivity;
    private NativeAd preparedNativeAd;
    private OverlayAdStyle preparedStyle;
    private OverlayAdContentView preparedContentView;
    private Activity preparedContentActivity;
    private NativeAd preparedContentAd;
    private boolean preparedContentCloseOnLeft;
    private OverlayAdActivity.CloseRelay preparedContentCloseRelay;
    private String preparedContentMediaSignature;
    // The screen the prepared face was laid out for; a turned screen needs
    // the other arrangement.
    private int preparedContentScreenWidth;
    private int preparedContentScreenHeight;
    private String preparedPresentationMediaSignature;

    // The face of the creative's assets at one moment. A presentation built
    // at load time is only valid while this stays the same: assets that
    // finish arriving later change the layout the ad needs, and a stale
    // face must be rebuilt rather than shown.
    // An ad and the moment it arrived - the only two things needed to know
    // whether it may still be shown, and to give it back in age order.
    private static final class CachedAd {
        final NativeAd ad;
        final long loadedAtMs;

        CachedAd(
                NativeAd ad
              , long loadedAtMs) {
            this.ad = ad;
            this.loadedAtMs = loadedAtMs;
        }
    }

    private static String MediaSignature(
            NativeAd nativeAd) {
        com.google.android.gms.ads.MediaContent mediaContent =
                nativeAd.getMediaContent();
        boolean hasVideo = mediaContent != null
                && mediaContent.hasVideoContent();
        boolean hasMainImage = mediaContent != null
                && mediaContent.getMainImage() != null;
        float aspectRatio = mediaContent != null
                ? mediaContent.getAspectRatio()
                : 0f;
        boolean hasAnyImage = false;
        java.util.List<NativeAd.Image>
                images = nativeAd.getImages();
        if (images != null) {
            for (NativeAd.Image image
                    : images) {
                if (image != null && image.getDrawable() != null) {
                    hasAnyImage = true;
                    break;
                }
            }
        }
        return hasVideo + "|" + hasMainImage + "|" + hasAnyImage
                + "|" + aspectRatio;
    }
    private NativeAdCompletedListener activeShowCompleted;
    private String activeActivitySessionId;

    // The prepared full-screen face: the content view built at load time,
    // waiting for the Activity that will host it.
    static final class PreparedFullScreenContent {
        final OverlayAdContentView view;
        final boolean closeOnLeft;
        final OverlayAdActivity.CloseRelay closeRelay;

        PreparedFullScreenContent(
                OverlayAdContentView view
              , boolean closeOnLeft
              , OverlayAdActivity.CloseRelay closeRelay) {
            this.view = view;
            this.closeOnLeft = closeOnLeft;
            this.closeRelay = closeRelay;
        }
    }

    public OverlayAd(String adUnitId) {
        if (adUnitId == null || adUnitId.trim().isEmpty()) {
            throw new IllegalArgumentException(
                    "OverlayAd requires a non-empty adUnitId");
        }
        this.adUnitId = adUnitId;
    }

    // The full-screen listener surface stays flat (no slot index), so this
    // format keeps the NativeAdLoadListener shape and owns its delivery.
    private volatile NativeAdLoadListener loadListener;

    public void SetListener(NativeAdLoadListener listener) {
        if (released) return;
        loadListener = listener;
        NotifyCurrentState();
    }

    private void ClearLoadListener() {
        loadListener = null;
    }

    // Every Notify* below hands its event to AdEventDispatcher instead of
    // crossing into C# on the calling thread - see the note on that class.
    // Arguments read from mutable state are snapshotted into locals BEFORE
    // the post, so each event carries the values of the moment it left,
    // not of the moment the queue got around to it.
    private void NotifyLoadingStarted() {
        NativeAdLoadListener listener = loadListener;
        if (listener == null) return;
        AdEventDispatcher.Post(() -> {
            try {
                listener.OnLoadingStarted();
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnLoadingStarted callback failed", exception);
            }
        });
    }

    private void NotifyStateChanged(
            boolean isReady
          , boolean isLoading) {
        NativeAdLoadListener listener = loadListener;
        if (listener == null) return;
        AdEventDispatcher.Post(() -> {
            try {
                listener.OnStateChanged(isReady, isLoading);
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnStateChanged callback failed", exception);
            }
        });
    }

    private void NotifyShowNotReady() {
        NativeAdLoadListener listener = loadListener;
        if (listener == null) return;
        AdEventDispatcher.Post(() -> {
            try {
                listener.OnShowNotReady();
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnShowNotReady callback failed", exception);
            }
        });
    }

    private void NotifyLoadingCompleted(
            int errorCode
          , String errorMessage) {
        NativeAdLoadListener listener = loadListener;
        if (listener == null) return;
        // The unit's pool, as every format on the unit reports it.
        int cachedCountNow = pool == null ? 0 : pool.CachedCount();
        int cacheSizeNow = pool == null ? cacheSize : pool.Capacity();
        AdEventDispatcher.Post(() -> {
            try {
                listener.OnLoadingCompleted(
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
        NativeAdLoadListener listener = loadListener;
        if (listener == null) return;
        AdEventDispatcher.Post(() -> {
            try {
                listener.OnAdPaid(
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

    private void NotifyDisplayed() {
        NativeAdLoadListener listener = loadListener;
        if (listener == null) return;
        AdEventDispatcher.Post(() -> {
            try {
                listener.OnDisplayed();
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnDisplayed callback failed", exception);
            }
        });
    }

    private void NotifyPresentationFailed(
            int errorCode
          , String errorMessage) {
        NativeAdLoadListener listener = loadListener;
        if (listener == null) return;
        AdEventDispatcher.Post(() -> {
            try {
                listener.OnPresentationFailed(errorCode, errorMessage);
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnPresentationFailed callback failed", exception);
            }
        });
    }

    public synchronized void Configure(
            boolean fullScreen
          , float heightRatio
          , int backgroundColor
          , int cacheSize
          , int cooldown
          , int closeSide
          , int timerSide
          , boolean redirectOnClose) {
        if (released) {
            Log.e(
                    TAG
                  , "Configure ignored after Release; create a new instance");
            return;
        }
        if (configured) {
            Log.e(
                    TAG
                  , "Configure may only be called once per Ad instance");
            return;
        }

        // How many ads the pool keeps warm for it. One - always hold a
        // spare - is the placement that never asked; a chained placement
        // raises it so the follow-up is already in hand when the first ad
        // closes.
        this.cacheSize = Math.max(1, cacheSize);
        configuredStyle = new OverlayAdStyle(
                fullScreen
              , cooldown
              , closeSide
              , timerSide
              , heightRatio
              , backgroundColor
              , redirectOnClose
              , 0);
        configured = true;
        // A game paused under the ad lets its video play with sound; one
        // beside the game keeps it muted.
        final boolean startMuted = !configuredStyle.PausesGame();
        RunOnMainThread(() -> {
            if (!released) pool = NativeAdPool.Join(adUnitId, startMuted, this);
        });
    }

    public void SetClose(
            int cooldown
          , int closeSide
          , int timerSide
          , boolean redirectOnClose) {
        OverlayAdStyle currentStyle = configuredStyle;
        if (!configured || released || currentStyle == null) {
            Log.e(TAG, "SetClose ignored before Configure or after Release");
            return;
        }
        OverlayAdStyle updatedStyle = currentStyle.WithClose(
                cooldown
              , closeSide
              , timerSide
              , redirectOnClose);
        configuredStyle = updatedStyle;
        RequestPreparedFaceRebuild(updatedStyle);
    }

    // Called straight after Configure. The pool it joined may already hold an
    // ad another placement warmed, and a face may have been prepared from it
    // in between, so that face is rebuilt as SetClose's is.
    public void SetLayout(int layout) {
        OverlayAdStyle currentStyle = configuredStyle;
        if (!configured || released || currentStyle == null) {
            Log.e(TAG, "SetLayout ignored before Configure or after Release");
            return;
        }
        OverlayAdStyle updatedStyle = currentStyle.WithLayout(layout);
        configuredStyle = updatedStyle;
        RequestPreparedFaceRebuild(updatedStyle);
    }

    public void Load(final Activity activity) {
        if (!configured || released) {
            Log.e(TAG, "Load ignored before Configure or after Release");
            return;
        }
        if (!IsActivityUsable(activity)) {
            Log.e(TAG, "Load ignored because Activity is not usable");
            return;
        }

        RunOnMainThread(() -> Refill(activity));
    }

    // ------------------------------------------------------------------
    // THE POOL
    //
    // The ads come from the unit's NativeAdPool, shared with every format
    // drawing from the same unit and mute policy. This unit holds one of
    // them at a time - the head, the one the next Show takes and the only
    // one worth a prepared face - and the pool keeps the rest for whoever
    // asks first. A creative this panel cannot lay out goes back, marked
    // with the panel's size key, for a format that can.
    //
    // One valve on top: if nothing has EVER laid out here and creatives
    // keep failing, the panel is the likelier culprit, so the unit stops
    // asking and settles into a slow poll.
    // ------------------------------------------------------------------

    private static final int RETRY_IMMEDIATE_LAYOUT_ATTEMPTS = 2;
    private static final int MAX_UNPROVEN_LAYOUT_FAILURES = 20;
    private static final long UNPROVEN_LAYOUT_RETRY_DELAY_MS = 300_000L;
    private static final long PANEL_SIZE_TAG = 1L << 62;

    // Holds the head when the pool has one this panel may fit, else asks the
    // pool for more. Says whether a head is held.
    private boolean Refill(Activity activity) {
        if (released || !configured || pool == null) return false;
        if (IsActivityUsable(activity)) {
            cacheActivity = activity;
            pool.AdoptActivity(activity);
        }
        if (!cachedAds.isEmpty()) return true;
        if (layoutRetryScheduled) return false;

        NativeAdPool.CachedAd taken = pool.Take(SizeKey(), this);
        if (taken == null) {
            pool.RequestFor(SizeKey());
            return false;
        }

        AddCachedAd(taken.ad, taken.loadedAtMs);
        BindPaidEvent(taken.ad, adUnitId, () -> OwnsAd(taken.ad));
        NotifyCurrentState();
        PrepareFace(cacheActivity, taken.ad, configuredStyle);
        return true;
    }

    // The panel this unit lays creatives out in, as the pool keys sizes:
    // tagged so it never meets a rect ad.
    private long SizeKey() {
        OverlayAdStyle style = configuredStyle;
        if (style == null) return NativeAdPool.ANY_SIZE;

        return PANEL_SIZE_TAG
                | ((style.fullScreen ? 1L : 0L) << 32)
                | (Float.floatToIntBits(style.heightRatio) & 0xffffffffL);
    }

    private long ScheduleLayoutRetry() {
        CancelLayoutRetry();
        if (released) return NativeAdPool.NO_RETRY_SCHEDULED_MS;

        ++layoutFailStreak;
        if (!layoutProven
                && layoutFailStreak >= MAX_UNPROVEN_LAYOUT_FAILURES) {
            Log.e(
                    TAG
                  , layoutFailStreak
                            + " creatives in a row could not be laid out in"
                            + " this panel and none has ever rendered here;"
                            + " falling back to a slow poll");
            return PostLayoutRetry(UNPROVEN_LAYOUT_RETRY_DELAY_MS);
        }
        return PostLayoutRetry(
                NativeAdPool.BackoffDelayMs(
                        layoutFailStreak
                      , RETRY_IMMEDIATE_LAYOUT_ATTEMPTS));
    }

    private long PostLayoutRetry(long delayMs) {
        layoutRetryScheduled = true;
        main.postDelayed(layoutRetryRunnable, Math.max(0L, delayMs));
        return delayMs;
    }

    private void CancelLayoutRetry() {
        main.removeCallbacks(layoutRetryRunnable);
        layoutRetryScheduled = false;
    }

    private void HandleLayoutRetry() {
        layoutRetryScheduled = false;
        Refill(cacheActivity);
    }

    private String BuildFailureMessage(
            String reason
          , long retryDelayMs) {
        String retryDescription =
                retryDelayMs == NativeAdPool.NO_RETRY_SCHEDULED_MS
                        ? "not scheduled"
                        : retryDelayMs + "ms";
        return String.valueOf(reason)
                + " | " + (pool == null ? "no pool" : pool.Describe())
                + " | layoutFail=" + layoutFailStreak
                + " | retry=" + retryDescription;
    }

    // A creative this panel cannot lay out: back to the pool for a format
    // that can, and the next one asked for.
    private void DropUnrenderableAd(NativeAd ad) {
        CachedAd dropped = null;
        for (java.util.Iterator<CachedAd> entries = cachedAds.iterator();
                entries.hasNext();) {
            CachedAd entry = entries.next();
            if (entry.ad == ad) {
                dropped = entry;
                entries.remove();
                break;
            }
        }
        cachedCount = cachedAds.size();
        if (dropped != null) GiveBack(dropped.ad, dropped.loadedAtMs, SizeKey());
        long retryDelayMs = ScheduleLayoutRetry();
        Log.i(TAG, "layout: gave back unrenderable creative, retry in "
                + retryDelayMs + "ms");
        NotifyCurrentState();
    }

    // ---- NativeAdPool.Consumer

    @Override
    public int WantedCount() {
        return cacheSize;
    }

    @Override
    public boolean CouldUse(NativeAdPool.CachedAd cached) {
        return configured && !cached.unfitSizes.contains(SizeKey());
    }

    @Override
    public void OnPoolChanged() {
        if (released || !configured || !cachedAds.isEmpty()) return;

        Refill(cacheActivity);
    }

    @Override
    public void OnLoadStarted() {
        isAdLoading = true;
        NotifyCurrentState();
        NotifyLoadingStarted();
    }

    @Override
    public void OnLoadCompleted(
            int code
          , String message
          , long retryDelayMs) {
        isAdLoading = false;
        NotifyCurrentState();
        NotifyLoadingCompleted(
                code
              , code == 0 ? "" : BuildFailureMessage(message, retryDelayMs));
    }

    @Override
    public void OnAdClicked(NativeAd ad) {
        if (ad == null || ad != activeNativeAd) return;

        String activitySessionId = activeActivitySessionId;
        if (activitySessionId != null) {
            OverlayAdActivity.CommitAdClick(activitySessionId);
            return;
        }
        NativeAdPresentation activePresentation = presentation;
        if (activePresentation != null) activePresentation.OnAdClicked();
    }

    // The half-screen panel's height in pixels: the number its window gets at
    // show time, and the one something standing behind it has to match. Safe
    // from any thread - display metrics and a ratio, nothing that lives on
    // the main thread - and known before any ad has loaded, because the
    // creative never moves it.
    public float ResolvePanelHeight(Activity activity) {
        OverlayAdStyle style = configuredStyle;
        if (activity == null || style == null) return 0f;

        return OverlayAdContentView.ResolveInitialPanelHeight(
                activity
              , style.fullScreen
              , style.heightRatio);
    }

    public void Show(
            final Activity activity
          , final NativeAdCompletedListener onCompleted
          , final boolean slideIn
          , final boolean slideOutOnClose) {
        // The config boundary of one show: updates completed before this
        // Show call apply to it, later updates belong to the next one.
        final OverlayAdStyle requestedShowStyle = configuredStyle;

        RunOnMainThread(() -> {
            if (released) {
                NotifyCompleted(onCompleted, "Ad released", false);
                return;
            }
            if (!configured || IsShowingInternal()
                    || activeNativeAd != null) {
                NotifyCompleted(
                        onCompleted
                      , !configured
                                ? "Ad not configured"
                                : "Ad already showing"
                      , false);
                return;
            }
            cacheActivity = activity;
            if (RemoveExpiredCachedAds()) {
                NotifyCurrentState();
                Refill(activity);
            }
            if (cachedAds.isEmpty() || requestedShowStyle == null
                    || !IsActivityUsable(activity)) {
                NotifyCompleted(onCompleted, "Ad not ready", false);
                return;
            }

            CachedAd taken = TakeCachedAd();
            final NativeAd shownAd = taken.ad;
            activeNativeAd = shownAd;
            activeLoadedAtMs = taken.loadedAtMs;
            activeShowCompleted = onCompleted;
            NotifyCurrentState();
            // The head this show took is replaced at once, and earns its
            // face. Both wait a turn so this show takes the face it was
            // promised first.
            main.post(() -> {
                if (!released) Refill(activity);
            });

            if (requestedShowStyle.fullScreen) {
                // NOTE - deliberately no IsLayoutUnrenderable station on
                // this path. The panel is the entire screen; mandatory
                // chrome that outgrows a whole display does not exist in
                // practice, so the full-screen layout's one job is to look
                // good, not to fight for room. Owner's call, 2026-08.
                ReleasePreparedPresentation();
                PreparedFullScreenContent preparedContent =
                        TakePreparedFullScreenContent(activity, shownAd);
                String sessionId =
                        OverlayAdActivity.RegisterSession(
                                activity
                              , this
                              , shownAd
                              , requestedShowStyle
                              , preparedContent);
                activeActivitySessionId = sessionId;
                if (sessionId == null
                        || !OverlayAdActivity.StartSession(
                                activity
                              , sessionId)) {
                    activeActivitySessionId = null;
                    OverlayAdActivity.CancelSession(sessionId);
                    CompletePresentation(
                            shownAd
                          , "Failed to start native full-screen Activity");
                }
                return;
            }

            try {
                OverlayAdPresentation createdPresentation =
                        TakePreparedPresentation(
                                activity
                              , shownAd
                              , requestedShowStyle);
                if (createdPresentation == null) {
                    createdPresentation = CreatePresentation(
                            activity
                          , shownAd
                          , requestedShowStyle);
                    presentation = createdPresentation;
                    if (!createdPresentation.Prepare()) {
                        createdPresentation.Release();
                        presentation = null;
                        CompletePresentation(
                                shownAd
                              , "Failed to prepare ad presentation");
                        return;
                    }
                }
                presentation = createdPresentation;

                // The prepared-face path already refused unrenderable
                // creatives, but this is the path for an ad that never had
                // a face - built seconds before its show, which is exactly
                // when a creative that fits no layout must NOT slip through.
                if (createdPresentation.IsLayoutUnrenderable()) {
                    createdPresentation.Release();
                    presentation = null;
                    long retryDelayMs = ScheduleLayoutRetry();
                    Log.i(TAG, "layout: refused unrenderable creative at"
                            + " show, retry in " + retryDelayMs + "ms");
                    CompletePresentation(
                            shownAd
                          , "Creative cannot be laid out in this panel"
                          , true);
                    return;
                }
                layoutProven = true;
                layoutFailStreak = 0;

                createdPresentation.SetArrival(
                        slideIn
                      , slideOutOnClose
                      , () -> {
                            if (!released && activeNativeAd == shownAd) {
                                NotifyDisplayed();
                            }
                        });
                if (!createdPresentation.Show()) {
                    createdPresentation.Release();
                    if (presentation == createdPresentation) {
                        presentation = null;
                    }
                    // A non-empty message: the wrapper turns it into
                    // OnAdDisplayFailed. The empty string that stood here
                    // reported a show that never reached the screen as a
                    // normal close.
                    CompletePresentation(
                            shownAd
                          , "Failed to show ad presentation");
                }
            } catch (RuntimeException exception) {
                Log.e(TAG, "Failed to materialize native ad", exception);
                CleanupFailedPresentation();
                CompletePresentation(
                        shownAd
                      , "Failed to show ad: "
                                + String.valueOf(exception.getMessage()));
            }
        });
    }

    public void Hide(final boolean slideOut) {
        RunOnMainThread(() -> {
            String activitySessionId = activeActivitySessionId;
            if (activitySessionId != null) {
                OverlayAdActivity.DismissSession(
                        activitySessionId);
                return;
            }

            OverlayAdPresentation currentPresentation =
                    presentation;
            if (currentPresentation != null
                    && currentPresentation.IsShowing()) {
                currentPresentation.Dismiss(slideOut);
            }
        });
    }

    public boolean IsReady() {
        return configured && !released && activeNativeAd == null
                && cachedCount > 0;
    }

    public boolean IsLoading() {
        return configured && !released && isAdLoading;
    }

    private void NotifyCurrentState() {
        NotifyStateChanged(IsReady(), IsLoading());
    }

    public void Release() {
        released = true;
        InvalidateLoadGeneration();
        main.removeCallbacksAndMessages(null);
        RunOnMainThread(() -> {
            isAdLoading = false;
            CancelLayoutRetry();

            ReleasePreparedPresentation();
            ReleasePreparedFullScreenContent();

            String activitySessionId = activeActivitySessionId;
            activeActivitySessionId = null;
            OverlayAdActivity.CancelSession(
                    activitySessionId);

            OverlayAdPresentation currentPresentation =
                    presentation;
            presentation = null;
            if (currentPresentation != null) {
                try {
                    currentPresentation.Release();
                } catch (RuntimeException exception) {
                    Log.e(TAG, "Failed to release native ad presentation"
                          , exception);
                }
            }
            // The head was never displayed: another format may still show it.
            for (CachedAd cached : cachedAds) {
                GiveBack(cached.ad, cached.loadedAtMs, NativeAdPool.ANY_SIZE);
            }
            cachedAds.clear();
            cachedCount = 0;
            cacheActivity = null;
            ForgetAd(activeNativeAd);
            activeNativeAd = null;
            activeShowCompleted = null;
            if (pool != null) pool.Leave(this);
            pool = null;

            configuredStyle = null;
            ClearLoadListener();
        });
    }

    private void AddCachedAd(NativeAd ad, long loadedAtMs) {
        ownedAds.add(ad);
        cachedAds.addLast(new CachedAd(ad, loadedAtMs));
        cachedCount = cachedAds.size();
        ScheduleCacheExpiry();
    }

    // Pops for one show; the caller starts the replacement.
    private CachedAd TakeCachedAd() {
        CachedAd next = cachedAds.pollFirst();
        cachedCount = cachedAds.size();
        ScheduleCacheExpiry();
        return next;
    }

    private NativeAd HeadAd() {
        CachedAd head = cachedAds.peekFirst();
        return head == null ? null : head.ad;
    }

    // An hour after it arrived a warm ad may no longer be shown, so it is
    // destroyed and replaced, prepared face and all.
    private void HandleCacheExpiry() {
        if (released) return;

        if (RemoveExpiredCachedAds()) NotifyCurrentState();
        Refill(cacheActivity);
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
              , Math.max(
                    0L
                  , nextExpiryAtMs - SystemClock.elapsedRealtime()));
    }

    // Also swept on the way into a Show: the timer runs on uptime, which
    // stops while the device sleeps, so a phone woken after a long night
    // can hold ads older than the timer believes.
    private boolean RemoveExpiredCachedAds() {
        long nowMs = SystemClock.elapsedRealtime();
        CachedAd head = cachedAds.peekFirst();
        // Oldest first, so nothing behind the head can be stale while the
        // head is not: one look answers for the whole queue.
        if (head == null
                || nowMs - head.loadedAtMs < MAX_CACHED_AD_AGE_MS) {
            return false;
        }

        // The prepared face is built on the ad about to be destroyed, so it
        // goes first. Releasing a presentation whose creative is already
        // gone is not a road worth walking.
        ReleasePreparedPresentation();
        ReleasePreparedFullScreenContent();
        for (Iterator<CachedAd> it = cachedAds.iterator(); it.hasNext(); ) {
            CachedAd cached = it.next();
            if (nowMs - cached.loadedAtMs < MAX_CACHED_AD_AGE_MS) continue;

            ForgetAd(cached.ad);
            it.remove();
        }
        cachedCount = cachedAds.size();
        return true;
    }

    // Ours until destroyed - cached or on screen, both count. The paid
    // event asks this from the SDK's thread.
    private boolean OwnsAd(NativeAd ad) {
        return !released && ad != null && ownedAds.contains(ad);
    }

    private boolean IsHeadAd(NativeAd ad) {
        return ad != null && HeadAd() == ad;
    }

    private void ForgetAd(NativeAd ad) {
        if (ad == null) return;

        ownedAds.remove(ad);
        if (pool == null) {
            ad.destroy();
            return;
        }

        pool.Destroy(ad);
    }

    // An ad never displayed, back to the pool; with a size key, it did not
    // fit this panel.
    private void GiveBack(
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

    // The face of the ad at the head, prepaid before any show asks for it:
    // the bottom-slice kind prebuilds a whole presentation, the full-screen
    // kind the Activity's content view. Only one of the two is ever held.
    private void PrepareFace(
            Activity activity
          , NativeAd ad
          , OverlayAdStyle style) {
        if (style == null) {
            ReleasePreparedPresentation();
            ReleasePreparedFullScreenContent();
            return;
        }

        if (style.fullScreen) {
            ReleasePreparedPresentation();
            PrepareFullScreenContent(activity, ad, style);
        } else {
            ReleasePreparedFullScreenContent();
            PreparePresentation(activity, ad, style);
        }
    }

    private OverlayAdPresentation CreatePresentation(
            Activity activity
          , NativeAd ad
          , OverlayAdStyle style) {
        boolean closeOnLeftForPresentation = style.ResolveCloseOnLeft();
        OverlayAdPresentation createdPresentation =
                new OverlayAdPresentation(
                activity
              , ad
              , style.cooldown
              , closeOnLeftForPresentation
              , style.ResolveTimerOnLeft(closeOnLeftForPresentation)
              , style.fullScreen
              , style.heightRatio
              , style.backgroundColor
              , style.redirectOnClose);
        createdPresentation.setOnDismissListener(
                ignored -> CompletePresentation(ad, ""));
        return createdPresentation;
    }

    private boolean PreparePresentation(
            Activity activity
          , NativeAd ad
          , OverlayAdStyle style) {
        ReleasePreparedPresentation();
        if (released
                || ad == null
                || style == null
                || !IsHeadAd(ad)
                || !IsActivityUsable(activity)) {
            return false;
        }

        OverlayAdPresentation createdPresentation = null;
        try {
            createdPresentation = CreatePresentation(activity, ad, style);
            if (!createdPresentation.Prepare()
                    || released
                    || !IsHeadAd(ad)
                    || configuredStyle != style
                    || !IsActivityUsable(activity)) {
                createdPresentation.Release();
                return false;
            }

            // Prepare built the whole view, so the verdict is already in.
            // A creative no layout can show is dropped here, with the ad
            // still in the cache and nothing on screen - the one moment
            // where refusing it costs the player nothing.
            if (createdPresentation.IsLayoutUnrenderable()) {
                createdPresentation.Release();
                DropUnrenderableAd(ad);
                return false;
            }
            layoutProven = true;
            layoutFailStreak = 0;

            preparedPresentation = createdPresentation;
            preparedActivity = activity;
            preparedNativeAd = ad;
            preparedStyle = style;
            preparedPresentationMediaSignature = MediaSignature(ad);
            return true;
        } catch (RuntimeException exception) {
            if (createdPresentation != null) {
                try {
                    createdPresentation.Release();
                } catch (RuntimeException cleanupException) {
                    Log.e(TAG, "Failed to clean prepared native ad"
                          , cleanupException);
                }
            }
            Log.e(TAG, "Failed to prepare native ad presentation"
                  , exception);
            return false;
        }
    }

    private void RequestPreparedFaceRebuild(
            OverlayAdStyle requestedStyle) {
        // Every runtime presentation setter must publish a fresh style
        // snapshot and pass through here, so a prepared UI never keeps
        // stale config.
        RunOnMainThread(() -> {
            // A show in progress is no reason to skip: the rebuild only
            // ever touches the prepared face of the ad at the HEAD of
            // the cache, never the one on screen, and skipping it left the
            // replacement fetched during the show holding a stale
            // countdown, which then had to be rebuilt at show time.
            //
            // Both faces rebuild, not just the bottom-slice one. The
            // full-screen face carries the close side, the timer side and
            // the redirect flag baked in at build time, and the take-back
            // check compares ad identity and assets - never the style - so
            // a face left standing here is a face that would be shown with
            // last plan's controls.
            if (released
                    || requestedStyle == null
                    || configuredStyle != requestedStyle) {
                return;
            }

            NativeAd head = HeadAd();
            if (head == null) return;

            PrepareFace(cacheActivity, head, requestedStyle);
        });
    }

    private OverlayAdPresentation TakePreparedPresentation(
            Activity activity
          , NativeAd ad
          , OverlayAdStyle style) {
        if (preparedPresentation == null
                || preparedActivity != activity
                || preparedNativeAd != ad
                || preparedStyle != style
                || !MediaSignature(ad).equals(
                        preparedPresentationMediaSignature)) {
            ReleasePreparedPresentation();
            return null;
        }

        OverlayAdPresentation result = preparedPresentation;
        preparedPresentation = null;
        preparedActivity = null;
        preparedNativeAd = null;
        preparedStyle = null;
        return result;
    }

    private void ReleasePreparedPresentation() {
        OverlayAdPresentation prepared = preparedPresentation;
        preparedPresentation = null;
        preparedActivity = null;
        preparedNativeAd = null;
        preparedStyle = null;
        if (prepared == null) return;

        try {
            prepared.Release();
        } catch (RuntimeException exception) {
            Log.e(TAG, "Failed to release prepared native ad presentation"
                  , exception);
        }
    }

    // The Activity remains the full-screen stage, but its content is built
    // here, at load time: binding a creative - a video above all - is the
    // slow half of the first frame, and prepaying it leaves the show with
    // only the Activity switch itself to spend.
    private void PrepareFullScreenContent(
            Activity activity
          , NativeAd ad
          , OverlayAdStyle style) {
        ReleasePreparedFullScreenContent();
        if (released
                || ad == null
                || style == null
                || !IsHeadAd(ad)
                || !IsActivityUsable(activity)) {
            return;
        }

        try {
            int requestedPanelHeight =
                    OverlayAdContentView.ResolveInitialPanelHeight(
                            activity
                          , true
                          , style.heightRatio);
            boolean closeOnLeft = style.ResolveCloseOnLeft();
            OverlayAdActivity.CloseRelay closeRelay =
                    new OverlayAdActivity.CloseRelay();
            OverlayAdContentView createdContentView =
                    new OverlayAdContentView(
                            activity
                          , ad
                          , style.cooldown * 1000L
                          , closeOnLeft
                          , style.ResolveTimerOnLeft(closeOnLeft)
                          , true
                          , style.backgroundColor
                          , style.redirectOnClose
                          , requestedPanelHeight
                          , style.layout
                          , closeRelay);
            preparedContentView = createdContentView;
            preparedContentActivity = activity;
            preparedContentAd = ad;
            preparedContentCloseOnLeft = closeOnLeft;
            preparedContentCloseRelay = closeRelay;
            preparedContentMediaSignature = MediaSignature(ad);
            DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
            preparedContentScreenWidth = metrics.widthPixels;
            preparedContentScreenHeight = metrics.heightPixels;
        } catch (RuntimeException exception) {
            Log.e(TAG, "Failed to prepare full-screen ad content"
                  , exception);
            ReleasePreparedFullScreenContent();
        }
    }

    private PreparedFullScreenContent TakePreparedFullScreenContent(
            Activity activity
          , NativeAd ad) {
        // Ad identity and the assets' face are the keys - the style is
        // not among them, because SetClose rebuilds this content outright
        // rather than leaving a stale one to be caught here. What this does
        // catch is assets that finished arriving after the build and
        // changed the layout the ad needs, and a screen turned since.
        DisplayMetrics metrics = activity.getResources().getDisplayMetrics();
        if (preparedContentView == null
                || preparedContentActivity != activity
                || preparedContentAd != ad
                || preparedContentCloseRelay == null
                || !MediaSignature(ad).equals(
                        preparedContentMediaSignature)
                || metrics.widthPixels != preparedContentScreenWidth
                || metrics.heightPixels != preparedContentScreenHeight) {
            ReleasePreparedFullScreenContent();
            return null;
        }

        PreparedFullScreenContent result = new PreparedFullScreenContent(
                preparedContentView
              , preparedContentCloseOnLeft
              , preparedContentCloseRelay);
        preparedContentView = null;
        preparedContentActivity = null;
        preparedContentAd = null;
        preparedContentCloseRelay = null;
        return result;
    }

    private void ReleasePreparedFullScreenContent() {
        OverlayAdContentView prepared = preparedContentView;
        preparedContentView = null;
        preparedContentActivity = null;
        preparedContentAd = null;
        preparedContentCloseRelay = null;
        if (prepared == null) return;

        try {
            prepared.Release();
        } catch (RuntimeException exception) {
            Log.e(TAG, "Failed to release prepared full-screen ad content"
                  , exception);
        }
    }

    void OnActivityPresentationDisplayed(
            String sessionId
          , NativeAd shownAd) {
        if (released
                || sessionId == null
                || !sessionId.equals(activeActivitySessionId)
                || activeNativeAd != shownAd) {
            return;
        }
        NotifyDisplayed();
    }

    void OnActivityPresentationCompleted(
            String sessionId
          , NativeAd shownAd
          , String errorMessage) {
        if (sessionId == null
                || !sessionId.equals(activeActivitySessionId)
                || activeNativeAd != shownAd) {
            return;
        }
        activeActivitySessionId = null;
        CompletePresentation(shownAd, errorMessage);
    }

    private void CompletePresentation(
            NativeAd shownAd
          , String errorMessage) {
        CompletePresentation(shownAd, errorMessage, false);
    }

    // isUnfit: refused before display because this panel cannot lay it out,
    // so it goes back to the pool for a format that can.
    private void CompletePresentation(
            NativeAd shownAd
          , String errorMessage
          , boolean isUnfit) {
        // Identity guard: completion and destroy run once per shown ad.
        if (activeNativeAd != shownAd) return;

        NativeAdCompletedListener onCompleted = activeShowCompleted;
        activeShowCompleted = null;
        activeNativeAd = null;
        activeActivitySessionId = null;
        if (isUnfit) {
            GiveBack(shownAd, activeLoadedAtMs, SizeKey());
        } else {
            ForgetAd(shownAd);
        }
        presentation = null;

        // State first, completion second. It no longer decides anything -
        // the completion carries the cache count itself, so a listener is
        // right either way - but publishing the wider picture before the
        // event that makes people act on it costs nothing and keeps the two
        // in agreement for anyone watching the log.
        NotifyCurrentState();
        // adConsumed distinguishes a completion that spent the cached ad
        // from a Show rejected early.
        NotifyCompleted(
                onCompleted
              , errorMessage == null ? "" : errorMessage
              , true);
    }

    private void CleanupFailedPresentation() {
        OverlayAdPresentation failedPresentation = presentation;
        presentation = null;
        if (failedPresentation == null) return;
        try {
            failedPresentation.Release();
        } catch (RuntimeException cleanupException) {
            Log.e(TAG, "Failed to clean native ad presentation"
                  , cleanupException);
        }
    }

    private boolean IsShowingInternal() {
        String activitySessionId = activeActivitySessionId;
        if (activitySessionId != null) {
            return OverlayAdActivity.IsSessionActive(
                    activitySessionId);
        }
        OverlayAdPresentation currentPresentation = presentation;
        return currentPresentation != null
                && currentPresentation.IsShowing();
    }

    // The cache count is read here rather than passed in, so every
    // completion reports the cache as it stands at the instant it leaves -
    // there is no call site that could hand over a number from earlier.
    // "The instant it leaves" is the instant of the post, which is why the
    // count goes into a local before the event enters the queue.
    private void NotifyCompleted(
            NativeAdCompletedListener listener
          , String errorMessage
          , boolean adConsumed) {
        if (listener == null) return;
        int cachedCountNow = cachedCount;
        AdEventDispatcher.Post(() -> {
            try {
                listener.OnAdCompleted(
                        errorMessage, adConsumed, cachedCountNow);
            } catch (RuntimeException exception) {
                Log.e(TAG, "OnAdCompleted callback failed", exception);
            }
        });
    }
}