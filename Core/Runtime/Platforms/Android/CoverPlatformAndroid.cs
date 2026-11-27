using UnityEngine;
using UnityEngine.Android;

namespace RiseOn.Ads.Platforms.Android {
    internal sealed class CoverPlatformAndroid : ICoverPlatform {
        // One Java class per cover, exactly as on this side.
        private const string FullScreenClass = "com.riseon.ads.FullScreenCover";
        private const string HalfScreenClass = "com.riseon.ads.HalfScreenCover";

        private readonly object javaStateLock = new();

        private AndroidJavaClass fullScreenClass;
        private AndroidJavaClass halfScreenClass;

        // The full-screen cover is an Activity, so the system pauses Unity
        // behind it without anyone asking - the same pause a full-screen ad
        // Activity gets. Nothing on this side calls UnityPlayer.pause.
        public void ShowFullScreen() {
            CallFullScreen("Show", AndroidApplication.currentActivity);
        }

        public void HideFullScreen() {
            CallFullScreen("Hide");
        }

        // Pixels, whole: the ad's window is a whole number of them, and the
        // two windows must end on the same line.
        public void PrepareHalfScreen(float panelHeight) {
            CallHalfScreen(
                "Prepare"
              , AndroidApplication.currentActivity
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

        private void CallFullScreen(string methodName, params object[] parameters) {
            lock (javaStateLock) {
                fullScreenClass ??= new(FullScreenClass);
                fullScreenClass.CallStatic(methodName, parameters);
            }
        }

        private void CallHalfScreen(string methodName, params object[] parameters) {
            lock (javaStateLock) {
                halfScreenClass ??= new(HalfScreenClass);
                halfScreenClass.CallStatic(methodName, parameters);
            }
        }
    }
}