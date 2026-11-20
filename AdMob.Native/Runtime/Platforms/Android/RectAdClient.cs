using System;
using UnityEngine;
using UnityEngine.Android;

namespace RiseOn.Ads.AdMob.Native.Platforms.Android {
    internal sealed class RectAdClient : IRectAdClient {
        private const string JAVA_CLASS_NAME          = "com.riseon.ads.admobnative.RectAd";
        private const string JAVA_SET_LISTENER_METHOD = "SetListener";
        private const string JAVA_CONFIGURE_METHOD    = "Configure";
        private const string JAVA_SHOW_METHOD         = "Show";
        private const string JAVA_HIDE_METHOD         = "Hide";
        private const string JAVA_SET_POSITION_METHOD = "SetPosition";
        private const string JAVA_RELEASE_METHOD      = "Release";

        private AndroidJavaObject javaObject;
        // Anchors the proxy while the Java side holds it.
        private RectAdListenerProxy listener;
        private Vector2Int positionPx;

        internal RectAdClient(
            RectAd.Settings settings
          , IRectAdCallbacks callbacks) {
            javaObject = new AndroidJavaObject(
                JAVA_CLASS_NAME
              , AndroidApplication.currentActivity
              , settings.AdUnitId
              , settings.CacheSize
              , AdColor.Pack(settings.BackgroundColor));
            listener = new RectAdListenerProxy(callbacks);
            javaObject.Call(
                JAVA_SET_LISTENER_METHOD
              , new object[] { listener });
        }

        public void SetSize(Vector2Int sizePx, int roundCornerPx) {
            javaObject?.Call(
                JAVA_CONFIGURE_METHOD
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
                JAVA_SET_POSITION_METHOD
              , newPositionPx.x
              , newPositionPx.y);
        }

        public void Show() {
            javaObject?.Call(
                JAVA_SHOW_METHOD
              , AndroidApplication.currentActivity);
        }

        public void Hide() {
            javaObject?.Call(JAVA_HIDE_METHOD);
        }

        public void Release() {
            var releasedObject = javaObject;
            javaObject = null;
            listener   = null;
            try {
                releasedObject?.Call(JAVA_RELEASE_METHOD);
            } catch (Exception exception) {
                Debug.LogException(exception);
            } finally {
                releasedObject?.Dispose();
            }
        }
    }
}