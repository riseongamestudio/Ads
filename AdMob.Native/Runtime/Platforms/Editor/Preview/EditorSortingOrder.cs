namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    // Where every preview canvas sits: on a device each layer is its own native window, Activity or subview, stacked by the
    // platform; in the Editor everything is a Screen Space Overlay canvas, and sorting order is the only stacking there is. The
    // numbers come from NativeAdEditorLayers, which the code showing the ads sets to fit its own layers between them.
    internal static class EditorSortingOrder {
        internal static int FullScreenAd => NativeAdEditorLayers.FullScreen;

        internal static int HalfScreenAd => NativeAdEditorLayers.HalfScreen;

        internal static int RectAd => NativeAdEditorLayers.RectAd;
    }
}