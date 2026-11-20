using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// The ground under a half-screen ad while a chain swaps one ad for the next. It does not pause the game.<br/>
    /// Prepare adds it hidden, Reveal and Conceal only show and hide it where it stands, and Remove takes it away.<br/>
    /// On Android it stays under the ad only because it was added first - the two are windows of the same type, ordered by when they arrived - so Prepare has to come before the ad's Show, from the same thread, and nothing between Prepare and Remove ever adds it again.<br/>
    /// On iOS and in the Editor its place comes from the layer table, so the order of arrival does not matter there.<br/>
    /// Touches are swallowed over the panel only; the strip above it reaches the game.
    /// </summary>
    internal static class HalfScreenCover {
        /// <param name="panelHeight">The ad's own PanelHeight, passed through untouched: pixels on Android, points on iOS, Screen pixels in the Editor. At or below 0 means the whole screen.</param>
        public static void Prepare(Color color, float panelHeight) {
            CoverPlatform.Installed?.PrepareHalfScreen(color, panelHeight);
        }

        public static void Reveal() {
            CoverPlatform.Installed?.RevealHalfScreen();
        }

        public static void Conceal() {
            CoverPlatform.Installed?.ConcealHalfScreen();
        }

        public static void Remove() {
            CoverPlatform.Installed?.RemoveHalfScreen();
        }
    }
}