#import "RANOverlayAdPresentation.h"

#import "RANLayerOrder.h"
#import "RANOverlayAdContentView.h"

// Unity's own entry point. Declared here rather than pulled in from
// UnityInterface.h for the same reason the Google SDK declares it: the
// trampoline headers are not on a plugin's include path.
extern "C" void UnityPause(int pause);

static NSString *const kRANTag = @"Overlay";

// The slide, both ways. The Android side keeps the same number.
static const NSTimeInterval kRANSlideDuration = 0.25;

// What holds the panel's ground while the panel moves. Two things would let a
// touch there through to the game otherwise: UIKit leaves a view out of hit
// testing while it animates, and UIView's own touch methods hand a touch up
// the responder chain - which, from here, leads to the Unity view. So this
// answers every touch on the panel's resting frame and passes none on.
@interface RANTouchShieldView : UIView
@end

@implementation RANTouchShieldView

- (void)touchesBegan:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event {}
- (void)touchesMoved:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event {}
- (void)touchesEnded:(NSSet<UITouch *> *)touches withEvent:(UIEvent *)event {}
- (void)touchesCancelled:(NSSet<UITouch *> *)touches
               withEvent:(UIEvent *)event {}

@end

// The Activity: a translucent full-screen controller whose whole content is
// the ad face. Status bar and home indicator step back the way the Android
// immersive flags pushed the system bars away.
@interface RANOverlayAdViewController : UIViewController
@property (nonatomic, strong, nullable) RANOverlayAdContentView *contentView;
@end

@implementation RANOverlayAdViewController

- (void)viewDidLoad {
    [super viewDidLoad];
    self.view.backgroundColor = UIColor.clearColor;
    if (self.contentView != nil) {
        self.contentView.frame = self.view.bounds;
        self.contentView.autoresizingMask =
                UIViewAutoresizingFlexibleWidth
                        | UIViewAutoresizingFlexibleHeight;
        [self.view addSubview:self.contentView];
    }
}

- (BOOL)prefersStatusBarHidden {
    return YES;
}

- (BOOL)prefersHomeIndicatorAutoHidden {
    return YES;
}

@end

@implementation RANOverlayAdPresentation {
    __weak UIViewController *_hostViewController;
    GADNativeAd *_nativeAd;
    int32_t _countdownSec;
    BOOL _closeOnLeft;
    BOOL _timerOnLeft;
    BOOL _fullScreen;
    BOOL _redirectOnClose;
    int32_t _layout;
    float _heightRatio;
    int32_t _backgroundColor;

    RANOverlayAdContentView *_contentView;
    RANOverlayAdViewController *_presentedController;
    RANTouchShieldView *_touchShield;
    BOOL _showing;
    BOOL _dismissed;
    BOOL _observingLifecycle;
    BOOL _askedForPause;
    BOOL _slideIn;
    BOOL _slideOutOnClose;
    // On its way in: the countdown has not started and onShow has not fired.
    BOOL _sliding;
    // On its way out: nothing may bring it back or announce it.
    BOOL _leaving;
}

- (instancetype)initWithViewController:(UIViewController *)viewController
                              nativeAd:(GADNativeAd *)nativeAd
                          countdownSec:(int32_t)countdownSec
                           closeOnLeft:(BOOL)closeOnLeft
                           timerOnLeft:(BOOL)timerOnLeft
                            fullScreen:(BOOL)fullScreen
                           heightRatio:(float)heightRatio
                       backgroundColor:(int32_t)backgroundColor
                       redirectOnClose:(BOOL)redirectOnClose
                                layout:(int32_t)layout {
    self = [super init];
    if (self == nil) return nil;

    _hostViewController = viewController;
    _nativeAd = nativeAd;
    _countdownSec = MAX(0, countdownSec);
    _closeOnLeft = closeOnLeft;
    _timerOnLeft = timerOnLeft;
    _fullScreen = fullScreen;
    _heightRatio = heightRatio;
    _backgroundColor = backgroundColor;
    _redirectOnClose = redirectOnClose;
    _layout = layout;
    return self;
}

- (void)dealloc {
    [self ran_stopObservingLifecycle];
}

- (void)setArrivalWithSlideIn:(BOOL)slideIn
              slideOutOnClose:(BOOL)slideOutOnClose {
    _slideIn = slideIn && !_fullScreen;
    _slideOutOnClose = slideOutOnClose && !_fullScreen;
}

