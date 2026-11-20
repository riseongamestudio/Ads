using System;
using UnityEngine;

namespace RiseOn.Ads.AdMob.Native {
    /// <summary>
    /// An overlay ad that covers a bottom slice of the screen - the collapsible placement.<br/>
    /// HeightRatio is the covered fraction.
    /// </summary>
    public sealed class HalfScreenAd : OverlayAd {
        private const string DefaultFormat = "NATIVE_HALF_SCREEN";
        
        [Serializable]
        public struct Settings {
            public string AdUnitId;
            public float  HeightRatio;

            /// <summary>
            /// How many ads this placement asks its pool to keep warm; 0 means 1, always a spare.<br/>
            /// The pool keeps the most any of its formats asks for.<br/>
            /// Raise it where one show is followed straight by another, so the second is already in hand.
            /// </summary>
            public int CacheSize;

            /// <summary>
            /// The panel's own color, alpha included.<br/>
            /// Passed through whole - there is no "unset": a fully transparent color means exactly that, and black is what every placement here asks for.
            /// </summary>
            public Color BackgroundColor;

            [SerializeField] private string format;

            /// <summary>
            /// Where the game puts this placement.<br/>
            /// Unset means NATIVE_HALF_SCREEN.
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
        }

        public HalfScreenAd(in Settings settings)
            : base(
                settings.AdUnitId
              , settings.Format
              , fullScreen: false
              , settings.HeightRatio
              , settings.CacheSize
              , settings.BackgroundColor
              , settings.Close) {}

        /// <summary>
        /// The panel's height in the platform's own units: pixels on Android, points on iOS, Screen pixels in the Editor.<br/>
        /// It follows from HeightRatio and the screen alone, never from the creative, so it can be read before any ad has loaded - which is what lets whatever stands behind the panel be sized and placed before the panel arrives.<br/>
        /// Callable from any thread.
        /// </summary>
        public float PanelHeight => ResolvePanelHeight();

        /// <summary>
        /// <paramref name="slideIn"/> brings the panel up from the bottom edge; OnAdDisplayed then fires once it has arrived, and the close countdown starts at that moment.<br/>
        /// Touches on the panel are swallowed while it moves.<br/>
        /// <paramref name="slideOutOnClose"/> sends it back down when the player closes it, and OnAdHidden fires once it has left.<br/>
        /// A close under RedirectOnClose never slides: the redirect is already taking the screen.
        /// </summary>
        public void Show(bool slideIn, bool slideOutOnClose) {
            ShowOverlay(slideIn, slideOutOnClose);
        }

        /// <summary>Takes the panel down from code, sliding when <paramref name="slideOut"/> asks for it.</summary>
        public void Hide(bool slideOut) {
            HideOverlay(slideOut);
        }
    }
}