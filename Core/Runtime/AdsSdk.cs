using System;
using System.Collections.Generic;
using System.ComponentModel;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// The Ads pack, as a mediation SDK's own API: started before the first scene from Project Settings > RiseOn > Ads, it starts the mediations once the consent flow allows ads, and shows what it is asked to with no rule of its own.<br/>
    /// The game's rules, such as remove-ads or pacing, are the game's, in its <see cref="IAdsManager"/> class or anywhere; <see cref="AdPacing"/>, <see cref="AdCollapsiblePlan.Pick"/> and <see cref="SetSuppressed"/> are tools for them.<br/>
    /// Unity's thread only; every event comes on it.
    /// </summary>
    public static class AdsSdk {
        private const string LaunchesKey = "RiseOn.Ads.Launches";

        private static AdsHost host;
        private static UniTaskCompletionSource initialized = new();
        private static bool launched;
        private static Type managerType;
        private static Func<IAdsManager> createManager;
        private static bool managerConflict;

        // Here rather than on the host, so that the game may set it before the pack starts.
        private static readonly HashSet<AdFormat> suppressed = new();

        #region State

        /// <summary>
        /// The consent flow completed and every mediation some priority list names started or failed to; or consent does not allow ads yet, and they start once the player's choices do.
        /// </summary>
        public static bool IsInitialized => host is { IsInitialized: true };

        /// <summary>The mediations starting or started, for <see cref="AdsDebugger"/>.</summary>
        internal static IReadOnlyList<IAdMediation> RunningMediations => host != null ? host.Running : Array.Empty<IAdMediation>();

        /// <summary>Completes once <see cref="IsInitialized"/>; callable before the pack starts too.</summary>
        public static UniTask WaitUntilInitializedAsync() => initialized.Task;

        /// <summary>A full-screen ad or chain is up, or ended less than a second ago (the resume it causes is not the player's).</summary>
        public static bool IsShowingFullScreen => host is { IsShowingFullScreen: true };

        /// <summary>
        /// The format's priority list names a mediation on this platform, so it is not off: the page's list until Remote Config fetched, then the one a key named in its place; whether the mediation started does not count.<br/>
        /// For the game's own choices, such as hiding a button of a format no mediation serves.
        /// </summary>
        public static bool Serves(AdFormat format) => Host.Serves(format);

        /// <summary>This launch of the game on the device, 1 for the first, counted by the pack.</summary>
        public static int Launches { get; private set; }

        /// <summary>
        /// Seconds since the format last displayed, from any chain; for the native collapsible, since the first ad of its last chain; none before its first.<br/>
        /// What <see cref="AdPacing"/> counts from.
        /// </summary>
        public static double? SecondsSinceDisplay(AdFormat format) => Host.SecondsSinceDisplay(format);

        #endregion

        #region Events

        /// <summary>Every impression's revenue, from every mediation and format. The analytics pack logs it from here when installed.</summary>
        public static event Action<AdImpression> Paid;

        /// <summary>A full-screen ad is on screen, from any chain; the placement as passed to its show.</summary>
        public static event Action<AdFullScreenFormat, string> Displayed;

        /// <summary>A native collapsible ad slid in or swapped in, from any collapsible chain.</summary>
        public static event Action CollapsibleDisplayed;

        /// <summary>A mediation, by id, loaded an ad of the format.</summary>
        public static event Action<AdFormat, string> Loaded;

        /// <summary>A mediation, by id, failed to load an ad of the format.</summary>
        public static event Action<AdFormat, string, AdError> LoadFailed;

        /// <summary>The player left the game; one of the pack's own full-screen ads taking the screen, or any pause while <see cref="AdsDebugger"/> shows, is not that.</summary>
        public static event Action PlayerLeft;

        /// <summary>The player came back after <see cref="PlayerLeft"/>, with the seconds away; comes once <see cref="IsInitialized"/> only.</summary>
        public static event Action<double> PlayerReturned;

        #endregion

        #region Full screen

        /// <summary>A started mediation in the format's list has an ad loaded, and the format is not suppressed.</summary>
        public static bool IsReady(AdFullScreenFormat format) => !IsSuppressed(format.ToFormat()) && Host.IsReady(format.ToFormat());

        /// <summary>One full-screen ad: a chain of one step. <paramref name="placement"/> reaches <see cref="Displayed"/> and the mediation, which reports it with the impression where its SDK takes one.</summary>
        public static UniTask<AdChainResult> Show(AdFullScreenFormat format, string placement = null, Action onDisplayed = null) => Chain().Then(format, placement, onDisplayed).Run();

        /// <summary>True when an ad displayed.</summary>
        public static UniTask<bool> ShowInterstitialAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.Interstitial, placement);

        /// <summary>True when the player earned the reward; also while the format is suppressed, which rewards with no ad.</summary>
        public static UniTask<bool> ShowRewardedAsync(string placement = null) => RewardedAsync(AdFullScreenFormat.Rewarded, placement);

        /// <summary>As <see cref="ShowRewardedAsync"/>.</summary>
        public static UniTask<bool> ShowRewardedInterstitialAsync(string placement = null) => RewardedAsync(AdFullScreenFormat.RewardedInterstitial, placement);

        /// <summary>As <see cref="ShowInterstitialAsync"/>.</summary>
        public static UniTask<bool> ShowAppOpenAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.AppOpen, placement);

        /// <summary>As <see cref="ShowInterstitialAsync"/>.</summary>
        public static UniTask<bool> ShowNativeInterstitialAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.NativeInterstitial, placement);

        /// <summary>As <see cref="ShowInterstitialAsync"/>.</summary>
        public static UniTask<bool> ShowNativeAppOpenAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.NativeAppOpen, placement);

        /// <summary>As <see cref="ShowInterstitialAsync"/>.</summary>
        public static UniTask<bool> ShowNativeEndCardAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.NativeEndCard, placement);

        private static async UniTask<bool> DisplayedAsync(AdFullScreenFormat format, string placement) => (await Show(format, placement)).AnyDisplayed;
        private static async UniTask<bool> RewardedAsync(AdFullScreenFormat format, string placement) => (await Show(format, placement)).Rewarded;

        /// <summary>Full-screen ads one after the other, such as an interstitial then the end card; a suppressed format's step is left out, and a suppressed rewarding one counts as rewarded.</summary>
        public static AdChain Chain() => new(Host, format => IsSuppressed(format.ToFormat()) ? AdChainSkip.Suppressed : AdChainSkip.None);

        /// <summary>As <see cref="IsReady"/>, for the native collapsible.</summary>
        public static bool IsCollapsibleReady => !IsSuppressed(AdFormat.NativeCollapsible) && Host.IsReady(AdFormat.NativeCollapsible);

        /// <summary>Collapsibles one after the other; one that shows nothing while the collapsible is suppressed.</summary>
        public static AdCollapsibleChain CollapsibleChain() => new(Host, IsSuppressed(AdFormat.NativeCollapsible));

        /// <summary>
        /// The plan's steps one after the other, as <see cref="CollapsibleChain"/>; nothing for no plan, such as <see cref="AdCollapsiblePlan.Pick"/> found none.<br/>
        /// <paramref name="onDisplayed"/> comes once, when the first ad displays. Resolves to how many ads displayed.
        /// </summary>
        public static UniTask<int> ShowCollapsibleAsync(AdCollapsiblePlan plan, Action onDisplayed = null) {
            if (plan?.Steps is not { Length: > 0 } steps) return UniTask.FromResult(0);

            var chain = CollapsibleChain();
            for (var i = 0; i < steps.Length; i++) chain.Then(steps[i], i > 0 ? null : onDisplayed);
            return chain.Run();
        }

        /// <summary>Slides out whatever collapsible is up and ends its chain.</summary>
        public static void HideCollapsible() => Host.HideCollapsible();

        #endregion

        #region On screen

        /// <summary>Shows the banner and keeps wanting it: on the best started mediation, or once one starts; while suppressed, once that is lifted.</summary>
        public static void ShowBanner() => Host.ShowBanner();

        public static void HideBanner() => Host.HideBanner();

        /// <summary>As <see cref="ShowBanner"/>.</summary>
        public static void ShowMRec() => Host.ShowMRec();

        public static void HideMRec() => Host.HideMRec();

        /// <summary>
        /// A native MRec block on the best started mediation.<br/>
        /// Made before <see cref="IsInitialized"/>, or before a mediation serving the format started, the handle gets its block once one has, on the best; what the game set on it meanwhile, its position and whether it is on, carries over.<br/>
        /// While the format is suppressed the block stays off, and comes back once that is lifted.
        /// </summary>
        public static NativeRectHandle CreateNativeMRec(Vector2Int sizePx, int roundCornerPx = 0) => Host.CreateNativeRect(AdFormat.NativeMRec, sizePx, roundCornerPx);

        /// <summary>As <see cref="CreateNativeMRec"/>, an in-feed cell.</summary>
        public static NativeRectHandle CreateNativeInFeed(Vector2Int sizePx, int roundCornerPx = 0) => Host.CreateNativeRect(AdFormat.NativeInFeed, sizePx, roundCornerPx);

        #endregion

        #region Suppression

        /// <summary>
        /// Holds a format back, such as every format a remove-ads purchase takes, or lets it through again.<br/>
        /// Suppressed, a full-screen format's step is left out of every chain, a rewarding one counting as rewarded; the banner, the MRec, the collapsible and the native blocks go off screen, and what the game wants of them comes back once lifted.<br/>
        /// Not saved: the game sets it from its save at every launch, before the first scene's ads; callable before the pack starts.
        /// </summary>
        public static void SetSuppressed(AdFormat format, bool on) {
            if (on ? suppressed.Add(format) : suppressed.Remove(format)) host?.ApplySuppressed(format);
        }

        public static bool IsSuppressed(AdFormat format) => suppressed.Contains(format);

        #endregion

        #region The manager

        /// <summary>The game's manager, null when it has none; the pack makes it just before the first scene.</summary>
        internal static IAdsManager Manager { get; private set; }

        /// <summary>The running manager as its own class, for the manager's <c>Ins</c>.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static T GetManager<T>() where T : class, IAdsManager {
            if (Manager is T manager) return manager;

            throw new InvalidOperationException(Manager != null
                ? $"{nameof(AdsSdk)}: the running manager is {Manager.GetType().Name}, not {typeof(T).Name}"
                : launched
                    ? $"{nameof(AdsSdk)}: no manager runs; the console says why"
                    : $"{nameof(AdsSdk)}: {typeof(T).Name}.Ins is read before the pack starts, which is just before the first scene loads");
        }

        /// <summary>
        /// For the code the pack compiles into the game's manager class, which calls it before the first scene; the game never does.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void Register(Type type, Func<IAdsManager> create) {
            if (managerType != null && managerType != type) {
                // Two assemblies that do not reference each other; the Editor reports it too.
                Debug.LogError($"{nameof(AdsSdk)}: both {managerType.FullName} and {type.FullName} implement {nameof(IAdsManager)}, so neither runs; keep one");
                managerConflict = true;
                return;
            }

            managerType   = type;
            createManager = create;
        }

        #endregion

        #region For the host

        private static AdsHost Host => host ?? throw new InvalidOperationException($"{nameof(AdsSdk)} is used before it starts, which is just before the first scene loads");

        internal static void RaisePaid(in AdImpression impression) {
            var copy = impression;
            Raise(Paid, handler => handler(copy));
        }

        // Only a full-screen step displays through a chain.
        internal static void RaiseDisplayed(AdFormat format, string placement) => Raise(Displayed, handler => handler((AdFullScreenFormat) format, placement));

        internal static void RaiseCollapsibleDisplayed() => Raise(CollapsibleDisplayed, handler => handler());

        internal static void RaiseLoaded(AdFormat format, string mediationId) => Raise(Loaded, handler => handler(format, mediationId));

        internal static void RaiseLoadFailed(AdFormat format, string mediationId, AdError error) => Raise(LoadFailed, handler => handler(format, mediationId, error));

        internal static void RaisePlayerLeft() => Raise(PlayerLeft, handler => handler());

        internal static void RaisePlayerReturned(double awaySeconds) => Raise(PlayerReturned, handler => handler(awaySeconds));

        // The Editor's pause under a full-screen ad: the manager's, else the default.
        internal static void PauseInEditor(bool paused) {
            try {
                if (Manager != null) Manager.PauseInEditor(paused);
                else EditorGamePause.Apply(paused);
            } catch (Exception exception) {
                Debug.LogException(exception);
            }
        }

        // Each handler alone, so that one failing in the game neither stops the others nor the pack's own bookkeeping.
        private static void Raise<T>(T handlers, Action<T> call) where T : Delegate {
            if (handlers == null) return;

            foreach (var handler in handlers.GetInvocationList()) {
                try {
                    call((T) handler);
                } catch (Exception exception) {
                    Debug.LogException(exception);
                }
            }
        }

        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Launch() {
            // The Editor runs a copy, so that the manager's state neither dirties the asset nor outlives Play mode.
            var asset    = AdsSettings.Load();
            var settings = Application.isEditor ? UnityEngine.Object.Instantiate(asset) : asset;
            launched = true;

            AdsEditorLayers.lowest = settings.EditorSortingOrder;
            host = new AdsHost(settings, initialized);
            Application.quitting += host.Dispose;

            var lifecycle = new GameObject(nameof(AdsSdk)) { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(lifecycle);
            lifecycle.AddComponent<AdsLifecycle>().Host = host;

            Launches = PlayerPrefs.GetInt(LaunchesKey) + 1;
            PlayerPrefs.SetInt(LaunchesKey, Launches);
            PlayerPrefs.Save();

            Manager = CreateManager(settings);
            host.InitializeAsync().Forget();
        }

        // The settings keep the fields the page edited; a class without them, never opened there, starts as written.
        private static IAdsManager CreateManager(AdsSettings settings) {
            if (managerConflict || managerType == null) return null;

            var saved = settings.Manager;
            if (saved != null && saved.GetType() == managerType) return saved;

            if (saved != null) Debug.LogWarning($"{nameof(AdsSdk)}: the settings hold the fields of {saved.GetType().Name}, not {managerType.Name}, so {managerType.Name} runs with its own defaults; open Project Settings > RiseOn > Ads to take it in");
            return createManager();
        }

        // Static state outlives a play session when the Editor skips the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            if (host != null) Application.quitting -= host.Dispose;

            host                 = null;
            initialized          = new UniTaskCompletionSource();
            launched             = false;
            managerType          = null;
            createManager        = null;
            managerConflict      = false;
            Manager              = null;
            Launches             = 0;
            suppressed.Clear();
            Paid                 = null;
            Displayed            = null;
            CollapsibleDisplayed = null;
            Loaded               = null;
            LoadFailed           = null;
            PlayerLeft           = null;
            PlayerReturned       = null;
        }
    }

    // Unity sends application pauses to components only.
    internal sealed class AdsLifecycle : MonoBehaviour {
        public AdsHost Host;

        private void OnApplicationPause(bool paused) {
            Host?.OnApplicationPause(paused);
        }
    }
}