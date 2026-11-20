using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using RiseOn.Consent;
using UnityEngine;
using Debug = UnityEngine.Debug;
#if ADS_SUPPORT_REMOTECONFIG
using RiseOn.Firebase.RemoteConfig;
#endif

namespace RiseOn.Ads {
    /// <summary>
    /// The mechanics behind <see cref="AdsSdk"/>: the mediations started once the consent flow allows ads, the priority lists, the covers and the threading rules they need.<br/>
    /// One per session; <see cref="AdsSdk"/> is its public face.
    /// </summary>
    internal sealed class AdsHost : IAdChainHost, IAdCollapsibleHost, IDisposable {
        private readonly AdsSettings settings;
        // Each format's mediations for the session, best first: the page's list, or what Remote Config named in its place.
        private readonly Dictionary<AdFormat, string[]> priorities = new();
        private readonly List<IAdMediation>              running    = new();
        private readonly List<IAdMediation>              started    = new();
        private readonly CancellationTokenSource        lifetime   = new();
        private readonly UniTaskCompletionSource        initialized;

        private bool isStarting;

        // The Editor's stand-in of each started mediation, which serves its formats that are not native with the pack's placeholders.
        private readonly Dictionary<IAdMediation, EditorStandIn> standIns = new();

        // Unity's thread.
        private bool isPausedInEditor;

        private int  fullScreenCount;
        private long fullScreenEndTimestamp;

        private IAdMediationBanner     shownBanner;
        private IAdMediationMRec       shownMRec;

        private IAdMediationNativeCollapsible collapsibleMediation;
        private int                          collapsibleSession;
        private bool                         collapsibleUsesCover;

        // What the game wants on screen, shown once a mediation serving it starts.
        private bool    bannerWanted;
        private bool    mRecWanted;

        private DateTime pausedAt;

        public AdsHost(AdsSettings settings, UniTaskCompletionSource initialized) {
            this.settings    = settings;
            this.initialized = initialized;
        }

        public bool IsInitialized { get; private set; }

        /// <summary>The mediations starting or started; one that failed to start is left out.</summary>
        public IReadOnlyList<IAdMediation> Running => running;

        public void Dispose() {
            ConsentFlow.StateChanged -= ApplyConsent;
            lifetime.Cancel();
            lifetime.Dispose();

            foreach (var mediation in running) {
                try {
                    mediation.Dispose();
                } catch (Exception exception) {
                    Debug.LogException(exception);
                }
            }

            running.Clear();
            started.Clear();
        }

        public void OnApplicationPause(bool paused) {
            foreach (var mediation in started) {
                if (mediation.IsInitialized && mediation is IAdMediationLifecycle lifecycle) lifecycle.OnApplicationPause(paused);
            }

            // Our own full-screen ad pauses the app on Android, not on iOS; neither is the player's leaving or return. Nor is a debugging
            // tool the debugger opened: coming back from it would show the app open ad.
            if (IsShowingFullScreen || AdsDebugger.IsShowing) return;

            if (paused) {
                pausedAt = DateTime.UtcNow;
                AdsSdk.RaisePlayerLeft();
                return;
            }

            if (!IsInitialized) return;

            var away = pausedAt == default ? 0 : (DateTime.UtcNow - pausedAt).TotalSeconds;
            AdsSdk.RaisePlayerReturned(away);
        }

        #region Consent and initialization

        /// <summary>
        /// Once the session's priorities are known and the consent flow allows ads, every mediation they name.<br/>
        /// Initialized when the mediations started or failed to, or when consent holds them back; they start later, once the player's choices allow ads.
        /// </summary>
        public async UniTaskVoid InitializeAsync() {
            var cancellationToken = lifetime.Token;
            try {
                // Side by side: Remote Config fetches while the player answers the consent form.
                var resolving = ResolvePrioritiesAsync();
                var consent   = await ConsentFlow.WaitUntilCompleteAsync();
                await resolving;
                cancellationToken.ThrowIfCancellationRequested();
                ConsentFlow.StateChanged += ApplyConsent;

                if (consent.CanRequestAds) await StartAllAsync(consent, cancellationToken);
                else Debug.LogWarning($"{nameof(AdsSdk)}: consent does not allow ads yet; the mediations start once it does");
            } catch (OperationCanceledException) {
                return;
            } finally {
                if (!cancellationToken.IsCancellationRequested) {
                    IsInitialized = true;
                    initialized.TrySetResult();
                }
            }
        }

