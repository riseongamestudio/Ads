using System;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>How the MRec shows, whichever mediation serves it, on both platforms.</summary>
    [Serializable]
    public sealed class AdMRecSettings {
        [SerializeField]
        private AdScreenPosition position = AdScreenPosition.Bottom;

        public AdScreenPosition Position => position;
    }
}