package com.riseon.ads.admobnative;

import android.annotation.SuppressLint;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.text.SpannableString;
import android.text.Spanned;
import android.text.TextUtils;
import android.text.style.ReplacementSpan;
import android.util.TypedValue;
import android.widget.TextView;

import java.util.Locale;

// The ad's star rating as every ad of the pack draws it, the rect ad and the
// Ads placeholders alike: five stars filled to the score, as a store's
// listing shows it, then the score in the advertiser's grey. The stars are a
// span in the text, so the line aligns, sizes and ellipsizes as one.
@SuppressLint("AppCompatCustomView")
final class NativeAdStarRatingView extends TextView {
    private static final float TEXT_SIZE_SP = 13f;
    private static final int STAR_COUNT = 5;

    NativeAdStarRatingView(Context context) {
        super(context);
        setTextSize(TypedValue.COMPLEX_UNIT_SP, TEXT_SIZE_SP);
        setTextColor(AdTheme.Grey(context));
        setSingleLine(true);
        setEllipsize(TextUtils.TruncateAt.END);
        setIncludeFontPadding(false);
    }

    void SetRating(float value) {
        setText(RatingText(value, AdTheme.IsDark(getContext())));
        setContentDescription(RatingDescription(value));
    }

    // The stars and the score as a line of text, for any TextView: the rect
    // ad's rating writes it too. onDark picks the empty stars' grey for a
    // dark ground.
    static CharSequence RatingText(float value, boolean onDark) {
        float rating = Clamp(value);
        SpannableString text = new SpannableString(
                String.format(Locale.US, "\uFFFC %.1f", rating));
        text.setSpan(
                new StarsSpan(
                        rating
                      , AdTheme.StarFilled()
                      , AdTheme.StarEmpty(onDark))
              , 0
              , 1
              , Spanned.SPAN_EXCLUSIVE_EXCLUSIVE);
        return text;
    }

    static String RatingDescription(float value) {
        return String.format(Locale.US, "%.1f of %d stars", Clamp(value), STAR_COUNT);
    }

    private static float Clamp(float value) {
        return Math.max(0f, Math.min(STAR_COUNT, value));
    }

    // Five stars a little under the text's size, the first `rating` of them
    // filled, the last one partly.
    private static final class StarsSpan extends ReplacementSpan {
        private static final float STAR_SIZE_RATIO = 1f;
        private static final float GAP_RATIO = .1f;
        private static final float INNER_RADIUS_RATIO = .45f;

        private final float rating;
        private final int filledColor;
        private final int emptyColor;
        private final Path star = new Path();

        StarsSpan(float rating, int filledColor, int emptyColor) {
            this.rating = rating;
            this.filledColor = filledColor;
            this.emptyColor = emptyColor;
        }

        @Override
        public int getSize(
                Paint paint
              , CharSequence text
              , int start
              , int end
              , Paint.FontMetricsInt metrics) {
            float size = paint.getTextSize() * STAR_SIZE_RATIO;
            return Math.round(
                    STAR_COUNT * size + (STAR_COUNT - 1) * size * GAP_RATIO);
        }

        @Override
        public void draw(
                Canvas canvas
              , CharSequence text
              , int start
              , int end
              , float x
              , int top
              , int y
              , int bottom
              , Paint paint) {
            float size = paint.getTextSize() * STAR_SIZE_RATIO;
            float step = size * (1f + GAP_RATIO);
            // Centred on the digits, not on the line's box.
            Paint.FontMetrics metrics = paint.getFontMetrics();
            float centreY = y + (metrics.ascent + metrics.descent) / 2f;
            int color = paint.getColor();
            Paint.Style style = paint.getStyle();
            paint.setStyle(Paint.Style.FILL);
            for (int index = 0; index < STAR_COUNT; ++index) {
                float left = x + index * step;
                StarPath(star, left + size / 2f, centreY, size / 2f);
                paint.setColor(emptyColor);
                canvas.drawPath(star, paint);
                float fill = Math.max(0f, Math.min(1f, rating - index));
                if (fill <= 0f) continue;

                canvas.save();
                canvas.clipRect(left, centreY - size, left + size * fill, centreY + size);
                paint.setColor(filledColor);
                canvas.drawPath(star, paint);
                canvas.restore();
            }
            paint.setColor(color);
            paint.setStyle(style);
        }

        private static void StarPath(Path path, float cx, float cy, float radius) {
            path.reset();
            float inner = radius * INNER_RADIUS_RATIO;
            for (int point = 0; point < 10; ++point) {
                double angle = -Math.PI / 2 + point * Math.PI / 5;
                float r = point % 2 == 0 ? radius : inner;
                float px = cx + (float) (Math.cos(angle) * r);
                float py = cy + (float) (Math.sin(angle) * r);
                if (point == 0) path.moveTo(px, py);
                else path.lineTo(px, py);
            }
            path.close();
        }
    }
}