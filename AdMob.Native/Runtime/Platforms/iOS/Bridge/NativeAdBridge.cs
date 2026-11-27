using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
namespace RiseOn.Ads.AdMob.Native.Platforms.IOS {
    /// <summary>
    /// The C# side of the Obj-C++ bridge.<br/>
    /// Native calls back through static trampolines (an IL2CPP requirement) that look the client up by its instance id and hand over; the core wrappers marshal to the Unity thread themselves.
    /// </summary>
    internal static class NativeAdBridge {
        internal delegate void LoadingStartedDelegate(int instanceId);
        internal delegate void LoadingCompletedDelegate(
            int instanceId
          , int errorCode
          , string errorMessage
          , int cachedCount
          , int cacheSize);
        internal delegate void AdPaidDelegate(
            int instanceId
          , string source
          , string adUnitId
          , double value
          , string currencyCode
          , int precision);
        internal delegate void DisplayedDelegate(int instanceId);
        internal delegate void PresentationFailedDelegate(
            int instanceId, int errorCode, string errorMessage);
        internal delegate void StateChangedDelegate(
            int instanceId
          , [MarshalAs(UnmanagedType.I1)] bool isReady
          , [MarshalAs(UnmanagedType.I1)] bool isLoading);
        internal delegate void ShowNotReadyDelegate(int instanceId);
        internal delegate void ShowCompletedDelegate(
            int instanceId
          , int showId
          , string errorMessage
          , [MarshalAs(UnmanagedType.I1)] bool adConsumed
          , int cachedCount);

        [DllImport("__Internal")]
        internal static extern IntPtr RANRectAdCreate(
            string adUnitId
          , int cacheSize
          , int instanceId);
        [DllImport("__Internal")]
        internal static extern void RANRectAdSetListener(
            IntPtr handle
          , LoadingStartedDelegate loadingStarted
          , LoadingCompletedDelegate loadingCompleted
          , AdPaidDelegate adPaid
          , DisplayedDelegate displayed
          , ShowNotReadyDelegate showNotReady
          , PresentationFailedDelegate presentationFailed);
        [DllImport("__Internal")]
        internal static extern void RANRectAdConfigure(
            IntPtr handle
          , int xPx
          , int yPx
          , int widthPx
          , int heightPx
          , int roundCornerPx);
        [DllImport("__Internal")]
        internal static extern void RANRectAdShow(IntPtr handle);
        [DllImport("__Internal")]
        internal static extern void RANRectAdHide(IntPtr handle);
        [DllImport("__Internal")]
        internal static extern void RANRectAdSetPosition(
            IntPtr handle, int xPx, int yPx);
        [DllImport("__Internal")]
        internal static extern void RANRectAdRelease(IntPtr handle);

        [DllImport("__Internal")]
        internal static extern IntPtr RANOverlayAdCreate(
            string adUnitId, int instanceId);
        [DllImport("__Internal")]
        internal static extern void RANOverlayAdSetListener(
            IntPtr handle
          , LoadingStartedDelegate loadingStarted
          , LoadingCompletedDelegate loadingCompleted
          , AdPaidDelegate adPaid
          , DisplayedDelegate displayed
          , PresentationFailedDelegate presentationFailed
          , StateChangedDelegate stateChanged
          , ShowNotReadyDelegate showNotReady);
        [DllImport("__Internal")]
        internal static extern void RANOverlayAdConfigure(
            IntPtr handle
          , [MarshalAs(UnmanagedType.I1)] bool fullScreen
          , float heightRatio
          , int cacheSize
          , int cooldown
          , int closeSide
          , int timerSide
          , [MarshalAs(UnmanagedType.I1)] bool redirectOnClose);
        [DllImport("__Internal")]
        internal static extern void RANOverlayAdSetLayout(
            IntPtr handle
          , int layout);
        [DllImport("__Internal")]
        internal static extern void RANOverlayAdSetClose(
            IntPtr handle
          , int cooldown
          , int closeSide
          , int timerSide
          , [MarshalAs(UnmanagedType.I1)] bool redirectOnClose);
        [DllImport("__Internal")]
        internal static extern void RANOverlayAdLoad(IntPtr handle);
        [DllImport("__Internal")]
        internal static extern void RANOverlayAdShow(
            IntPtr handle
          , int showId
          , [MarshalAs(UnmanagedType.I1)] bool slideIn
          , [MarshalAs(UnmanagedType.I1)] bool slideOutOnClose
          , ShowCompletedDelegate onCompleted);
        [DllImport("__Internal")]
        internal static extern void RANOverlayAdHide(
            IntPtr handle
          , [MarshalAs(UnmanagedType.I1)] bool slideOut);
        [DllImport("__Internal")]
        internal static extern float RANOverlayAdResolvePanelHeight(
            IntPtr handle);
        [DllImport("__Internal")]
        internal static extern void RANOverlayAdRelease(IntPtr handle);

