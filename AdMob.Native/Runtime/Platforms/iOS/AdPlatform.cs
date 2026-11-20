namespace RiseOn.Ads.AdMob.Native.Platforms.IOS {
    internal sealed class AdPlatform : IAdPlatform {
        public IRectAdClient CreateRectAd(
            RectAd.Settings settings
          , IRectAdCallbacks callbacks) {
            return new RectAdClient(settings, callbacks);
        }

        public IOverlayAdClient CreateOverlay(
            OverlayAdSettings settings
          , IOverlayAdCallbacks callbacks) {
            return new OverlayAdClient(settings, callbacks);
        }
    }
}