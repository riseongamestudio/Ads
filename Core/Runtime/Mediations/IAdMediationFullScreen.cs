namespace RiseOn.Ads {
    /// <summary>
    /// The full-screen formats a mediation serves (<see cref="AdFormatUtils.IsFullScreen"/>).<br/>
    /// <see cref="AdChain"/> runs them, under the full-screen cover when they are chained.
    /// </summary>
    public interface IAdMediationFullScreen : IAdMediation {
        bool Supports(AdFormat format);

        /// <summary>Must be safe from any thread for a format that <see cref="ShowsFromAnyThread"/>.</summary>
        bool IsReady(AdFormat format);

        /// <summary>
        /// Whether the show's listener calls never wait for Unity's player loop.<br/>
        /// Only then can another step follow it: the cover between them keeps Unity paused, so a callback marshalled to the main thread never arrives.
        /// </summary>
        bool CallsBackOffLoop(AdFormat format);

        /// <summary>Whether <see cref="IsReady"/> and <see cref="Show"/> work from any thread. Only then can the format follow another step.</summary>
        bool ShowsFromAnyThread(AdFormat format);

        /// <param name="placement">What the game passed to the show, null for none: for the SDK where it takes one, and for <see cref="AdImpression.Placement"/>.</param>
        void Show(AdFormat format, string placement, IAdShowListener listener);
    }
}