namespace RiseOn.Ads {
    /// <summary>
    /// The game's ads manager: the one partial class implementing this, where the game keeps its own ad rules, such as remove-ads, pacing or the ad on return, and calls <see cref="AdsSdk"/> under them; Project Settings > RiseOn > Ads edits its fields.<br/>
    /// The class implements this itself, not through an interface of its own, and is neither abstract, generic nor nested; a second such class does not compile.<br/>
    /// A project without one runs the pack's <see cref="DefaultAdsManager"/>, the rules most of the studio's games run; Create Own, beside its script on the page, copies it into the game to change.<br/>
    /// The pack makes it just before the first scene. The pack's source generator seals it and adds <c>Ins</c>, the running instance.<br/>
    /// Its members are for the pack alone: the class implements them explicitly, and no other code can call them, not even through this interface.
    /// </summary>
    public interface IAdsManager {
        /// <summary>
        /// The Editor's stand-in for the pause a device makes while a full-screen ad is up, where no SDK pauses Unity: true as the first ad of a chain shows, false once the last one closed.<br/>
        /// Only the game knows what pausing stops for it; <see cref="EditorGamePause.Apply"/> sets the time scale to zero and pauses the audio, both back as they were after.
        /// </summary>
        protected internal void PauseInEditor(bool paused);

        /// <summary>Once, as the first scene has loaded: where the manager subscribes to <see cref="AdsSdk"/>'s events and shows what the game shows from the start, such as the banner.</summary>
        protected internal void Started();
    }
}