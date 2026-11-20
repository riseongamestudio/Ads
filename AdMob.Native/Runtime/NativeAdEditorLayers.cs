namespace RiseOn.Ads.AdMob.Native {
    /// <summary>
    /// The sorting orders of the Editor's native ad previews, each on a Screen Space Overlay canvas: the code that shows the ads sets them to fit the stack its own Editor layers draw, with the game's canvases below.<br/>
    /// The defaults keep the previews over a game drawing at 0 and up, a full-screen one over everything.
    /// </summary>
    public static class NativeAdEditorLayers {
        public static int RectAd     = short.MaxValue - 4;
        public static int HalfScreen = short.MaxValue - 2;
        public static int FullScreen = short.MaxValue;
    }
}