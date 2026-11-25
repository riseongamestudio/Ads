namespace RiseOn.Ads {
    /// <summary>
    /// The game's ads manager: the one partial class implementing this, where the game keeps its own ad rules, such as remove-ads, pacing or the ad on return, and calls <see cref="AdsSdk"/> under them; Project Settings > RiseOn > Ads edits its fields.<br/>
    /// The pack makes it just before the first scene; a second class implementing this does not compile.<br/>
    /// The pack's source generator seals it and adds <c>Ins</c>, the running instance.<br/>
    /// Its members are for the pack alone: the class implements them explicitly, and no other code can call them, not even through this interface.<br/>
    /// The pack's sample AdsManager holds the rules most of the studio's games run.
    /// </summary>
    public interface IAdsManager {
        /// <summary>
        /// The Editor's stand-in for the pause a device makes while a full-screen ad is up, where no SDK pauses Unity: true as the first ad of a chain shows, false once the last one closed.<br/>
        /// Only the game knows what pausing stops for it; <see cref="EditorGamePause.Apply"/> sets the time scale to zero and pauses the audio, both back as they were after.
        /// </summary>
        protected internal void PauseInEditor(bool paused);
    }
}