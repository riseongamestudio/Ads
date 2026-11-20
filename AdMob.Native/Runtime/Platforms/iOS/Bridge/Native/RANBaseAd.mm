#import "RANBaseAd.h"

#include <stdatomic.h>

// Provided by Unity's iOS trampoline at link time.
extern "C" UIViewController* UnityGetGLViewController(void);

const int32_t RANNativeAdInternalLoadError = -1;
const int32_t RANNativeAdInternalPresentationError = -2;

@implementation RANBaseAd {
    atomic_bool _released;
    // Callbacks are read on SDK threads and written from Unity's thread; the
    // struct swap is guarded so a reader never sees half of an update.
    RANNativeAdListenerCallbacks _callbacks;
    NSObject *_callbacksLock;
}

- (instancetype)initWithInstanceID:(int32_t)instanceID {
    self = [super init];
    if (self == nil) return nil;

    _instanceID = instanceID;
    _callbacksLock = [NSObject new];
    atomic_init(&_released, false);
    return self;
}

- (BOOL)released {
    return atomic_load(&_released);
}

- (void)markReleased {
    atomic_store(&_released, true);
}

- (void)setListenerCallbacks:(RANNativeAdListenerCallbacks)callbacks {
    if (self.released) return;
    @synchronized (_callbacksLock) {
        _callbacks = callbacks;
    }
}

- (void)clearListenerCallbacks {
    RANNativeAdListenerCallbacks empty = {0};
    @synchronized (_callbacksLock) {
        _callbacks = empty;
    }
}

- (RANNativeAdListenerCallbacks)currentCallbacks {
    @synchronized (_callbacksLock) {
        return _callbacks;
    }
}

+ (void)runOnMainThread:(dispatch_block_t)action {
    if (action == nil) return;

    if (NSThread.isMainThread) {
        action();
    } else {
        dispatch_async(dispatch_get_main_queue(), action);
    }
}

+ (NSArray<GADAdLoaderOptions *> *)adLoaderOptionsWithStartMuted:(BOOL)startMuted {
    GADVideoOptions *videoOptions = [[GADVideoOptions alloc] init];
    videoOptions.startMuted = startMuted;

    GADNativeAdMediaAdLoaderOptions *mediaOptions =
            [[GADNativeAdMediaAdLoaderOptions alloc] init];
    mediaOptions.mediaAspectRatio = GADMediaAspectRatioAny;

    GADNativeAdViewAdOptions *viewOptions =
            [[GADNativeAdViewAdOptions alloc] init];
    viewOptions.preferredAdChoicesPosition =
            GADAdChoicesPositionTopRightCorner;

    return @[ videoOptions, mediaOptions, viewOptions ];
}

- (void)bindPaidEventForAd:(GADNativeAd *)ad
              paidAdUnitID:(NSString *)paidAdUnitID
               isCurrentAd:(BOOL (^)(void))isCurrentAd {
    __weak RANBaseAd *weakSelf = self;
    __weak GADNativeAd *weakAd = ad;
    ad.paidEventHandler = ^(GADAdValue *_Nonnull adValue) {
        RANBaseAd *strongSelf = weakSelf;
        if (strongSelf == nil
                || strongSelf.released
                || isCurrentAd == nil
                || !isCurrentAd()) {
            return;
        }

        NSString *source = @"";
        GADNativeAd *strongAd = weakAd;
        GADAdNetworkResponseInfo *loadedResponse =
                strongAd.responseInfo.loadedAdNetworkResponseInfo;
        if (loadedResponse.adNetworkClassName != nil) {
            source = loadedResponse.adNetworkClassName;
        }
        // GADAdValue is already denominated in currency units, unlike the
        // Android micros - no division here or the value shrinks 10^6-fold.
        [strongSelf notifyAdPaidWithSource:source
                                  adUnitID:paidAdUnitID
                                     value:adValue.value.doubleValue
                              currencyCode:adValue.currencyCode
                                 precision:(int32_t)adValue.precision];
    };
}

- (void)notifyLoadingStarted {
    RANNativeAdListenerCallbacks callbacks = [self currentCallbacks];
    if (callbacks.loadingStarted == NULL) return;
    callbacks.loadingStarted(self.instanceID);
}

- (void)notifyLoadingCompletedWithCode:(int32_t)errorCode
                               message:(NSString *)errorMessage
                           cachedCount:(int32_t)cachedCount
                             cacheSize:(int32_t)cacheSize {
    RANNativeAdListenerCallbacks callbacks = [self currentCallbacks];
    if (callbacks.loadingCompleted == NULL) return;
    callbacks.loadingCompleted(
            self.instanceID
          , errorCode
          , (errorMessage ?: @"").UTF8String
          , cachedCount
          , cacheSize);
}

- (void)notifyAdPaidWithSource:(NSString *)source
                      adUnitID:(NSString *)adUnitID
                         value:(double)value
                  currencyCode:(NSString *)currencyCode
                     precision:(int32_t)precision {
    RANNativeAdListenerCallbacks callbacks = [self currentCallbacks];
    if (callbacks.adPaid == NULL) return;
    callbacks.adPaid(
            self.instanceID
          , (source ?: @"").UTF8String
          , (adUnitID ?: @"").UTF8String
          , value
          , (currencyCode ?: @"").UTF8String
          , precision);
}

- (void)notifyStateChangedWithReady:(BOOL)isReady loading:(BOOL)isLoading {
    RANNativeAdListenerCallbacks callbacks = [self currentCallbacks];
    if (callbacks.stateChanged == NULL) return;
    callbacks.stateChanged(self.instanceID, isReady, isLoading);
}

- (void)notifyShowNotReady {
    RANNativeAdListenerCallbacks callbacks = [self currentCallbacks];
    if (callbacks.showNotReady == NULL) return;
    callbacks.showNotReady(self.instanceID);
}

- (void)notifyDisplayed {
    RANNativeAdListenerCallbacks callbacks = [self currentCallbacks];
    if (callbacks.displayed == NULL) return;
    callbacks.displayed(self.instanceID);
}

- (void)notifyPresentationFailedWithCode:(int32_t)errorCode
                                 message:(NSString *)errorMessage {
    RANNativeAdListenerCallbacks callbacks = [self currentCallbacks];
    if (callbacks.presentationFailed == NULL) return;
    callbacks.presentationFailed(
            self.instanceID
          , errorCode
          , (errorMessage ?: @"").UTF8String);
}

+ (UIViewController *)unityViewController {
    return UnityGetGLViewController();
}

+ (BOOL)isViewControllerUsable:(UIViewController *)viewController {
    return viewController != nil && viewController.view.window != nil;
}

@end