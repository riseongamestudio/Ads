using System.Runtime.InteropServices;
using UnityEngine;

namespace RiseOn.Ads.Platforms.IOS {
    // The C entry points of ROAdsCover.mm. The two covers are kept apart the
    // whole way down, because both can be up at once. Every entry point hops
    // to the main queue itself, so any thread may call.
    internal sealed class CoverPlatformIOS : ICoverPlatform {
        [DllImport("__Internal")] private static extern void ROAdsFullScreenCoverShow();
        [DllImport("__Internal")] private static extern void ROAdsFullScreenCoverHide();
        [DllImport("__Internal")] private static extern void ROAdsHalfScreenCoverPrepare(float panelHeight);
        [DllImport("__Internal")] private static extern void ROAdsHalfScreenCoverReveal();
        [DllImport("__Internal")] private static extern void ROAdsHalfScreenCoverConceal();
        [DllImport("__Internal")] private static extern void ROAdsHalfScreenCoverRemove();

        // iOS has no Activity, so the pause Android gets for free is taken by
        // hand - and the full-screen cover is the one that takes it. See
        // ROAdsCover.mm for how it holds it against everyone else.
        public void ShowFullScreen() {
            ROAdsFullScreenCoverShow();
        }

        public void HideFullScreen() {
            ROAdsFullScreenCoverHide();
        }

        // Points, fractional ones included: rounding would leave up to a
        // third of a point between the cover and the ad on a 3x screen.
        public void PrepareHalfScreen(float panelHeight) {
            ROAdsHalfScreenCoverPrepare(panelHeight);
        }

        public void RevealHalfScreen() {
            ROAdsHalfScreenCoverReveal();
        }

        public void ConcealHalfScreen() {
            ROAdsHalfScreenCoverConceal();
        }

        public void RemoveHalfScreen() {
            ROAdsHalfScreenCoverRemove();
        }
    }
}