- (BOOL)prepare {
    if (_dismissed) return NO;
    if (_contentView != nil) return YES;

    BOOL hasVideoContent = _nativeAd.mediaContent.hasVideoContent;
    CGFloat requestedPanelHeight = [RANOverlayAdContentView
            resolveInitialPanelHeightForFullScreen:_fullScreen
                                       heightRatio:_heightRatio
                                   hasVideoContent:hasVideoContent];
    __weak RANOverlayAdPresentation *weakSelf = self;
    _contentView = [[RANOverlayAdContentView alloc]
            initWithNativeAd:_nativeAd
        countDownRemainingMs:(int64_t)_countdownSec * 1000
                 closeOnLeft:_closeOnLeft
                 timerOnLeft:_timerOnLeft
                  fullScreen:_fullScreen
             backgroundColor:_backgroundColor
             redirectOnClose:_redirectOnClose
        requestedPanelHeight:requestedPanelHeight
                      layout:_layout
                     onClose:^{ [weakSelf ran_closeByPlayer]; }];
    return _contentView != nil;
}

- (BOOL)isLayoutUnrenderable {
    return _contentView != nil && _contentView.layoutUnrenderable;
}

- (BOOL)show {
    if (_dismissed || _showing) return NO;
    if (![self prepare]) return NO;

    UIViewController *host = _hostViewController;
    if (host == nil || host.view.window == nil) {
        NSLog(@"%@: Show rejected because the host controller is gone", kRANTag);
        return NO;
    }

    if (_fullScreen) {
        _presentedController =
                [[RANOverlayAdViewController alloc] init];
        _presentedController.contentView = _contentView;
        _presentedController.modalPresentationStyle =
                UIModalPresentationOverFullScreen;
        _presentedController.modalTransitionStyle =
                UIModalTransitionStyleCrossDissolve;
        // Straight to the Unity controller, the way the Google SDK presents
        // its own full-screen ads - never a blind walk to the end of the
        // presented chain, which would stack this ad on whatever some other
        // SDK happened to be showing. Everything that lives as a subview of
        // the Unity view is underneath this without anyone arranging it.
        //
        // A host already presenting refuses the next presentation with
        // nothing but a console log; this method then used to declare
        // victory anyway - displayed fired, nothing on screen, no close
        // and no completion ever coming. Refusing here, before the pause
        // is asked for, lets the caller fail the show honestly.
        if (host.presentedViewController != nil) return NO;

        // The pause every full-screen ad SDK takes: Unity stops while the
        // ad is up and resumes when it goes. UnityPause is a flag, not a
        // count, so whatever else wants the game stopped across this ad has
        // to hold its own pause and put it back after the resume below.
        _askedForPause = YES;
        UnityPause(1);
        [host presentViewController:_presentedController
                           animated:NO
                         completion:nil];
    } else {
        UIView *hostView = host.view;
        CGFloat panelHeight = _contentView.resolvedPanelHeight;
        _contentView.frame = CGRectMake(
                0
              , hostView.bounds.size.height - panelHeight
              , hostView.bounds.size.width
              , panelHeight);
        _contentView.autoresizingMask =
                UIViewAutoresizingFlexibleWidth
                        | UIViewAutoresizingFlexibleTopMargin;
        // By tier, so the panel lands above the rect ads and whatever
        // else marks itself lower, and below anything marked higher - in
        // whichever order the layers arrived. See RANLayerOrder.h.
        RANInsertSubviewAtTier(hostView, _contentView, kRANHalfScreenAdLayerTier);
    }

    _showing = YES;
    [self ran_startObservingLifecycle];
    if (_slideIn) {
        [self ran_slideIn];
        return YES;
    }
    [_contentView onPresented];
    if (self.onShow != nil) self.onShow();
    return YES;
}

// Up from the bottom edge. The panel is translated, never resized: its frame
// is the panel's own from the first frame to the last.
- (void)ran_slideIn {
    _sliding = YES;
    [self ran_raiseTouchShield];
    _contentView.transform = CGAffineTransformMakeTranslation(
            0, _contentView.bounds.size.height);
    UIView *contentView = _contentView;
    __weak RANOverlayAdPresentation *weakSelf = self;
    [UIView animateWithDuration:kRANSlideDuration
                          delay:0
                        options:UIViewAnimationOptionCurveEaseOut
                                | UIViewAnimationOptionBeginFromCurrentState
                     animations:^{
        contentView.transform = CGAffineTransformIdentity;
    }
                     completion:^(BOOL finished) {
        [weakSelf ran_finishSlideIn];
    }];
}

// Called on an interrupted slide too - a slide out begun mid-way, or the
// view torn down - which the flags turn away.
- (void)ran_finishSlideIn {
    if (!_sliding || _leaving || _dismissed) return;

    _sliding = NO;
    [self ran_lowerTouchShield];
    // The countdown buys watch time, and a panel still on its way in is not
    // being watched yet.
    [_contentView onPresented];
    if (self.onShow != nil) self.onShow();
}

