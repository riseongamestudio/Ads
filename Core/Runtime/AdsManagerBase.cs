using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiseOn.Ads {
    /// <summary>
    /// The game's ads manager: the one partial class deriving from this, which the rest of the game calls for every ad.<br/>
    /// It wraps all of <see cref="AdsSdk"/>, applying the rules most games share, set in Project Settings > RiseOn > Ads: in the Formats section remove-ads,
    /// the full-screen formats' pacing and end card and the collapsible's plans, in the On Return To Game section the ad on return.<br/>
    /// Each rule is a virtual member the game's class can replace with its own, reading the page's values from <see cref="Formats"/> and <see cref="ReturnToGame"/>.<br/>
    /// The pack makes it just before the first scene; a second class deriving from this does not compile. The pack's source generator adds <c>Ins</c>, the running instance.<br/>
    /// Unity's thread only; every event comes on it.
    /// </summary>
    [Serializable]
    public abstract class AdsManagerBase {
        // Each paced format's last display, for its pacing; the collapsible's is its last show's first display.
        private readonly Dictionary<AdFormat, double> lastDisplays = new();

        // What the game wants on screen, so that lifting remove-ads brings it back.
        private bool    bannerWanted;
        private bool    mRecWanted;
        private readonly List<NativeRectHandle> rects = new();

        // Auto Show as the manager read it on start: the banner is the manager's, and the game's calls only warn.
        private bool bannerAuto;

        /// <summary>
        /// Holds the ad on return back while the game sends the player out on purpose, such as a purchase sheet, a share sheet or a store page, whose return is not a new visit.
        /// </summary>
        public bool ReturnAdBlocked { get; set; }

        #region State and events

        /// <summary>As <see cref="AdsSdk.IsInitialized"/>.</summary>
        public bool IsInitialized => AdsSdk.IsInitialized;

        /// <summary>As <see cref="AdsSdk.WaitUntilInitializedAsync"/>.</summary>
        public UniTask WaitUntilInitializedAsync() => AdsSdk.WaitUntilInitializedAsync();

        /// <summary>As <see cref="AdsSdk.IsShowingFullScreen"/>.</summary>
        public bool IsShowingFullScreen => AdsSdk.IsShowingFullScreen;

        /// <summary>As <see cref="AdsSdk.Paid"/>, after <see cref="OnPaid"/>.</summary>
        public event Action<AdImpression> Paid;

        /// <summary>As <see cref="AdsSdk.Displayed"/>, after <see cref="OnDisplayed"/>.</summary>
        public event Action<AdFullScreenFormat, string> Displayed;

        /// <summary>As <see cref="AdsSdk.CollapsibleDisplayed"/>, after <see cref="OnCollapsibleDisplayed"/>.</summary>
        public event Action CollapsibleDisplayed;

        /// <summary>As <see cref="AdsSdk.Loaded"/>, after <see cref="OnLoaded"/>.</summary>
        public event Action<AdFormat, string> Loaded;

        /// <summary>As <see cref="AdsSdk.LoadFailed"/>, after <see cref="OnLoadFailed"/>.</summary>
        public event Action<AdFormat, string, AdError> LoadFailed;

        /// <summary>As <see cref="AdsSdk.PlayerLeft"/>, after <see cref="OnPlayerLeft"/>.</summary>
        public event Action PlayerLeft;

        /// <summary>As <see cref="AdsSdk.PlayerReturned"/>, after <see cref="OnPlayerReturned"/>.</summary>
        public event Action<double> PlayerReturned;

        #endregion

        #region The game's answers

        /// <summary>
        /// The player bought remove-ads, read from the game's save; <see cref="IsRemoved"/> applies it to the formats whose Included In Remove Ads the page ticks.<br/>
        /// Call <see cref="RefreshAdsRemoved"/> when it changes.
        /// </summary>
        protected virtual bool AdsRemoved => false;

        /// <summary>Whether remove-ads holds this format back now; override for more than one purchase, such as remove-ads and a VIP pass.</summary>
        protected virtual bool IsRemoved(AdFormat format) => AdsRemoved && Formats.RemoveAdsTakes(format);

        /// <summary>
        /// Past the format's pacing on the page: its first show not before First After from launch, then each at least Min Interval after its last display, and at least After Rewarded
        /// after a rewarded ad's; always for the rewarded, which has none.<br/>
        /// Each format has its own: an end card chained after another ad keeps to its own pacing, and its display does not count for that ad's.
        /// </summary>
        protected virtual bool PacingAllows(AdFullScreenFormat format) => Paced(Formats.PacingOf(format), format.ToFormat());

        /// <summary>As <see cref="PacingAllows"/>, for <see cref="ShowCollapsibleAsync"/>: Min Interval from the first ad of its last show.</summary>
        protected virtual bool CollapsiblePacingAllows() => Paced(Formats.NativeCollapsible.Pacing, AdFormat.NativeCollapsible);

        private bool Paced(AdPacingSettings pacing, AdFormat format) {
            var now = Time.realtimeSinceStartupAsDouble;
            if (lastDisplays.TryGetValue(AdFormat.Rewarded, out var rewarded) && now - rewarded < pacing.AfterRewardedSeconds) return false;

            return lastDisplays.TryGetValue(format, out var last) ? now - last >= pacing.MinIntervalSeconds : now >= pacing.FirstSeconds;
        }

        /// <summary>Whether a native end card follows the format's ad in <see cref="ShowAsync"/>, never asked for the end card itself; the format's Use Native End Card on the page by default.</summary>
        protected virtual bool EndCardFollows(AdFullScreenFormat format) => Formats.EndCardFollows(format);

        /// <summary>The format shown when the player comes back, none for no ad; the page's On Return To Game section by default.</summary>
        protected virtual AdFullScreenFormat? ReturnFormat {
            get {
                if (Launches < ReturnToGame.FromLaunch) return null;

                AdFullScreenFormat? picked = null;
                foreach (var format in ReturnToGame.Formats) {
                    picked = format;
                    if (IsReady(format)) break;
                }

                return picked;
            }
        }

        /// <summary>Whether <see cref="ReturnFormat"/> also shows at launch, over the game's first scene; the page's On Launch by default.</summary>
        protected virtual bool ShowsOnLaunch => ReturnToGame.OnLaunch;

        /// <summary>
        /// Whether the manager shows the banner as it starts, just before <see cref="OnAttached"/>, and keeps it, so that <see cref="ShowBanner"/> only warns and <see cref="HideBanner"/>
        /// logs an error; the page's Banner Auto Show by default. Read once.
        /// </summary>
        protected virtual bool AutoShowsBanner => Formats.Banner.AutoShow;

        /// <summary>Seconds the game has to stay in the background before its return shows the ad.</summary>
        protected virtual double ReturnAfterAwaySeconds => ReturnToGame.AfterAwaySeconds;

        /// <summary>
        /// The page's Formats section, with Remote Config's values where the page picks a key, for the game's own rules:
        /// each format's Included In Remove Ads, Use Native End Card, pacing and settings.
        /// </summary>
        protected static AdFormatSettings Formats => AdsSdk.Formats;

        /// <summary>The page's On Return To Game section, with Remote Config's values where the page picks a key, for the game's own rules.</summary>
        protected static AdReturnSettings ReturnToGame => AdsSdk.ReturnToGame;

        /// <summary>This launch of the game on the device, 1 for the first, counted by the pack.</summary>
        protected static int Launches => AdsSdk.Launches;

        /// <summary>Seconds since the format last displayed, from any chain; none before its first, for the game's own pacing.</summary>
        protected double? SecondsSinceDisplay(AdFullScreenFormat format) => Since(format.ToFormat());

        /// <summary>Seconds since the first ad of the last <see cref="ShowCollapsibleAsync"/> displayed; none before the first.</summary>
        protected double? SecondsSinceCollapsible => Since(AdFormat.NativeCollapsible);

        private double? Since(AdFormat format) => lastDisplays.TryGetValue(format, out var last) ? Time.realtimeSinceStartupAsDouble - last : null;

        #endregion

        #region Full screen

        /// <summary>Not removed, past its pacing, and loaded: what the typed <c>CanShow</c> members ask, for the game's own rules.</summary>
        protected bool IsReady(AdFullScreenFormat format) => !IsRemoved(format.ToFormat()) && PacingAllows(format) && AdsSdk.IsReady(format);

        /// <summary>
        /// One full-screen ad under the game's rules, the path every typed <c>Show</c> member takes, for the game's own rules: nothing for a removed format,
        /// or a removed rewarding one rewarded with no ad; nothing within its pacing; followed by a native end card when <see cref="EndCardFollows"/>.
        /// </summary>
        protected UniTask<AdChainResult> ShowAsync(AdFullScreenFormat format, string placement = null, Action onDisplayed = null) {
            Guard(() => OnShowRequested(format, placement));

            // Only after an ad that shows: a removed or paced-out first step leaves the end card first, which would then show alone.
            var shows   = !IsRemoved(format.ToFormat()) && PacingAllows(format);
            var endCard = shows && format is not AdFullScreenFormat.NativeEndCard && EndCardFollows(format);
            return Chain().Then(format, placement, onDisplayed).ThenIf(endCard, AdFullScreenFormat.NativeEndCard, placement).Run();
        }

        /// <summary>
        /// Full-screen ads one after the other; the steps of removed formats, and of formats within their pacing, are left out.<br/>
        /// A removed rewarding step counts as rewarded.
        /// </summary>
        public AdChain Chain() => AdsSdk.Chain(format =>
            IsRemoved(format.ToFormat()) ? AdChainSkip.Removed
          : !PacingAllows(format) ? AdChainSkip.Pacing
          : AdChainSkip.None);

        public bool CanShowInterstitial => IsReady(AdFullScreenFormat.Interstitial);

        /// <summary>True when an ad displayed.</summary>
        public UniTask<bool> ShowInterstitialAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.Interstitial, placement);

        /// <summary>Also true while removed, which rewards with no ad.</summary>
        public bool CanShowRewarded => IsReady(AdFullScreenFormat.Rewarded) || IsRemoved(AdFormat.Rewarded);

        /// <summary>True when the player earned the reward.</summary>
        public UniTask<bool> ShowRewardedAsync(string placement = null) => RewardedAsync(AdFullScreenFormat.Rewarded, placement);

        /// <summary>As <see cref="CanShowRewarded"/>.</summary>
        public bool CanShowRewardedInterstitial => IsReady(AdFullScreenFormat.RewardedInterstitial) || IsRemoved(AdFormat.RewardedInterstitial);

        /// <summary>As <see cref="ShowRewardedAsync"/>.</summary>
        public UniTask<bool> ShowRewardedInterstitialAsync(string placement = null) => RewardedAsync(AdFullScreenFormat.RewardedInterstitial, placement);

        public bool CanShowAppOpen => IsReady(AdFullScreenFormat.AppOpen);

        /// <summary>As <see cref="ShowInterstitialAsync"/>.</summary>
        public UniTask<bool> ShowAppOpenAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.AppOpen, placement);

        public bool CanShowNativeInterstitial => IsReady(AdFullScreenFormat.NativeInterstitial);

        /// <summary>As <see cref="ShowInterstitialAsync"/>.</summary>
        public UniTask<bool> ShowNativeInterstitialAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.NativeInterstitial, placement);

        public bool CanShowNativeAppOpen => IsReady(AdFullScreenFormat.NativeAppOpen);

        /// <summary>As <see cref="ShowInterstitialAsync"/>.</summary>
        public UniTask<bool> ShowNativeAppOpenAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.NativeAppOpen, placement);

        public bool CanShowNativeEndCard => IsReady(AdFullScreenFormat.NativeEndCard);

        /// <summary>As <see cref="ShowInterstitialAsync"/>.</summary>
        public UniTask<bool> ShowNativeEndCardAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.NativeEndCard, placement);

        private async UniTask<bool> DisplayedAsync(AdFullScreenFormat format, string placement) => (await ShowAsync(format, placement)).AnyDisplayed;
        private async UniTask<bool> RewardedAsync(AdFullScreenFormat format, string placement) => (await ShowAsync(format, placement)).Rewarded;

        /// <summary>A collapsible chain; one that shows nothing while remove-ads holds the collapsible back. The page's pacing and plans are <see cref="ShowCollapsibleAsync"/>'s.</summary>
        public AdCollapsibleChain CollapsibleChain() => AdsSdk.CollapsibleChain(IsRemoved(AdFormat.NativeCollapsible));

        /// <summary>Not removed, past its pacing, and loaded.</summary>
        public bool CanShowCollapsible => !IsRemoved(AdFormat.NativeCollapsible) && CollapsiblePacingAllows() && AdsSdk.IsCollapsibleReady;

        /// <summary>
        /// The collapsible under the game's rules: nothing while removed or within its pacing; else a plan drawn by weight, its steps one after the other.<br/>
        /// <paramref name="onDisplayed"/> comes once, when the first ad displays. Resolves to how many ads displayed.
        /// </summary>
        public UniTask<int> ShowCollapsibleAsync(Action onDisplayed = null) {
            if (IsRemoved(AdFormat.NativeCollapsible) || !CollapsiblePacingAllows()) return UniTask.FromResult(0);

            var plan = DrawCollapsiblePlan();
            if (plan == null) return UniTask.FromResult(0);

            var chain = CollapsibleChain();
            for (var i = 0; i < plan.Steps.Length; i++) {
                chain.Then(plan.Steps[i], i > 0 ? null : () => {
                    lastDisplays[AdFormat.NativeCollapsible] = Time.realtimeSinceStartupAsDouble;
                    onDisplayed?.Invoke();
                });
            }

            return chain.Run();
        }

        /// <summary>The plan <see cref="ShowCollapsibleAsync"/> runs: one with steps, each one's chance its weight over all the weights; none when no plan has a weight above 0.</summary>
        protected virtual AdCollapsiblePlan DrawCollapsiblePlan() {
            var plans = Formats.NativeCollapsible.Plans;
            var total = 0f;
            foreach (var plan in plans) {
                if (IsDrawable(plan)) total += plan.Weight;
            }

            if (total <= 0) {
                Debug.LogWarning($"{nameof(AdsManagerBase)}: no collapsible plan has steps and a weight above 0, so ShowCollapsibleAsync shows nothing; raise one in the page's Formats section or in Remote Config");
                return null;
            }

            var draw = UnityEngine.Random.value * total;
            AdCollapsiblePlan last = null;
            foreach (var plan in plans) {
                if (!IsDrawable(plan)) continue;

                last =  plan;
                draw -= plan.Weight;
                if (draw < 0) break;
            }

            return last;

            static bool IsDrawable(AdCollapsiblePlan plan) => plan is { Steps: { Length: > 0 } } && plan.Weight > 0;
        }

        public void HideCollapsible() => AdsSdk.HideCollapsible();

        #endregion

        #region On screen

        /// <summary>
        /// As <see cref="AdsSdk.ShowBanner"/>, held back while removed and back when remove-ads is lifted; up until <see cref="HideBanner"/>.<br/>
        /// For a game whose page turns Banner Auto Show off: with it on, the manager shows the banner itself, and a call only warns.
        /// </summary>
        public void ShowBanner() {
            if (bannerAuto) {
                Debug.LogWarning($"{GetType().Name}: Banner Auto Show is on, so the manager already shows the banner; turn it off to call ShowBanner");
                return;
            }

            ShowBannerNow();
        }

        /// <summary>For a game whose page turns Banner Auto Show off: with it on, the banner stays and the call logs an error.</summary>
        public void HideBanner() {
            if (bannerAuto) {
                Debug.LogError($"{GetType().Name}: Banner Auto Show is on, so the banner stays; turn it off to hide the banner with HideBanner");
                return;
            }

            bannerWanted = false;
            AdsSdk.HideBanner();
        }

        private void ShowBannerNow() {
            bannerWanted = true;
            if (!IsRemoved(AdFormat.Banner)) AdsSdk.ShowBanner();
        }

        /// <summary>As <see cref="ShowBanner"/>.</summary>
        public void ShowMRec() {
            mRecWanted = true;
            if (!IsRemoved(AdFormat.MRec)) AdsSdk.ShowMRec();
        }

        public void HideMRec() {
            mRecWanted = false;
            AdsSdk.HideMRec();
        }

        /// <summary>As <see cref="AdsSdk.CreateNativeMRec"/>; the block is held back while removed and back when remove-ads is lifted.</summary>
        public NativeRectHandle CreateNativeMRec(Vector2Int sizePx, int roundCornerPx = 0) => CreateNativeRect(AdFormat.NativeMRec, sizePx, roundCornerPx);

        /// <summary>As <see cref="AdsSdk.CreateNativeInFeed"/>; the cell is held back while removed and back when remove-ads is lifted.</summary>
        public NativeRectHandle CreateNativeInFeed(Vector2Int sizePx, int roundCornerPx = 0) => CreateNativeRect(AdFormat.NativeInFeed, sizePx, roundCornerPx);

        private NativeRectHandle CreateNativeRect(AdFormat format, Vector2Int sizePx, int roundCornerPx) {
            var handle = AdsSdk.CreateNativeRect(format, sizePx, roundCornerPx, () => !IsRemoved(format), disposed => rects.Remove(disposed));
            rects.Add(handle);
            return handle;
        }

        /// <summary>After <see cref="AdsRemoved"/> changes, such as a remove-ads purchase or its refund: hides what it now holds back, or brings back what the game wants.</summary>
        public void RefreshAdsRemoved() {
            Apply(bannerWanted, AdFormat.Banner, AdsSdk.ShowBanner, AdsSdk.HideBanner);
            Apply(mRecWanted, AdFormat.MRec, AdsSdk.ShowMRec, AdsSdk.HideMRec);
            if (IsRemoved(AdFormat.NativeCollapsible)) AdsSdk.HideCollapsible();
            foreach (var rect in rects) rect.Refresh();

            void Apply(bool wanted, AdFormat format, Action show, Action hide) {
                if (IsRemoved(format)) hide();
                else if (wanted) show();
            }
        }

        #endregion

        #region Hooks

        /// <summary>The pack made the manager, just before the first scene.</summary>
        protected virtual void OnAttached() { }

        /// <summary>Every impression's revenue, from every mediation and format.</summary>
        protected virtual void OnPaid(in AdImpression impression) { }

        /// <summary>A typed <c>Show</c> member, or the ad on return, asked for the format, before the rules decide whether it shows: an ad opportunity, for the game's analytics.</summary>
        protected virtual void OnShowRequested(AdFullScreenFormat format, string placement) { }

        /// <summary>A full-screen ad is on screen, from any chain; <paramref name="placement"/> as passed to it.</summary>
        protected virtual void OnDisplayed(AdFullScreenFormat format, string placement) { }

        /// <summary>A native collapsible ad slid in or swapped in, from any collapsible chain.</summary>
        protected virtual void OnCollapsibleDisplayed() { }

        protected virtual void OnLoaded(AdFormat format, string mediationId) { }
        protected virtual void OnLoadFailed(AdFormat format, string mediationId, in AdError error) { }

        /// <summary>As <see cref="AdsSdk.PlayerLeft"/>.</summary>
        protected virtual void OnPlayerLeft() { }

        /// <summary>
        /// The Editor's stand-in for the pause a device makes while a full-screen ad is up, where no SDK pauses Unity: true as the first ad of a chain shows, false once the last one closed.<br/>
        /// Only the game knows what pausing stops for it. By default the time scale goes to zero and the audio pauses, both back as they were after.
        /// </summary>
        protected virtual void PauseInEditor(bool paused) => EditorGamePause.Apply(paused);

        /// <summary>
        /// As <see cref="AdsSdk.PlayerReturned"/>.<br/>
        /// Shows the ad on return by default: a format picked, not blocked, away long enough, then under the format's own rules as <see cref="ShowAsync"/>.
        /// </summary>
        protected virtual void OnPlayerReturned(double awaySeconds) {
            if (ReturnFormat is not { } format || ReturnAdBlocked || awaySeconds < ReturnAfterAwaySeconds) return;

            ShowAsync(format, "return").Forget();
        }

        /// <summary>
        /// Shows the ad at launch by default, when <see cref="ShowsOnLaunch"/> and <see cref="ReturnFormat"/> give one: once the mediations started and the ad loaded,
        /// while the game's first scene is still the active one, under the format's own rules as <see cref="ShowAsync"/>; leaving that scene first ends the wait.
        /// </summary>
        protected virtual async UniTask ShowLaunchAdAsync() {
            if (!ShowsOnLaunch || ReturnFormat is null) return;

            // The pack runs before the first scene loads, which is active from the next frame.
            await UniTask.NextFrame();
            var first = SceneManager.GetActiveScene();
            await WaitUntilInitializedAsync();
            await UniTask.WaitUntil(() => ReturnFormat is { } ready && IsReady(ready) || SceneManager.GetActiveScene() != first);
            if (SceneManager.GetActiveScene() == first && ReturnFormat is { } format) await ShowAsync(format, "launch");
        }

        #endregion

        // The pack's events reach the hooks, then the game's own listeners, each alone.
        internal void Attach() {
            AdsSdk.Displayed += (format, _) => lastDisplays[format.ToFormat()] = Time.realtimeSinceStartupAsDouble;
            AdsSdk.Paid += impression => {
                Guard(() => OnPaid(impression));
                AdsSdk.Raise(Paid, handler => handler(impression));
            };
            AdsSdk.Displayed += (format, placement) => {
                Guard(() => OnDisplayed(format, placement));
                AdsSdk.Raise(Displayed, handler => handler(format, placement));
            };
            AdsSdk.CollapsibleDisplayed += () => {
                Guard(OnCollapsibleDisplayed);
                AdsSdk.Raise(CollapsibleDisplayed, handler => handler());
            };
            AdsSdk.Loaded += (format, mediationId) => {
                Guard(() => OnLoaded(format, mediationId));
                AdsSdk.Raise(Loaded, handler => handler(format, mediationId));
            };
            AdsSdk.LoadFailed += (format, mediationId, error) => {
                Guard(() => OnLoadFailed(format, mediationId, error));
                AdsSdk.Raise(LoadFailed, handler => handler(format, mediationId, error));
            };
            AdsSdk.PlayerLeft += () => {
                Guard(OnPlayerLeft);
                AdsSdk.Raise(PlayerLeft, handler => handler());
            };
            AdsSdk.PlayerReturned += away => {
                Guard(() => OnPlayerReturned(away));
                AdsSdk.Raise(PlayerReturned, handler => handler(away));
            };

            // Read once: the banner is the manager's from here, or the game's.
            bannerAuto = AutoShowsBanner;
            if (bannerAuto) ShowBannerNow();
            Guard(OnAttached);
            ShowLaunchAdAsync().Forget();
        }

        internal void RunPauseInEditor(bool paused) => Guard(() => PauseInEditor(paused));

        private static void Guard(Action hook) {
            try {
                hook();
            } catch (Exception exception) {
                Debug.LogException(exception);
            }
        }
    }
}