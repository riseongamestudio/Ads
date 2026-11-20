using UnityEngine;

namespace RiseOn.Ads.Platforms.Android {
    internal static class CoverPlatformBootstrap {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install() {
            if (Application.platform is not RuntimePlatform.Android) return;

            CoverPlatform.Install(new CoverPlatformAndroid());
        }
    }
}