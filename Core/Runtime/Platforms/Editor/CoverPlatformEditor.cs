using UnityEngine;

namespace RiseOn.Ads.Platforms.Editor {
    // The preview's covers: one of each size, drawn on canvases at their layers of AdsEditorLayers, so they land where the device
    // puts them - the full-screen cover over the game and under every full-screen ad, the half-screen cover under its ad and over
    // the in-feed cells. Neither pauses the game: the host's full-screen session does, through the manager.
    //
    // What the preview cannot test, it does not pretend to. A full-screen cover on a device takes the game down with it, and a hide
    // scheduled through the player loop never arrives; here the loop keeps running and every hide arrives. A cover that comes down
    // cleanly in the Editor says nothing about whether it comes down on a device.
    internal sealed class CoverPlatformEditor : ICoverPlatform {
        private const string FullScreenObjectName = "Full Screen Cover";
        private const string HalfScreenObjectName = "Half Screen Cover";

        private EditorCover fullScreen;
        private EditorCover halfScreen;

        public void ShowFullScreen(Color color) => EditorThread.Run(() => {
            if (!fullScreen) fullScreen = EditorCover.Create(FullScreenObjectName, AdsEditorLayers.FullScreenCover);
            fullScreen.Apply(color, 0f);
            fullScreen.SetRevealed(true);
        });

        public void HideFullScreen() => EditorThread.Run(() => {
            if (fullScreen) fullScreen.Close();
            fullScreen = null;
        });

        // Screen pixels: the unit the Editor's own surface is measured in,
        // and the one a half-screen ad's preview publishes its height in.
        public void PrepareHalfScreen(Color color, float panelHeight)
            => EditorThread.Run(() => {
                if (!halfScreen) halfScreen = EditorCover.Create(HalfScreenObjectName, AdsEditorLayers.HalfScreenCover);
                halfScreen.Apply(color, panelHeight);
            });

        public void RevealHalfScreen() => EditorThread.Run(() => {
            if (halfScreen) halfScreen.SetRevealed(true);
        });

        public void ConcealHalfScreen() => EditorThread.Run(() => {
            if (halfScreen) halfScreen.SetRevealed(false);
        });

        public void RemoveHalfScreen() => EditorThread.Run(() => {
            if (halfScreen) halfScreen.Close();
            halfScreen = null;
        });
    }
}