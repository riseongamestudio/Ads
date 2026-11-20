using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// The pack's window for test builds: the device's advertising ID, to register the device as a test device on the networks' dashboards, and a
    /// button for each running mediation's own debugging tool.<br/>
    /// Nothing opens it but <see cref="Show"/>: a game calls it from its cheat menu, which its store builds do not have.<br/>
    /// Unity's thread only.
    /// </summary>
    public static class AdsDebugger {
        private static AdsDebuggerView view;

        public static bool IsShowing => view != null;

        public static void Show() {
            if (view == null) view = AdsDebuggerView.Create();
        }

        public static void Hide() {
            if (view != null) Object.Destroy(view.gameObject);
            view = null;
        }

        // Static state outlives a play session when the Editor skips the domain reload.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => view = null;
    }
}