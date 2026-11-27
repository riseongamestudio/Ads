// The counterpart of a TextView run through ApplyTextMode: two modes and no
// middle ground. Wrapped lays the whole value over at most maxLines lines
// and cuts without an ellipsis when it does not fit - the state validators
// detect and reject. Marquee is one line that scrolls the entire value.
// iOS has no built-in marquee, so this recreates Android's: 1200ms still,
// 30pt per second, a ghost copy a third of the view apart, 1200ms before it
// repeats - the same cadence every Android app shows.

#import <UIKit/UIKit.h>

#import "RANMeasureLayout.h"

NS_ASSUME_NONNULL_BEGIN

@interface RANAdTextLabel : RANLayoutContainerView

@property (nonatomic, copy, nullable) NSString *text;
@property (nonatomic, strong) UIFont *font;
@property (nonatomic, strong) UIColor *textColor;
// Wrapped mode only; ignored while marquee is on.
@property (nonatomic) NSInteger maxLines;
@property (nonatomic, getter=isMarquee) BOOL marquee;
@property (nonatomic) NSTextAlignment textAlignment;
// Wrapped mode only: the lines' height over the font's (1 by default), as
// Android's setLineSpacing multiplier.
@property (nonatomic) CGFloat lineHeightMultiple;
// Wrapped mode only: the last allowed line ends in "…" when the value runs
// past it, as Android's END ellipsis. Off by default: the rect templates
// reject a value that does not fit instead.
@property (nonatomic) BOOL ellipsizes;
// Five stars filled to this score drawn before the text, as the rating's
// line shows them (RANStarsText); 0 draws none. starsOnDark picks the empty
// stars' grey for a dark ground.
@property (nonatomic) CGFloat stars;
@property (nonatomic) BOOL starsOnDark;

// Whether the whole value is readable: always true for marquee (the scroll
// will bring the rest around), and for wrapped only when the allotted lines
// hold everything - the whole-or-scrolling policy check.
- (BOOL)ran_showsEntireTextForWidth:(CGFloat)width;
- (BOOL)ran_fitsWidthWithoutOverflow:(CGFloat)width;

// The height one rendered line takes at the current font.
- (CGFloat)ran_singleLineHeight;

@end

NS_ASSUME_NONNULL_END