        // Static references keep the delegates alive while native code
        // still holds their function pointers.
        internal static readonly LoadingStartedDelegate OnLoadingStartedCallback
            = OnLoadingStarted;
        internal static readonly LoadingCompletedDelegate OnLoadingCompletedCallback
            = OnLoadingCompleted;
        internal static readonly AdPaidDelegate onAdPaidCallback = OnAdPaid;
        internal static readonly DisplayedDelegate OnDisplayedCallback
            = OnDisplayed;
        internal static readonly PresentationFailedDelegate
            OnPresentationFailedCallback = OnPresentationFailed;
        internal static readonly StateChangedDelegate OnStateChangedCallback
            = OnStateChanged;
        internal static readonly ShowNotReadyDelegate OnShowNotReadyCallback
            = OnShowNotReady;
        internal static readonly ShowCompletedDelegate OnShowCompletedCallback
            = OnShowCompleted;
        internal static readonly DisplayedDelegate OnRectAdDisplayedCallback
            = OnRectAdDisplayed;
        internal static readonly ShowNotReadyDelegate
            OnRectAdShowNotReadyCallback = OnRectAdShowNotReady;
        internal static readonly PresentationFailedDelegate
            OnRectAdPresentationFailedCallback = OnRectAdPresentationFailed;

        private static readonly Dictionary<int, object> instances = new();
        private static readonly object registryLock = new();
        private static int nextInstanceId;

        internal static int Register(object client) {
            lock (registryLock) {
                var instanceId = ++nextInstanceId;
                instances[instanceId] = client;
                return instanceId;
            }
        }

        internal static void Unregister(int instanceId) {
            lock (registryLock) {
                instances.Remove(instanceId);
            }
        }

        private static object Find(int instanceId) {
            lock (registryLock) {
                return instances.TryGetValue(instanceId, out var client)
                    ? client
                    : null;
            }
        }

        [MonoPInvokeCallback(typeof(LoadingStartedDelegate))]
        private static void OnLoadingStarted(int instanceId)
            => (Find(instanceId) as INativeAdSharedHandlers)?.HandleLoadingStarted();

        [MonoPInvokeCallback(typeof(LoadingCompletedDelegate))]
        private static void OnLoadingCompleted(
            int instanceId
          , int errorCode
          , string errorMessage
          , int cachedCount
          , int cacheSize)
            => (Find(instanceId) as INativeAdSharedHandlers)
                ?.HandleLoadingCompleted(
                    errorCode
                  , errorMessage
                  , cachedCount
                  , cacheSize);

        [MonoPInvokeCallback(typeof(AdPaidDelegate))]
        private static void OnAdPaid(
            int instanceId
          , string source
          , string adUnitId
          , double value
          , string currencyCode
          , int precision)
            => (Find(instanceId) as INativeAdSharedHandlers)?.HandleAdPaid(
                source, adUnitId, value, currencyCode, precision);

        [MonoPInvokeCallback(typeof(DisplayedDelegate))]
        private static void OnDisplayed(int instanceId)
            => (Find(instanceId) as OverlayAdClient)?.HandleDisplayed();

        [MonoPInvokeCallback(typeof(PresentationFailedDelegate))]
        private static void OnPresentationFailed(
            int instanceId, int errorCode, string errorMessage)
            => (Find(instanceId) as OverlayAdClient)
                ?.HandlePresentationFailed(errorCode, errorMessage);

        [MonoPInvokeCallback(typeof(StateChangedDelegate))]
        private static void OnStateChanged(
            int instanceId, bool isReady, bool isLoading)
            => (Find(instanceId) as OverlayAdClient)
                ?.HandleStateChanged(isReady, isLoading);

        [MonoPInvokeCallback(typeof(ShowNotReadyDelegate))]
        private static void OnShowNotReady(int instanceId)
            => (Find(instanceId) as OverlayAdClient)?.HandleShowNotReady();

        [MonoPInvokeCallback(typeof(ShowCompletedDelegate))]
        private static void OnShowCompleted(
            int instanceId
          , int showId
          , string errorMessage
          , bool adConsumed
          , int cachedCount)
            => (Find(instanceId) as OverlayAdClient)
                ?.HandleShowCompleted(
                    showId, errorMessage, adConsumed, cachedCount);

        [MonoPInvokeCallback(typeof(DisplayedDelegate))]
        private static void OnRectAdDisplayed(int instanceId)
            => (Find(instanceId) as RectAdClient)?.HandleDisplayed();

        [MonoPInvokeCallback(typeof(ShowNotReadyDelegate))]
        private static void OnRectAdShowNotReady(int instanceId)
            => (Find(instanceId) as RectAdClient)?.HandleShowNotReady();

        [MonoPInvokeCallback(typeof(PresentationFailedDelegate))]
        private static void OnRectAdPresentationFailed(
            int instanceId, int errorCode, string errorMessage)
            => (Find(instanceId) as RectAdClient)
                ?.HandlePresentationFailed(errorCode, errorMessage);
    }
}