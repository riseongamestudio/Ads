using System;
using UnityEngine;

namespace RiseOn.Ads.AdMob.Native.Platforms.IOS {
    internal sealed class RectAdClient : IRectAdClient, INativeAdSharedHandlers {
        private readonly IRectAdCallbacks callbacks;
        private IntPtr handle;
        private int instanceId;
        private Vector2Int positionPx;

        internal RectAdClient(
            RectAd.Settings settings
          , IRectAdCallbacks callbacks) {
            this.callbacks = callbacks;
            instanceId = NativeAdBridge.Register(this);
            handle = NativeAdBridge.RANRectAdCreate(
                settings.AdUnitId
              , settings.CacheSize
              , AdColor.Pack(settings.BackgroundColor)
              , instanceId);
            if (handle == IntPtr.Zero) return;

            NativeAdBridge.RANRectAdSetListener(
                handle
              , NativeAdBridge.OnLoadingStartedCallback
              , NativeAdBridge.OnLoadingCompletedCallback
              , NativeAdBridge.OnAdPaidCallback
              , NativeAdBridge.OnRectAdDisplayedCallback
              , NativeAdBridge.OnRectAdShowNotReadyCallback
              , NativeAdBridge.OnRectAdPresentationFailedCallback);
        }

        public void SetSize(Vector2Int sizePx, int roundCornerPx) {
            if (handle == IntPtr.Zero) return;

            NativeAdBridge.RANRectAdConfigure(
                handle
              , positionPx.x
              , positionPx.y
              , sizePx.x
              , sizePx.y
              , roundCornerPx);
        }

        public void SetPosition(Vector2Int newPositionPx) {
            positionPx = newPositionPx;
            if (handle == IntPtr.Zero) return;

            NativeAdBridge.RANRectAdSetPosition(
                handle
              , newPositionPx.x
              , newPositionPx.y);
        }

        public void Show() {
            if (handle == IntPtr.Zero) return;

            NativeAdBridge.RANRectAdShow(handle);
        }

        public void Hide() {
            if (handle == IntPtr.Zero) return;

            NativeAdBridge.RANRectAdHide(handle);
        }

        public void Release() {
            var releasedHandle = handle;
            handle = IntPtr.Zero;
            if (releasedHandle != IntPtr.Zero) {
                NativeAdBridge.RANRectAdRelease(releasedHandle);
            }
            NativeAdBridge.Unregister(instanceId);
        }

        // Nothing downstream listens for a load beginning, and neither
        // AdMob nor MAX reports one. The trampoline still arrives.
        void INativeAdSharedHandlers.HandleLoadingStarted() {}

        void INativeAdSharedHandlers.HandleLoadingCompleted(
            int errorCode
          , string errorMessage
          , int cachedCount
          , int cacheSize)
            => callbacks.OnLoadingCompleted(
                errorCode
              , errorMessage
              , cachedCount
              , cacheSize);

        void INativeAdSharedHandlers.HandleAdPaid(
            string source
          , string adUnitId
          , double value
          , string currencyCode
          , int precision)
            // Precision is reported by the SDK but nothing downstream asks
            // for it, so it stops here rather than riding along unused.
            => callbacks.OnAdPaid(source, adUnitId, value, currencyCode);

        internal void HandleDisplayed() => callbacks.OnDisplayed();

        internal void HandleShowNotReady() => callbacks.OnShowNotReady();

        internal void HandlePresentationFailed(int errorCode, string errorMessage)
            => callbacks.OnDisplayFailed(errorCode, errorMessage);
    }
}