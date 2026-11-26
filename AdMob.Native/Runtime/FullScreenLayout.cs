namespace RiseOn.Ads.AdMob.Native {
    /// <summary>
    /// How a full-screen ad arranges its card.<br/>
    /// The ordinals cross into the native layers, so they are spelled out rather than left to declaration order.
    /// </summary>
    public enum FullScreenLayout {
        /// <summary>The picture across the card's top, then the app, the pitch and the button.</summary>
        Interstitial = 0
        /// <summary>The interstitial's card under a bar back to the game, with its icon and name, as AdMob's own app open ad has.</summary>
      , AppOpen      = 1
        /// <summary>The app first - a big icon and its name - then the picture, the pitch and the button.</summary>
      , EndCard      = 2
    }
}