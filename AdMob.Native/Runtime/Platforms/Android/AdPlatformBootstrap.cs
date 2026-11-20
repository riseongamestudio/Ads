using UnityEngine;

namespace RiseOn.Ads.AdMob.Native.Platforms.Android {
    internal static class AdPlatformBootstrap {
        // This assembly only compiles into Android builds, so it is the one
        // platform present; install before any scene code constructs an ad.
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install() {
            if (Application.platform is not RuntimePlatform.Android) return;

            AdPlatformRegistry.Install(new AdPlatform());
        }
    }
}