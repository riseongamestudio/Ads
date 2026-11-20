using System;
using UnityEngine;

namespace RiseOn.Ads.AdMob.Native.Platforms.Android {
    internal sealed class NativeAdCompletedListenerProxy : AndroidJavaProxy {
        private const string JavaListenerClassName = "com.riseon.ads.admobnative.NativeAdCompletedListener";

        private readonly Action<string, bool, int> onAdCompleted;

        internal NativeAdCompletedListenerProxy(
            Action<string, bool, int> onAdCompleted)
            : base(JavaListenerClassName) {
            this.onAdCompleted = onAdCompleted;
        }

        public void OnAdCompleted(
            string errorMessage
          , bool adConsumed
          , int cachedCount) {
            onAdCompleted?.Invoke(errorMessage, adConsumed, cachedCount);
        }
    }
}