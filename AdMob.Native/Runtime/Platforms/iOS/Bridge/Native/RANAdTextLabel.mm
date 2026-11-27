#import "RANAdTextLabel.h"
#import "RANNativeAdStarRatingView.h"

// The slack Android spends under the same name, TEXT_BOUNDS_TOLERANCE_PX.
// One point against one pixel: the two are not the same quantity, and the
// pack has now borrowed this number three times without naming it.
static const CGFloat kRANTextBoundsTolerance = 1;
static const NSTimeInterval kRANMarqueeInitialDelaySeconds = 1.2;
static const NSTimeInterval kRANMarqueeRepeatDelaySeconds = 1.2;
static const CGFloat kRANMarqueePointsPerSecond = 30;
// Android separates the ghost from the tail by a third of the view width.
static const CGFloat kRANMarqueeGhostGapRatio = 1.0f / 3.0f;
// A scrolling line fades out over this much at each end, as Android's
// fading edge, rather than cutting a letter in half at the edge.
static const CGFloat kRANMarqueeFadeLength = 12;

@implementation RANAdTextLabel {
    UILabel *_label;
    UILabel *_ghost;
    // Identity of the current animation cycle; bumping it orphans every
    // pending delayed block, the way invalidating a generation does.
    NSInteger _marqueeGeneration;
    // The size the content was last laid out for. A layout pass at the same
    // size leaves a running cycle alone: restarting it on every pass would
    // keep a cell that lays out often from ever scrolling.
    CGSize _laidOutSize;
    BOOL _laidOut;
}

- (instancetype)initWithFrame:(CGRect)frame {
    self = [super initWithFrame:frame];
    if (self == nil) return nil;

    self.clipsToBounds = YES;
    // An asset view: the touch falls through to the native ad view, which
    // reports the click, as Google's native ad guide asks.
    self.userInteractionEnabled = NO;
    _font = [UIFont systemFontOfSize:UIFont.labelFontSize];
    _textColor = UIColor.whiteColor;
    _maxLines = 1;
    _lineHeightMultiple = 1;
    _textAlignment = NSTextAlignmentLeft;
    _label = [[UILabel alloc] init];
    _ghost = [[UILabel alloc] init];
    _ghost.hidden = YES;
    [self addSubview:_label];
    [self addSubview:_ghost];
    [self ran_applyTextConfiguration];
    return self;
}

- (void)setText:(NSString *)text {
    _text = [text copy];
    [self ran_applyTextConfiguration];
}

- (void)setFont:(UIFont *)font {
    _font = font;
    [self ran_applyTextConfiguration];
}

- (void)setTextColor:(UIColor *)textColor {
    _textColor = textColor;
    [self ran_applyTextConfiguration];
}

- (void)setMaxLines:(NSInteger)maxLines {
    _maxLines = MAX(1, maxLines);
    [self ran_applyTextConfiguration];
}

- (void)setMarquee:(BOOL)marquee {
    if (_marquee == marquee) return;
    _marquee = marquee;
    [self ran_applyTextConfiguration];
}

- (void)setTextAlignment:(NSTextAlignment)textAlignment {
    _textAlignment = textAlignment;
    [self ran_applyTextConfiguration];
}

- (void)setLineHeightMultiple:(CGFloat)lineHeightMultiple {
    _lineHeightMultiple = MAX(1, lineHeightMultiple);
    [self ran_applyTextConfiguration];
}

- (void)setEllipsizes:(BOOL)ellipsizes {
    _ellipsizes = ellipsizes;
    [self ran_applyTextConfiguration];
}

- (void)setStars:(CGFloat)stars {
    _stars = stars;
    [self ran_applyTextConfiguration];
}

- (void)setStarsOnDark:(BOOL)starsOnDark {
    _starsOnDark = starsOnDark;
    [self ran_applyTextConfiguration];
}

