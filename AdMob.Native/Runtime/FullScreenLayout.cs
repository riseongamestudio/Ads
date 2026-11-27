namespace RiseOn.Ads.AdMob.Native {
    /// <summary>
    /// How a full-screen ad arranges its face.<br/>
    /// The ordinals cross into the native layers, so they are spelled out rather than left to declaration order.
    /// </summary>
    public enum FullScreenLayout {
        /// <summary>Over the whole screen: the picture as large as it goes in the middle, the app's strip at the foot - the icon beside the name, the pitch, the button.</summary>
        Interstitial = 0
        /// <summary>A card in the middle of the dimmed game: the picture across its top, then the app, the pitch and the button; without a redirecting close, under a bar back to the game with its icon and name, as AdMob's own app open ad has.</summary>
      , AppOpen      = 1
        /// <summary>The app's store page: the picture as a banner over a dimmed, blurred copy of itself, then the big icon beside the name, the store's figures, the pitch, and the button at the foot.</summary>
      , EndCard      = 2
    }
}