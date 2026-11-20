namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    // Where every preview canvas sits: on a device each layer is its own native window, Activity or subview, stacked by the
    // platform; in the Editor everything is a Screen Space Overlay canvas, and sorting order is the only stacking there is. The
    // numbers come from NativeAdEditorLayers, which the code showing the ads sets to fit its own layers between them.
    internal static class EditorSortingOrder {
        internal static int FULL_SCREEN_AD => NativeAdEditorLayers.FullScreen;

        internal static int HALF_SCREEN_AD => NativeAdEditorLayers.HalfScreen;

        internal static int RECT_AD => NativeAdEditorLayers.RectAd;
    }
}