// One attributed rebuild for every configuration change: full hyphenation in
// wrapped mode matches the Android factory's HYPHENATION_FREQUENCY_FULL, so
// line counts - and with them which candidates fit - agree across platforms.
- (void)ran_applyTextConfiguration {
    ++_marqueeGeneration;
    NSMutableParagraphStyle *paragraph = [[NSMutableParagraphStyle alloc] init];
    paragraph.alignment = _textAlignment;
    if (self.marquee) {
        paragraph.lineBreakMode = NSLineBreakByClipping;
    } else {
        paragraph.lineBreakMode = NSLineBreakByWordWrapping;
        paragraph.hyphenationFactor = 1;
        paragraph.lineHeightMultiple = _lineHeightMultiple;
    }

    NSDictionary *attributes = @{
        NSFontAttributeName: self.font
      , NSForegroundColorAttributeName: self.textColor
      , NSParagraphStyleAttributeName: paragraph
    };
    NSMutableAttributedString *value = [[NSMutableAttributedString alloc]
            initWithString:self.text ?: @""
                attributes:attributes];
    if (_stars > 0) {
        NSMutableAttributedString *stars = [[NSMutableAttributedString alloc]
                initWithAttributedString:RANStarsText(_stars, self.font, _starsOnDark)];
        [stars appendAttributedString:[[NSAttributedString alloc]
                initWithString:@" "
                    attributes:attributes]];
        [stars addAttribute:NSParagraphStyleAttributeName
                      value:paragraph
                      range:NSMakeRange(0, stars.length)];
        [value insertAttributedString:stars atIndex:0];
    }
    _label.attributedText = value;
    _ghost.attributedText = value;
    _label.numberOfLines = self.marquee ? 1 : self.maxLines;
    // The label's own mode applies to its last line only: the lines above
    // still wrap at words.
    _label.lineBreakMode = !self.marquee && _ellipsizes
            ? NSLineBreakByTruncatingTail
            : (self.marquee ? NSLineBreakByClipping : NSLineBreakByWordWrapping);
    _ghost.numberOfLines = 1;
    _ghost.hidden = YES;
    [self setNeedsLayout];
    [self ran_layoutContentAndRestartMarquee];
}

- (void)ran_layoutContentIfResized {
    if (_laidOut && CGSizeEqualToSize(_laidOutSize, self.bounds.size)) return;
    [self ran_layoutContentAndRestartMarquee];
}

- (CGFloat)ran_textNaturalWidth {
    return ceil([_label sizeThatFits:
            CGSizeMake(CGFLOAT_MAX, CGFLOAT_MAX)].width);
}

- (CGFloat)ran_singleLineHeight {
    return ceil(self.font.lineHeight);
}

// Wrapped: the value over its allowed lines, bounded by the width offered.
// Marquee: a single line whose desired width is the whole text, the same
// desired size Android's horizontally scrolling TextView reports; the
// container's EXACTLY spec then clamps it to the slot.
- (CGSize)sizeThatFits:(CGSize)size {
    if (self.marquee) {
        return CGSizeMake(
                [self ran_textNaturalWidth]
              , [self ran_singleLineHeight]);
    }
    CGSize bounded = [_label sizeThatFits:
            CGSizeMake(size.width, CGFLOAT_MAX)];
    return CGSizeMake(ceil(bounded.width), ceil(bounded.height));
}

- (BOOL)ran_showsEntireTextForWidth:(CGFloat)width {
    if (self.marquee) return YES;
    if ((self.text ?: @"").length == 0) return YES;
    if (width <= 0) return NO;

    // The height the whole value would need against the height the allowed
    // line count grants; anything beyond the cap is cut, not shown.
    CGRect unbounded = [_label.attributedText
            boundingRectWithSize:CGSizeMake(width, CGFLOAT_MAX)
                         options:NSStringDrawingUsesLineFragmentOrigin
                         context:nil];
    NSInteger cappedLines = _label.numberOfLines;
    CGFloat allowedHeight = cappedLines <= 0
            ? CGFLOAT_MAX
            : cappedLines * self.font.lineHeight;
    return ceil(unbounded.size.height) <= ceil(allowedHeight) + 1;
}

// A line whose glyphs run past the slot - an unbreakable token, a long URL,
// a compound noun - never ellipsizes, so it slips past every height-based
// check and simply draws outside the box. The Java side walks the layout's
// lines for exactly this and rejects the plan.
//
// There are no per-line metrics to walk here, so the question is asked of
// the TOKENS rather than the paragraph: a wrapping label can only exceed its
// slot when one token is wider than the slot on its own, and a single token
// cannot wrap, so its unconstrained width is unambiguous. Measuring the
// paragraph instead would mean trusting how the typesetter reports a
// fragment too wide to fit, which is not something this pack reads anywhere
// else and not something anyone here can run to find out.
//
// Unknowns answer YES: a check that cannot see the geometry must not reject
// a plan.
- (BOOL)ran_fitsWidthWithoutOverflow:(CGFloat)width {
    if (self.marquee) return YES;

    NSString *value = self.text ?: @"";
    if (value.length == 0 || self.font == nil || width <= 0) return YES;

    NSDictionary *attributes = @{ NSFontAttributeName: self.font };
    NSArray<NSString *> *tokens = [value
            componentsSeparatedByCharactersInSet:
                    NSCharacterSet.whitespaceAndNewlineCharacterSet];
    for (NSString *token in tokens) {
        if (token.length == 0) continue;

        CGFloat tokenWidth = [token sizeWithAttributes:attributes].width;
        if (ceil(tokenWidth) > ceil(width) + kRANTextBoundsTolerance) {
            return NO;
        }
    }
    return YES;
}

