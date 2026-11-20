namespace RiseOn.Ads.AdMob.Native {
    // Callbacks may arrive on any thread; the wrapper marshals and gates.
    internal interface IRectAdCallbacks {
        void OnLoadingCompleted(
            int errorCode
          , string errorMessage
          , int cachedCount
          , int cacheSize);
        void OnAdPaid(
            string source
          , string adUnitId
          , double value
          , string currencyCode);
        void OnDisplayed();
        void OnShowNotReady();
        void OnDisplayFailed(int errorCode, string errorMessage);
    }
}