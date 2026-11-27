using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using Sirenix.OdinInspector;
using RiseOn.Utils;
using UnityEngine;
using GmaError = GoogleMobileAds.Api.AdError;

namespace RiseOn.Ads.AdMob {
    internal sealed partial class AdMobMediation {
        [SerializeField, AdUnitTest("ca-app-pub-3940256099942544/1033173712", "ca-app-pub-3940256099942544/4411468910"), AdFormatSection("Interstitial", -1)]
        [SectionGroup(AdFormatSectionAttribute.Group, Foldable = true, Tooltip = AdFormatSectionAttribute.GroupTooltip)]
        private AdUnitId interstitial;

        [SerializeField, AdUnitTest("ca-app-pub-3940256099942544/5224354917", "ca-app-pub-3940256099942544/1712485313"), AdFormatSection("Rewarded", -1)]
        private AdUnitId rewarded;

        [SerializeField, AdUnitTest("ca-app-pub-3940256099942544/5354046379", "ca-app-pub-3940256099942544/6978759866"), AdFormatSection("Rewarded Interstitial", -1)]
        private AdUnitId rewardedInterstitial;

        [SerializeField, AdUnitTest("ca-app-pub-3940256099942544/9257395921", "ca-app-pub-3940256099942544/5575463023"), AdFormatSection("App Open", -1)]
        private AdUnitId appOpen;

        // Written only while starting and disposing, so a chain may read it from any thread.
        private Dictionary<AdFormat, FullScreenSlot> fullScreenSlots;

        public bool Supports(AdFormat format) => format.IsFullScreen();

        public bool IsReady(AdFormat format) => SlotOf(format)?.IsReady == true;

        // The SDK's events stay on its own thread (RaiseAdEventsOnUnityMainThread is off), except the reward on iOS,
        // which the plugin defers to Unity's thread: under the cover it would arrive after the chain ended.
        public bool CallsBackOffLoop(AdFormat format) {
            return !(format.IsRewarding() && Application.platform is RuntimePlatform.IPhonePlayer);
        }

        // Google's formats load and show through Unity's thread; the native views of this pack do not need it.
        public bool ShowsFromAnyThread(AdFormat format) {
            return format.IsNative();
        }

        public void Show(AdFormat format, string placement, in AdCloseSettings close, IAdShowListener listener) {
            // Google's SDK takes no placement, so its paid event gets the show's from here.
            lock (showPlacements) showPlacements[format] = placement;

            var slot = SlotOf(format);
            if (slot == null) {
                listener.OnFailed(new AdError(-1, $"{format} is not loaded by {MediationId}"));
                return;
            }

            slot.Show(close, listener);
        }

        private FullScreenSlot SlotOf(AdFormat format) {
            return fullScreenSlots != null && fullScreenSlots.TryGetValue(format, out var slot) ? slot : null;
        }

        private void StartFullScreen(AdFormat format) {
            var slot = CreateFullScreenSlot(format);
            if (slot == null) return;

            fullScreenSlots ??= new Dictionary<AdFormat, FullScreenSlot>();
            fullScreenSlots[format] = slot;
            slot.Load();
        }

        private FullScreenSlot CreateFullScreenSlot(AdFormat format) {
            switch (format) {
                case AdFormat.Interstitial:
                    return CreateGmaSlot(format, interstitial, unitId => new InterstitialSlot(this, unitId));

                case AdFormat.Rewarded:
                    return CreateGmaSlot(format, rewarded, unitId => new RewardedSlot(this, unitId));

                case AdFormat.RewardedInterstitial:
                    return CreateGmaSlot(format, rewardedInterstitial, unitId => new RewardedInterstitialSlot(this, unitId));

                case AdFormat.AppOpen:
                    return CreateGmaSlot(format, appOpen, unitId => new AppOpenSlot(this, unitId));

                case AdFormat.NativeInterstitial:
                    return CreateNativeFullScreenSlot(format, nativeInterstitial);

                case AdFormat.NativeAppOpen:
                    return CreateNativeFullScreenSlot(format, nativeAppOpen);

                case AdFormat.NativeEndCard:
                    return CreateNativeFullScreenSlot(format, nativeEndCard);

                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }

            static FullScreenSlot CreateGmaSlot(AdFormat format, AdUnitId unitId, Func<string, FullScreenSlot> create) {
                if (!unitId.IsEmpty) return create(unitId.Value);

                LogNoUnitId(format);
                return null;
            }
        }

        private void DisposeFullScreen() {
            if (fullScreenSlots == null) return;

            foreach (var slot in fullScreenSlots.Values) slot.Dispose();
            fullScreenSlots = null;
        }

