#import <UIKit/UIKit.h>

// Unity's own entry points. Declared here rather than pulled in from
// UnityInterface.h for the same reason the Google SDK declares them: the
// trampoline headers are not on a plugin's include path.
extern "C" UIViewController *UnityGetGLViewController(void);
extern "C" void UnityPause(int pause);
extern "C" int UnityIsPaused(void);

static NSString *const kROAdsTag = @"Cover";

// Both covers are SUBVIEWS of the Unity view. Neither is a window of its own
// and neither is presented, and that is the whole design.
//
// A UIWindow with its own windowLevel orders things absolutely, which is
// tempting - but it orders them above EVERYTHING, the ad SDKs' own surfaces
// included, and it leaves the cover a stranger to Unity. Presenting the cover
// from the Unity controller is worse still: a controller can present exactly
// one thing, so a cover sitting in that slot stops a mediation SDK from
// presenting its ad at all - and a cover is typically raised for the length
// of an ad, which is precisely when that slot is wanted.
//
// As subviews they cost nothing to anyone. A presented controller always
// draws above every subview of the controller that presented it, so every
// full-screen ad lands above these covers without being told to, which is the
// order that was wanted anyway.
//
// The full-screen cover pauses the game; the half-screen one never does,
// because the game is still visible above it.
//
// The pause is the whole point of the full-screen cover, and it is also what
// makes it sharp: a paused Unity runs no C#, and Hide IS a C# call. Whatever
// ends a show has to reach Hide from a thread that is still moving - an ad
// SDK's callback delivered on the SDK's own thread, never one marshalled onto
// Unity's main thread or scheduled through the player loop. Route a hide
// through the player loop while this cover is up and the screen stays black
// for good. The Android side carries the same rule and the same warning.

#pragma mark - Layer order

// Where each surface sits among the Unity view's subviews, by a convention
// every package drawing there follows: a surface marks itself with a tier,
// layer.name = @"riseon.layer.<tier>", and goes in directly under the first
// subview, counted from the bottom, whose tier is HIGHER than its own; with
// none, on top. A view with no mark counts as tier 0. No package has to know
// another's views to land in the right place - the table in the README is
// the whole contract, and each package writes this helper for itself.
//
// UIKit hit-tests in the order of the subviews array, not by zPosition, so
// the order has to be the array's own; a zPosition alone would draw one view
// over another and still hand its touches to the one underneath.
static NSString *const kROAdsLayerNamePrefix = @"riseon.layer.";

static const NSInteger kROAdsHalfScreenCoverLayerTier = 1000;
static const NSInteger kROAdsFullScreenCoverLayerTier = 3000;

static NSInteger ROAdsLayerTierOf(UIView *view) {
    NSString *name = view.layer.name;
    if (![name hasPrefix:kROAdsLayerNamePrefix]) return 0;

    return [[name substringFromIndex:kROAdsLayerNamePrefix.length] integerValue];
}

static void ROAdsInsertSubviewAtTier(UIView *host, UIView *view, NSInteger tier) {
    view.layer.name = [NSString stringWithFormat:@"%@%ld"
          , kROAdsLayerNamePrefix
          , (long)tier];
    for (UIView *sibling in host.subviews) {
        if (sibling == view) continue;
        if (ROAdsLayerTierOf(sibling) <= tier) continue;

        [host insertSubview:view belowSubview:sibling];
        return;
    }
    [host addSubview:view];
}

#pragma mark - Pause

// The full-screen cover is the one thing here that stops the game, and it
// holds that stop against everyone else.
//
// UnityPause is a FLAG, not a counter, and every full-screen ad writes it: it
// pauses when its ad opens and RESUMES when its ad closes. The cover goes up
// before such an ad and comes down after it, so the ad's resume lands in the
// middle, leaving an opaque cover on screen with the game free to render and
// play audio behind it.
//
// Nothing notifies anyone of that write, so the only way to hold the pause is
// to look. While the cover is up, this checks once a frame that the game is
// still stopped, and stops it again when it is not. It costs a comparison per
// frame, only for as long as the cover is up, and it can strand nothing: with
// the cover down it asserts nothing and stops running.
//
// Android needs none of this: its cover is an Activity, and the system
// decides what a stacked Activity does to the one below - there is no flag
// for anyone to clear.
static BOOL ROAdsCoverHoldsPause;

