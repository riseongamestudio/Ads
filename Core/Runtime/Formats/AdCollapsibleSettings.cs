using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>How the native collapsible shows, whichever mediation serves it, on both platforms; a show may bring its own close.</summary>
    [Serializable]
    public sealed class AdCollapsibleSettings {
        [SerializeField, PropertyRange(0, 1)]
        [Tooltip("The part of the screen's height the panel covers, from the bottom.")]
        private float heightRatio = .5f;

        [SerializeField]
        private AdCloseSettings close = new() { Cooldown = 3, CloseSide = AdCloseSide.Random, TimerSide = AdTimerSide.OppositeOfClose };

        public float HeightRatio => Mathf.Clamp01(heightRatio);

        /// <summary>How a collapsible shown without a close of its own offers to close.</summary>
        public AdCloseSettings Close => close;
    }
}