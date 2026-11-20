using System;
using System.Collections.Generic;
using RiseOn.Serializables;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// How each format shows, whichever mediation serves it, and the rules the game's manager applies to it, the same on both platforms.<br/>
    /// A mediation reads it from <see cref="IAdMediationContext.Formats"/> and turns it into its SDK's terms.<br/>
    /// The formats with nothing to set have no entry.
    /// </summary>
    [Serializable]
    public sealed class AdFormatSettings {
        // Every format a remove-ads purchase takes but the rewarding ones, which would otherwise grant their reward with no ad.
        [SerializeField]
        private EnumMap<AdFormat, bool> removeAds = NewRemoveAds();

        // Read for each full-screen format but the end card itself; on after the interstitial and the rewarded, as most of the studio's games run it.
        [SerializeField]
        private EnumMap<AdFormat, AdRemoteSetting<bool>> endCards = NewEndCards();

        [SerializeField]
        private AdBannerSettings banner = new();

        [SerializeField]
        private AdMRecSettings mRec = new();

        // What most of the studio's games run in Remote Config (Get In The Bowl, Bounce Adventure, Fish Sort).
        [SerializeField]
        private AdPacingSettings interstitial = new(45, 25);

        // No network publishes a figure; 0 leaves the pacing to the game until it picks one, as for every format below but the native interstitial.
        [SerializeField]
        private AdPacingSettings rewardedInterstitial = new();

        [SerializeField]
        private AdPacingSettings appOpen = new();

        // In the interstitial's place, so paced as it is.
        [SerializeField]
        private AdNativeFullScreenSettings nativeInterstitial = new(new AdPacingSettings(45, 25), Color.black, new AdCloseSettings { Cooldown = 5, CloseSide = AdCloseSide.Random, TimerSide = AdTimerSide.OppositeOfClose });

        [SerializeField]
        private AdNativeFullScreenSettings nativeAppOpen = new(new AdPacingSettings(), new Color(0, 0, 0, .88f), new AdCloseSettings { Cooldown = 3, CloseSide = AdCloseSide.Random, TimerSide = AdTimerSide.OppositeOfClose });

        // 0 for both, so the end card follows the ad it is chained after each time that one shows.
        [SerializeField]
        private AdNativeFullScreenSettings nativeEndCard = new(new AdPacingSettings(), Color.black, new AdCloseSettings { Cooldown = 0, CloseSide = AdCloseSide.Random, TimerSide = AdTimerSide.OppositeOfClose });

        [SerializeField]
        private AdCollapsibleSettings nativeCollapsible = new();

        [SerializeField]
        private AdNativeRectSettings nativeMRec = new();

        [SerializeField]
        private AdNativeRectSettings nativeInFeed = new();

        public AdBannerSettings Banner => banner;
        public AdMRecSettings MRec => mRec;
        public AdPacingSettings Interstitial => interstitial;
        public AdPacingSettings RewardedInterstitial => rewardedInterstitial;
        public AdPacingSettings AppOpen => appOpen;
        public AdNativeFullScreenSettings NativeInterstitial => nativeInterstitial;
        public AdNativeFullScreenSettings NativeAppOpen => nativeAppOpen;
        public AdNativeFullScreenSettings NativeEndCard => nativeEndCard;
        public AdCollapsibleSettings NativeCollapsible => nativeCollapsible;
        public AdNativeRectSettings NativeMRec => nativeMRec;
        public AdNativeRectSettings NativeInFeed => nativeInFeed;

        /// <summary>Whether a remove-ads purchase takes the format away; a rewarding one then grants its reward with no ad.</summary>
        public bool RemoveAdsTakes(AdFormat format) => removeAds.TryGetValue(format, out var taken) && taken;

        /// <summary>Whether the game's manager follows the format's ad with a native end card, in the same chain; never the end card itself.</summary>
        public bool EndCardFollows(AdFullScreenFormat format) => TakesEndCard(format.ToFormat()) && endCards.TryGetValue(format.ToFormat(), out var on) && on != null && on.Value;

        // Whether the page offers the format Use Native End Card: a full-screen one but the end card itself.
        internal static bool TakesEndCard(AdFormat format) => format.IsFullScreen() && format is not AdFormat.NativeEndCard;

        /// <summary>How often the game's manager lets the format show; the rewarded, which the player asks for, has no pacing on the page and none here: 0 and 0.</summary>
        public AdPacingSettings PacingOf(AdFullScreenFormat format) => format switch {
            AdFullScreenFormat.Interstitial         => interstitial
          , AdFullScreenFormat.RewardedInterstitial => rewardedInterstitial
          , AdFullScreenFormat.AppOpen              => appOpen
          , AdFullScreenFormat.NativeInterstitial   => nativeInterstitial.Pacing
          , AdFullScreenFormat.NativeAppOpen        => nativeAppOpen.Pacing
          , AdFullScreenFormat.NativeEndCard        => nativeEndCard.Pacing
          , _                                       => noPacing
        };

        private static readonly AdPacingSettings noPacing = new();

        // Each setting's Remote Config key with the type its value needs, for the page's check.
        internal IEnumerable<(string Setting, int KeyId, Type Type)> RemoteKeyUses {
            get {
                foreach (AdFullScreenFormat format in Enum.GetValues(typeof(AdFullScreenFormat))) {
                    if (format is AdFullScreenFormat.Rewarded) continue;

                    foreach (var use in PacingOf(format).RemoteKeyUses(format.ToFormat())) yield return use;
                }

                foreach (var use in nativeCollapsible.Pacing.RemoteKeyUses(AdFormat.NativeCollapsible)) yield return use;

                foreach (var (format, endCard) in endCards) {
                    if (endCard != null && TakesEndCard(format)) yield return ($"{format} Use Native End Card", endCard.KeyId, typeof(bool));
                }

                foreach (var use in nativeCollapsible.RemoteKeyUses) yield return use;
            }
        }

        private static EnumMap<AdFormat, AdRemoteSetting<bool>> NewEndCards() {
            var map = new EnumMap<AdFormat, AdRemoteSetting<bool>>();
            foreach (AdFormat format in Enum.GetValues(typeof(AdFormat))) {
                // On after the interstitial and the rewarded, as most of the studio's games run it.
                if (TakesEndCard(format)) map[format] = new AdRemoteSetting<bool>(format is AdFormat.Interstitial or AdFormat.Rewarded);
            }

            return map;
        }

        private static EnumMap<AdFormat, bool> NewRemoveAds() {
            var map = new EnumMap<AdFormat, bool>();
            foreach (AdFormat format in Enum.GetValues(typeof(AdFormat))) map[format] = format is not (AdFormat.Rewarded or AdFormat.RewardedInterstitial);
            return map;
        }
    }
}