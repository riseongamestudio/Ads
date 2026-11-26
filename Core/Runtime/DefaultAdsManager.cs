using System;
using Cysharp.Threading.Tasks;
using RiseOn.Serializables;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiseOn.Ads {
    /// <summary>
    /// The ad rules most of the studio's games run, on top of AdsSdk: remove-ads and a tester's ads off, each moment's pacing, interstitials from a level, the banner from the start,
    /// the ad when the player comes back and at launch, the collapsible's plans drawn by weight, and a native end card after some formats.<br/>
    /// Its fields show on Project Settings > RiseOn > Ads, each number or tick with a Remote Config key there while that pack is installed.<br/>
    /// The pack runs it while the project has no ads manager of its own; Create Own, beside its script on that page, copies it into the game, values and all, to change.
    /// </summary>
    public partial class DefaultAdsManager : IAdsManager {
        #region Fields

        // The page's order: the rules over many formats first, the more formats the higher (Remove Ads, Pacing); then each format's own,
        // named for it and in the order of AdFormat (Banner, Interstitial, App Open, Native Collapsible, Native End Card).

        // Every format but the rewarding ones, which would otherwise grant their reward with no ad.
        [SerializeField, LabelText("Remove Ads")]
        [Tooltip("The formats a remove-ads purchase takes away, ticked; SetAdsRemoved suppresses them.")]
        private EnumMap<AdFormat, bool> removeAds = new() {
            [AdFormat.Banner]               = true
          , [AdFormat.MRec]                 = true
          , [AdFormat.Interstitial]         = true
          , [AdFormat.Rewarded]             = false
          , [AdFormat.RewardedInterstitial] = false
          , [AdFormat.AppOpen]              = true
          , [AdFormat.NativeInFeed]         = true
          , [AdFormat.NativeCollapsible]    = true
          , [AdFormat.NativeInterstitial]   = true
          , [AdFormat.NativeAppOpen]        = true
          , [AdFormat.NativeEndCard]        = true
          , [AdFormat.NativeMRec]           = true
        };

        // Interstitial: what most of the studio's games run in Remote Config (Get In The Bowl, Bounce Adventure, Fish Sort).
        // Native Collapsible: what most of them run, the first at once, then 15 seconds apart.
        // No game of the studio paces Rewarded Interstitial or App Open; 0 is no pacing.
        // Native End Card at 0, so it follows the ad it is chained after each time that one shows.
        [SerializeField, LabelText("Pacing"), NoRowShade]
        [Tooltip("Each moment's spacing, in seconds: from launch to its first show, from its last show, and from the last rewarded ad. "
               + "Two formats that fill the same moment, the interstitials at a break, the app open ads on a return, share one, counted from whichever showed last.")]
        private EnumMap<PacedAd, AdPacing> pacing = new() {
            [PacedAd.Interstitial]         = new(45, 25)
          , [PacedAd.RewardedInterstitial] = new()
          , [PacedAd.AppOpen]              = new()
          , [PacedAd.NativeCollapsible]    = new(0, 15)
          , [PacedAd.NativeEndCard]        = new()
        };

        // Banner.
        [SerializeField, SectionGroup("Banner", Foldable = true), LabelText("Auto Show")]
        [Tooltip("Shows the banner as the game starts and keeps it up, held back while remove-ads takes it.")]
        private bool bannerAutoShow = true;

        // Interstitial and Native Interstitial. Most of the studio's games show them from the first level; Get In The Bowl and Triple Pop from the fourth.
        [SerializeField, SectionGroup("Interstitial", Foldable = true), LabelText("From Level")]
        [Tooltip("The interstitials show from this level on, for a show that names its level: 1 from the first.")]
        private AdRemoteSetting<float> interstitialFromLevel = new(1);

        // App Open and Native App Open, the ad on return to the game.
        // As most of the studio's games run it: the native one when loaded, else the network's.
        [SerializeField, SectionGroup("App Open", Foldable = true), LabelText("Format")]
        [Tooltip("The ad shown when the player comes back to the game, under its own pacing; Native App Open, else App Open takes the native one when it is loaded and the app open ad otherwise. "
               + "None for no ad on return.")]
        private ReturnAd returnAd = ReturnAd.NativeAppOpenElseAppOpen;

        // Every one of the studio's games but OrganiCat shows it from the first launch.
        [SerializeField, SectionGroup("App Open"), LabelText("From Launch"), MinValue(1)]
        [Tooltip("The ad shows from this launch of the game on the device, on launch and on return alike: 1 from the first launch.")]
        private int returnFromLaunch = 1;

        // As most of the studio's games and Google's cold start guidance: over the loading screen, when the ad loads before the game is ready.
        [SerializeField, SectionGroup("App Open"), LabelText("On Launch")]
        [Tooltip("The ad also shows at launch, over the game's first scene, when it loads before the game leaves that scene. A game with one scene turns it off.")]
        private AdRemoteSetting<bool> returnOnLaunch = new(true);

        [SerializeField, SectionGroup("App Open"), LabelText("After Away (s)")]
        [Tooltip("Seconds the game has to stay in the background before its return shows the ad; 0 for every return.")]
        private AdRemoteSetting<float> returnAfterAwaySeconds = new(0);

        // Native Collapsible: Get In The Bowl's three plans.
        [SerializeField, SectionList(Item = "plan", Foldable = true), LabelText("Collapsible Plans")]
        [Tooltip("The ways ShowCollapsibleAsync may run: each show draws one, each plan's chance its weight over all the weights.")]
        private AdCollapsiblePlan[] collapsiblePlans = {
            new("Plain", .6f, new AdCloseSettings { Cooldown = 3, CloseSide = AdCloseSide.Random, TimerSide = AdTimerSide.OppositeOfClose })
          , new("Chained", .3f
              , new AdCloseSettings { Cooldown = 3, CloseSide = AdCloseSide.Right, TimerSide = AdTimerSide.Right }
              , new AdCloseSettings { CloseSide = AdCloseSide.Left, TimerSide = AdTimerSide.Left })
          , new("Redirect", .1f, new AdCloseSettings { Cooldown = 3, CloseSide = AdCloseSide.Right, TimerSide = AdTimerSide.Right, RedirectOnClose = true })
        };

        // Native End Card: after the interstitial and the rewarded, as most of the studio's games run it.
        [SerializeField, SectionGroup("End Card", Foldable = true), LabelText("After Interstitial")]
        private AdRemoteSetting<bool> endCardAfterInterstitial = new(true);

        [SerializeField, SectionGroup("End Card"), LabelText("After Rewarded")]
        private AdRemoteSetting<bool> endCardAfterRewarded = new(true);

        [SerializeField, SectionGroup("End Card"), LabelText("After Native Interstitial")]
        private AdRemoteSetting<bool> endCardAfterNativeInterstitial = new(false);

        #endregion

        /// <summary>
        /// Each moment the manager paces. A moment two formats fill, the network's and the native one, paces both, counted from whichever showed last:
        /// the game shows one or the other there, so two clocks would let both through.
        /// </summary>
        public enum PacedAd {
            [InspectorName("Interstitial, Native Interstitial")] Interstitial
          , [InspectorName("Rewarded Interstitial")] RewardedInterstitial
          , [InspectorName("App Open, Native App Open")] AppOpen
          , [InspectorName("Native Collapsible")] NativeCollapsible
          , [InspectorName("Native End Card")] NativeEndCard
        }

        /// <summary>The formats the ad on return can be, best first.</summary>
        public enum ReturnAd {
            [InspectorName("Native App Open, else App Open")] NativeAppOpenElseAppOpen
          , [InspectorName("App Open")] AppOpen
          , [InspectorName("Native App Open")] NativeAppOpen
          , None
        }

        /// <summary>
        /// A show the rules let through, before it is known whether an ad is loaded: what analytics counts as an ad opportunity, such as an interstitial's trigger.<br/>
        /// Not for a format remove-ads or the tester's switch takes, nor one its pacing or level holds back.
        /// </summary>
        public event Action<AdFormat, string> Opportunity;

        #region Remove ads and ads off

        private bool adsRemoved;
        private bool adsOff;

        /// <summary>The player owns remove-ads: suppresses the formats it takes, or lets them through. Call it at launch from the save, and after a purchase, a refund or a timed no-ads running out.</summary>
        public void SetAdsRemoved(bool removed) {
            adsRemoved = removed;
            ApplySuppressed();
        }

        public bool AdsRemoved => adsRemoved;

        /// <summary>A tester's switch: no ad of any format shows, and a rewarding format rewards at once, as a suppressed one does; off, remove-ads holds as set.</summary>
        public bool AdsOff {
            get => adsOff;
            set {
                adsOff = value;
                ApplySuppressed();
            }
        }

        // One place decides each format's suppression, so remove-ads and the tester's switch never undo each other.
        private void ApplySuppressed() {
            foreach (var (format, takes) in removeAds) AdsSdk.SetSuppressed(format, adsOff || adsRemoved && takes);
        }

        #endregion

        #region Full screen

        /// <summary>The interstitial may show now: its pacing and, given the level, its From Level allow it, and one is loaded.</summary>
        public bool CanShowInterstitial(int? level = null) => Allows(AdFullScreenFormat.Interstitial, level) && AdsSdk.IsReady(AdFullScreenFormat.Interstitial);

        /// <summary>True when an ad displayed. <paramref name="level"/>, the level the player is at, holds it back before From Level; without it, From Level does not apply.</summary>
        public UniTask<bool> ShowInterstitialAsync(string placement = null, int? level = null) => DisplayedAsync(AdFullScreenFormat.Interstitial, placement, level);

        /// <summary>As <see cref="CanShowInterstitial"/>.</summary>
        public bool CanShowNativeInterstitial(int? level = null) => Allows(AdFullScreenFormat.NativeInterstitial, level) && AdsSdk.IsReady(AdFullScreenFormat.NativeInterstitial);

        /// <summary>As <see cref="ShowInterstitialAsync"/>.</summary>
        public UniTask<bool> ShowNativeInterstitialAsync(string placement = null, int? level = null) => DisplayedAsync(AdFullScreenFormat.NativeInterstitial, placement, level);

        /// <summary>Also true while suppressed, which rewards with no ad.</summary>
        public bool CanShowRewarded => AdsSdk.IsSuppressed(AdFormat.Rewarded) || AdsSdk.IsReady(AdFullScreenFormat.Rewarded);

        /// <summary>True when the player earned the reward.</summary>
        public async UniTask<bool> ShowRewardedAsync(string placement = null) => (await ShowAsync(AdFullScreenFormat.Rewarded, placement)).Rewarded;

        public bool CanShowRewardedInterstitial => AdsSdk.IsSuppressed(AdFormat.RewardedInterstitial) || Allows(AdFullScreenFormat.RewardedInterstitial) && AdsSdk.IsReady(AdFullScreenFormat.RewardedInterstitial);

        public async UniTask<bool> ShowRewardedInterstitialAsync(string placement = null) => (await ShowAsync(AdFullScreenFormat.RewardedInterstitial, placement)).Rewarded;

        /// <summary>
        /// One full-screen ad under these rules: nothing within its pacing, nor an interstitial before From Level; followed by a native end card when the format's End Card tick is on and the end card's own pacing allows.<br/>
        /// A suppressed rewarding format rewards with no ad, as AdsSdk does.
        /// </summary>
        public UniTask<AdChainResult> ShowAsync(AdFullScreenFormat format, string placement = null, int? level = null) {
            var suppressed = AdsSdk.IsSuppressed(format.ToFormat());
            if (!suppressed) {
                if (!Allows(format, level)) return UniTask.FromResult(default(AdChainResult));

                Opportunity?.Invoke(format.ToFormat(), placement);
            }

            var endCard = EndCardAfter(format) && !suppressed && Allows(AdFullScreenFormat.NativeEndCard);
            return AdsSdk.Chain().Then(format, placement).ThenIf(endCard, AdFullScreenFormat.NativeEndCard, placement).Run();
        }

        private async UniTask<bool> DisplayedAsync(AdFullScreenFormat format, string placement, int? level) => (await ShowAsync(format, placement, level)).AnyDisplayed;

        /// <summary>The format's pacing allows a show now, and for an interstitial given the level, its From Level; the rewarded, which the player asks for, has no pacing.</summary>
        public bool Allows(AdFullScreenFormat format, int? level = null) {
            if (format is AdFullScreenFormat.Interstitial or AdFullScreenFormat.NativeInterstitial && level < interstitialFromLevel.Value) return false;

            return format switch {
                AdFullScreenFormat.Interstitial or AdFullScreenFormat.NativeInterstitial => pacing[PacedAd.Interstitial].Allows(AdFormat.Interstitial, AdFormat.NativeInterstitial)
              , AdFullScreenFormat.AppOpen or AdFullScreenFormat.NativeAppOpen           => pacing[PacedAd.AppOpen].Allows(AdFormat.AppOpen, AdFormat.NativeAppOpen)
              , AdFullScreenFormat.RewardedInterstitial                                  => pacing[PacedAd.RewardedInterstitial].Allows(format)
              , AdFullScreenFormat.NativeEndCard                                         => pacing[PacedAd.NativeEndCard].Allows(format)
              , _                                                                        => true
            };
        }

        private bool EndCardAfter(AdFullScreenFormat format) => format switch {
            AdFullScreenFormat.Interstitial       => endCardAfterInterstitial.Value
          , AdFullScreenFormat.Rewarded           => endCardAfterRewarded.Value
          , AdFullScreenFormat.NativeInterstitial => endCardAfterNativeInterstitial.Value
          , _                                     => false
        };

        #endregion

        #region Return to game

        private static readonly AdFullScreenFormat[] appOpenOnly              = { AdFullScreenFormat.AppOpen };
        private static readonly AdFullScreenFormat[] nativeAppOpenOnly        = { AdFullScreenFormat.NativeAppOpen };
        private static readonly AdFullScreenFormat[] nativeAppOpenElseAppOpen = { AdFullScreenFormat.NativeAppOpen, AdFullScreenFormat.AppOpen };

        private AdFullScreenFormat[] ReturnFormats => returnAd switch {
            ReturnAd.AppOpen                  => appOpenOnly
          , ReturnAd.NativeAppOpen            => nativeAppOpenOnly
          , ReturnAd.NativeAppOpenElseAppOpen => nativeAppOpenElseAppOpen
          , _                                 => Array.Empty<AdFullScreenFormat>()
        };

        /// <summary>Holds the ad on return back while the game sends the player out on purpose, such as a purchase sheet or a store page, whose return is not a new visit.</summary>
        public bool ReturnAdBlocked { get; set; }

        /// <summary>The format the ad on return would show now: the first of the App Open section's that is allowed and loaded; none before From Launch.</summary>
        public AdFullScreenFormat? ReturnFormat {
            get {
                if (AdsSdk.Launches < returnFromLaunch) return null;

                foreach (var format in ReturnFormats) {
                    if (!AdsSdk.IsSuppressed(format.ToFormat()) && Allows(format) && AdsSdk.IsReady(format)) return format;
                }

                return null;
            }
        }

        /// <summary>The ad on return now, for a game that shows it itself, such as over its own loading screen once the bar is full; true when an ad displayed.</summary>
        public async UniTask<bool> ShowReturnAdAsync(string placement = "return") => ReturnFormat is { } format && (await ShowAsync(format, placement)).AnyDisplayed;

        private void OnPlayerReturned(double awaySeconds) {
            if (ReturnAdBlocked || awaySeconds < returnAfterAwaySeconds.Value) return;

            ShowReturnAdAsync().Forget();
        }

        // Once the mediations started and the ad loaded, while the game's first scene is still the active one; leaving it first ends the wait.
        private async UniTaskVoid ShowLaunchAdAsync() {
            if (!returnOnLaunch.Value || returnAd is ReturnAd.None) return;

            var first = SceneManager.GetActiveScene();
            await AdsSdk.WaitUntilInitializedAsync();
            await UniTask.WaitUntil(() => ReturnFormat != null || SceneManager.GetActiveScene() != first);
            if (SceneManager.GetActiveScene() == first) await ShowReturnAdAsync("launch");
        }

        #endregion

        #region Banner and collapsible

        public void ShowBanner() => AdsSdk.ShowBanner();
        public void HideBanner() => AdsSdk.HideBanner();

        public bool CanShowCollapsible => pacing[PacedAd.NativeCollapsible].Allows(AdFormat.NativeCollapsible) && AdsSdk.IsCollapsibleReady;

        /// <summary>The collapsible under these rules: nothing within its pacing; else a plan drawn by weight, its steps one after the other. Resolves to how many ads displayed.</summary>
        public UniTask<int> ShowCollapsibleAsync(Action onDisplayed = null) {
            if (AdsSdk.IsSuppressed(AdFormat.NativeCollapsible) || !pacing[PacedAd.NativeCollapsible].Allows(AdFormat.NativeCollapsible)) return UniTask.FromResult(0);

            var plan = AdCollapsiblePlan.Pick(collapsiblePlans);
            if (plan == null) Debug.LogWarning($"{GetType().Name}: no collapsible plan has steps and a weight above 0, so ShowCollapsibleAsync shows nothing");
            else Opportunity?.Invoke(AdFormat.NativeCollapsible, null);
            return AdsSdk.ShowCollapsibleAsync(plan, onDisplayed);
        }

        #endregion

        void IAdsManager.PauseInEditor(bool paused) => EditorGamePause.Apply(paused);

        void IAdsManager.Started() {
            AdsSdk.PlayerReturned += OnPlayerReturned;
            if (bannerAutoShow) AdsSdk.ShowBanner();
            ShowLaunchAdAsync().Forget();
        }
    }
}