        // Side by side, so that an SDK slow to start, or one that never does, holds no other back.
        private UniTask StartAllAsync(ConsentState consent, CancellationToken cancellationToken) {
            isStarting = true;
            var starts = new List<UniTask>();
            foreach (var mediation in settings.Mediations) {
                var formats = FormatsOf(mediation.Id);
                if (formats.Count > 0) starts.Add(StartAsync(mediation, formats, consent, cancellationToken));
            }

            return UniTask.WhenAll(starts);
        }

        private async UniTask StartAsync(IAdMediation mediation, HashSet<AdFormat> formats, ConsentState consent, CancellationToken cancellationToken) {
            running.Add(mediation);
            var       context = new MediationContext(mediation.Id, settings.Formats);
            Exception failure = null;
            try {
                mediation.ApplyConsent(consent);
                if (Application.isEditor) {
                    // The pack's placeholders serve the rest; the SDK starts only for the native formats, which keep its own preview.
                    var native = new HashSet<AdFormat>();
                    foreach (var format in formats) {
                        if (format.IsNative()) native.Add(format);
                    }

                    if (native.Count > 0) await mediation.InitializeAsync(context, native, cancellationToken);
                } else {
                    await mediation.InitializeAsync(context, formats, cancellationToken);
                }
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception exception) {
                failure = exception;
            }

            // The SDK may finish on its own thread; the lists are Unity's thread's.
            await UniTask.SwitchToMainThread(cancellationToken);
            if (failure != null) {
                running.Remove(mediation);
                Debug.LogError($"{nameof(AdsSdk)}: {mediation.Id} failed to start");
                Debug.LogException(failure);
                return;
            }

            started.Add(mediation);
            if (Application.isEditor) {
                standIns[mediation] = new EditorStandIn(mediation, settings.Formats);
                foreach (var format in formats) {
                    if (!format.IsNative() && format.IsServedBy(mediation)) context.ReportLoaded(format);
                }
            }

            ShowWanted();
        }

        // The player changed their choices in the privacy options form; a mediation still starting hears it too. Choices that allow ads
        // now, where the launch's did not, start the mediations.
        private void ApplyConsent(ConsentState consent) {
            foreach (var mediation in running) mediation.ApplyConsent(consent);
            if (!isStarting && consent.CanRequestAds) StartAllAsync(consent, lifetime.Token).Forget();
        }

        private HashSet<AdFormat> FormatsOf(string mediationId) {
            var formats = new HashSet<AdFormat>();
            foreach (var (format, ids) in priorities) {
                if (Array.IndexOf(ids, mediationId) >= 0) formats.Add(format);
            }

            return formats;
        }

        #endregion

        #region Priorities

        // Fixed before any mediation starts, so each loads the formats it serves from the start; a change published later shows from the next launch.
        private async UniTask ResolvePrioritiesAsync() {
            foreach (var (format, list) in settings.Priorities) priorities[format] = list == null ? Array.Empty<string>() : ToArray(list.MediationIds);

#if ADS_SUPPORT_REMOTECONFIG
            if (!settings.HasRemoteKeys) return;

            await RemoteConfigInit.WaitUntilCompleteAsync();
            foreach (var (format, key) in settings.RemoteKeys) {
                if (key.Id == 0) continue;

                // The page reports a key removed from Remote Config, and stops the build.
                if (Parse(format, ((RemoteKey) key.Id).Get<string>().Value) is { } ids) priorities[format] = ids;
            }
#else
            await UniTask.CompletedTask;
#endif

            static string[] ToArray(IReadOnlyList<string> ids) {
                var array = new string[ids.Count];
                for (var i = 0; i < ids.Count; i++) array[i] = ids[i];
                return array;
            }
        }

