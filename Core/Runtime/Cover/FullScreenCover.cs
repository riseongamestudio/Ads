using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// A flat color over the whole screen, drawn by the platform above everything Unity draws, with the game paused behind it.<br/>
    /// Every full-screen ad still opens above it.<br/>
    /// On Android the cover is an Activity, so the system does the pausing; on iOS the cover holds UnityPause itself and takes it back each frame from any full-screen ad that resumes the game on closing.<br/>
    /// The Editor's cover pauses nothing: the pause there is IEditorPauseHandler's, for the whole full-screen session.<br/>
    /// Keep it up until the ad behind it is completely gone - taking it down resumes the game.<br/>
    /// <see cref="Hide"/> must come from a callback that does not need the player loop: a paused Unity runs none.
    /// </summary>
    internal static class FullScreenCover {
        public static void Show(Color color) {
            CoverPlatform.Installed?.ShowFullScreen(color);
        }

        public static void Hide() {
            CoverPlatform.Installed?.HideFullScreen();
        }
    }
}