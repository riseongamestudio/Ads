using System;
using UnityEngine;

namespace RiseOn.Ads.AdMob.Native {
    /// <summary>
    /// An overlay ad that covers the whole screen - app open, interstitial, end card.<br/>
    /// The bottom-slice sibling is HalfScreenAd.
    /// </summary>
    public sealed class FullScreenAd : OverlayAd {
        private const string DefaultFormat = "NATIVE_FULL_SCREEN";
        
        [Serializable]
        public struct Settings {
            public string AdUnitId;

            /// <summary>
            /// How many ads this placement asks its pool to keep warm; 0 means 1, always a spare.<br/>
            /// The pool keeps the most any of its formats asks for.<br/>
            /// Raise it where one show is followed straight by another, so the second is already in hand.
            /// </summary>
            public int CacheSize;

            [SerializeField] private string format;

            /// <summary>
            /// Where the game puts this placement.<br/>
            /// Unset means NATIVE_FULL_SCREEN.
            /// </summary>
            public string Format {
                // A struct cannot initialize a field, so the default lives in
                // the reading of it rather than in an assignment.
                readonly get => string.IsNullOrEmpty(format) ? DefaultFormat : format;
                set => format = value;
            }

            /// <summary>
            /// The close button and the countdown that gates it.<br/>
            /// Changeable afterwards through SetClose.
            /// </summary>
            public CloseSettings Close;

            /// <summary>
            /// How the card is arranged; unset is Interstitial.
            /// </summary>
            public FullScreenLayout Layout;
        }

        public FullScreenAd(in Settings settings)
            : base(
                settings.AdUnitId
              , settings.Format
              , fullScreen: true
              , heightRatio: 1
              , settings.CacheSize
              , settings.Close
              , settings.Layout) {}

        /// <summary>
        /// Puts the ad over the whole screen.<br/>
        /// On a device it stops the game while it is up, the way every full-screen ad SDK does: on Android its Activity pauses Unity, on iOS it calls UnityPause itself, and it lets the game go when it closes.<br/>
        /// The Editor preview pauses nothing.
        /// </summary>
        public void Show() {
            ShowOverlay(false, false);
        }

        public void Hide() {
            HideOverlay(false);
        }
    }
}