        /// <summary>A remote value's mediations: null for an empty value, which keeps the page's list; none for <c>off</c>; else the ids split by commas, those the page lacks left out.</summary>
        private string[] Parse(AdFormat format, string text) {
            text = text?.Trim();
            if (string.IsNullOrEmpty(text)) return null;
            if (text == "off") return Array.Empty<string>();

            var ids = new List<string>();
            foreach (var part in text.Split(',')) {
                var id = part.Trim();
                if (id.Length == 0 || ids.Contains(id)) continue;

                if (IsListed(id)) ids.Add(id);
                else Debug.LogWarning($"{nameof(AdsSdk)}: Remote Config names {id} for {format}, which the mediations list does not have; it is left out");
            }

            return ids.ToArray();

            bool IsListed(string id) {
                foreach (var mediation in settings.Mediations) {
                    if (mediation.Id == id) return true;
                }

                return false;
            }
        }

        private List<TMediation> Candidates<TMediation>(AdFormat format) where TMediation : class, IAdMediation {
            var result = new List<TMediation>();
            for (var index = 0; NextCandidate<TMediation>(format, ref index) is { } mediation;) result.Add(mediation);
            return result;
        }

        /// <summary>
        /// The format's next started mediation of this type, best first, from <paramref name="index"/> on; null past the last.<br/>
        /// In the Editor, a format that is not native gets the mediation's stand-in.<br/>
        /// Allocates nothing, for the ready checks a game makes every frame.
        /// </summary>
        private TMediation NextCandidate<TMediation>(AdFormat format, ref int index) where TMediation : class, IAdMediation {
            if (!priorities.TryGetValue(format, out var order)) return null;

            while (index < order.Length) {
                var id = order[index++];
                foreach (var mediation in started) {
                    if (mediation.Id != id || mediation is not TMediation typed) continue;

                    return !format.IsNative() && standIns.TryGetValue(mediation, out var standIn) ? standIn as TMediation : typed;
                }
            }

            return null;
        }

        #endregion

        #region Full screen

        public bool IsShowingFullScreen =>
            Volatile.Read(ref fullScreenCount) > 0
         || Stopwatch.GetTimestamp() - Volatile.Read(ref fullScreenEndTimestamp) < Stopwatch.Frequency;

        public bool IsReady(AdFormat format) {
            var index = 0;
            if (format.IsFullScreen()) {
                while (NextCandidate<IAdMediationFullScreen>(format, ref index) is { } mediation) {
                    if (mediation.IsInitialized && mediation.IsReady(format)) return true;
                }

                return false;
            }

            if (format is AdFormat.NativeCollapsible) {
                while (NextCandidate<IAdMediationNativeCollapsible>(format, ref index) is { } mediation) {
                    if (mediation.IsInitialized && mediation.IsCollapsibleReady) return true;
                }

                return false;
            }

            return NextCandidate<IAdMediation>(format, ref index) != null;
        }

        IReadOnlyList<IAdMediationFullScreen> IAdChainHost.FullScreenCandidates(AdFormat format) => Candidates<IAdMediationFullScreen>(format);

        // Unity's thread: a chain begins from its Run.
        void IAdChainHost.BeginFullScreen() {
            Interlocked.Increment(ref fullScreenCount);
            if (!Application.isEditor || isPausedInEditor) return;

            isPausedInEditor = true;
            AdsSdk.PauseInEditor(true);
        }

        // Any thread: on a device the last step's close may come while Unity sleeps. The Editor's loop runs on, so its pause ends
        // through it, unless another chain began in between.
        void IAdChainHost.EndFullScreen() {
            Volatile.Write(ref fullScreenEndTimestamp, Stopwatch.GetTimestamp());
            if (Interlocked.Decrement(ref fullScreenCount) != 0 || !Application.isEditor) return;

            UniTask.Post(() => {
                if (Volatile.Read(ref fullScreenCount) != 0 || !isPausedInEditor) return;

                isPausedInEditor = false;
                AdsSdk.PauseInEditor(false);
            });
        }

        Color IAdChainHost.CoverColor => settings.CoverColor;
        Color IAdCollapsibleHost.CoverColor => settings.CoverColor;

        void IAdChainHost.Displayed(AdFormat format, string placement) => AdsSdk.RaiseDisplayed(format, placement);