        private abstract class FullScreenSlot {
            public abstract bool IsReady { get; }
            public abstract void Load();
            /// <param name="close">For a native slot; Google's formats close as their SDK draws them.</param>
            public abstract void Show(in AdCloseSettings close, IAdShowListener listener);
            public abstract void Dispose();
        }

        /// <summary>
        /// One of Google's full-screen formats.<br/>
        /// Loads happen on Unity's thread, the show's events reach its listener on the SDK's thread, and a new ad loads once the shown one is gone.
        /// </summary>
        private abstract class GmaSlot<TAd> : FullScreenSlot where TAd : class {
            protected readonly AdMobMediation owner;
            protected readonly string          unitId;
            protected readonly AdFormat        format;

            private readonly TimeSpan  maxAge;
            private readonly Stopwatch age = new();

            private TAd             ad;
            private bool            loading;
            private bool            disposed;
            private int             attempt;
            private IAdShowListener listener;

            protected GmaSlot(AdMobMediation owner, string unitId, AdFormat format, TimeSpan maxAge) {
                this.owner  = owner;
                this.unitId = unitId;
                this.format = format;
                this.maxAge = maxAge;
            }

            public override bool IsReady {
                get {
                    if (ad == null) return false;
                    if (age.Elapsed < maxAge && CanShow(ad)) return true;

                    // Gone stale: Google counts no impression for it, so a fresh one replaces it.
                    Drop();
                    Load();
                    return false;
                }
            }

            public override void Load() {
                if (disposed || loading || ad != null) return;

                loading = true;
                LoadAd(unitId, owner.NewRequest(), (loaded, error) => UniTask.Post(() => HandleLoaded(loaded, error)));
            }

            public override void Show(in AdCloseSettings close, IAdShowListener showListener) {
                if (!IsReady) {
                    showListener.OnFailed(new AdError(-1, $"{format} not ready"));
                    return;
                }

                Volatile.Write(ref listener, showListener);
                ShowAd(ad, showListener);
            }

            public override void Dispose() {
                disposed = true;
                Drop();
            }

            protected abstract void LoadAd(string unitId, AdRequest request, Action<TAd, LoadAdError> onLoaded);
            protected abstract bool CanShow(TAd shown);
            protected abstract void ShowAd(TAd shown, IAdShowListener showListener);
            protected abstract ResponseInfo ResponseOf(TAd loaded);
            protected abstract void Destroy(TAd dropped);

            /// <summary>Hooks the ad's events to the handlers below; the formats share no interface to do it here.</summary>
            protected abstract void Subscribe(TAd loaded);

            protected void HandlePaid(TAd paid, AdValue value) {
                owner.ReportPaid(format, unitId, value, ResponseOf(paid));
            }

            protected void HandleOpened() {
                Volatile.Read(ref listener)?.OnDisplayed();
            }

            protected void HandleClosed() {
                if (format.IsRewarding() && Application.platform is RuntimePlatform.IPhonePlayer) {
                    UniTask.Post(() => EndAfterRewardAsync().Forget());
                    return;
                }

                Interlocked.Exchange(ref listener, null)?.OnClosed();
                UniTask.Post(Replace);
            }

            // On iOS the plugin raises the reward on Unity's thread, so it may come after the close; one frame lets it
            // land before the listener hears the show is over.
            private async UniTaskVoid EndAfterRewardAsync() {
                await UniTask.NextFrame();

                Interlocked.Exchange(ref listener, null)?.OnClosed();
                Replace();
            }

            protected void HandleShowFailed(GmaError error) {
                Interlocked.Exchange(ref listener, null)?.OnFailed(new AdError(error.GetCode(), error.GetMessage()));
                UniTask.Post(Replace);
            }

            private void HandleLoaded(TAd loaded, LoadAdError error) {
                loading = false;

                if (disposed) {
                    if (loaded != null) Destroy(loaded);
                    return;
                }

                if (error != null || loaded == null) {
                    owner.context?.ReportLoadFailed(format, error == null ? new AdError(-1, "No ad") : new AdError(error.GetCode(), error.GetMessage()));
                    owner.RetryLater(ref attempt, Load);
                    return;
                }

                attempt = 0;
                ad      = loaded;
                age.Restart();
                Subscribe(loaded);
                owner.context?.ReportLoaded(format);
            }

            private void Replace() {
                Drop();
                Load();
            }

            private void Drop() {
                if (ad != null) Destroy(ad);
                ad = null;
            }
        }

        // An hour is how long Google keeps a loaded interstitial or rewarded worth showing; app open, four.
        private static readonly TimeSpan oneHour   = TimeSpan.FromHours(1);
        private static readonly TimeSpan fourHours = TimeSpan.FromHours(4);