@interface ROAdsPauseGuard : NSObject
@end

static CADisplayLink *ROAdsPauseGuardLink;
static BOOL ROAdsPauseCorrectionLogged;

@implementation ROAdsPauseGuard

- (void)roads_tick {
    if (!ROAdsCoverHoldsPause || UnityIsPaused() != 0) return;

    // Once per cover, not once per frame: the correction repeats every frame
    // until whoever cleared the flag stops doing so.
    if (!ROAdsPauseCorrectionLogged) {
        ROAdsPauseCorrectionLogged = YES;
        NSLog(@"%@: the game was resumed from elsewhere while the full-screen "
               "cover was up; pausing it again", kROAdsTag);
    }
    UnityPause(1);
}

@end

static void ROAdsSetPauseGuardRunning(BOOL running) {
    if (running == (ROAdsPauseGuardLink != nil)) return;

    if (!running) {
        [ROAdsPauseGuardLink invalidate];
        ROAdsPauseGuardLink = nil;
        ROAdsPauseCorrectionLogged = NO;
        return;
    }

    ROAdsPauseGuardLink =
            [CADisplayLink displayLinkWithTarget:[[ROAdsPauseGuard alloc] init]
                                        selector:@selector(roads_tick)];
    // Common modes, so a tracking loop anywhere else in the app cannot
    // silence the check for as long as it runs.
    [ROAdsPauseGuardLink addToRunLoop:NSRunLoop.mainRunLoop
                              forMode:NSRunLoopCommonModes];
}

static void ROAdsHoldPause(BOOL hold) {
    ROAdsCoverHoldsPause = hold;
    UnityPause(hold ? 1 : 0);
    ROAdsSetPauseGuardRunning(hold);
}

#pragma mark - Color

static UIColor *ROAdsColorFromARGB(int32_t argb) {
    uint32_t value = (uint32_t)argb;
    return [UIColor colorWithRed:((value >> 16) & 0xFF) / 255.0
                           green:((value >> 8) & 0xFF) / 255.0
                            blue:(value & 0xFF) / 255.0
                           alpha:((value >> 24) & 0xFF) / 255.0];
}

static UIView *ROAdsHostView(void) {
    return UnityGetGLViewController().view;
}

#pragma mark - Full-screen cover

static UIView *ROAdsFullScreenCover = nil;

static void ROAdsShowFullScreen(int32_t argb) {
    UIColor *color = ROAdsColorFromARGB(argb);
    if (ROAdsFullScreenCover != nil) {
        // Already up: repaint rather than stack a second one. The pause is
        // taken again rather than assumed - showing the cover again is the
        // caller saying it wants the game stopped, and the flag set the first
        // time may have been cleared by someone else since.
        ROAdsFullScreenCover.backgroundColor = color;
        ROAdsHoldPause(YES);
        return;
    }

    UIView *hostView = ROAdsHostView();
    if (hostView == nil) return;

    UIView *cover = [[UIView alloc] initWithFrame:hostView.bounds];
    cover.backgroundColor = color;
    // Rotation and split-screen resize the host without anyone calling in, so
    // the cover follows its bounds instead of a size captured at show time.
    cover.autoresizingMask =
            UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
    // It swallows touches on purpose: nothing behind a full cover is meant to
    // be reachable, the game least of all.
    cover.userInteractionEnabled = YES;
    cover.isAccessibilityElement = NO;
    ROAdsFullScreenCover = cover;

    ROAdsHoldPause(YES);

    // Nothing is presented and nothing animates: the cover exists to hide a
    // seam, and a fade of its own would be one more seam to hide.
    ROAdsInsertSubviewAtTier(hostView, cover, kROAdsFullScreenCoverLayerTier);
}

static void ROAdsHideFullScreen(void) {
    UIView *cover = ROAdsFullScreenCover;
    // No cover, no pause of its own to give back - and resuming anyway could
    // restart a game that a full-screen ad is holding stopped.
    if (cover == nil) return;

    ROAdsFullScreenCover = nil;
    ROAdsHoldPause(NO);
    [cover removeFromSuperview];
}

#pragma mark - Half-screen cover

@interface ROAdsHalfScreenCoverView : UIView
@property (nonatomic) CGFloat panelHeight;
@property (nonatomic, strong) UIView *cover;
@end

@implementation ROAdsHalfScreenCoverView