        void IAdCollapsibleHost.Displayed() => AdsSdk.RaiseCollapsibleDisplayed();

        #endregion

        #region Collapsible

        public void HideCollapsible() {
            if (collapsibleMediation == null) return;

            ++collapsibleSession;
            collapsibleMediation.HideCollapsible(slideOut: true);
            if (collapsibleUsesCover) {
                HalfScreenCover.Conceal();
                HalfScreenCover.Remove();
            }

            collapsibleMediation  = null;
            collapsibleUsesCover = false;
        }

        IReadOnlyList<IAdMediationNativeCollapsible> IAdCollapsibleHost.CollapsibleCandidates => Candidates<IAdMediationNativeCollapsible>(AdFormat.NativeCollapsible);

        int IAdCollapsibleHost.BeginCollapsible(IAdMediationNativeCollapsible mediation, bool usesCover) {
            HideCollapsible();

            collapsibleMediation  = mediation;
            collapsibleUsesCover = usesCover;
            return ++collapsibleSession;
        }

        bool IAdCollapsibleHost.IsCurrentCollapsible(int session) => session == collapsibleSession;

        void IAdCollapsibleHost.EndCollapsible(int session) {
            if (session != collapsibleSession) return;

            collapsibleMediation  = null;
            collapsibleUsesCover = false;
        }

        #endregion

        #region Banner, MRec, native in-feed, native MRec

        public void ShowBanner() {
            bannerWanted = true;
            var mediation = First<IAdMediationBanner>(AdFormat.Banner);
            if (mediation == null) return;

            if (shownBanner != null && shownBanner != mediation) shownBanner.HideBanner();
            shownBanner = mediation;
            mediation.ShowBanner();
        }

        public void HideBanner() {
            bannerWanted = false;
            shownBanner?.HideBanner();
            shownBanner = null;
        }

        public void ShowMRec() {
            mRecWanted = true;
            var mediation = First<IAdMediationMRec>(AdFormat.MRec);
            if (mediation == null) return;

            if (shownMRec != null && shownMRec != mediation) shownMRec.HideMRec();
            shownMRec = mediation;
            mediation.ShowMRec();
        }

        public void HideMRec() {
            mRecWanted = false;
            shownMRec?.HideMRec();
            shownMRec = null;
        }

        public NativeRectHandle CreateNativeRect(AdFormat format, Vector2Int sizePx, int roundCornerPx, Func<bool> canShow, Action<NativeRectHandle> disposed) {
            var cell = format is AdFormat.NativeMRec
                ? First<IAdMediationNativeMRec>(format)?.CreateNativeMRec(sizePx, roundCornerPx)
                : First<IAdMediationNativeInFeed>(format)?.CreateInFeed(sizePx, roundCornerPx);
            return new NativeRectHandle(cell, canShow, disposed);
        }

        private TMediation First<TMediation>(AdFormat format) where TMediation : class, IAdMediation {
            for (var index = 0; NextCandidate<TMediation>(format, ref index) is { } mediation;) {
                if (mediation.IsInitialized) return mediation;
            }

            return null;
        }

        // What the game wants on screen, on the best mediation now: one asked for before any mediation started, or
        // moved to a better one that started since.
        private void ShowWanted() {
            if (bannerWanted) ShowBanner();
            if (mRecWanted) ShowMRec();
        }

        #endregion

        // Mediations report from their SDK's threads; the game hears on Unity's.
        private sealed class MediationContext : IAdMediationContext {
            private readonly string mediationId;

            public MediationContext(string mediationId, AdFormatSettings formats) {
                this.mediationId = mediationId;
                Formats          = formats;
            }

            public AdFormatSettings Formats { get; }

            public void ReportPaid(in AdImpression impression) {
                var copy = impression;
                UniTask.Post(() => AdsSdk.RaisePaid(copy));
            }

            public void ReportLoaded(AdFormat format) {
                UniTask.Post(() => AdsSdk.RaiseLoaded(format, mediationId));
            }

            public void ReportLoadFailed(AdFormat format, in AdError error) {
                var copy = error;
                UniTask.Post(() => AdsSdk.RaiseLoadFailed(format, mediationId, copy));
            }
        }
    }
}