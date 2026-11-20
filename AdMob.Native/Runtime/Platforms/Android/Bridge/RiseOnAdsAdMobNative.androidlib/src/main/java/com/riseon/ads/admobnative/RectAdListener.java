package com.riseon.ads.admobnative;

// The package and method names must match the C# proxy exactly:
// AndroidJavaProxy dispatches by name. The loading and paid events describe
// the pool the rect draws from; the others describe the rect itself.
public interface RectAdListener {
    void OnLoadingStarted();
    void OnLoadingCompleted(
            int errorCode
          , String errorMessage
          , int cachedCount
          , int cacheSize);
    void OnAdPaid(
        String adSource
      , String adUnitId
      , double value
      , String currencyCode
      , int precision);
    void OnDisplayed();
    void OnShowNotReady();
    void OnPresentationFailed(int errorCode, String errorMessage);
}