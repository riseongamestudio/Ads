using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>How the native collapsible shows, whichever mediation serves it, on both platforms; how it closes comes with each show.</summary>
    [Serializable]
    public sealed class AdCollapsibleSettings {
        [SerializeField, PropertyRange(0, 1)]
        [Tooltip("The part of the screen's height the panel covers, from the bottom.")]
        private float heightRatio = .5f;

        [SerializeField]
        [Tooltip("The panel's backdrop where the creative does not reach, alpha included.")]
        private Color backgroundColor = Color.black;

        public float HeightRatio => Mathf.Clamp01(heightRatio);
        public Color BackgroundColor => backgroundColor;
    }
}