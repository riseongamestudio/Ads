using System;
using Sirenix.OdinInspector;

namespace RiseOn.Ads {
    public enum AdCloseSide {
        Left
      , Right
      , Random
    }

    public enum AdTimerSide {
        Left
      , Right
      , Random
      , OppositeOfClose
      , SameAsClose
    }

    /// <summary>How a native ad drawn over the game (collapsible, native interstitial, native app open, end card) offers to close.</summary>
    [Serializable]
    public struct AdCloseSettings {
        /// <summary>Seconds before the close button shows; 0 shows it at once, with no timer.</summary>
        [MinValue(0)] public int Cooldown;

        public AdCloseSide CloseSide;
        public AdTimerSide TimerSide;

        /// <summary>Closing opens the ad's link instead.</summary>
        public bool RedirectOnClose;
    }
}