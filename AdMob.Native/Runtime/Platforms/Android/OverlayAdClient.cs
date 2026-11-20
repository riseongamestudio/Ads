using System;
using UnityEngine;
using UnityEngine.Android;

namespace RiseOn.Ads.AdMob.Native.Platforms.Android {
    internal sealed class OverlayAdClient : IOverlayAdClient {
        private const string JavaClassName         = "com.riseon.ads.admobnative.OverlayAd";
        private const string JavaSetListenerMethod = "SetListener";
        private const string JavaConfigureMethod   = "Configure";
        private const string JavaSetCloseMethod    = "SetClose";
        private const string JavaLoadAdMethod      = "Load";
        private const string JavaShowAdMethod      = "Show";
        private const string JavaHideAdMethod      = "Hide";
        private const string JavaReleaseMethod     = "Release";
        private const string JavaPanelHeightMethod = "ResolvePanelHeight";

        private readonly IOverlayAdCallbacks callbacks;
        private AndroidJavaObject javaObject;
        // Anchor the proxies while the Java side holds them.
        private NativeAdLoadListenerProxy listener;
        private NativeAdCompletedListenerProxy activeShowCompleted;

        internal OverlayAdClient(
            OverlayAdSettings settings
          , IOverlayAdCallbacks callbacks) {
            this.callbacks = callbacks;
            javaObject = new AndroidJavaObject(
                JavaClassName
              , settings.AdUnitId);
            javaObject.Call(
                JavaConfigureMethod
              , settings.FullScreen
              , settings.HeightRatio
              , settings.BackgroundColor
              , settings.CacheSize
              , settings.Close.Cooldown
              , (int)settings.Close.CloseSide
              , (int)settings.Close.TimerSide
              , settings.Close.RedirectOnClose);
            listener = new NativeAdLoadListenerProxy(callbacks);
            javaObject.Call(
                JavaSetListenerMethod
              , new object[] { listener });
        }

        public float PanelHeight
            => javaObject?.Call<float>(
                    JavaPanelHeightMethod
                  , AndroidApplication.currentActivity)
               ?? 0f;

        public void SetClose(in CloseSettings controls) {
            javaObject?.Call(
                JavaSetCloseMethod
              , controls.Cooldown
              , (int)controls.CloseSide
              , (int)controls.TimerSide
              , controls.RedirectOnClose);
        }

        public void Load() {
            javaObject?.Call(
                JavaLoadAdMethod
              , AndroidApplication.currentActivity);
        }

        public void Show(int showId, bool slideIn, bool slideOutOnClose) {
            var completedListener = new NativeAdCompletedListenerProxy(
                (errorMessage, adConsumed, cachedCount) =>
                    callbacks.OnShowCompleted(
                        showId
                      , errorMessage
                      , adConsumed
                      , cachedCount));
            activeShowCompleted = completedListener;
            try {
                javaObject?.Call(
                    JavaShowAdMethod
                  , AndroidApplication.currentActivity
                  , completedListener
                  , slideIn
                  , slideOutOnClose);
            } catch (Exception exception) {
                Debug.LogException(exception);
                // The call never reached Java, so there is no cache news to
                // report. An empty cache is the safe thing to claim: it can
                // only skip an ad, and the next state notification corrects
                // it - claiming a warm one would offer an ad nobody has.
                callbacks.OnShowCompleted(showId, exception.Message, false, 0);
            }
        }

        public void Hide(bool slideOut) {
            javaObject?.Call(JavaHideAdMethod, slideOut);
        }

        public void Release() {
            var releasedObject = javaObject;
            javaObject          = null;
            listener            = null;
            activeShowCompleted = null;
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