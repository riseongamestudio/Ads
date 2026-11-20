using System;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>How a full-screen native format (interstitial, app open, end card) shows, whichever mediation serves it, on both platforms, and how often the game's manager lets it.</summary>
    [Serializable]
    public sealed class AdNativeFullScreenSettings {
        [SerializeField]
        private AdPacingSettings pacing;

        [SerializeField]
        [Tooltip("The ad's backdrop where the creative does not reach, alpha included.")]
        private Color backgroundColor;

        [SerializeField]
        private AdCloseSettings close;

        public AdNativeFullScreenSettings(AdPacingSettings pacing, Color backgroundColor, AdCloseSettings close) {
            this.pacing          = pacing;
            this.backgroundColor = backgroundColor;
            this.close           = close;
        }

        public AdPacingSettings Pacing => pacing;
        public Color BackgroundColor => backgroundColor;
        public AdCloseSettings Close => close;
    }
}