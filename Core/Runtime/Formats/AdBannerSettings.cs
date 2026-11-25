using System;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>How the banner shows, whichever mediation serves it, on both platforms.</summary>
    [Serializable]
    public sealed class AdBannerSettings {
        [SerializeField]
        private AdScreenPosition position = AdScreenPosition.Bottom;

        [SerializeField]
        [Tooltip("As wide as the screen, as tall as the network's adaptive banner for that width; off, the standard 320×50.")]
        private bool adaptive = true;

        public AdScreenPosition Position => position;
        public bool Adaptive => adaptive;
    }
}