- (instancetype)initWithColor:(UIColor *)color panelHeight:(CGFloat)panelHeight {
    self = [super initWithFrame:CGRectZero];
    if (self == nil) return nil;

    _panelHeight = panelHeight;
    _cover = [[UIView alloc] initWithFrame:CGRectZero];
    _cover.backgroundColor = color;
    _cover.userInteractionEnabled = YES;
    _cover.isAccessibilityElement = NO;
    [self addSubview:_cover];
    return self;
}

// The container fills the host, so rotation and split-screen resize it for
// free; the panel height is applied to the inner view on every layout pass.
//
// The height is the ad's, not a formula of this view's own. A cover that
// measured the host's bounds for itself ended up a different height from the
// ad it sits behind wherever the Unity view is not the whole screen, and the
// surplus showed as an empty band above the ad.
- (void)layoutSubviews {
    [super layoutSubviews];

    CGFloat hostHeight = self.bounds.size.height;
    CGFloat height = self.panelHeight > 0
            ? MIN(self.panelHeight, hostHeight)
            : hostHeight;
    self.cover.frame = CGRectMake(
            0
          , hostHeight - height
          , self.bounds.size.width
          , height);
}

// The strip above the cover has to reach the game. The container spans the
// whole host, so it is asked about the whole screen - and everywhere the
// inner cover is not, it answers "not me", which passes the touch to the
// view behind.
- (UIView *)hitTest:(CGPoint)point withEvent:(UIEvent *)event {
    UIView *hit = [super hitTest:point withEvent:event];
    return hit == self ? nil : hit;
}

@end

static ROAdsHalfScreenCoverView *ROAdsHalfScreenCover = nil;

// Added hidden, at its tier, so it is in place before the ad it stands under
// arrives - and in place whatever order the two arrive in, since the tier
// decides, not the timing. A cover already in place is repainted and resized
// where it stands.
static void ROAdsPrepareHalfScreen(int32_t argb, CGFloat panelHeight) {
    UIColor *color = ROAdsColorFromARGB(argb);
    ROAdsHalfScreenCoverView *existing = ROAdsHalfScreenCover;
    if (existing != nil && existing.superview != nil) {
        existing.cover.backgroundColor = color;
        existing.panelHeight = panelHeight;
        [existing setNeedsLayout];
        return;
    }

    UIView *hostView = ROAdsHostView();
    if (hostView == nil) return;

    ROAdsHalfScreenCoverView *cover =
            [[ROAdsHalfScreenCoverView alloc] initWithColor:color
                                                panelHeight:panelHeight];
    cover.frame = hostView.bounds;
    cover.autoresizingMask =
            UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
    cover.hidden = YES;
    ROAdsHalfScreenCover = cover;
    ROAdsInsertSubviewAtTier(hostView, cover, kROAdsHalfScreenCoverLayerTier);
}

static void ROAdsSetHalfScreenRevealed(BOOL revealed) {
    ROAdsHalfScreenCover.hidden = !revealed;
}

static void ROAdsRemoveHalfScreen(void) {
    ROAdsHalfScreenCoverView *cover = ROAdsHalfScreenCover;
    ROAdsHalfScreenCover = nil;
    [cover removeFromSuperview];
}

#pragma mark - C surface

extern "C" {

void ROAdsFullScreenCoverShow(int32_t argb) {
    dispatch_async(dispatch_get_main_queue(), ^{ ROAdsShowFullScreen(argb); });
}

void ROAdsFullScreenCoverHide(void) {
    dispatch_async(dispatch_get_main_queue(), ^{ ROAdsHideFullScreen(); });
}

void ROAdsHalfScreenCoverPrepare(int32_t argb, float panelHeight) {
    dispatch_async(dispatch_get_main_queue(), ^{
        ROAdsPrepareHalfScreen(argb, panelHeight);
    });
}

void ROAdsHalfScreenCoverReveal(void) {
    dispatch_async(dispatch_get_main_queue(), ^{ ROAdsSetHalfScreenRevealed(YES); });
}

void ROAdsHalfScreenCoverConceal(void) {
    dispatch_async(dispatch_get_main_queue(), ^{ ROAdsSetHalfScreenRevealed(NO); });
}

void ROAdsHalfScreenCoverRemove(void) {
    dispatch_async(dispatch_get_main_queue(), ^{ ROAdsRemoveHalfScreen(); });
}

}