namespace RiseOn.Ads {
    /// <summary>
    /// What a mediation tells about one show.<br/>
    /// Any member may arrive on any thread, and every show ends in exactly one <see cref="OnClosed"/> or <see cref="OnFailed"/>, the display failures included.
    /// </summary>
    public interface IAdShowListener {
        void OnDisplayed();

        /// <summary>The player earned the reward; comes before <see cref="OnClosed"/>.</summary>
        void OnRewarded();

        void OnClosed();
        void OnFailed(in AdError error);
    }
}