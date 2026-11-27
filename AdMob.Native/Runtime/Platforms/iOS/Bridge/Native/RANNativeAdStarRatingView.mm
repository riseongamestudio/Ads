// Counterpart of com.riseon.ads.admobnative.NativeAdStarRatingView: five
// stars and the score at 13pt in the theme's grey.

#import "RANNativeAdStarRatingView.h"
#import "RANAdTheme.h"

static const CGFloat kRANRatingTextSize = 13;
static const NSInteger kRANStarCount = 5;
static const CGFloat kRANStarGapRatio = .1;
static const CGFloat kRANStarInnerRadiusRatio = .45;

static UIColor *RANStarColor(uint32_t argb) {
    return [UIColor colorWithRed:((argb >> 16) & 0xFF) / 255.0
                           green:((argb >> 8) & 0xFF) / 255.0
                            blue:(argb & 0xFF) / 255.0
                           alpha:((argb >> 24) & 0xFF) / 255.0];
}

static UIBezierPath *StarPath(CGPoint centre, CGFloat radius) {
    UIBezierPath *path = [UIBezierPath bezierPath];
    CGFloat inner = radius * kRANStarInnerRadiusRatio;
    for (NSInteger point = 0; point < 10; ++point) {
        CGFloat angle = -M_PI_2 + point * M_PI / 5;
        CGFloat r = point % 2 == 0 ? radius : inner;
        CGPoint at = CGPointMake(centre.x + cos(angle) * r, centre.y + sin(angle) * r);
        if (point == 0) [path moveToPoint:at];
        else [path addLineToPoint:at];
    }
    [path closePath];
    return path;
}

UIImage *RANStarsImage(CGFloat rating, CGFloat pointSize, BOOL onDark) {
    CGFloat size = MAX(1, pointSize);
    CGFloat step = size * (1 + kRANStarGapRatio);
    CGSize canvas = CGSizeMake(
            ceil(kRANStarCount * size + (kRANStarCount - 1) * size * kRANStarGapRatio)
          , ceil(size));
    CGFloat filled = MAX(0, MIN(kRANStarCount, rating));
    UIGraphicsImageRenderer *renderer =
            [[UIGraphicsImageRenderer alloc] initWithSize:canvas];
    return [renderer imageWithActions:^(UIGraphicsImageRendererContext *context) {
        for (NSInteger index = 0; index < kRANStarCount; ++index) {
            CGFloat left = index * step;
            UIBezierPath *star = StarPath(
                    CGPointMake(left + size / 2, canvas.height / 2), size / 2);
            [RANStarColor(RANThemeStarEmpty(onDark)) setFill];
            [star fill];
            CGFloat fill = MAX(0, MIN(1, filled - index));
            if (fill <= 0) continue;

            CGContextSaveGState(context.CGContext);
            UIRectClip(CGRectMake(left, 0, size * fill, canvas.height));
            [RANStarColor(kRANThemeStarFilled) setFill];
            [star fill];
            CGContextRestoreGState(context.CGContext);
        }
    }];
}

NSAttributedString *RANStarsText(CGFloat rating, UIFont *font, BOOL onDark) {
    NSTextAttachment *stars = [[NSTextAttachment alloc] init];
    stars.image = RANStarsImage(rating, font.pointSize, onDark);
    CGSize size = stars.image.size;
    // Centred on the digits, not on the line's box.
    stars.bounds = CGRectMake(0, (font.capHeight - size.height) / 2, size.width, size.height);
    return [NSAttributedString attributedStringWithAttachment:stars];
}

@implementation RANNativeAdStarRatingView {
    CGFloat _rating;
}

- (instancetype)initWithFrame:(CGRect)frame {
    self = [super initWithFrame:frame];
    if (self == nil) return nil;

    self.backgroundColor = UIColor.clearColor;
    self.opaque = NO;
    // An asset view: the native ad view under it takes the touch.
    self.userInteractionEnabled = NO;
    uint32_t grey = RANThemeGrey();
    _textSize = kRANRatingTextSize;
    _textColor = [UIColor colorWithRed:((grey >> 16) & 0xFF) / 255.0
                                 green:((grey >> 8) & 0xFF) / 255.0
                                  blue:(grey & 0xFF) / 255.0
                                 alpha:((grey >> 24) & 0xFF) / 255.0];
    self.ran_minimumSize = [self ran_textSize];
    return self;
}

- (void)setRating:(CGFloat)rating {
    _rating = MAX(0, MIN(5, rating));
    self.ran_minimumSize = [self ran_textSize];
    [self setNeedsDisplay];
}

- (CGFloat)rating {
    return _rating;
}

- (void)setTextSize:(CGFloat)textSize {
    _textSize = textSize;
    self.ran_minimumSize = [self ran_textSize];
    [self setNeedsDisplay];
}

- (void)setTextColor:(UIColor *)textColor {
    _textColor = textColor;
    [self setNeedsDisplay];
}

- (NSAttributedString *)ran_text {
    UIFont *font = [UIFont systemFontOfSize:_textSize];
    NSMutableAttributedString *text = [[NSMutableAttributedString alloc]
            initWithAttributedString:RANStarsText(_rating, font, RANThemeIsDark())];
    [text appendAttributedString:[[NSAttributedString alloc]
            initWithString:[NSString stringWithFormat:@" %.1f", _rating]
                attributes:@{
                    NSFontAttributeName: font
                  , NSForegroundColorAttributeName: _textColor
                }]];
    return text;
}

- (CGSize)ran_textSize {
    CGSize size = [[self ran_text] size];
    return CGSizeMake(ceil(size.width), ceil(size.height));
}

- (CGSize)sizeThatFits:(CGSize)size {
    return [self ran_textSize];
}

- (CGSize)intrinsicContentSize {
    return [self ran_textSize];
}

- (void)drawRect:(CGRect)rect {
    NSAttributedString *text = [self ran_text];
    CGSize size = [text size];
    // Left-aligned and centred down the box, as a label sets its line.
    [text drawAtPoint:CGPointMake(0, (self.bounds.size.height - size.height) / 2)];
}

@end