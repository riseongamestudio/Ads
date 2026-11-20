using System;
using UnityEngine;
using UnityEngine.Android;

namespace RiseOn.Ads.AdMob.Native.Platforms.Android {
    internal sealed class RectAdClient : IRectAdClient {
        private const string JavaClassName         = "com.riseon.ads.admobnative.RectAd";
        private const string JavaSetListenerMethod = "SetListener";
        private const string JavaConfigureMethod   = "Configure";
        private const string JavaShowMethod        = "Show";
        private const string JavaHideMethod        = "Hide";
        private const string JavaSetPositionMethod = "SetPosition";
        private const string JavaReleaseMethod     = "Release";

        private AndroidJavaObject javaObject;
        // Anchors the proxy while the Java side holds it.
        private RectAdListenerProxy listener;
        private Vector2Int positionPx;

        internal RectAdClient(
            RectAd.Settings settings
          , IRectAdCallbacks callbacks) {
            javaObject = new AndroidJavaObject(
                JavaClassName
              , AndroidApplication.currentActivity
              , settings.AdUnitId
              , settings.CacheSize
              , AdColor.Pack(settings.BackgroundColor));
            listener = new RectAdListenerProxy(callbacks);
            javaObject.Call(
                JavaSetListenerMethod
              , new object[] { listener });
        }

        public void SetSize(Vector2Int sizePx, int roundCornerPx) {
            javaObject?.Call(
                JavaConfigureMethod
              , AndroidApplication.currentActivity
              , positionPx.x
              , positionPx.y
              , sizePx.x
              , sizePx.y
              , roundCornerPx);
        }

        public void SetPosition(Vector2Int newPositionPx) {
            positionPx = newPositionPx;
            javaObject?.Call(
                JavaSetPositionMethod
              , newPositionPx.x
              , newPositionPx.y);
        }

        public void Show() {
            javaObject?.Call(
                JavaShowMethod
              , AndroidApplication.currentActivity);
        }

        public void Hide() {
            javaObject?.Call(JavaHideMethod);
        }

        public void Release() {
            var releasedObject = javaObject;
            javaObject = null;
            listener   = null;
            try {
                releasedObject?.Call(JavaReleaseMethod);
            } catch (Exception exception) {
                Debug.LogException(exception);
            } finally {
                releasedObject?.Dispose();
            }
        }
    }
}