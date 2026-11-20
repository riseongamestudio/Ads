using System;
using System.Collections.Generic;
using System.ComponentModel;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// The Ads pack's core, as a mediation SDK's own API: started before the first scene from Project Settings > RiseOn > Ads, it starts the mediations once the consent flow allows ads, and shows what it is asked to with no rule of its own.<br/>
    /// A game works through its <see cref="AdsManagerBase"/> class, which wraps all of this with the game's rules; this is for a game that writes its own manager instead.<br/>
    /// Unity's thread only; every event comes on it.
    /// </summary>
    public static class AdsSdk {
        private static AdsHost host;
        private static UniTaskCompletionSource initialized = new();
        private static bool launched;
        private static Type managerType;
        private static Func<AdsManagerBase> createManager;
        private static bool managerConflict;

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

        /// <summary>A started mediation in the format's list has an ad loaded.</summary>
        public static bool IsReady(AdFullScreenFormat format) => Host.IsReady(format.ToFormat());

        /// <summary>One full-screen ad: a chain of one step.</summary>
        public static UniTask<AdChainResult> Show(AdFullScreenFormat format, string placement = null, Action onDisplayed = null) => Chain().Then(format, placement, onDisplayed).Run();

        /// <summary>Full-screen ads one after the other, such as an interstitial then the end card.</summary>
        public static AdChain Chain() => new(Host, null);

        /// <summary>As <see cref="IsReady"/>, for the native collapsible.</summary>
        public static bool IsCollapsibleReady => Host.IsReady(AdFormat.NativeCollapsible);

        public static AdCollapsibleChain CollapsibleChain() => new(Host, false);

        /// <summary>Slides out whatever collapsible is up and ends its chain.</summary>
        public static void HideCollapsible() => Host.HideCollapsible();

        #endregion

        #region On screen

        /// <summary>Shows the banner and keeps wanting it: on the best started mediation, or once one starts.</summary>
        public static void ShowBanner() => Host.ShowBanner();

        public static void HideBanner() => Host.HideBanner();

        /// <summary>As <see cref="ShowBanner"/>.</summary>
        public static void ShowMRec() => Host.ShowMRec();

        public static void HideMRec() => Host.HideMRec();

        /// <summary>
        /// A native MRec block on the best started mediation; an empty handle when the format is off.<br/>
        /// Throws before <see cref="IsInitialized"/>: create blocks after <see cref="WaitUntilInitializedAsync"/>.
        /// </summary>
        public static NativeRectHandle CreateNativeMRec(Vector2Int sizePx, int roundCornerPx = 0) => CreateNativeRect(AdFormat.NativeMRec, sizePx, roundCornerPx, null, null);

        /// <summary>As <see cref="CreateNativeMRec"/>, an in-feed cell.</summary>
        public static NativeRectHandle CreateNativeInFeed(Vector2Int sizePx, int roundCornerPx = 0) => CreateNativeRect(AdFormat.NativeInFeed, sizePx, roundCornerPx, null, null);

        // canShow is asked at each show, for the manager's remove-ads; a show it turns down shows nothing.

        internal static NativeRectHandle CreateNativeRect(AdFormat format, Vector2Int sizePx, int roundCornerPx, Func<bool> canShow, Action<NativeRectHandle> disposed) {
            if (!IsInitialized) throw new InvalidOperationException($"{nameof(AdsSdk)}: a {format} is made before the mediations start; await {nameof(WaitUntilInitializedAsync)} first");

            return host.CreateNativeRect(format, sizePx, roundCornerPx, canShow, disposed);
        }

        #endregion

        #region The manager

        /// <summary>The game's manager, null when it has none; the pack makes it just before the first scene.</summary>
        internal static AdsManagerBase Manager { get; private set; }

        /// <summary>The running manager as its own class, for the manager's <c>Ins</c>.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static T GetManager<T>() where T : AdsManagerBase {
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
        public static void Register(Type type, Func<AdsManagerBase> create) {
            if (managerType != null && managerType != type) {
                // Two assemblies that do not reference each other; the Editor reports it too.
                Debug.LogError($"{nameof(AdsSdk)}: both {managerType.FullName} and {type.FullName} derive from {nameof(AdsManagerBase)}, so neither runs; keep one");
                managerConflict = true;
                return;
            }

            managerType   = type;
            createManager = create;
        }

        #endregion

        #region For the host

        private static AdsHost Host => host ?? throw new InvalidOperationException($"{nameof(AdsSdk)} is used before it starts, which is just before the first scene loads");

        // A chain the manager builds, without the formats its rules hold back.
        internal static AdChain Chain(Func<AdFullScreenFormat, AdChainSkip> skip) => new(Host, skip);

        internal static AdCollapsibleChain CollapsibleChain(bool off) => new(Host, off);

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
            if (Manager != null) Manager.RunPauseInEditor(paused);
            else EditorGamePause.Apply(paused);
        }

        // Each handler alone, so that one failing in the game neither stops the others nor the pack's own bookkeeping.
        internal static void Raise<T>(T handlers, Action<T> call) where T : Delegate {
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

        // The page's Formats section, for the manager's rules set there.
        internal static AdFormatSettings Formats { get; private set; }

        // The page's On Return To Game section, for the manager.
        internal static AdReturnSettings ReturnToGame { get; private set; }

        // This launch of the game on the device, 1 for the first, for the manager.
        internal static int Launches { get; private set; }

        private const string LaunchesKey = "RiseOn.Ads.Launches";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Launch() {
            // The Editor runs a copy, so that the manager's state neither dirties the asset nor outlives Play mode.
            var asset    = AdsSettings.Load();
            var settings = Application.isEditor ? UnityEngine.Object.Instantiate(asset) : asset;
            launched = true;

            AdsEditorLayers.Lowest = settings.EditorSortingOrder;
            host = new AdsHost(settings, initialized);
            Application.quitting += host.Dispose;

            var lifecycle = new GameObject(nameof(AdsSdk)) { hideFlags = HideFlags.HideInHierarchy };
            UnityEngine.Object.DontDestroyOnLoad(lifecycle);
            lifecycle.AddComponent<AdsLifecycle>().Host = host;

            Launches = PlayerPrefs.GetInt(LaunchesKey) + 1;
            PlayerPrefs.SetInt(LaunchesKey, Launches);
            PlayerPrefs.Save();

            Formats      = settings.Formats;
            ReturnToGame = settings.ReturnToGame;
            Manager = CreateManager(settings);
            Manager?.Attach();

            host.InitializeAsync().Forget();
        }

        // The settings keep the fields the page edited; a class without them, never opened there, starts as written.
        private static AdsManagerBase CreateManager(AdsSettings settings) {
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

            host            = null;
            initialized     = new UniTaskCompletionSource();
            launched        = false;
            managerType     = null;
            createManager   = null;
            managerConflict = false;
            Manager         = null;
            Paid            = null;
            Displayed       = null;
            CollapsibleDisplayed = null;
            Loaded          = null;
            LoadFailed      = null;
            PlayerLeft      = null;
            PlayerReturned  = null;
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