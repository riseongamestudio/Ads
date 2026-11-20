using UnityEngine;

namespace RiseOn.Ads.Platforms.IOS {
    internal static class CoverPlatformBootstrap {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install() {
            if (Application.platform is not RuntimePlatform.IPhonePlayer) return;

            CoverPlatform.Install(new CoverPlatformIOS());
        }
    }
}