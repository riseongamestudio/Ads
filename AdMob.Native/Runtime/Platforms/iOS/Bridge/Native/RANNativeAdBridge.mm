// The transport between Unity C# and the two ad classes. Handles are
// retained bridge casts; Release both tears the ad down and balances the
// retain. Pixels from Unity convert to points here, once, at the boundary.

#import "RANNativeAdBridge.h"

#import <UIKit/UIKit.h>

#import "RANOverlayAd.h"
#import "RANRectAd.h"

static CGFloat RANPointsFromPixels(int32_t px) {
    return px / MAX(1, UIScreen.mainScreen.nativeScale);
}

static NSString *RANStringFromUtf8(const char *value) {
    return value == NULL ? @"" : [NSString stringWithUTF8String:value];
}

static RANNativeAdListenerCallbacks RANMakeCallbacks(
        RANNativeAdLoadingStartedCallback loadingStarted
      , RANNativeAdLoadingCompletedCallback loadingCompleted
      , RANNativeAdPaidCallback adPaid
      , RANNativeAdDisplayedCallback displayed
      , RANNativeAdPresentationFailedCallback presentationFailed
      , RANNativeAdStateChangedCallback stateChanged
      , RANNativeAdShowNotReadyCallback showNotReady) {
    RANNativeAdListenerCallbacks callbacks;
    callbacks.loadingStarted = loadingStarted;
    callbacks.loadingCompleted = loadingCompleted;
    callbacks.adPaid = adPaid;
    callbacks.displayed = displayed;
    callbacks.presentationFailed = presentationFailed;
    callbacks.stateChanged = stateChanged;
    callbacks.showNotReady = showNotReady;
    return callbacks;
}

// ---------------------------------------------------------------------------
// Rect ad
// ---------------------------------------------------------------------------

void* RANRectAdCreate(
        const char* adUnitID
      , int32_t cacheSize
      , int32_t instanceID) {
    NSString *unit = RANStringFromUtf8(adUnitID);
    if (unit.length == 0) return NULL;
    RANRectAd *ad =
            [[RANRectAd alloc] initWithAdUnitID:unit
                                      cacheSize:cacheSize
                                     instanceID:instanceID];
    return (void *)CFBridgingRetain(ad);
}

void RANRectAdSetListener(
        void* handle
      , RANNativeAdLoadingStartedCallback loadingStarted
      , RANNativeAdLoadingCompletedCallback loadingCompleted
      , RANNativeAdPaidCallback adPaid
      , RANNativeAdDisplayedCallback displayed
      , RANNativeAdShowNotReadyCallback showNotReady
      , RANNativeAdPresentationFailedCallback presentationFailed) {
    if (handle == NULL) return;
    RANRectAd *ad = (__bridge RANRectAd *)handle;
    RANRectAdListenerCallbacks callbacks;
    callbacks.loadingStarted = loadingStarted;
    callbacks.loadingCompleted = loadingCompleted;
    callbacks.adPaid = adPaid;
    callbacks.displayed = displayed;
    callbacks.showNotReady = showNotReady;
    callbacks.presentationFailed = presentationFailed;
    [ad setRectAdListenerCallbacks:callbacks];
}

void RANRectAdConfigure(
        void* handle
      , int32_t xPx
      , int32_t yPx
      , int32_t widthPx
      , int32_t heightPx
      , int32_t roundCornerPx) {
    if (handle == NULL) return;
    RANRectAd *ad = (__bridge RANRectAd *)handle;
    [ad configureWithX:RANPointsFromPixels(xPx)
                     y:RANPointsFromPixels(yPx)
                 width:RANPointsFromPixels(widthPx)
                height:RANPointsFromPixels(heightPx)
           roundCorner:RANPointsFromPixels(roundCornerPx)];
}

void RANRectAdShow(void* handle) {
    if (handle == NULL) return;
    [(__bridge RANRectAd *)handle show];
}

void RANRectAdHide(void* handle) {
    if (handle == NULL) return;
    [(__bridge RANRectAd *)handle hide];
}

