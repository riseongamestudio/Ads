// An interstitial or rewarded ad of LevelPlay's own iOS API, whose events reach C# without Unity's player loop.
//
// Unity's bridge hands every event to the player loop, which a pause under a full-screen ad stops, so a cover could never
// come down on a LevelPlay close. Here the delegate hands each event to a serial queue of the pack's, which calls the C# function: in
// order, and never through the player loop.
// The pause itself is the pack's full-screen cover's, which lies under every ad these show.

#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <IronSource/LPMInterstitialAd.h>
#import <IronSource/LPMInterstitialAdDelegate.h>
#import <IronSource/LPMRewardedAd.h>
#import <IronSource/LPMRewardedAdDelegate.h>
#import <IronSource/LPMReward.h>
#import <IronSource/LPMImpressionData.h>
#import <IronSource/LPMImpressionDataDelegate.h>

// The events, in step with the C# side.
typedef NS_ENUM(int, ROLevelPlayEvent) {
    ROLevelPlayEventLoaded = 0,
    ROLevelPlayEventLoadFailed = 1,
    ROLevelPlayEventDisplayed = 2,
    ROLevelPlayEventDisplayFailed = 3,
    ROLevelPlayEventRewarded = 4,
    ROLevelPlayEventClosed = 5,
    ROLevelPlayEventPaid = 6,
};

// owner: the C# side's handle. code: an error's code. first, second: an error's message, or a payment's network and ad
// unit. revenue: a payment's, in US dollars.
typedef void (*ROLevelPlayCallback)(void *owner, int event, int code, const char *first, const char *second, double revenue);

// How long a close waits for the reward of a network that reports it after the close.
static const int64_t ROLevelPlayRewardGraceNs = 1 * NSEC_PER_SEC;

@interface ROLevelPlayFullScreenAd : NSObject <LPMInterstitialAdDelegate, LPMRewardedAdDelegate, LPMImpressionDataDelegate>
@end

@implementation ROLevelPlayFullScreenAd {
    void *_owner;
    ROLevelPlayCallback _callback;
    LPMInterstitialAd *_interstitial;
    LPMRewardedAd *_rewarded;
    // On the serial queue below only.
    BOOL _rewardSeen;
    BOOL _closePending;
    dispatch_queue_t _events;
}

- (instancetype)initWithAdUnitId:(NSString *)adUnitId rewarding:(BOOL)rewarding owner:(void *)owner callback:(ROLevelPlayCallback)callback {
    self = [super init];
    if (self) {
        _owner = owner;
        _callback = callback;
        _events = dispatch_queue_create("com.riseon.ads.levelplay.events", DISPATCH_QUEUE_SERIAL);
        if (rewarding) {
            _rewarded = [[LPMRewardedAd alloc] initWithAdUnitId:adUnitId];
            [_rewarded setDelegate:self];
            [_rewarded setImpressionDataDelegate:self];
        } else {
            _interstitial = [[LPMInterstitialAd alloc] initWithAdUnitId:adUnitId];
            [_interstitial setDelegate:self];
            [_interstitial setImpressionDataDelegate:self];
        }
    }
    return self;
}

// No event reaches C# after this. LevelPlay's iOS ads have no destroy and take no nil delegate: dropped, they go.
- (void)detach {
    _callback = NULL;
    [_rewarded setImpressionDataDelegate:nil];
    [_interstitial setImpressionDataDelegate:nil];
    _rewarded = nil;
    _interstitial = nil;
}

- (void)load {
    if (_rewarded) [_rewarded loadAd];
    else [_interstitial loadAd];
}

- (BOOL)isReady {
    return _rewarded ? [_rewarded isAdReady] : [_interstitial isAdReady];
}

- (void)showWithPlacement:(NSString *)placement {
    dispatch_async(_events, ^{ self->_rewardSeen = NO; });
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController *root = [UIApplication sharedApplication].keyWindow.rootViewController;
        if (self->_rewarded) [self->_rewarded showAdWithViewController:root placementName:placement];
        else [self->_interstitial showAdWithViewController:root placementName:placement];
    });
}

