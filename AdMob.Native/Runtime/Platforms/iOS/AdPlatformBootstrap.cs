using UnityEngine;

namespace RiseOn.Ads.AdMob.Native.Platforms.IOS {
    internal static class AdPlatformBootstrap {
        // This assembly only compiles into iOS builds, so it is the one
        // platform present; install before any scene code constructs an ad.
        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install() {
            if (Application.platform is not RuntimePlatform.IPhonePlayer) return;

            AdPlatformRegistry.Install(new AdPlatform());
        }
    }
}