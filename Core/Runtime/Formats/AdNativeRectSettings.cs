using System;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>How a native format in a rect the game places (MRec, in-feed cell) shows, whichever mediation serves it, on both platforms.</summary>
    [Serializable]
    public sealed class AdNativeRectSettings {
        [SerializeField]
        [Tooltip("The rect's backdrop behind the ad, alpha included; clear lets the game's own placeholder show through.")]
        private Color backgroundColor = Color.clear;

        public Color BackgroundColor => backgroundColor;
    }
}