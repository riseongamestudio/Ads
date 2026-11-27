using System;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// How each format shows, whichever mediation serves it, the same on both platforms.<br/>
    /// A mediation reads it from <see cref="IAdMediationContext.Formats"/> and turns it into its SDK's terms.<br/>
    /// The formats with nothing to set have no entry.
    /// </summary>
    [Serializable]
    public sealed class AdFormatSettings {
        [SerializeField]
        private AdBannerSettings banner = new();

        [SerializeField]
        private AdMRecSettings mRec = new();

        [SerializeField]
        private AdNativeFullScreenSettings nativeInterstitial = new(new AdCloseSettings { Cooldown = 5, CloseSide = AdCloseSide.Random, TimerSide = AdTimerSide.OppositeOfClose });

        [SerializeField]
        private AdNativeFullScreenSettings nativeAppOpen = new(new AdCloseSettings { Cooldown = 3, CloseSide = AdCloseSide.Random, TimerSide = AdTimerSide.OppositeOfClose });

        [SerializeField]
        private AdNativeFullScreenSettings nativeEndCard = new(new AdCloseSettings { Cooldown = 0, CloseSide = AdCloseSide.Random, TimerSide = AdTimerSide.OppositeOfClose });

        [SerializeField]
        private AdCollapsibleSettings nativeCollapsible = new();

        public AdBannerSettings Banner => banner;
        public AdMRecSettings MRec => mRec;
        public AdNativeFullScreenSettings NativeInterstitial => nativeInterstitial;
        public AdNativeFullScreenSettings NativeAppOpen => nativeAppOpen;
        public AdNativeFullScreenSettings NativeEndCard => nativeEndCard;
        public AdCollapsibleSettings NativeCollapsible => nativeCollapsible;
    }
}