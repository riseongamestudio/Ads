using System;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>How a full-screen native format (interstitial, app open, end card) shows, whichever mediation serves it, on both platforms; a show may bring its own close.</summary>
    [Serializable]
    public sealed class AdNativeFullScreenSettings {
        [SerializeField]
        private AdCloseSettings close;

        public AdNativeFullScreenSettings(AdCloseSettings close) => this.close = close;

        /// <summary>How an ad shown without a close of its own offers to close.</summary>
        public AdCloseSettings Close => close;
    }
}