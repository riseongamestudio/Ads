using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RiseOn.Consent;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads.Max {
    /// <summary>
    /// AppLovin MAX: banner, MRec, interstitial, rewarded and app open.<br/>
    /// The SDK key and the AdMob app ids live in the AppLovin settings file, drawn here; MAX's own consent flow stays off, the consent flow of com.riseon.consent runs instead.
    /// </summary>
    [Serializable, TypeRegistryItem("MAX")]
    internal sealed class MaxMediation
        : IAdMediationBanner
        , IAdMediationMRec
        , IAdMediationFullScreen
        , IAdMediationDebugger {
        public const string MediationId = "max";

        private const string LogTag = nameof(MaxMediation);

        [SerializeField, AdFormatSection("Banner")]
        [SectionGroup(AdFormatSectionAttribute.Group, Foldable = true, Tooltip = AdFormatSectionAttribute.GroupTooltip)]
        private AdUnitId banner;

        [SerializeField, AdFormatSection("MRec")]
        private AdUnitId mRec;

        [SerializeField, AdFormatSection("Interstitial", -1)]
        private AdUnitId interstitial;

        [SerializeField, AdFormatSection("Rewarded", -1)]
        private AdUnitId rewarded;

        [SerializeField, AdFormatSection("App Open", -1)]
        private AdUnitId appOpen;

        public string Id => MediationId;
        public bool IsInitialized { get; private set; }

        private IAdMediationContext                    context;
        private CancellationTokenSource               lifetime;
        private string                                bannerId;
        private string                                mRecId;
        private Dictionary<AdFormat, FullScreenUnit> fullScreenUnits;
        private bool                                  childDirected;

#if UNITY_EDITOR
        [OnInspectorGUI, PropertyOrder(-10)]
        private void DrawSdkSettings() => AdsEditorHooks.DrawMediation(this);
#endif

        public void ApplyConsent(in ConsentState consent) {
            childDirected = consent.ChildDirected;
            MaxSdk.SetHasUserConsent(consent.HasUserConsent);
            MaxSdk.SetDoNotSell(consent.DoNotSell);
        }

        public async UniTask InitializeAsync(IAdMediationContext context, IReadOnlyCollection<AdFormat> formats, CancellationToken cancellationToken) {
            // AppLovin's privacy terms: the SDK may not be initialized at all for a child.
            if (childDirected) throw new InvalidOperationException($"{LogTag}: AppLovin may not run in a game for children (Child Directed in Project Settings > RiseOn > Consent)");

            this.context = context;
            lifetime     = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Events off Unity's thread: a chain needs the close callbacks while the cover keeps Unity paused.
            MaxSdk.InvokeEventsOnUnityMainThread = false;

            var initialized = new UniTaskCompletionSource();
            void HandleInitialized(MaxSdkBase.SdkConfiguration _) => initialized.TrySetResult();

            MaxSdkCallbacks.OnSdkInitializedEvent += HandleInitialized;
            try {
                MaxSdk.InitializeSdk();
                await initialized.Task;
            } finally {
                MaxSdkCallbacks.OnSdkInitializedEvent -= HandleInitialized;
            }

            await UniTask.SwitchToMainThread(cancellationToken);

            Subscribe();
            IsInitialized = true;
            foreach (var format in formats) StartFormat(format);
        }

        public string DebuggerName => "Mediation Debugger";

        public void OpenDebugger(Action<string> done) {
            if (!IsInitialized) {
                done("MAX has not started");
                return;
            }

            MaxSdk.ShowMediationDebugger();
            done(null);
        }

        public void Dispose() {
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;

            if (!IsInitialized) return;

            Unsubscribe();
            if (bannerId != null) MaxSdk.DestroyBanner(bannerId);
            if (mRecId != null) MaxSdk.DestroyMRec(mRecId);
            bannerId        = null;
            mRecId          = null;
            fullScreenUnits = null;
            IsInitialized   = false;
        }

        private static MaxSdkBase.AdViewPosition ToMax(AdScreenPosition position) {
            switch (position) {
                case AdScreenPosition.TopLeft:     return MaxSdkBase.AdViewPosition.TopLeft;
                case AdScreenPosition.Top:         return MaxSdkBase.AdViewPosition.TopCenter;
                case AdScreenPosition.TopRight:    return MaxSdkBase.AdViewPosition.TopRight;
                case AdScreenPosition.Center:      return MaxSdkBase.AdViewPosition.Centered;
                case AdScreenPosition.BottomLeft:  return MaxSdkBase.AdViewPosition.BottomLeft;
                case AdScreenPosition.Bottom:      return MaxSdkBase.AdViewPosition.BottomCenter;
                case AdScreenPosition.BottomRight: return MaxSdkBase.AdViewPosition.BottomRight;
                default:                           throw new ArgumentOutOfRangeException(nameof(position), position, null);
            }
        }

        private void StartFormat(AdFormat format) {
            switch (format) {
                case AdFormat.Banner:
                    if (!HasUnitId(format, banner)) return;

                    bannerId = banner.Value;
                    var bannerSettings = context.Formats.Banner;
                    MaxSdk.CreateBanner(bannerId, new MaxSdkBase.AdViewConfiguration(ToMax(bannerSettings.Position)) { IsAdaptive = bannerSettings.Adaptive });
                    MaxSdk.HideBanner(bannerId);
                    break;

                case AdFormat.MRec:
                    if (!HasUnitId(format, mRec)) return;

                    mRecId = mRec.Value;
                    MaxSdk.CreateMRec(mRecId, new MaxSdkBase.AdViewConfiguration(ToMax(context.Formats.MRec.Position)));
                    MaxSdk.HideMRec(mRecId);
                    break;

                case AdFormat.Interstitial:
                    StartFullScreen(format, interstitial);
                    break;

                case AdFormat.Rewarded:
                    StartFullScreen(format, rewarded);
                    break;

                case AdFormat.AppOpen:
                    StartFullScreen(format, appOpen);
                    break;

                case AdFormat.RewardedInterstitial:
                case AdFormat.NativeInFeed:
                case AdFormat.NativeMRec:
                case AdFormat.NativeCollapsible:
                case AdFormat.NativeInterstitial:
                case AdFormat.NativeAppOpen:
                case AdFormat.NativeEndCard:
                    Debug.LogError($"{LogTag}: MAX does not serve {format}; take it off the priority list");
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

        public void ShowBanner() {
            if (bannerId != null) MaxSdk.ShowBanner(bannerId);
        }

        public void HideBanner() {
            if (bannerId != null) MaxSdk.HideBanner(bannerId);
        }

        public void ShowMRec() {
            if (mRecId != null) MaxSdk.ShowMRec(mRecId);
        }

        public void HideMRec() {
            if (mRecId != null) MaxSdk.HideMRec(mRecId);
        }

        // ---- Full screen

        public bool Supports(AdFormat format) {
            return format is AdFormat.Interstitial or AdFormat.Rewarded or AdFormat.AppOpen;
        }

        public bool IsReady(AdFormat format) {
            var unit = UnitOf(format);
            if (unit == null) return false;

            switch (format) {
                case AdFormat.Interstitial: return MaxSdk.IsInterstitialReady(unit.UnitId);
                case AdFormat.Rewarded:     return MaxSdk.IsRewardedAdReady(unit.UnitId);
                case AdFormat.AppOpen:      return MaxSdk.IsAppOpenAdReady(unit.UnitId);
                default:                    return false;
            }
        }

        // The SDK's events stay on its own thread (InvokeEventsOnUnityMainThread is off).
        public bool CallsBackOffLoop(AdFormat format) => true;

        // MAX documents its calls for Unity's thread only.
        public bool ShowsFromAnyThread(AdFormat format) => false;

        public void Show(AdFormat format, IAdShowListener listener) {
            var unit = UnitOf(format);
            if (unit == null || !IsReady(format)) {
                listener.OnFailed(new AdError(-1, $"{format} not ready"));
                return;
            }

            Volatile.Write(ref unit.Listener, listener);
            switch (format) {
                case AdFormat.Interstitial:
                    MaxSdk.ShowInterstitial(unit.UnitId);
                    break;

                case AdFormat.Rewarded:
                    MaxSdk.ShowRewardedAd(unit.UnitId);
                    break;

                case AdFormat.AppOpen:
                    MaxSdk.ShowAppOpenAd(unit.UnitId);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        private FullScreenUnit UnitOf(AdFormat format) {
            return fullScreenUnits != null && fullScreenUnits.TryGetValue(format, out var unit) ? unit : null;
        }

        private FullScreenUnit UnitOf(string unitId) {
            if (fullScreenUnits == null) return null;

            foreach (var unit in fullScreenUnits.Values) {
                if (unit.UnitId == unitId) return unit;
            }

            return null;
        }

        private void StartFullScreen(AdFormat format, in AdUnitId unitId) {
            if (!HasUnitId(format, unitId)) return;

            fullScreenUnits ??= new Dictionary<AdFormat, FullScreenUnit>();
            fullScreenUnits[format] = new FullScreenUnit(format, unitId.Value);
            Load(format);
        }

        private void Load(AdFormat format) {
            var unit = UnitOf(format);
            if (unit == null) return;

            switch (format) {
                case AdFormat.Interstitial:
                    MaxSdk.LoadInterstitial(unit.UnitId);
                    break;

                case AdFormat.Rewarded:
                    MaxSdk.LoadRewardedAd(unit.UnitId);
                    break;

                case AdFormat.AppOpen:
                    MaxSdk.LoadAppOpenAd(unit.UnitId);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        private sealed class FullScreenUnit {
            public readonly AdFormat        Format;
            public readonly string          UnitId;
            public          IAdShowListener Listener;
            public          int             Attempt;

            public FullScreenUnit(AdFormat format, string unitId) {
                Format = format;
                UnitId = unitId;
            }
        }

        // ---- Events, on the SDK's thread

        private void Subscribe() {
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += HandleBannerPaid;
            MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent   += HandleMRecPaid;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent        += HandleLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent    += HandleLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent     += HandleDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += HandleDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent        += HandleHidden;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent   += HandleFullScreenPaid;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent         += HandleLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent     += HandleLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent      += HandleDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent  += HandleDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent         += HandleHidden;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent    += HandleFullScreenPaid;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += HandleRewarded;

            MaxSdkCallbacks.AppOpen.OnAdLoadedEvent        += HandleLoaded;
            MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent    += HandleLoadFailed;
            MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent     += HandleDisplayed;
            MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += HandleDisplayFailed;
            MaxSdkCallbacks.AppOpen.OnAdHiddenEvent        += HandleHidden;
            MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent   += HandleFullScreenPaid;
        }

        private void Unsubscribe() {
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent -= HandleBannerPaid;
            MaxSdkCallbacks.MRec.OnAdRevenuePaidEvent   -= HandleMRecPaid;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent        -= HandleLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent    -= HandleLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent     -= HandleDisplayed;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= HandleDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent        -= HandleHidden;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent   -= HandleFullScreenPaid;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent         -= HandleLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent     -= HandleLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent      -= HandleDisplayed;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent  -= HandleDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent         -= HandleHidden;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent    -= HandleFullScreenPaid;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= HandleRewarded;

            MaxSdkCallbacks.AppOpen.OnAdLoadedEvent        -= HandleLoaded;
            MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent    -= HandleLoadFailed;
            MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent     -= HandleDisplayed;
            MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent -= HandleDisplayFailed;
            MaxSdkCallbacks.AppOpen.OnAdHiddenEvent        -= HandleHidden;
            MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent   -= HandleFullScreenPaid;
        }

        private void HandleBannerPaid(string unitId, MaxSdkBase.AdInfo info) => ReportPaid(AdFormat.Banner, info);
        private void HandleMRecPaid(string unitId, MaxSdkBase.AdInfo info)   => ReportPaid(AdFormat.MRec, info);

        private void HandleFullScreenPaid(string unitId, MaxSdkBase.AdInfo info) {
            var unit = UnitOf(unitId);
            if (unit != null) ReportPaid(unit.Format, info);
        }

        private void HandleLoaded(string unitId, MaxSdkBase.AdInfo info) {
            var unit = UnitOf(unitId);
            if (unit == null) return;

            unit.Attempt = 0;
            context?.ReportLoaded(unit.Format);
        }

        private void HandleLoadFailed(string unitId, MaxSdkBase.ErrorInfo error) {
            var unit = UnitOf(unitId);
            if (unit == null) return;

            context?.ReportLoadFailed(unit.Format, new AdError((int)error.Code, error.Message));

            // 2, 4, 8... up to 32 seconds, as AppLovin recommends.
            unit.Attempt = Math.Min(unit.Attempt + 1, 5);
            UniTask.Post(() => RetryAsync(unit.Format, 1 << unit.Attempt).Forget());
        }

        private async UniTaskVoid RetryAsync(AdFormat format, int seconds) {
            var token = lifetime?.Token ?? CancellationToken.None;
            if (token.IsCancellationRequested) return;

            await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.Realtime, PlayerLoopTiming.Update, token).SuppressCancellationThrow();
            if (token.IsCancellationRequested) return;

            Load(format);
        }

        private void HandleDisplayed(string unitId, MaxSdkBase.AdInfo info) {
            var unit = UnitOf(unitId);
            if (unit != null) Volatile.Read(ref unit.Listener)?.OnDisplayed();
        }

        private void HandleRewarded(string unitId, MaxSdkBase.Reward reward, MaxSdkBase.AdInfo info) {
            var unit = UnitOf(unitId);
            if (unit != null) Volatile.Read(ref unit.Listener)?.OnRewarded();
        }

        private void HandleDisplayFailed(string unitId, MaxSdkBase.ErrorInfo error, MaxSdkBase.AdInfo info) {
            var unit = UnitOf(unitId);
            if (unit == null) return;

            Interlocked.Exchange(ref unit.Listener, null)?.OnFailed(new AdError((int)error.Code, error.Message));
            UniTask.Post(() => Load(unit.Format));
        }

        private void HandleHidden(string unitId, MaxSdkBase.AdInfo info) {
            var unit = UnitOf(unitId);
            if (unit == null) return;

            Interlocked.Exchange(ref unit.Listener, null)?.OnClosed();
            UniTask.Post(() => Load(unit.Format));
        }

        private void ReportPaid(AdFormat format, MaxSdkBase.AdInfo info) {
            // MAX reports revenue in US dollars.
            context?.ReportPaid(new AdImpression(format, MediationId, info.NetworkName, info.AdUnitIdentifier, info.Revenue, "USD"));
        }
    }
}