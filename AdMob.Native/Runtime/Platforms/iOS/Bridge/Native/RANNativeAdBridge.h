// The contract between Unity C# (DllImport "__Internal") and the iOS native
// ad implementation. It mirrors, call for call, the surface C# already uses
// on Android through AndroidJavaObject - same names, same ordering, same
// semantics - so the C# layer stays one code path with two transports.
//
// Threading: every function may be called from Unity's thread; the native
// side hops to the main thread itself, exactly as the Java side posts to its
// main Handler. Callbacks may arrive on any thread; the C# proxies marshal
// them through MobileAdsEventExecutor, the same way the Android proxies do.
//
// Handles: Create returns a retained opaque handle. Release both tears the
// ad down and balances the retain; no call is valid on a handle after it.
//
// Strings: UTF-8, valid only for the duration of the callback invocation.

#ifndef RAN_NATIVE_AD_BRIDGE_H
#define RAN_NATIVE_AD_BRIDGE_H

#include <stdbool.h>
#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

// ---------------------------------------------------------------------------
// Load listener callbacks - the AdLoadListener interface on Android.
// instanceID is the C#-chosen identity of the managed wrapper; the native
// side stores and echoes it so a late callback can be matched (or dropped)
// against the wrapper generation, mirroring the Android proxy check.
// ---------------------------------------------------------------------------
typedef void (*RANNativeAdLoadingStartedCallback)(int32_t instanceID);
typedef void (*RANNativeAdLoadingCompletedCallback)(
        int32_t instanceID
      , int32_t errorCode
      , const char* errorMessage
      , int32_t cachedCount
      , int32_t cacheSize);
typedef void (*RANNativeAdPaidCallback)(
        int32_t instanceID
      , const char* source
      , const char* adUnitID
      , double value
      , const char* currencyCode
      , int32_t precision);
typedef void (*RANNativeAdDisplayedCallback)(int32_t instanceID);
typedef void (*RANNativeAdPresentationFailedCallback)(
        int32_t instanceID
      , int32_t errorCode
      , const char* errorMessage);
typedef void (*RANNativeAdStateChangedCallback)(
        int32_t instanceID
      , bool isReady
      , bool isLoading);
typedef void (*RANNativeAdShowNotReadyCallback)(int32_t instanceID);

// The AdCompletedListener interface on Android. showID is the C#-side
// show generation, echoed back so a completion can only resolve the show
// that registered it. cachedCount is the cache as it stands after this
// completion, carried along so nobody has to ask for it afterwards.
typedef void (*RANNativeAdShowCompletedCallback)(
        int32_t instanceID
      , int32_t showID
      , const char* errorMessage
      , bool adConsumed
      , int32_t cachedCount);

// ---------------------------------------------------------------------------
// Rect ad - com.riseon.ads.admobnative.RectAd. One handle is one rect the game
// places. Positions and sizes are native screen pixels, exactly what Unity's
// Screen coordinates produce; the native side converts to points internally
// the way the Java side converts to dp.
// ---------------------------------------------------------------------------
void* RANRectAdCreate(
        const char* adUnitID
      , int32_t cacheSize
      , int32_t backgroundColor
      , int32_t instanceID);
void RANRectAdSetListener(
        void* handle
      , RANNativeAdLoadingStartedCallback loadingStarted
      , RANNativeAdLoadingCompletedCallback loadingCompleted
      , RANNativeAdPaidCallback adPaid
      , RANNativeAdDisplayedCallback displayed
      , RANNativeAdShowNotReadyCallback showNotReady
      , RANNativeAdPresentationFailedCallback presentationFailed);
void RANRectAdConfigure(
        void* handle
      , int32_t xPx
      , int32_t yPx
      , int32_t widthPx
      , int32_t heightPx
      , int32_t roundCornerPx);
void RANRectAdShow(void* handle);
void RANRectAdHide(void* handle);
void RANRectAdSetPosition(
        void* handle
      , int32_t xPx
      , int32_t yPx);
void RANRectAdRelease(void* handle);

// ---------------------------------------------------------------------------
// Full screen - com.riseon.ads.admobnative.Overlay.
// ---------------------------------------------------------------------------
void* RANOverlayAdCreate(const char* adUnitID, int32_t instanceID);
void RANOverlayAdSetListener(
        void* handle
      , RANNativeAdLoadingStartedCallback loadingStarted
      , RANNativeAdLoadingCompletedCallback loadingCompleted
      , RANNativeAdPaidCallback adPaid
      , RANNativeAdDisplayedCallback displayed
      , RANNativeAdPresentationFailedCallback presentationFailed
      , RANNativeAdStateChangedCallback stateChanged
      , RANNativeAdShowNotReadyCallback showNotReady);
void RANOverlayAdConfigure(
        void* handle
      , bool fullScreen
      , float heightRatio
      , int32_t backgroundColor
      , int32_t cacheSize
      , int32_t cooldown
      , int32_t closeSide
      , int32_t timerSide
      , bool redirectOnClose);
void RANOverlayAdSetClose(
        void* handle
      , int32_t cooldown
      , int32_t closeSide
      , int32_t timerSide
      , bool redirectOnClose);
void RANOverlayAdLoad(void* handle);
// The half-screen panel's height in points, before any ad has loaded.
float RANOverlayAdResolvePanelHeight(void* handle);
void RANOverlayAdShow(
        void* handle
      , int32_t showID
      , bool slideIn
      , bool slideOutOnClose
      , RANNativeAdShowCompletedCallback onCompleted);
void RANOverlayAdHide(void* handle, bool slideOut);
void RANOverlayAdRelease(void* handle);

#ifdef __cplusplus
}
#endif

#endif // RAN_NATIVE_AD_BRIDGE_H