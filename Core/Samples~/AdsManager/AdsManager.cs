using System;
using Cysharp.Threading.Tasks;
using RiseOn.Ads;
using RiseOn.Serializables;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The ad rules most of the studio's games run, on top of AdsSdk: remove-ads, each format's pacing, a native end card after some formats,
/// the banner from the start, the ad when the player comes back and at launch, and the collapsible's plans drawn by weight.<br/>
/// Its fields show on Project Settings > RiseOn > Ads, each number or tick with a Remote Config key there while that pack is installed.<br/>
/// A sample: copy it into the game and change what the game needs; the pack calls nothing here but PauseInEditor.
/// </summary>
public partial class AdsManager : IAdsManager {
    #region Fields

    // The page's order: the rules over many formats first, the more formats the higher (Remove Ads, Pacing); then each format's own,
    // named for it and in the order of AdFormat (Banner, App Open, Native Collapsible, Native End Card).

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

    // Interstitial and Native Interstitial: what most of the studio's games run in Remote Config (Get In The Bowl, Bounce Adventure, Fish Sort).
    // Native Collapsible: what most of them run, the first at once, then 15 seconds apart.
    // No game of the studio paces Rewarded Interstitial, App Open or Native App Open; 0 is no pacing.
    // Native End Card at 0, so it follows the ad it is chained after each time that one shows.
    [SerializeField, LabelText("Pacing"), NoRowShade]
    [Tooltip("Each paced format's spacing, in seconds: from launch to its first show, from its last show, and from the last rewarded ad.")]
    private EnumMap<PaceableFormat, AdPacing> pacing = new() {
        [PaceableFormat.Interstitial]         = new(45, 25)
      , [PaceableFormat.RewardedInterstitial] = new()
      , [PaceableFormat.AppOpen]              = new()
      , [PaceableFormat.NativeCollapsible]    = new(0, 15)
      , [PaceableFormat.NativeInterstitial]   = new(45, 25)
      , [PaceableFormat.NativeAppOpen]        = new()
      , [PaceableFormat.NativeEndCard]        = new()
    };

    // Banner.
    [SerializeField, SectionGroup("Banner", Foldable = true), LabelText("Auto Show")]
    [Tooltip("Shows the banner as the game starts and keeps it up, held back while remove-ads takes it.")]
    private bool bannerAutoShow = true;

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

    /// <summary>The formats the manager paces, each numbered as its <see cref="AdFormat"/>: every full-screen one but the rewarded, which the player asks for, and Native Collapsible.</summary>
    public enum PaceableFormat {
        Interstitial         = AdFormat.Interstitial
      , RewardedInterstitial = AdFormat.RewardedInterstitial
      , AppOpen              = AdFormat.AppOpen
      , NativeCollapsible    = AdFormat.NativeCollapsible
      , NativeInterstitial   = AdFormat.NativeInterstitial
      , NativeAppOpen        = AdFormat.NativeAppOpen
      , NativeEndCard        = AdFormat.NativeEndCard
    }

    /// <summary>The formats the ad on return can be, best first.</summary>
    public enum ReturnAd {
        [InspectorName("Native App Open, else App Open")] NativeAppOpenElseAppOpen
      , [InspectorName("App Open")] AppOpen
      , [InspectorName("Native App Open")] NativeAppOpen
      , None
    }

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

    #region Remove ads

    /// <summary>The player owns remove-ads: suppresses the formats it takes, or lets them through. Call it at launch from the save, and after a purchase or its refund.</summary>
    public void SetAdsRemoved(bool removed) {
        foreach (var (format, takes) in removeAds) {
            if (takes) AdsSdk.SetSuppressed(format, removed);
        }
    }

    #endregion

    #region Full screen

    public bool CanShowInterstitial => Allows(AdFullScreenFormat.Interstitial) && AdsSdk.IsReady(AdFullScreenFormat.Interstitial);