- (void)send:(int)event code:(int)code first:(NSString *)first second:(NSString *)second revenue:(double)revenue {
    if (!_callback) return;

    _callback(_owner, event, code, first ? [first UTF8String] : NULL, second ? [second UTF8String] : NULL, revenue);
}

#pragma mark - Both formats

- (void)didLoadAdWithAdInfo:(LPMAdInfo *)adInfo {
    dispatch_async(_events, ^{ [self send:ROLevelPlayEventLoaded code:0 first:nil second:nil revenue:0]; });
}

- (void)didFailToLoadAdWithAdUnitId:(NSString *)adUnitId error:(NSError *)error {
    dispatch_async(_events, ^{ [self send:ROLevelPlayEventLoadFailed code:(int)error.code first:error.localizedDescription second:nil revenue:0]; });
}

- (void)didDisplayAdWithAdInfo:(LPMAdInfo *)adInfo {
    dispatch_async(_events, ^{ [self send:ROLevelPlayEventDisplayed code:0 first:nil second:nil revenue:0]; });
}

- (void)didFailToDisplayAdWithAdInfo:(LPMAdInfo *)adInfo error:(NSError *)error {
    dispatch_async(_events, ^{ [self send:ROLevelPlayEventDisplayFailed code:(int)error.code first:error.localizedDescription second:nil revenue:0]; });
}

- (void)didCloseAdWithAdInfo:(LPMAdInfo *)adInfo {
    dispatch_async(_events, ^{
        if (!self->_rewarded || self->_rewardSeen) {
            [self send:ROLevelPlayEventClosed code:0 first:nil second:nil revenue:0];
            return;
        }

        self->_closePending = YES;
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, ROLevelPlayRewardGraceNs), self->_events, ^{
            if (!self->_closePending) return;

            self->_closePending = NO;
            [self send:ROLevelPlayEventClosed code:0 first:nil second:nil revenue:0];
        });
    });
}

#pragma mark - Rewarded

- (void)didRewardAdWithAdInfo:(LPMAdInfo *)adInfo reward:(LPMReward *)reward {
    dispatch_async(_events, ^{
        if (self->_rewardSeen) return;

        self->_rewardSeen = YES;
        [self send:ROLevelPlayEventRewarded code:0 first:nil second:nil revenue:0];
        if (!self->_closePending) return;

        self->_closePending = NO;
        [self send:ROLevelPlayEventClosed code:0 first:nil second:nil revenue:0];
    });
}

#pragma mark - Revenue

- (void)impressionDataDidSucceed:(LPMImpressionData *)impressionData {
    NSDictionary *data = [impressionData allData];
    NSString *network = [data[@"adNetwork"] description];
    NSString *unit = [data[@"mediationAdUnitId"] description];
    double revenue = [data[@"revenue"] respondsToSelector:@selector(doubleValue)] ? [data[@"revenue"] doubleValue] : 0;
    dispatch_async(_events, ^{ [self send:ROLevelPlayEventPaid code:0 first:network second:unit revenue:revenue]; });
}

@end

#ifdef __cplusplus
extern "C" {
#endif

void *ROLevelPlayFullScreenAdCreate(const char *adUnitId, bool rewarding, void *owner, ROLevelPlayCallback callback) {
    ROLevelPlayFullScreenAd *ad = [[ROLevelPlayFullScreenAd alloc] initWithAdUnitId:[NSString stringWithUTF8String:adUnitId] rewarding:rewarding owner:owner callback:callback];
    return (__bridge_retained void *)ad;
}

void ROLevelPlayFullScreenAdLoad(void *adRef) {
    [(__bridge ROLevelPlayFullScreenAd *)adRef load];
}

bool ROLevelPlayFullScreenAdIsReady(void *adRef) {
    return [(__bridge ROLevelPlayFullScreenAd *)adRef isReady];
}

void ROLevelPlayFullScreenAdDestroy(void *adRef) {
    ROLevelPlayFullScreenAd *ad = (__bridge_transfer ROLevelPlayFullScreenAd *)adRef;
    [ad detach];
}

void ROLevelPlayFullScreenAdShow(void *adRef, const char *placement) {
    [(__bridge ROLevelPlayFullScreenAd *)adRef showWithPlacement:placement ? [NSString stringWithUTF8String:placement] : nil];
}

#ifdef __cplusplus
}
#endif