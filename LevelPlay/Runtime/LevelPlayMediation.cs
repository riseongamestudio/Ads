using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RiseOn.Consent;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace RiseOn.Ads.LevelPlay {
    /// <summary>
    /// Unity LevelPlay: banner, MRec (a banner of the medium rectangle size), interstitial and rewarded.<br/>
    /// LevelPlay has no consent form of its own; it reads what UMP stored and what <see cref="ApplyConsent"/> tells it.
    /// </summary>
    /// <remarks>
    /// Unity's LevelPlay API raises its ad events through Unity's player loop, which the cover under a full-screen ad stops; the interstitial and the
    /// rewarded go through the pack's own bridge to LevelPlay's native API on Android and iOS instead, whose events need no player loop, so they take
    /// the cover and join chains.
    /// </remarks>
    [Serializable, TypeRegistryItem("LevelPlay")]
    internal sealed class LevelPlayMediation
        : IAdMediationBanner
        , IAdMediationMRec
        , IAdMediationFullScreen
        , IAdMediationDebugger {
        public const string MediationId = "levelplay";

        private const string LogTag = nameof(LevelPlayMediation);

        // LevelPlay opens its Test Suite only when told before Init, so the debugger's request waits on the device for the next launch.
        private const string TestSuiteKey = "RiseOn.Ads.LevelPlay.TestSuite";

        [SerializeField, PropertyOrder(-2), LabelText("App Key")]
        private AdUnitId appKey;

#if UNITY_EDITOR
        // LevelPlay's own settings files, after the app key.
        [OnInspectorGUI, PropertyOrder(-1.5f)]
        private void DrawSdkSettings() => AdsEditorHooks.DrawMediation(this);
#endif

        [SerializeField, AdFormatSection("Banner")]
        [SectionGroup(AdFormatSectionAttribute.Group, Foldable = true, Tooltip = AdFormatSectionAttribute.GroupTooltip)]
        private AdUnitId banner;

        [SerializeField, AdFormatSection("MRec")]
        private AdUnitId mRec;

        [SerializeField, AdFormatSection("Interstitial", -1)]
        private AdUnitId interstitial;

        [SerializeField, AdFormatSection("Rewarded", -1)]
        private AdUnitId rewarded;

        public string Id => MediationId;
        public bool IsInitialized { get; private set; }

        private IAdMediationContext      context;
        private CancellationTokenSource lifetime;

        private LevelPlayBannerAd       bannerAd;
        private LevelPlayBannerAd       mRecAd;
        private LevelPlayFullScreenAd   interstitialAd;
        private LevelPlayFullScreenAd   rewardedAd;
        private IAdShowListener         listener;
        private bool                    rewardEarned;
        private bool                    testSuiteEnabled;

        public void ApplyConsent(in ConsentState consent) {
            // Without GDPR the TCF string UMP wrote says nothing, and a consent flag would only narrow the auction.
            if (consent.GdprApplies) LevelPlayPrivacySettings.SetGDPRConsent(consent.HasUserConsent);
            LevelPlayPrivacySettings.SetCCPA(consent.DoNotSell);
            LevelPlayPrivacySettings.SetCOPPA(consent.ChildDirected);
        }

        public async UniTask InitializeAsync(IAdMediationContext context, IReadOnlyCollection<AdFormat> formats, CancellationToken cancellationToken) {
            if (appKey.IsEmpty) {
                Debug.LogError($"{LogTag}: no app key for {Application.platform}, LevelPlay does not start");
                return;
            }

            this.context = context;
            lifetime     = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var initialized = new UniTaskCompletionSource<bool>();
            void HandleSuccess(LevelPlayConfiguration _) => initialized.TrySetResult(true);

            void HandleFailure(LevelPlayInitError error) {
                Debug.LogError($"{LogTag}: init failed ({error.ErrorMessage})");
                initialized.TrySetResult(false);
            }

            // No SetPauseGame: LevelPlay reads it only in Unity's own full-screen delegates, which the pack's bridge replaces. The cover
            // under every full-screen ad pauses the game instead.
            Unity.Services.LevelPlay.LevelPlay.OnInitSuccess += HandleSuccess;
            Unity.Services.LevelPlay.LevelPlay.OnInitFailed  += HandleFailure;
            testSuiteEnabled = PlayerPrefs.GetInt(TestSuiteKey, 0) is 1;
            if (testSuiteEnabled) Unity.Services.LevelPlay.LevelPlay.SetMetaData("is_test_suite", "enable");
            bool succeeded;
            try {
                Unity.Services.LevelPlay.LevelPlay.Init(appKey.Value);
                succeeded = await initialized.Task;
            } finally {
                Unity.Services.LevelPlay.LevelPlay.OnInitSuccess -= HandleSuccess;
                Unity.Services.LevelPlay.LevelPlay.OnInitFailed  -= HandleFailure;
            }

            await UniTask.SwitchToMainThread(cancellationToken);
            if (!succeeded) return;

            IsInitialized = true;
            foreach (var format in formats) StartFormat(format);
        }

        public string DebuggerName => "Test Suite";

        public void OpenDebugger(Action<string> done) {
            if (!testSuiteEnabled) {
                PlayerPrefs.SetInt(TestSuiteKey, 1);
                PlayerPrefs.Save();
                done("The Test Suite turns on at the next launch: close the app fully and open it again");
                return;
            }

            if (!IsInitialized) {
                done("LevelPlay has not started");
                return;
            }

            Unity.Services.LevelPlay.LevelPlay.LaunchTestSuite();
            done(null);
        }

        public void Dispose() {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;

            bannerAd?.DestroyAd();
            mRecAd?.DestroyAd();
            interstitialAd?.Destroy();
            rewardedAd?.Destroy();
            bannerAd       = null;
            mRecAd         = null;
            interstitialAd = null;
            rewardedAd     = null;
            IsInitialized  = false;
        }

        private void StartFormat(AdFormat format) {
            switch (format) {
                case AdFormat.Banner:
                    if (!HasUnitId(format, banner)) return;

                    var bannerSettings = context.Formats.Banner;
                    bannerAd = CreateBanner(format, banner.Value, bannerSettings.Adaptive ? LevelPlayAdSize.CreateAdaptiveAdSize() : LevelPlayAdSize.BANNER, bannerSettings.Position);
                    break;

                case AdFormat.MRec:
                    if (!HasUnitId(format, mRec)) return;

                    mRecAd = CreateBanner(format, mRec.Value, LevelPlayAdSize.MEDIUM_RECTANGLE, context.Formats.MRec.Position);
                    break;

                case AdFormat.Interstitial:
                    if (!HasUnitId(format, interstitial)) return;

                    interstitialAd = LevelPlayFullScreenAd.Create(interstitial.Value, false, new FullScreenEvents(this, format));
                    interstitialAd.Load();
                    break;

                case AdFormat.Rewarded:
                    if (!HasUnitId(format, rewarded)) return;

                    rewardedAd = LevelPlayFullScreenAd.Create(rewarded.Value, true, new FullScreenEvents(this, format));
                    rewardedAd.Load();
                    break;

                case AdFormat.RewardedInterstitial:
                case AdFormat.AppOpen:
                case AdFormat.NativeInFeed:
                case AdFormat.NativeMRec:
                case AdFormat.NativeCollapsible:
                case AdFormat.NativeInterstitial:
                case AdFormat.NativeAppOpen:
                case AdFormat.NativeEndCard:
                    Debug.LogError($"{LogTag}: LevelPlay does not serve {format}; take it off the priority list");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        private static bool HasUnitId(AdFormat format, in AdUnitId unitId) {
            if (!unitId.IsEmpty) return true;

            Debug.LogError($"{LogTag}: no {format} unit id for {Application.platform}, it does not load");
            return false;
        }

        // ---- Banner, MRec

        private static LevelPlayBannerPosition ToLevelPlay(AdScreenPosition position) {
            switch (position) {
                case AdScreenPosition.TopLeft:     return LevelPlayBannerPosition.TopLeft;
                case AdScreenPosition.Top:         return LevelPlayBannerPosition.TopCenter;
                case AdScreenPosition.TopRight:    return LevelPlayBannerPosition.TopRight;
                case AdScreenPosition.Center:      return LevelPlayBannerPosition.Center;
                case AdScreenPosition.BottomLeft:  return LevelPlayBannerPosition.BottomLeft;
                case AdScreenPosition.Bottom:      return LevelPlayBannerPosition.BottomCenter;
                case AdScreenPosition.BottomRight: return LevelPlayBannerPosition.BottomRight;
                default:                           throw new ArgumentOutOfRangeException(nameof(position), position, null);
            }
        }

        public void ShowBanner() => bannerAd?.ShowAd();
        public void HideBanner() => bannerAd?.HideAd();
        public void ShowMRec()   => mRecAd?.ShowAd();
        public void HideMRec()   => mRecAd?.HideAd();

        private LevelPlayBannerAd CreateBanner(AdFormat format, string unitId, LevelPlayAdSize size, AdScreenPosition position) {
            var config = new LevelPlayBannerAd.Config.Builder();
            config.SetSize(size);
            config.SetPosition(ToLevelPlay(position));
            config.SetDisplayOnLoad(false);

            // A banner refreshes and retries by itself.
            var ad = new LevelPlayBannerAd(unitId, config.Build());
            ad.OnAdLoaded              += _ => context?.ReportLoaded(format);
            ad.OnAdLoadFailed          += error => context?.ReportLoadFailed(format, new AdError(error.ErrorCode, error.ErrorMessage));
            ad.OnAdImpressionDataReady += data => ReportPaid(format, data.AdNetwork, data.MediationAdUnitId, data.Revenue ?? 0);
            ad.LoadAd();
            return ad;
        }

        // ---- Full screen

        public bool Supports(AdFormat format) => format is AdFormat.Interstitial or AdFormat.Rewarded;

        public bool IsReady(AdFormat format) => Ad(format)?.IsReady() == true;

        // The pack's bridge: every event off Unity's player loop, and the show from any thread.
        public bool CallsBackOffLoop(AdFormat format)   => Ad(format) != null;
        public bool ShowsFromAnyThread(AdFormat format) => Ad(format) != null;

        private LevelPlayFullScreenAd Ad(AdFormat format) => format switch {
            AdFormat.Interstitial => interstitialAd
          , AdFormat.Rewarded     => rewardedAd
          , _                     => null
        };

        public void Show(AdFormat format, string placement, IAdShowListener showListener) {
            if (!IsReady(format)) {
                showListener.OnFailed(new AdError(-1, $"{format} not ready"));
                return;
            }

            listener     = showListener;
            rewardEarned = false;

            // Not to the SDK: a LevelPlay placement is one set up on its dashboard, with its own capping, and the game's names are not.
            lock (showPlacements) showPlacements[format] = placement;
            Ad(format).Show(null);
        }

        // ---- Full-screen events: off Unity's player loop, through the pack's bridge

        // One per ad, so an event knows its format and its ad.
        private sealed class FullScreenEvents : ILevelPlayFullScreenEvents {
            private readonly LevelPlayMediation mediation;
            private readonly AdFormat           format;
            private          int                attempt;

            public FullScreenEvents(LevelPlayMediation mediation, AdFormat format) {
                this.mediation = mediation;
                this.format    = format;
            }

            private LevelPlayFullScreenAd Ad => mediation.Ad(format);

            public void OnLoaded() {
                attempt = 0;
                mediation.context?.ReportLoaded(format);
            }

            public void OnLoadFailed(int code, string message) {
                mediation.context?.ReportLoadFailed(format, new AdError(code, message));

                // 2, 4, 8... up to 32 seconds, timed on Unity's loop.
                attempt = Math.Min(attempt + 1, 5);
                var seconds = 1 << attempt;
                UniTask.Post(() => mediation.RetryAsync(seconds, () => Ad?.Load()).Forget());
            }

            public void OnDisplayed() => mediation.listener?.OnDisplayed();
            public void OnDisplayFailed(int code, string message) => mediation.HandleDisplayFailed(new AdError(code, message), () => Ad?.Load());
            public void OnRewarded() => mediation.HandleRewarded();
            public void OnClosed() => mediation.HandleClosed(() => Ad?.Load());
            public void OnPaid(string adNetwork, string adUnitId, double revenue) => mediation.ReportPaid(format, adNetwork, adUnitId, revenue);
        }

        private async UniTaskVoid RetryAsync(int seconds, Action load) {
            var token = lifetime?.Token ?? CancellationToken.None;
            if (token.IsCancellationRequested) return;

            await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime, PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (token.IsCancellationRequested) return;

            load();
        }

        private void HandleRewarded() {
            if (rewardEarned) return;

            rewardEarned = true;
            listener?.OnRewarded();
        }

        private void HandleDisplayFailed(in AdError error, Action load) {
            var ended = listener;
            listener = null;
            ended?.OnFailed(error);
            load();
        }

        // The pack's bridge holds a close back for a late reward itself.
        private void HandleClosed(Action load) {
            load();

            var ended = listener;
            listener = null;
            ended?.OnClosed();
        }

        private void ReportPaid(AdFormat format, string adNetwork, string adUnitId, double revenue) {
            // LevelPlay reports revenue in US dollars.
            context?.ReportPaid(new AdImpression(format, MediationId, adNetwork, adUnitId, revenue, "USD", PlacementOf(format)));
        }

        // Each full-screen format's last show's placement; the paid event comes on the bridge's thread.
        private readonly Dictionary<AdFormat, string> showPlacements = new();

        private string PlacementOf(AdFormat format) {
            if (!format.IsFullScreen()) return null;

            lock (showPlacements) return showPlacements.TryGetValue(format, out var placement) ? placement : null;
        }
    }
}