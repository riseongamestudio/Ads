using UnityEngine;
using UnityEngine.Android;

namespace RiseOn.Ads.Platforms.Android {
    internal sealed class CoverPlatformAndroid : ICoverPlatform {
        // One Java class per cover, exactly as on this side.
        private const string FULL_SCREEN_CLASS = "com.riseon.ads.FullScreenCover";
        private const string HALF_SCREEN_CLASS = "com.riseon.ads.HalfScreenCover";

        private readonly object javaStateLock = new();

        private AndroidJavaClass fullScreenClass;
        private AndroidJavaClass halfScreenClass;

        // The full-screen cover is an Activity, so the system pauses Unity
        // behind it without anyone asking - the same pause a full-screen ad
        // Activity gets. Nothing on this side calls UnityPlayer.pause.
        public void ShowFullScreen(Color color) {
            CallFullScreen("Show", AndroidApplication.currentActivity, Argb(color));
        }

        public void HideFullScreen() {
            CallFullScreen("Hide");
        }

        // Pixels, whole: the ad's window is a whole number of them, and the
        // two windows must end on the same line.
        public void PrepareHalfScreen(Color color, float panelHeight) {
            CallHalfScreen(
                "Prepare"
              , AndroidApplication.currentActivity
              , Argb(color)
              , Mathf.RoundToInt(panelHeight));
        }

        public void RevealHalfScreen() {
            CallHalfScreen("Reveal");
        }

        public void ConcealHalfScreen() {
            CallHalfScreen("Conceal");
        }

        public void RemoveHalfScreen() {
            CallHalfScreen("Remove");
        }

        // Packed the way android.graphics.Color reads an int.
        private static int Argb(Color color) {
            Color32 channels = color;
            return channels.a << 24
                 | channels.r << 16
                 | channels.g << 8
                 | channels.b;
        }

        private void CallFullScreen(string methodName, params object[] parameters) {
            lock (javaStateLock) {
                fullScreenClass ??= new(FULL_SCREEN_CLASS);
                fullScreenClass.CallStatic(methodName, parameters);
            }
        }

        private void CallHalfScreen(string methodName, params object[] parameters) {
            lock (javaStateLock) {
                halfScreenClass ??= new(HALF_SCREEN_CLASS);
                halfScreenClass.CallStatic(methodName, parameters);
            }
        }
    }
}