        private sealed class InterstitialSlot : GmaSlot<InterstitialAd> {
            public InterstitialSlot(AdMobMediation owner, string unitId) : base(owner, unitId, AdFormat.Interstitial, oneHour) {}

            protected override void LoadAd(string unitId, AdRequest request, Action<InterstitialAd, LoadAdError> onLoaded) => InterstitialAd.Load(unitId, request, onLoaded);
            protected override bool CanShow(InterstitialAd shown) => shown.CanShowAd();
            protected override void ShowAd(InterstitialAd shown, IAdShowListener showListener) => shown.Show();
            protected override ResponseInfo ResponseOf(InterstitialAd loaded) => loaded.GetResponseInfo();
            protected override void Destroy(InterstitialAd dropped) => dropped.Destroy();

            protected override void Subscribe(InterstitialAd loaded) {
                loaded.OnAdPaid                    += value => HandlePaid(loaded, value);
                loaded.OnAdFullScreenContentOpened += HandleOpened;
                loaded.OnAdFullScreenContentClosed += HandleClosed;
                loaded.OnAdFullScreenContentFailed += HandleShowFailed;
            }
        }

        private sealed class RewardedSlot : GmaSlot<RewardedAd> {
            public RewardedSlot(AdMobMediation owner, string unitId) : base(owner, unitId, AdFormat.Rewarded, oneHour) {}

            protected override void LoadAd(string unitId, AdRequest request, Action<RewardedAd, LoadAdError> onLoaded) => RewardedAd.Load(unitId, request, onLoaded);
            protected override bool CanShow(RewardedAd shown) => shown.CanShowAd();
            protected override void ShowAd(RewardedAd shown, IAdShowListener showListener) => shown.Show(_ => showListener.OnRewarded());
            protected override ResponseInfo ResponseOf(RewardedAd loaded) => loaded.GetResponseInfo();
            protected override void Destroy(RewardedAd dropped) => dropped.Destroy();

            protected override void Subscribe(RewardedAd loaded) {
                loaded.OnAdPaid                    += value => HandlePaid(loaded, value);
                loaded.OnAdFullScreenContentOpened += HandleOpened;
                loaded.OnAdFullScreenContentClosed += HandleClosed;
                loaded.OnAdFullScreenContentFailed += HandleShowFailed;
            }
        }

        private sealed class RewardedInterstitialSlot : GmaSlot<RewardedInterstitialAd> {
            public RewardedInterstitialSlot(AdMobMediation owner, string unitId) : base(owner, unitId, AdFormat.RewardedInterstitial, oneHour) {}

            protected override void LoadAd(string unitId, AdRequest request, Action<RewardedInterstitialAd, LoadAdError> onLoaded) => RewardedInterstitialAd.Load(unitId, request, onLoaded);
            protected override bool CanShow(RewardedInterstitialAd shown) => shown.CanShowAd();
            protected override void ShowAd(RewardedInterstitialAd shown, IAdShowListener showListener) => shown.Show(_ => showListener.OnRewarded());
            protected override ResponseInfo ResponseOf(RewardedInterstitialAd loaded) => loaded.GetResponseInfo();
            protected override void Destroy(RewardedInterstitialAd dropped) => dropped.Destroy();

            protected override void Subscribe(RewardedInterstitialAd loaded) {
                loaded.OnAdPaid                    += value => HandlePaid(loaded, value);
                loaded.OnAdFullScreenContentOpened += HandleOpened;
                loaded.OnAdFullScreenContentClosed += HandleClosed;
                loaded.OnAdFullScreenContentFailed += HandleShowFailed;
            }
        }

        private sealed class AppOpenSlot : GmaSlot<AppOpenAd> {
            public AppOpenSlot(AdMobMediation owner, string unitId) : base(owner, unitId, AdFormat.AppOpen, fourHours) {}

            protected override void LoadAd(string unitId, AdRequest request, Action<AppOpenAd, LoadAdError> onLoaded) => AppOpenAd.Load(unitId, request, onLoaded);
            protected override bool CanShow(AppOpenAd shown) => shown.CanShowAd();
            protected override void ShowAd(AppOpenAd shown, IAdShowListener showListener) => shown.Show();
            protected override ResponseInfo ResponseOf(AppOpenAd loaded) => loaded.GetResponseInfo();
            protected override void Destroy(AppOpenAd dropped) => dropped.Destroy();

            protected override void Subscribe(AppOpenAd loaded) {
                loaded.OnAdPaid                    += value => HandlePaid(loaded, value);
                loaded.OnAdFullScreenContentOpened += HandleOpened;
                loaded.OnAdFullScreenContentClosed += HandleClosed;
                loaded.OnAdFullScreenContentFailed += HandleShowFailed;
            }
        }
    }
}