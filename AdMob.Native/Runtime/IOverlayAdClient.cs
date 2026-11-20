namespace RiseOn.Ads.AdMob.Native {
    internal interface IOverlayAdClient {
        /// <summary>
        /// The half-screen panel's height in the platform's own units - pixels on Android, points on iOS, Screen pixels in the Editor.<br/>
        /// Fixed by the height ratio and the screen, never by the creative, so it is known before any ad loads.
        /// </summary>
        float PanelHeight { get; }

        void SetClose(in CloseSettings controls);
        void Load();
        void Show(int showId, bool slideIn, bool slideOutOnClose);
        void Hide(bool slideOut);
        void Release();
    }
}