using UnityEngine;

namespace RiseOn.Ads.Platforms.Editor {
    internal static class CoverPlatformBootstrap {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Install() {
            if (!Application.isEditor) return;

            CoverPlatform.Install(new CoverPlatformEditor());
            EditorPlaceholders.Install(new EditorPlaceholdersUI());
        }
    }
}