void RANRectAdSetPosition(
        void* handle
      , int32_t xPx
      , int32_t yPx) {
    if (handle == NULL) return;
    [(__bridge RANRectAd *)handle
            setPositionX:RANPointsFromPixels(xPx)
                       y:RANPointsFromPixels(yPx)];
}

void RANRectAdRelease(void* handle) {
    if (handle == NULL) return;
    RANRectAd *ad = (RANRectAd *)CFBridgingRelease(handle);
    [ad releaseAd];
}

// ---------------------------------------------------------------------------
// Full screen
// ---------------------------------------------------------------------------

void* RANOverlayAdCreate(const char* adUnitID, int32_t instanceID) {
    NSString *unit = RANStringFromUtf8(adUnitID);
    if (unit.length == 0) return NULL;
    RANOverlayAd *ad =
            [[RANOverlayAd alloc] initWithAdUnitID:unit
                                                instanceID:instanceID];
    return (void *)CFBridgingRetain(ad);
}

void RANOverlayAdSetListener(
        void* handle
      , RANNativeAdLoadingStartedCallback loadingStarted
      , RANNativeAdLoadingCompletedCallback loadingCompleted
      , RANNativeAdPaidCallback adPaid
      , RANNativeAdDisplayedCallback displayed
      , RANNativeAdPresentationFailedCallback presentationFailed
      , RANNativeAdStateChangedCallback stateChanged
      , RANNativeAdShowNotReadyCallback showNotReady) {
    if (handle == NULL) return;
    RANOverlayAd *ad = (__bridge RANOverlayAd *)handle;
    [ad setListenerCallbacks:RANMakeCallbacks(
            loadingStarted
          , loadingCompleted
          , adPaid
          , displayed
          , presentationFailed
          , stateChanged
          , showNotReady)];
}

void RANOverlayAdConfigure(
        void* handle
      , bool fullScreen
      , float heightRatio
      , int32_t cacheSize
      , int32_t cooldown
      , int32_t closeSide
      , int32_t timerSide
      , bool redirectOnClose) {
    if (handle == NULL) return;
    [(__bridge RANOverlayAd *)handle
            configureWithFullScreen:fullScreen
                        heightRatio:heightRatio
                          cacheSize:cacheSize
                           cooldown:cooldown
                          closeSide:closeSide
                          timerSide:timerSide
                    redirectOnClose:redirectOnClose];
}

void RANOverlayAdSetLayout(void* handle, int32_t layout) {
    if (handle == NULL) return;
    [(__bridge RANOverlayAd *)handle setLayout:layout];
}

void RANOverlayAdSetClose(
        void* handle
      , int32_t cooldown
      , int32_t closeSide
      , int32_t timerSide
      , bool redirectOnClose) {
    if (handle == NULL) return;
    [(__bridge RANOverlayAd *)handle setCloseWithCooldown:cooldown
                                               closeSide:closeSide
                                               timerSide:timerSide
                                         redirectOnClose:redirectOnClose];
}

void RANOverlayAdLoad(void* handle) {
    if (handle == NULL) return;
    [(__bridge RANOverlayAd *)handle load];
}

float RANOverlayAdResolvePanelHeight(void* handle) {
    if (handle == NULL) return 0;
    return (float)[(__bridge RANOverlayAd *)handle resolvePanelHeight];
}

void RANOverlayAdShow(
        void* handle
      , int32_t showID
      , bool slideIn
      , bool slideOutOnClose
      , RANNativeAdShowCompletedCallback onCompleted) {
    if (handle == NULL) {
        if (onCompleted != NULL) {
            onCompleted(0, showID, "Ad released", false, 0);
        }
        return;
    }
    [(__bridge RANOverlayAd *)handle showWithShowID:showID
                                            slideIn:slideIn
                                    slideOutOnClose:slideOutOnClose
                                        onCompleted:onCompleted];
}

void RANOverlayAdHide(void* handle, bool slideOut) {
    if (handle == NULL) return;
    [(__bridge RANOverlayAd *)handle hideSlidingOut:slideOut];
}

void RANOverlayAdRelease(void* handle) {
    if (handle == NULL) return;
    RANOverlayAd *ad =
            (RANOverlayAd *)CFBridgingRelease(handle);
    [ad releaseAd];
}