- (void)ran_layoutWithFrame:(CGRect)frame {
    [super ran_layoutWithFrame:frame];
    [self ran_layoutContentIfResized];
}

- (void)layoutSubviews {
    [super layoutSubviews];
    [self ran_layoutContentIfResized];
}

// The running scroll is removed, not just its target: UIKit animations are
// additive, so setting the identity under a live one replays the remaining
// distance from the right of the box, and the text leaves the view.
- (void)ran_stopMarquee {
    ++_marqueeGeneration;
    [_label.layer removeAllAnimations];
    [_ghost.layer removeAllAnimations];
    _label.transform = CGAffineTransformIdentity;
    _ghost.transform = CGAffineTransformIdentity;
}

- (void)ran_layoutContentAndRestartMarquee {
    [self ran_stopMarquee];

    CGFloat viewWidth = self.bounds.size.width;
    CGFloat viewHeight = self.bounds.size.height;
    _laidOutSize = self.bounds.size;
    _laidOut = YES;
    self.layer.mask = nil;
    if (!self.marquee) {
        _ghost.hidden = YES;
        _label.frame = CGRectMake(0, 0, viewWidth, viewHeight);
        return;
    }

    CGFloat textWidth = [self ran_textNaturalWidth];
    CGFloat lineHeight = [self ran_singleLineHeight];
    CGFloat textTop = MAX(0, (viewHeight - lineHeight) / 2);
    _label.frame = CGRectMake(
            0, textTop, MAX(textWidth, viewWidth), lineHeight);
    if (textWidth <= viewWidth || viewWidth <= 0) {
        // Fits still; Android's marquee does not move either.
        _ghost.hidden = YES;
        return;
    }

    CGFloat ghostOffset = textWidth + viewWidth * kRANMarqueeGhostGapRatio;
    CGFloat fade = MIN(kRANMarqueeFadeLength / viewWidth, 0.25);
    CAGradientLayer *mask = [CAGradientLayer layer];
    mask.frame = self.bounds;
    mask.startPoint = CGPointMake(0, 0.5);
    mask.endPoint = CGPointMake(1, 0.5);
    mask.colors = @[
        (id)UIColor.clearColor.CGColor
      , (id)UIColor.blackColor.CGColor
      , (id)UIColor.blackColor.CGColor
      , (id)UIColor.clearColor.CGColor ];
    mask.locations = @[ @0, @(fade), @(1 - fade), @1 ];
    self.layer.mask = mask;
    _ghost.hidden = NO;
    _ghost.frame = CGRectMake(ghostOffset, textTop, textWidth, lineHeight);
    [self ran_scheduleMarqueeCycleWithOffset:ghostOffset
                                      delay:kRANMarqueeInitialDelaySeconds
                                 generation:_marqueeGeneration];
}

- (void)ran_scheduleMarqueeCycleWithOffset:(CGFloat)ghostOffset
                                    delay:(NSTimeInterval)delay
                               generation:(NSInteger)generation {
    __weak RANAdTextLabel *weakSelf = self;
    dispatch_after(
            dispatch_time(DISPATCH_TIME_NOW, (int64_t)(delay * NSEC_PER_SEC))
          , dispatch_get_main_queue()
          , ^{
        RANAdTextLabel *strongSelf = weakSelf;
        if (strongSelf == nil
                || generation != strongSelf->_marqueeGeneration) {
            return;
        }

        NSTimeInterval duration = ghostOffset / kRANMarqueePointsPerSecond;
        [UIView animateWithDuration:duration
                              delay:0
                            options:UIViewAnimationOptionCurveLinear
                         animations:^{
            CGAffineTransform scrolled =
                    CGAffineTransformMakeTranslation(-ghostOffset, 0);
            strongSelf->_label.transform = scrolled;
            strongSelf->_ghost.transform = scrolled;
        } completion:^(BOOL finished) {
            RANAdTextLabel *completedSelf = weakSelf;
            if (completedSelf == nil
                    || generation != completedSelf->_marqueeGeneration) {
                return;
            }
            completedSelf->_label.transform = CGAffineTransformIdentity;
            completedSelf->_ghost.transform = CGAffineTransformIdentity;
            if (!finished) return;
            [completedSelf
                    ran_scheduleMarqueeCycleWithOffset:ghostOffset
                                                delay:kRANMarqueeRepeatDelaySeconds
                                           generation:generation];
        }];
    });
}

- (void)willMoveToWindow:(UIWindow *)newWindow {
    [super willMoveToWindow:newWindow];
    if (newWindow == nil) {
        // Orphan pending cycles; layout restarts them on return.
        [self ran_stopMarquee];
    } else {
        [self ran_layoutContentAndRestartMarquee];
    }
}

@end