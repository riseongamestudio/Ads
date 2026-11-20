using System.ComponentModel;

namespace RiseOn.Ads {
    /// <summary>
    /// Where the Editor draws each ad layer: every layer is a Screen Space Overlay canvas, so the sorting order has to stack them as the device does, above the game.<br/>
    /// The lowest is the page's Editor Sorting Order; each layer above it is one more. For mediations whose preview draws a layer of its own.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AdsEditorLayers {
        // Set by the pack at launch, from the settings.
        internal static int Lowest = 1000;

        /// <summary>Banner, MRec and every view nothing marks: views over the game on the device.</summary>
        public static int OverGame => Lowest;

        /// <summary>The native in-feed cells and native MRec, windows over those views on the device.</summary>
        public static int NativeRect => Lowest + 1;

        public static int HalfScreenCover => Lowest + 2;

        /// <summary>The collapsible.</summary>
        public static int HalfScreenAd => Lowest + 3;

        public static int FullScreenCover => Lowest + 4;

        /// <summary>Every full-screen ad: over everything.</summary>
        public static int FullScreenAd => Lowest + 5;
    }
}