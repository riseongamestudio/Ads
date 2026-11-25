using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using RiseOn.Consent;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads.AdMob {
    /// <summary>
    /// Google AdMob and its mediation: every format the Google Mobile Ads SDK serves (banner, MRec, interstitial, rewarded, rewarded interstitial, app open) and the native ad views of <c>com.riseon.ads.admob.native</c> (interstitial, app open, end card, collapsible, MRec, in-feed).
    /// </summary>
    [Serializable, TypeRegistryItem("AdMob")]
    internal sealed partial class AdMobMediation
        : IAdMediationBanner
        , IAdMediationMRec
        , IAdMediationFullScreen
        , IAdMediationNativeInFeed
        , IAdMediationNativeMRec
        , IAdMediationNativeCollapsible
        , IAdMediationDebugger {
        public const string MediationId = "admob";

        private const string LogTag = nameof(AdMobMediation);

        public string Id => MediationId;
        public bool IsInitialized { get; private set; }

        private IAdMediationContext      context;
        private CancellationTokenSource lifetime;
        private ConsentState            consent;

#if UNITY_EDITOR
        [OnInspectorGUI, PropertyOrder(-10)]
        private void DrawSdkSettings() => AdsEditorHooks.DrawMediation(this);
#endif

        public void ApplyConsent(in ConsentState state) {
            consent = state;

            // Personalization comes from the TCF strings UMP writes; the age treatment has to be told. The rest stays the
            // game's, such as the test devices it registers.
            var configuration = MobileAds.GetRequestConfiguration() ?? new RequestConfiguration();
            configuration.AgeRestrictedTreatment = state.ChildDirected ? AgeRestrictedTreatment.Child : AgeRestrictedTreatment.Unspecified;
            MobileAds.SetRequestConfiguration(configuration);
        }

        public async UniTask InitializeAsync(IAdMediationContext context, IReadOnlyCollection<AdFormat> formats, CancellationToken cancellationToken) {
            this.context = context;
            lifetime     = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Events off Unity's thread: a chain needs the close callbacks while the cover keeps Unity paused. It is the
            // default; Google marks the switch obsolete, but a game setting it on would break chains, so it is set back.
#pragma warning disable CS0618
            MobileAds.RaiseAdEventsOnUnityMainThread = false;
#pragma warning restore CS0618

            // The game stops under a full-screen ad on every platform, as MAX does on its own.
            switch (Application.platform) {
                case RuntimePlatform.IPhonePlayer:
                    // Off by default: iOS keeps Unity running under the ad.
                    MobileAds.SetiOSAppPauseOnBackground(true);
                    break;
                case RuntimePlatform.Android:
                    // The ad's Activity takes the focus, which pauses Unity.
                    break;
                default:
                    // The Editor shows the pack's own placeholders, and the native previews between its layers.
                    Native.NativeAdEditorLayers.RectAd     = AdsEditorLayers.NativeRect;
                    Native.NativeAdEditorLayers.HalfScreen = AdsEditorLayers.HalfScreenAd;
                    Native.NativeAdEditorLayers.FullScreen = AdsEditorLayers.FullScreenAd;
                    break;
            }

            var initialized = new UniTaskCompletionSource();
            MobileAds.Initialize(_ => initialized.TrySetResult());
            await initialized.Task;
            await UniTask.SwitchToMainThread(cancellationToken);

            IsInitialized = true;
            foreach (var format in formats) StartFormat(format);
        }

        public string DebuggerName => "Ad Inspector";

        public void OpenDebugger(Action<string> done) {
            if (!IsInitialized) {
                done("AdMob has not started");
                return;
            }

            MobileAds.OpenAdInspector(error => done(error?.GetMessage()));
        }

        public void Dispose() {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;

            DisposeBanners();
            DisposeFullScreen();
            DisposeNative();
            IsInitialized = false;
        }

        private void StartFormat(AdFormat format) {
            switch (format) {
                case AdFormat.Banner:
                    StartBanner();
                    break;

                case AdFormat.MRec:
                    StartMRec();
                    break;

                case AdFormat.NativeInFeed:
                    StartNativeInFeed();
                    break;

                case AdFormat.NativeMRec:
                    StartNativeMRec();
                    break;

                case AdFormat.NativeCollapsible:
                    StartNativeCollapsible();
                    break;

                case AdFormat.Interstitial:
                case AdFormat.Rewarded:
                case AdFormat.RewardedInterstitial:
                case AdFormat.AppOpen:
                case AdFormat.NativeInterstitial:
                case AdFormat.NativeAppOpen:
                case AdFormat.NativeEndCard:
                    StartFullScreen(format);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        /// <summary>A request that carries the restricted data processing flag when the player opted out of the sale of their data.</summary>
        private AdRequest NewRequest() {
            var request = new AdRequest();
            if (consent.DoNotSell) request.Extras.Add("rdp", "1");
            return request;
        }

        private void ReportPaid(AdFormat format, string unitId, AdValue value, ResponseInfo responseInfo) {
            var network = responseInfo?.GetLoadedAdapterResponseInfo()?.AdSourceName;
            context?.ReportPaid(new AdImpression(format, MediationId, network, unitId, value.Value * 1e-6, value.CurrencyCode, PlacementOf(format)));
        }

        // Each full-screen format's last show's placement; a show and its paid event may come on any thread.
        private readonly Dictionary<AdFormat, string> showPlacements = new();

        private string PlacementOf(AdFormat format) {
            if (!format.IsFullScreen()) return null;

            lock (showPlacements) return showPlacements.TryGetValue(format, out var placement) ? placement : null;
        }

        /// <summary>Waits 2, 4, 8... up to 32 seconds before a reload, as the SDKs recommend.</summary>
        private void RetryLater(ref int attempt, Action load) {
            attempt = Math.Min(attempt + 1, 5);
            RetryAsync(1 << attempt, load).Forget();
        }

        private async UniTaskVoid RetryAsync(int seconds, Action load) {
            var token = lifetime?.Token ?? CancellationToken.None;
            if (token.IsCancellationRequested) return;

            await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime, PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (token.IsCancellationRequested) return;

            load();
        }

        private static void LogNoUnitId(AdFormat format) {
            Debug.LogError($"{LogTag}: no {format} unit id for {Application.platform}, it does not load");
        }
    }
}