    /// <summary>True when an ad displayed.</summary>
    public UniTask<bool> ShowInterstitialAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.Interstitial, placement);

    /// <summary>Also true while suppressed, which rewards with no ad.</summary>
    public bool CanShowRewarded => AdsSdk.IsSuppressed(AdFormat.Rewarded) || AdsSdk.IsReady(AdFullScreenFormat.Rewarded);

    /// <summary>True when the player earned the reward.</summary>
    public async UniTask<bool> ShowRewardedAsync(string placement = null) => (await ShowAsync(AdFullScreenFormat.Rewarded, placement)).Rewarded;

    public bool CanShowRewardedInterstitial => AdsSdk.IsSuppressed(AdFormat.RewardedInterstitial) || Allows(AdFullScreenFormat.RewardedInterstitial) && AdsSdk.IsReady(AdFullScreenFormat.RewardedInterstitial);

    public async UniTask<bool> ShowRewardedInterstitialAsync(string placement = null) => (await ShowAsync(AdFullScreenFormat.RewardedInterstitial, placement)).Rewarded;

    public bool CanShowNativeInterstitial => Allows(AdFullScreenFormat.NativeInterstitial) && AdsSdk.IsReady(AdFullScreenFormat.NativeInterstitial);

    public UniTask<bool> ShowNativeInterstitialAsync(string placement = null) => DisplayedAsync(AdFullScreenFormat.NativeInterstitial, placement);

    /// <summary>
    /// One full-screen ad under these rules: nothing within its pacing; followed by a native end card when the format's End Card tick is on and the end card's own pacing allows.<br/>
    /// A suppressed rewarding format rewards with no ad, as AdsSdk does.
    /// </summary>
    public UniTask<AdChainResult> ShowAsync(AdFullScreenFormat format, string placement = null) {
        if (!AdsSdk.IsSuppressed(format.ToFormat()) && !Allows(format)) return UniTask.FromResult(default(AdChainResult));

        var endCard = EndCardAfter(format) && !AdsSdk.IsSuppressed(format.ToFormat()) && Allows(AdFullScreenFormat.NativeEndCard);
        return AdsSdk.Chain().Then(format, placement).ThenIf(endCard, AdFullScreenFormat.NativeEndCard, placement).Run();
    }

    private async UniTask<bool> DisplayedAsync(AdFullScreenFormat format, string placement) => (await ShowAsync(format, placement)).AnyDisplayed;

    /// <summary>The format's pacing allows a show now; the rewarded, which the player asks for, has none.</summary>
    public bool Allows(AdFullScreenFormat format) => PacingOf(format)?.Allows(format) ?? true;

    // Every full-screen format but the rewarded is paceable.
    private AdPacing PacingOf(AdFullScreenFormat format) => format is AdFullScreenFormat.Rewarded ? null : pacing[(PaceableFormat) (int) format];

    private bool EndCardAfter(AdFullScreenFormat format) => format switch {
        AdFullScreenFormat.Interstitial       => endCardAfterInterstitial.Value
      , AdFullScreenFormat.Rewarded           => endCardAfterRewarded.Value
      , AdFullScreenFormat.NativeInterstitial => endCardAfterNativeInterstitial.Value
      , _                                     => false
    };

    #endregion

    #region Return to game

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

    public bool CanShowCollapsible => pacing[PaceableFormat.NativeCollapsible].Allows(AdFormat.NativeCollapsible) && AdsSdk.IsCollapsibleReady;

    /// <summary>The collapsible under these rules: nothing within its pacing; else a plan drawn by weight, its steps one after the other. Resolves to how many ads displayed.</summary>
    public UniTask<int> ShowCollapsibleAsync(Action onDisplayed = null) {
        if (!pacing[PaceableFormat.NativeCollapsible].Allows(AdFormat.NativeCollapsible)) return UniTask.FromResult(0);

        var plan = AdCollapsiblePlan.Pick(collapsiblePlans);
        if (plan == null) Debug.LogWarning($"{nameof(AdsManager)}: no collapsible plan has steps and a weight above 0, so ShowCollapsibleAsync shows nothing");
        return AdsSdk.ShowCollapsibleAsync(plan, onDisplayed);
    }

    #endregion

    void IAdsManager.PauseInEditor(bool paused) => EditorGamePause.Apply(paused);

    // In the first scene, once the pack has made the manager.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Start() {
        var manager = Ins;
        AdsSdk.PlayerReturned += manager.OnPlayerReturned;
        if (manager.bannerAutoShow) AdsSdk.ShowBanner();
        manager.ShowLaunchAdAsync().Forget();
    }
}