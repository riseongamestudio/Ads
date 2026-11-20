using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>How the banner shows, whichever mediation serves it, on both platforms, and whether the game's manager shows it by itself.</summary>
    [Serializable]
    public sealed class AdBannerSettings {
        [SerializeField]
        private AdScreenPosition position = AdScreenPosition.Bottom;

        [SerializeField]
        [Tooltip("As wide as the screen, as tall as the network's adaptive banner for that width; off, the standard 320×50.")]
        private bool adaptive = true;

        [SerializeField, LabelText("Auto Show")]
        [Tooltip("The manager shows the banner as the game starts and keeps it up, held back while remove-ads takes it; ShowBanner then only warns and HideBanner logs an error. Off for a game that calls ShowBanner and HideBanner itself.")]
        private bool autoShow = true;

        public AdScreenPosition Position => position;
        public bool Adaptive => adaptive;

        /// <summary>Whether the game's manager shows the banner as the game starts, until the game calls HideBanner.</summary>
        public bool AutoShow => autoShow;
    }
}