// Over the panel's resting frame, at the panel's own tier, directly above
// it - so it is in place whichever way the panel is moving.
- (void)ran_raiseTouchShield {
    UIView *hostView = _contentView.superview;
    if (_touchShield != nil || hostView == nil) return;

    _touchShield = [[RANTouchShieldView alloc] initWithFrame:_contentView.frame];
    _touchShield.backgroundColor = UIColor.clearColor;
    _touchShield.autoresizingMask = _contentView.autoresizingMask;
    _touchShield.isAccessibilityElement = NO;
    _touchShield.layer.name = _contentView.layer.name;
    [hostView insertSubview:_touchShield aboveSubview:_contentView];
}

- (void)ran_lowerTouchShield {
    [_touchShield removeFromSuperview];
    _touchShield = nil;
}

// The player's own close. A close under RedirectOnClose never slides: the
// redirect is already taking the screen, and a panel still on its way down
// would be the one thing left in sight.
- (void)ran_closeByPlayer {
    [self dismissSlidingOut:_slideOutOnClose && !_redirectOnClose];
}

- (BOOL)isShowing {
    return _showing && !_dismissed;
}

- (void)dismiss {
    [self ran_dismissWithNotify:YES];
}

- (void)dismissSlidingOut:(BOOL)slideOut {
    if (!slideOut || _fullScreen || !_showing || _dismissed
            || _contentView == nil) {
        [self dismiss];
        return;
    }
    if (_leaving) return;

    _leaving = YES;
    _sliding = NO;
    [self ran_raiseTouchShield];
    // A panel caught on its way in turns round where it stands, so the way
    // out is only as long as the distance it has come.
    CGFloat height = _contentView.bounds.size.height;
    CALayer *shown = _contentView.layer.presentationLayer
            ?: _contentView.layer;
    CGFloat remaining = height <= 0
            ? 1
            : MAX(0, MIN(1, 1 - shown.affineTransform.ty / height));
    UIView *contentView = _contentView;
    __weak RANOverlayAdPresentation *weakSelf = self;
    [UIView animateWithDuration:kRANSlideDuration * remaining
                          delay:0
                        options:UIViewAnimationOptionCurveEaseIn
                                | UIViewAnimationOptionBeginFromCurrentState
                     animations:^{
        contentView.transform = CGAffineTransformMakeTranslation(0, height);
    }
                     completion:^(BOOL finished) {
        [weakSelf ran_dismissWithNotify:YES];
    }];
}

- (void)releasePresentation {
    [self ran_dismissWithNotify:NO];
}

- (void)onAdClicked {
    [_contentView commitAdClick];
}

- (void)onAdWillPresentScreen {
    [_contentView adWillPresentScreen];
}

- (void)onAdDidDismissScreen {
    [_contentView adDidDismissScreen];
}

- (void)ran_dismissWithNotify:(BOOL)notify {
    if (_dismissed) return;
    _dismissed = YES;
    _showing = NO;
    _sliding = NO;
    _leaving = YES;
    [self ran_stopObservingLifecycle];

    [self ran_lowerTouchShield];
    [_contentView onPaused];
    [_contentView releaseContent];
    [_contentView removeFromSuperview];
    _contentView = nil;

    RANOverlayAdViewController *presented = _presentedController;
    _presentedController = nil;
    // Only the presentation that paused may resume. A HALF-screen ad closing
    // runs this same teardown, and it must not resume a game that a
    // full-screen ad - or anything else - is holding stopped.
    if (_askedForPause) {
        _askedForPause = NO;
        UnityPause(0);
    }
    if (presented != nil && presented.presentingViewController != nil) {
        [presented dismissViewControllerAnimated:NO completion:nil];
    }

    if (notify && self.onDismiss != nil) self.onDismiss();
}

// The Activity's onPause/onResume pair: the countdown holds its remaining
// time across backgrounding instead of burning through it unseen.
- (void)ran_startObservingLifecycle {
    if (_observingLifecycle) return;
    _observingLifecycle = YES;
    [NSNotificationCenter.defaultCenter
            addObserver:self
               selector:@selector(ran_applicationWillResignActive)
                   name:UIApplicationWillResignActiveNotification
                 object:nil];
    [NSNotificationCenter.defaultCenter
            addObserver:self
               selector:@selector(ran_applicationDidBecomeActive)
                   name:UIApplicationDidBecomeActiveNotification
                 object:nil];
}

- (void)ran_stopObservingLifecycle {
    if (!_observingLifecycle) return;
    _observingLifecycle = NO;
    [NSNotificationCenter.defaultCenter removeObserver:self];
}

- (void)ran_applicationWillResignActive {
    if (_showing) [_contentView onPaused];
}

- (void)ran_applicationDidBecomeActive {
    // A panel still sliding starts its countdown when it arrives, not here.
    if (_showing && !_sliding && !_leaving) [_contentView onPresented];
}

@end