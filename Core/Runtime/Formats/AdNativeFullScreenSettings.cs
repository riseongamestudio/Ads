using System;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>How a full-screen native format (interstitial, app open, end card) shows, whichever mediation serves it, on both platforms.</summary>
    [Serializable]
    public sealed class AdNativeFullScreenSettings {
        [SerializeField]
        [Tooltip("The ad's backdrop where the creative does not reach, alpha included.")]
        private Color backgroundColor;

        [SerializeField]
        private AdCloseSettings close;

        public AdNativeFullScreenSettings(Color backgroundColor, AdCloseSettings close) {
            this.backgroundColor = backgroundColor;
            this.close           = close;
        }

        public Color BackgroundColor => backgroundColor;
        public AdCloseSettings Close => close;
    }
}