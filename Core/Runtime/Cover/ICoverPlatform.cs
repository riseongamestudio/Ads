namespace RiseOn.Ads {
    /// <summary>
    /// The platform side of the two covers.<br/>
    /// Every member may be called from any thread: the full-screen cover pauses Unity, so whatever takes it down cannot wait for the player loop.
    /// </summary>
    internal interface ICoverPlatform {
        void ShowFullScreen();
        void HideFullScreen();

        /// <summary>Adds the half-screen cover hidden, so a half-screen ad added after it stays on top.</summary>
        /// <param name="panelHeight">The half-screen ad's panel height as the ad reports it, in the platform's own units: pixels on Android, points on iOS, Screen pixels in the Editor.</param>
        void PrepareHalfScreen(float panelHeight);

        void RevealHalfScreen();
        void ConcealHalfScreen();
        void RemoveHalfScreen();
    }

    internal static class CoverPlatform {
        public static ICoverPlatform Installed { get; private set; }

        public static void Install(ICoverPlatform platform) {
            Installed = platform;
        }
    }
}