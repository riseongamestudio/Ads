using System;
using System.ComponentModel;

namespace RiseOn.Ads {
    /// <summary>
    /// Where the Editor draws each ad layer: every layer is a Screen Space Overlay canvas, so the sorting order has to stack them as the device does, above the game.<br/>
    /// The lowest is the page's Editor Sorting Order; each layer above it is <see cref="Step"/> more, so a game canvas fits between two.<br/>
    /// For mediations whose preview draws a layer of its own.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AdsEditorLayers {
        // Set by the pack at launch, from the settings.
        internal static int lowest = 1000;

        /// <summary>How far each layer stands above the one under it.</summary>
        public const int Step = 100;

        /// <summary>Banner, MRec and every view nothing marks: views over the game on the device.</summary>
        public static int OverGame => lowest;

        /// <summary>The native in-feed cells and native MRec, windows over those views on the device.</summary>
        public static int NativeRect => lowest + Step;

        public static int HalfScreenCover => lowest + 2 * Step;

        /// <summary>The collapsible.</summary>
        public static int HalfScreenAd => lowest + 3 * Step;

        public static int FullScreenCover => lowest + 4 * Step;

        /// <summary>Every full-screen ad: over everything.</summary>
        public static int FullScreenAd => lowest + 5 * Step;
    }

    /// <summary>The page draws the field as a section: its value on the bar, the layers under it, each with the sorting order it takes from the value.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class AdsEditorLayersTableAttribute : Attribute { }
}