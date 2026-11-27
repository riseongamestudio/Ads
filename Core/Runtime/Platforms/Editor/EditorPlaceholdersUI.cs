using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RiseOn.Ads.Platforms.Editor {
    /// <summary>
    /// The pack's placeholders as Play mode draws them, each on a canvas of its own at its layer of <see cref="AdsEditorLayers"/>, sized in device points as on a phone 360 points across.<br/>
    /// Each is laid out the way the pack's own native ads are, a layout drawn from many networks' ads, whatever the mediation, in the light or dark appearance the Editor's theme has,
    /// filled as a test ad: the mediation that would serve it as the advertiser, with its icon, and the format's name on the picture.<br/>
    /// An interstitial fills the screen, as the networks' own do, and closes at once. A rewarding format is an app's store page with its video, the reward counted down
    /// for five seconds in a pill that closes it once earned, then its end card. An app open ad is a bar back to the game over the same card, the game dimmed behind,
    /// and closes from that bar once two seconds have passed. The interstitial and the card hold the same: the picture at the top, or the left on a wide screen, then the logo,
    /// the headline, the advertiser, the pitch and an Install pill, centred.<br/>
    /// A banner and an MRec sit where the page's Formats section places them in the safe area.
    /// </summary>
    internal sealed class EditorPlaceholdersUI : IEditorPlaceholders {
        private const float RewardSeconds = 5;

        // AdMob may hold an app open ad's close up to two seconds, showing no countdown.
        private const float AppOpenCloseSeconds = 2;

        // The part of an adaptive icon's 108dp layers a launcher shows: the middle 72dp.
        private static readonly Rect adaptiveVisible = new(1 / 6f, 1 / 6f, 2 / 3f, 2 / 3f);

        // Where each mediation pack keeps its icon, from the pack's folder.
        private const string IconPath = "/Editor/MediationIcon.png";

        private const string Headline = "Test Ad";
        private const string Install  = "Install";
        // Five stars filled to the score in Google's yellow, then the score, as the pack's native ads draw the rating.
        private const string Rating   = "<color=#FBBC04>★★★★★</color> 4.8";

        // Ads in a device's light and dark appearance, as Material 3 colors them.
        private static readonly Palette light = new() {
            Page = Color.white, Surface = Color.white, Ink = new(.12f, .12f, .12f, 1), Grey = new(.27f, .28f, .27f, 1), Line = new(.88f, .89f, .9f, 1),
            Primary = new(.04f, .34f, .82f, 1), OnPrimary = Color.white
        };

        private static readonly Palette dark = new() {
            Page = new(.21f, .2f, .21f, 1), Surface = new(.16f, .16f, .17f, 1), Ink = new(.89f, .89f, .89f, 1), Grey = new(.77f, .78f, .77f, 1), Line = new(.33f, .33f, .35f, 1),
            // The call to action stays a saturated fill on a dark page too: a pale one reads as the weakest thing there.
            Primary = new(.1f, .45f, .91f, 1), OnPrimary = Color.white
        };

        // The appearance of the placeholder being built: the Editor's own theme, read as it shows.
        private static Palette colors = light;

        // The marks' colors, the same in either appearance, as the pack's native ads draw them: the Ad tag's grey holds white text at 6:1.
        private static readonly Color tag      = new(.373f, .388f, .408f, 1);

        // The close and the timer are tonal discs, as the pack's native ads draw them: a faint tint of what is behind them (#14000000, or #24FFFFFF over
        // what is dark) and a glyph in the theme's secondary ink. The tint is blended here over the backdrop, opaque, so a linear project shows the
        // device's color.
        private static (Color Disc, Color Ink) Disc(Color backdrop) {
            var dark = backdrop.grayscale < .5f;
            var fill = Color.Lerp(backdrop, dark ? Color.white : Color.black, dark ? .14f : .078f);
            fill.a   = 1;
            return (fill, dark ? new Color(.89f, .89f, .89f, 1) : new Color(.267f, .278f, .275f, 1));
        }

        private GameObject banner;
        private GameObject mRec;

        public void ShowFullScreen(Type mediation, AdFormat format, IAdShowListener listener) => EditorThread.Run(() => {
            colors = Theme();
            var ad   = new Content(mediation, format);
            var root = Canvas("Ad Placeholder", AdsEditorLayers.FullScreenAd);
            // An app open ad dims the game rather than hiding it: the game is right there, and the card still reads on its own. An interstitial is the theme's
            // surface edge to edge; a rewarding format's page draws its own.
            var back = Box(root.transform, format switch {
                AdFormat.AppOpen                                      => Shade(.72f)
              , AdFormat.Rewarded or AdFormat.RewardedInterstitial => Color.black
              , _                                                     => colors.Surface
            });
            Stretch(back.rectTransform);
            back.raycastTarget = true;

            var area   = SafeArea(root.transform);
            var holder = root.AddComponent<EditorFullScreenPlaceholder>();
            switch (format) {
                case AdFormat.Rewarded:
                case AdFormat.RewardedInterstitial:
                    Rewarded(back.rectTransform, area, ad, holder, listener);
                    break;
                case AdFormat.AppOpen:
                    AppOpen(area, ad, holder, listener);
                    break;
                default:
                    Interstitial(area, ad, holder, listener);
                    break;
            }

            listener.OnDisplayed();
        });

        // An adaptive banner spans the safe area; its height stays the standard one, as the network's own varies with the device.
        public void ShowBanner(Type mediation, AdBannerSettings settings) => EditorThread.Run(() => {
            if (banner) Object.Destroy(banner);
            colors = Theme();
            banner = Banner(new Content(mediation, AdFormat.Banner), settings.Adaptive, settings.Position);
        });

        public void HideBanner() => EditorThread.Run(() => {
            if (banner) Object.Destroy(banner);
            banner = null;
        });

        public void ShowMRec(Type mediation, AdMRecSettings settings) => EditorThread.Run(() => {
            if (mRec) Object.Destroy(mRec);
            colors = Theme();
            mRec = MRec(new Content(mediation, AdFormat.MRec), settings.Position);
        });

        public void HideMRec() => EditorThread.Run(() => {
            if (mRec) Object.Destroy(mRec);
            mRec = null;
        });

        // The card's arrangement spread over the screen, the close disc in its top right corner.
        private static void Interstitial(RectTransform area, Content ad, EditorFullScreenPlaceholder holder, IAdShowListener listener) {
            Card(area, ad, 0, 0, true);
            var close = Close(area, colors.Surface);
            holder.Begin(listener, 0, close.Button, null, close.Cross, null, null, null);
        }

        // An app's video ad as a store page: its icon and name, its ratings, the video, the pitch and a wide Install button in reach of the thumb;
        // the reward counted down in a pill at the top right that closes it once earned; then the end card over the page.
        private static void Rewarded(RectTransform back, RectTransform area, Content ad, EditorFullScreenPlaceholder holder, IAdShowListener listener) {
            back.GetComponent<Image>().color = colors.Page;
            var column = new GameObject("Page", typeof(RectTransform)).GetComponent<RectTransform>();
            column.SetParent(area, false);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment         = TextAnchor.UpperCenter;
            layout.spacing                = 18 * Points();
            layout.childControlWidth      = layout.childControlHeight = true;
            layout.childForceExpandWidth  = true;
            layout.childForceExpandHeight = false;

            // The app: its icon, its name over the tagline, the store's mark.
            var app = Fixed(column, 80);
            Icon(app, ad.Icon, ad.Advertiser, new Vector2(0, .5f), new Vector2(20, 0), 80, 18);
            Place(Label(app, Headline, 26, colors.Ink, FontStyle.Bold, TextAnchor.UpperLeft).rectTransform, new Vector2(0, 1), new Vector2(116, -2), new Vector2(228, 34));
            Place(Label(app, $"{ad.Advertiser} · {FormatName(ad.Format)}", 14, colors.Grey, FontStyle.Normal, TextAnchor.UpperLeft).rectTransform, new Vector2(0, 1), new Vector2(116, -38), new Vector2(228, 20));
            Store(app, new Vector2(116, 8), 13);

            // The store's figures between two hairlines: the rating, the category, the developer.
            var figures = Fixed(column, 66);
            Hairline(figures, 16, true);
            Hairline(figures, 16, false);
            Figure(figures, 0, "RATINGS", "★ 4.8");
            Figure(figures, 1, "CATEGORY", "Games");
            Figure(figures, 2, "DEVELOPER", ad.Advertiser);

            // The video, its sound and pause over its top left corner, the AdChoices at its top right. On a tall screen its band takes the height
            // the page leaves, the video across its middle on the creative's darkest color, so the page has no dead gap.
            var video = new GameObject("Video", typeof(RectTransform), typeof(RectMask2D), typeof(EditorAspectHeight)).GetComponent<RectTransform>();
            video.SetParent(column, false);
            var band = video.GetComponent<EditorAspectHeight>();
            Stretch(Box(video, ad.From).rectTransform);
            var media = Media(video, ad);
            media.anchorMin = new Vector2(0, .5f);
            media.anchorMax = new Vector2(1, .5f);
            media.offsetMin = media.offsetMax = Vector2.zero;
            var across = media.gameObject.AddComponent<AspectRatioFitter>();
            across.aspectMode  = AspectRatioFitter.AspectMode.WidthControlsHeight;
            across.aspectRatio = 16 / 9f;
            Glyph(media, false, new Vector2(10, -10));
            Glyph(media, true, new Vector2(46, -10));
            AdChoices(media, Vector2.one, new Vector2(-10, -17.5f));

            var pitch = Fixed(column, 64);
            var body  = Label(pitch, ad.Body, 15, colors.Grey, FontStyle.Normal, TextAnchor.UpperCenter);
            Stretch(body.rectTransform);
            body.rectTransform.offsetMin = new Vector2(28, 0) * Points();
            body.rectTransform.offsetMax = new Vector2(-28, 0) * Points();

            var install = Cta(area, 16, Install, colors.Primary, colors.OnPrimary, 24, ad.Url);

            // A tall screen stacks the page down a column no wider than a store page on a tablet; a wide one puts the video at the left,
            // as tall as fits under the pill, and the app, its figures, the pitch and Install down the right.
            Responsive(area, size => {
                var wide = size.x > size.y;
                layout.spacing = (wide ? 6 : 18) * Points();
                band.Flexible = !wide;
                if (!wide) {
                    video.SetParent(column, false);
                    video.SetSiblingIndex(2);
                    var margin = Mathf.Max(0, (size.x - 600) / 2);
                    Span(column, margin, margin, 84, 56, true);
                    margin = Mathf.Max(24, (size.x - 552) / 2);
                    Span(install, margin, margin, 20, 48);
                    return;
                }

                var width = Mathf.Min(size.x / 2 - 32, (size.y - 72) * 16 / 9);
                video.SetParent(area, false);
                Place(video, new Vector2(0, .5f), new Vector2(16, -8), new Vector2(width, width * 9 / 16));
                var left = 16 + width + 16;
                Span(column, left, 16, 76, 56, true);
                Span(install, left + 8, 24, 16, 48);
            });

            var end = EndCard(area, ad);
            Tag(area, "Ad", new Vector2(0, 1), new Vector2(12, -18));
            var reward = RewardPill(area);
            holder.Begin(listener, RewardSeconds, reward.Button, null, reward.Cross, reward.Text, null, end);
        }

        // The pill a rewarded ad counts down in, at the top right, level with the Ad tag: its text, then, once the reward is earned, the close cross at its round end,
        // and the whole pill closes the ad. Colored as every format's close disc, over the page.
        private static (Button Button, Text Text, GameObject Cross) RewardPill(RectTransform area) {
            var (fill, ink) = Disc(colors.Page);
            var body        = Box(area, fill, 18);
            body.raycastTarget = true;
            Place(body.rectTransform, Vector2.one, new Vector2(-12, -9), new Vector2(206, 36));
            var text = Label(body.transform, "", 14, ink, FontStyle.Normal, TextAnchor.MiddleLeft);
            Span(text.rectTransform, 16, 40, 0, 0, true);
            var end = Group(body.transform);
            Place(end, new Vector2(1, .5f), new Vector2(-3, 0), new Vector2(30, 30));
            Cross(end, ink);
            var button = body.gameObject.AddComponent<Button>();
            // Held without a sign, as Google holds it: no disabled tint.
            button.transition = Selectable.Transition.None;
            return (button, text, end.gameObject);
        }

        // What a rewarded video ends on: the page dimmed as the app open ad dims the game, and in its middle a card of the app: its icon, its name, its store,
        // and Install across it. Hidden until then; the Ad tag and the pill stay over the veil.
        private static GameObject EndCard(RectTransform area, Content ad) {
            var group = Group(area);
            var veil  = Box(group, Shade(.72f)).rectTransform;
            var safe  = Screen.safeArea;
            Stretch(veil);
            veil.offsetMin = -safe.min;
            veil.offsetMax = new Vector2(Screen.width - safe.xMax, Screen.height - safe.yMax);

            var center = new Vector2(.5f, .5f);
            // From the top: 32, the icon, 24, the name, 16, the store, 32, Install, 32.
            var card   = Box(group, colors.Page, 28).rectTransform;
            Place(card, center, Vector2.zero, new Vector2(296, 344));
            Icon(card, ad.Icon, ad.Advertiser, center, new Vector2(0, 88), 104, 24);
            Place(Label(card, Headline, 28, colors.Ink, FontStyle.Bold, TextAnchor.MiddleCenter).rectTransform, center, new Vector2(0, -7), new Vector2(256, 38));
            Store(card, Vector2.zero, 14, center, new Vector2(18, -51));
            Place(Cta(card, 16, Install, colors.Primary, colors.OnPrimary, 24, ad.Url), center, new Vector2(0, -116), new Vector2(248, 48));
            // Shrunk to fit a screen on its side.
            Responsive(area, size => card.localScale = Vector3.one * Mathf.Min(1, (size.y - 24) / 344));
            group.gameObject.SetActive(false);
            return group.gameObject;
        }

        // One of the store page's figures, in the column at its index of three: the caption over the value, on the same lines in every column; a divider before all but the first.
        private static void Figure(RectTransform parent, int index, string caption, string value) {
            var cell = new GameObject(caption, typeof(RectTransform)).GetComponent<RectTransform>();
            cell.SetParent(parent, false);
            cell.anchorMin = new Vector2(index / 3f, 0);
            cell.anchorMax = new Vector2((index + 1) / 3f, 1);
            cell.offsetMin = cell.offsetMax = Vector2.zero;
            Place(Label(cell, caption, 10, colors.Grey, FontStyle.Normal, TextAnchor.MiddleCenter).rectTransform, new Vector2(.5f, .5f), new Vector2(0, 12), new Vector2(110, 14));
            Place(Label(cell, value, 16, colors.Ink, FontStyle.Bold, TextAnchor.MiddleCenter).rectTransform, new Vector2(.5f, .5f), new Vector2(0, -8), new Vector2(110, 22));
            if (index == 0) return;

            var divider = Box(cell, colors.Line).rectTransform;
            Place(divider, new Vector2(0, .5f), Vector2.zero, new Vector2(1, 30));
        }

        // No cross, but a bar back to the game at the top, the theme's surface with its left end round, running off the right edge and as long as it holds:
        // the game's icon and name, then in one slot a spinner while the close is held and Continue to app after. The bar says where the way out leads,
        // which a cross does not; under it the card, the game dimmed around both.
        private static void AppOpen(RectTransform area, Content ad, EditorFullScreenPlaceholder holder, IAdShowListener listener) {
            const float barHeight = 48, iconSize = 26, nameLeft = 14 + iconSize + 10, nameGap = 24, chevronSize = 18;
            var back = Box(area, colors.Surface, barHeight / 2);
            back.raycastTarget = true;
            var button = back.gameObject.AddComponent<Button>();
            // Held without a sign, as AdMob holds it: no disabled tint.
            button.transition = Selectable.Transition.None;
            var bar = back.rectTransform;

            var game = GameIcon();
            var tile = Icon(bar, game.Length > 0 ? game[0] : null, Application.productName, new Vector2(0, .5f), new Vector2(14, 0), iconSize, 6, true);
            if (game.Length > 1) {
                tile.GetComponentInChildren<RawImage>().uvRect = adaptiveVisible;
                var front = new GameObject("Foreground", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
                front.transform.SetParent(tile, false);
                front.texture       = game[1];
                front.uvRect        = adaptiveVisible;
                front.raycastTarget = false;
                Stretch(front.rectTransform);
            }

            // A long name gives way first.
            var title     = Label(bar, Application.productName, 15, colors.Ink, FontStyle.Normal, TextAnchor.MiddleLeft);
            var nameWidth = Mathf.Min(title.preferredWidth / Points() + 2, 140);
            Place(title.rectTransform, new Vector2(0, .5f), new Vector2(nameLeft, 0), new Vector2(nameWidth, 24));

            // The slot is as wide as Continue to app, so the bar keeps its length when the spinner gives way to it.
            var ready     = Group(bar);
            var go        = Label(ready, "Continue to app", 15, colors.Ink, FontStyle.Normal, TextAnchor.MiddleLeft);
            var goWidth   = go.preferredWidth / Points() + 2;
            var slotLeft  = nameLeft + nameWidth + nameGap;
            var slotWidth = goWidth + chevronSize;
            Place(go.rectTransform, new Vector2(0, .5f), new Vector2(slotLeft, 0), new Vector2(goWidth, 24));
            var chevron = new GameObject("Chevron", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorChevronGraphic)).GetComponent<EditorChevronGraphic>();
            chevron.transform.SetParent(ready, false);
            chevron.color         = colors.Ink;
            chevron.raycastTarget = false;
            Place(chevron.rectTransform, new Vector2(0, .5f), new Vector2(slotLeft + goWidth, 0), new Vector2(chevronSize, chevronSize));

            var waiting = Group(bar);
            var spinner = new GameObject("Spinner", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorSpinnerGraphic)).GetComponent<EditorSpinnerGraphic>();
            spinner.transform.SetParent(waiting, false);
            spinner.color         = colors.Primary;
            spinner.raycastTarget = false;
            // At the slot's right end, turning about its own middle.
            Place(spinner.rectTransform, new Vector2(0, .5f), new Vector2(slotLeft + slotWidth - 10, 0), new Vector2(20, 20));
            spinner.rectTransform.pivot = new Vector2(.5f, .5f);

            Place(bar, Vector2.one, new Vector2(barHeight / 2, -8), new Vector2(slotLeft + slotWidth + 8 + barHeight / 2, barHeight));

            Card(area, ad, 16, 8 + barHeight + 16);
            holder.Begin(listener, AppOpenCloseSeconds, button, null, ready.gameObject, null, null, null, waiting.gameObject);
        }

        // The ad as one card, as the pack's native ads draw it, centred between the given points from the parent's bottom and top. A tall screen stacks the picture at the top,
        // as wide as the card and at most 60% of the room, over the logo, the headline, the advertiser, the pitch and an Install pill, all centred, the pill in reach of a
        // thumb; a wide screen puts the picture down the card's left. Short of room, the pitch goes first. The Ad tag and the AdChoices sit on the picture's top corners,
        // and a faint rim keeps its shape where a dark picture meets a dark ground. Spread (the interstitial), the creative is the stage, as Google's own full-screen
        // native ads are: on a tall screen the picture as large as it goes, the app's strip at the foot read from the left - the logo beside the name and the
        // advertiser, the pitch, the pill - no wider than 560 points; on a wide one the picture from the screen's left edge and the text's column keeps the close's
        // strip at its top.
        private static void Card(RectTransform parent, Content ad, float bottom, float top, bool spread = false) {
            const float pad = 20, logoSize = 56, pill = 52, textMost = 560, controlColumn = 30 + 2 * 12;
            var rim  = spread ? null : Box(parent, new Color(1, 1, 1, .2f), 21).rectTransform;
            var card = Box(parent, colors.Surface, spread ? 0 : 20);
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var rect    = card.rectTransform;
            var picture = Media(rect, ad);
            var logo    = new GameObject("Logo", typeof(RectTransform)).GetComponent<RectTransform>();
            logo.SetParent(rect, false);
            Icon(logo, ad.Icon, ad.Advertiser, new Vector2(.5f, .5f), Vector2.zero, logoSize, 12);
            var headline   = Label(rect, Headline, 20, colors.Ink, FontStyle.Bold, TextAnchor.UpperCenter);
            // The advertiser and its rating, a step under the headline and apart from the pitch, as the pack's native card writes them.
            var advertiser = Label(rect, $"{ad.Advertiser} · {Rating}", 13, new Color(colors.Ink.r, colors.Ink.g, colors.Ink.b, .7f), FontStyle.Normal, TextAnchor.UpperCenter);
            var body       = Label(rect, ad.Body, 14, colors.Grey, FontStyle.Normal, TextAnchor.UpperCenter);
            var install    = Cta(rect, 16, Install, colors.Primary, colors.OnPrimary, pill / 2, ad.Url);
            // Flush on the picture's top corners, square everywhere: on the card the picture stands 6 points in from the edges it meets, by what the
            // rounding takes from a corner, so not a pixel of the creative is cut.
            Marks(picture, 0, 0);
            var inset = spread ? 0f : 6f;

            Responsive(parent, size => {
                var wide   = size.x > size.y;
                var room   = size.y - bottom - top;
                var width  = spread ? size.x : Mathf.Min(size.x - 32, wide ? 760 : 440);
                var height = spread ? room : Mathf.Min(room, 420);
                var media  = wide ? Mathf.Min(width * .58f, (height - 2 * inset) * 16 / 9) : width;
                var column = Mathf.Min(wide ? width - media - inset : width, spread ? textMost : float.MaxValue) - 2 * pad;
                var most   = body.fontSize * 3.5f / Points();
                var three  = headline.fontSize * 3.6f / Points();
                var lines  = Mathf.Min(Measure(headline, column), three) + 2 + Measure(advertiser, column);
                var pitch  = Mathf.Min(Measure(body, column), most) + 12;
                // The logo, the lines, the pitch and the pill with their gaps, inside the padding.
                var text = logoSize + 12 + lines + pitch + 16 + pill;
                // The close's strip: 12 points down, 30 tall, 4 under it.
                const float strip = 46;
                body.enabled = wide ? text <= height - pad - (spread ? strip : pad) : room - (text + 2 * pad) >= 120;
                if (!body.enabled) text -= pitch;

                if (spread && !wide) {
                    Place(rect, new Vector2(.5f, 0), new Vector2(0, bottom), new Vector2(width, room));
                    foreach (var label in new[] { headline, advertiser, body }) label.alignment = TextAnchor.UpperLeft;
                    // From the foot up: the pill, the pitch, the row of the logo beside the name and the advertiser, read from the left.
                    var foot  = room - pad - 8 - pill;
                    var left  = -column / 2;
                    var name  = column - logoSize - 12;
                    var two   = headline.fontSize * 2.4f / Points();
                    var named = Mathf.Min(Measure(headline, name), two) + 2 + Measure(advertiser, name);
                    var row   = Mathf.Max(logoSize, named);
                    var told  = Mathf.Min(Measure(body, column), body.fontSize * 2.4f / Points());
                    // The picture, as large as it goes over the strip: under the controls' strip at the whole width, or up to the top between their columns.
                    float Strip(bool pitch) => foot - 16 - (pitch ? told + 12 : 0) - row - pad;
                    (float Top, float Width, float Height) Picture(float above) {
                        var under   = Mathf.Min(width * 9 / 16, above - strip);
                        var between = Mathf.Min((width - 2 * controlColumn) * 9 / 16, above);
                        return between * between > under * under ? (0, between * 16 / 9, between) : (strip, Mathf.Min(width, under * 16 / 9), under);
                    }
                    body.enabled = Picture(Strip(true)).Height >= 120;
                    var stripTop = Strip(body.enabled);
                    var shot     = Picture(stripTop);
                    Place(picture, new Vector2(.5f, 1), new Vector2(0, -(shot.Top + (stripTop - shot.Top - shot.Height) / 2)), new Vector2(shot.Width, Mathf.Max(0, shot.Height)));
                    Place(logo, new Vector2(.5f, 1), new Vector2(left + logoSize / 2, -(stripTop + pad + (row - logoSize) / 2)), new Vector2(logoSize, logoSize));
                    var advertiserTop = Row(headline, left + logoSize + 12 + name / 2, stripTop + pad + (row - named) / 2, name, two) + 2;
                    Row(advertiser, left + logoSize + 12 + name / 2, advertiserTop, name);
                    if (body.enabled) Row(body, 0, stripTop + pad + row + 12, column, told);
                    Place(install, new Vector2(.5f, 1), new Vector2(0, -foot), new Vector2(column, pill));
                    return;
                }

                if (wide) {
                    Place(rect, new Vector2(.5f, 0), new Vector2(0, bottom + (room - height) / 2), new Vector2(width, height));
                    Place(picture, new Vector2(0, 1), new Vector2(inset, -inset), new Vector2(media, height - 2 * inset));
                } else {
                    media  = Mathf.Min((width - 2 * inset) * 9 / 16, room * .6f, room - inset - text - 2 * pad);
                    height = inset + media + text + 2 * pad;
                    Place(rect, new Vector2(.5f, 0), new Vector2(0, bottom + (room - height) / 2), new Vector2(width, height));
                    Place(picture, new Vector2(.5f, 1), new Vector2(0, -inset), new Vector2(width - 2 * inset, media));
                }

                if (rim) Place(rim, new Vector2(.5f, 0), new Vector2(0, bottom + (room - height) / 2 - 1), new Vector2(width + 2, height + 2));
                // Down the column: under the picture on a tall screen, centred beside it on a wide one.
                var x = wide ? (media + inset) / 2 : 0;
                var y = wide ? Mathf.Max(spread ? strip : 0, (height - text) / 2) : inset + media + pad;
                Place(logo, new Vector2(.5f, 1), new Vector2(x, -y), new Vector2(logoSize, logoSize));
                y = Row(headline, x, y + logoSize + 12, column, three) + 2;
                y = Row(advertiser, x, y, column);
                if (body.enabled) y = Row(body, x, y + 12, column, most);
                Place(install, new Vector2(.5f, 1), new Vector2(x, -(y + 16)), new Vector2(column, pill));
            });
        }

        // The label's height in points at the given width.
        private static float Measure(Text label, float width) {
            Place(label.rectTransform, new Vector2(.5f, 1), Vector2.zero, new Vector2(width, 10));
            return label.preferredHeight / Points();
        }

        // The label at the column's middle, the given points down, no taller than the most; the points under it.
        private static float Row(Text label, float x, float y, float width, float most = float.MaxValue) {
            var height = Mathf.Min(Measure(label, width), most);
            Place(label.rectTransform, new Vector2(.5f, 1), new Vector2(x, -y), new Vector2(width, height));
            return y + height;
        }

        // Lays parts out from the parent's size in device points, now and each time it changes.
        private static void Responsive(Transform parent, Action<Vector2> layout) {
            var watcher = new GameObject("Responsive", typeof(RectTransform), typeof(EditorResized)).GetComponent<EditorResized>();
            var rect    = (RectTransform) watcher.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            watcher.Resized = size => layout(size / Points());
            watcher.Check();
        }

        // A box of the standard height at its place in the safe area, or as wide as it, framed by a hairline, 12 points in on both sides: the logo, the headline
        // over the Ad tag and the advertiser with its rating, and the Install pill; the AdChoices in the top right corner, clear of the pill's round end.
        private static GameObject Banner(Content ad, bool fullWidth, AdScreenPosition position) {
            var box   = Slot("Banner Placeholder", new Vector2(320, 50), fullWidth, position);
            var inner = (RectTransform) box.GetChild(0);

            Icon(inner, ad.Icon, ad.Advertiser, new Vector2(0, .5f), new Vector2(12, 0), 36, 8);
            var install = Cta(inner, 14, Install, colors.Primary, colors.OnPrimary, 16, ad.Url);
            Place(install, new Vector2(1, .5f), new Vector2(-12, 0), new Vector2(88, 32));
            AdChoices(inner, Vector2.one, Vector2.zero);
            AppLines(inner, ad, 60, 112, 4, 14);
            return box.root.gameObject;
        }

        // The picture over the top part, the Ad tag and the AdChoices on its corners; under it the logo, the headline over the Ad tag and the advertiser,
        // and the Install pill across the foot, the logo, the text and the pill on the same 12 point edges.
        private static GameObject MRec(Content ad, AdScreenPosition position) {
            const float under = 250 * .46f;
            var box = Slot("MRec Placeholder", new Vector2(300, 250), false, position);
            box.gameObject.AddComponent<RectMask2D>();
            var picture = Media(box, ad);
            picture.anchorMin = new Vector2(0, under / 250);
            picture.anchorMax = Vector2.one;
            picture.offsetMin = picture.offsetMax = Vector2.zero;
            Marks(picture, 8, 8);

            var install = Cta(box, 15, Install, colors.Primary, colors.OnPrimary, 20, ad.Url);
            Span(install, 12, 12, 12, 40);
            // The logo and its lines centred in what the pill leaves under the picture.
            var middle = (12 + 40 + under) / 2;
            Icon(box, ad.Icon, ad.Advertiser, Vector2.zero, new Vector2(12, middle - 20), 40, 9);
            AppLines(box, ad, 64, 12, 250 - middle - 21, 16);
            return box.root.gameObject;
        }

        // The headline over the Ad tag and the advertiser with its rating, left aligned, the given points from the parent's sides and its top; the headline as large as given.
        private static void AppLines(RectTransform parent, Content ad, float left, float right, float top, float points) {
            var headline = Label(parent, Headline, points, colors.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            headline.rectTransform.anchorMin = new Vector2(0, 1);
            headline.rectTransform.anchorMax = Vector2.one;
            headline.rectTransform.offsetMin = new Vector2(left, -(top + 21)) * Points();
            headline.rectTransform.offsetMax = new Vector2(-right, -top) * Points();
            Tag(parent, "Ad", new Vector2(0, 1), new Vector2(left, -(top + 22)));
            var advertiser = Label(parent, $"{ad.Advertiser} · {Rating}", 12, colors.Grey, FontStyle.Normal, TextAnchor.MiddleLeft);
            advertiser.rectTransform.anchorMin = new Vector2(0, 1);
            advertiser.rectTransform.anchorMax = Vector2.one;
            advertiser.rectTransform.offsetMin = new Vector2(left + 32, -(top + 40)) * Points();
            advertiser.rectTransform.offsetMax = new Vector2(-right, -(top + 22)) * Points();
        }

        // An empty layer over the whole parent, for parts shown together.
        private static RectTransform Group(Transform parent) {
            var group = new GameObject("Group", typeof(RectTransform)).GetComponent<RectTransform>();
            group.SetParent(parent, false);
            Stretch(group);
            return group;
        }

        // A child of a layout group as tall as the given points.
        private static RectTransform Fixed(Transform parent, float height) {
            var rect = new GameObject("Row", typeof(RectTransform), typeof(LayoutElement)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.GetComponent<LayoutElement>().preferredHeight = height * Points();
            return rect;
        }

        // A hairline across the parent, the given points in from its sides, at its top or its bottom.
        private static void Hairline(RectTransform parent, float inset, bool top) {
            var line = Box(parent, colors.Line).rectTransform;
            line.anchorMin        = new Vector2(0, top ? 1 : 0);
            line.anchorMax        = new Vector2(1, top ? 1 : 0);
            line.pivot            = new Vector2(.5f, top ? 1 : 0);
            line.sizeDelta        = new Vector2(-2 * inset * Points(), 1);
            line.anchoredPosition = Vector2.zero;
        }

        // The box a banner or an MRec is drawn in: the given device points at its place in the safe area, or as wide as the safe area; its hairline frame the box's own color.
        private static RectTransform Slot(string name, Vector2 points, bool fullWidth, AdScreenPosition position) {
            var root   = Canvas(name, AdsEditorLayers.OverGame);
            var area   = SafeArea(root.transform);
            var box    = Box(area, colors.Line);
            var rect   = box.rectTransform;
            var anchor = Anchor(position);
            rect.anchorMin        = fullWidth ? new Vector2(0, anchor.y) : anchor;
            rect.anchorMax        = fullWidth ? new Vector2(1, anchor.y) : anchor;
            rect.pivot            = anchor;
            rect.sizeDelta        = new Vector2(fullWidth ? 0 : points.x * Points(), points.y * Points());
            rect.anchoredPosition = Vector2.zero;
            box.raycastTarget     = true;
            var inner = Box(rect, colors.Surface).rectTransform;
            Stretch(inner);
            inner.offsetMin = Vector2.one;
            inner.offsetMax = -Vector2.one;
            return rect;
        }

        // The creative in the advertiser's colors, the format's name on it over a smaller Test ad when labelled.
        private static RectTransform Media(Transform parent, Content ad, bool labelled = true) {
            // The light discs run past the creative's edges; the mask cuts them there.
            var media = new GameObject("Media", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            media.SetParent(parent, false);
            var graphic = new GameObject("Creative", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorMediaGraphic)).GetComponent<EditorMediaGraphic>();
            graphic.transform.SetParent(media, false);
            Stretch(graphic.rectTransform);
            graphic.From          = ad.From;
            graphic.To            = ad.To;
            graphic.raycastTarget = false;
            if (!labelled) return media;

            var small = ad.Format is AdFormat.MRec;
            var name  = Label(media, FormatName(ad.Format), small ? 24 : 32, Color.white, FontStyle.Bold, TextAnchor.LowerCenter);
            Span(name.rectTransform, 12, 12, 0, 0, true);
            name.rectTransform.anchorMin = new Vector2(0, .5f);
            name.rectTransform.offsetMin = new Vector2(12, small ? -2 : -4) * Points();
            var test = Label(media, "Test ad", small ? 12 : 15, new Color(1, 1, 1, .8f), FontStyle.Normal, TextAnchor.UpperCenter);
            Span(test.rectTransform, 12, 12, 0, 0, true);
            test.rectTransform.anchorMax = new Vector2(1, .5f);
            test.rectTransform.offsetMax = new Vector2(-12, small ? -4 : -6) * Points();
            return media;
        }

        // The icon on a white rounded tile, as the pack's native ads lay one, so a transparent icon stays clear on a dark page. The icon sits inside the tile's
        // corners rather than clipped to them, which keeps the tile's edge smooth; a clipped one is for an icon drawn to its edges, as a launcher's is.
        // With no icon, the advertiser's initial.
        private static RectTransform Icon(Transform parent, Texture icon, string name, Vector2 anchor, Vector2 position, float size, float corner, bool clip = false) {
            var tile = Box(parent, Color.white, corner);
            Place(tile.rectTransform, anchor, position, new Vector2(size, size));
            if (clip) tile.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            if (icon) {
                var image = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(tile.transform, false);
                image.texture       = icon;
                image.raycastTarget = false;
                Stretch(image.rectTransform, clip ? 0 : corner * .3f * Points());
            } else {
                tile.color = colors.Line;
                var initial = Label(tile.transform, name.Length > 0 ? name.Substring(0, 1) : "", size * .5f, colors.Grey, FontStyle.Bold, TextAnchor.MiddleCenter);
                Stretch(initial.rectTransform);
            }

            return tile.rectTransform;
        }

        // A grey tag, its corners slightly round, with white bold text, as the pack's native ads tag an ad, one size on every format; flush in a square corner,
        // a tab - square along the corner's edges, round where it reaches into the ad.
        private static void Tag(Transform parent, string text, Vector2 anchor, Vector2 position, bool tab = false) {
            var image = Box(parent, tag, 4);
            if (tab) image.sprite = EditorShapes.Rounded(Mathf.Max(1, Mathf.RoundToInt(4 * Points())), true);
            var box = image.rectTransform;
            Place(box, anchor, position, new Vector2(text.Length * 7 + 12, 18));
            Stretch(Label(box, text, 11, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter).rectTransform);
        }

        // The Ad tag and the AdChoices on the parent's top corners, each the given points in from its own corner.
        private static void Marks(Transform parent, float left, float right) {
            Tag(parent, "Ad", new Vector2(0, 1), new Vector2(left, -left), left <= 0);
            // Both in from their corners, the AdChoices stands level with the tag; otherwise each keeps its own corner.
            AdChoices(parent, Vector2.one, new Vector2(-right, left > 0 && right > 0 ? -(right + 1.5f) : -right));
        }

        // The store's mark and name, the given points tall, from its left at the position.
        private static void Store(Transform parent, Vector2 position, float points, Vector2? anchor = null, Vector2? at = null) {
            var appStore = EditorUserBuildSettings.activeBuildTarget is BuildTarget.iOS;
            var row      = new GameObject("Store", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            if (anchor is { } middle) {
                Place(row, middle, at ?? Vector2.zero, new Vector2(140, points + 4));
            } else {
                Place(row, new Vector2(0, 0), position, new Vector2(140, points + 4));
            }

            var mark = new GameObject("Mark", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorStoreGraphic)).GetComponent<EditorStoreGraphic>();
            mark.transform.SetParent(row, false);
            mark.AppStore      = appStore;
            mark.raycastTarget = false;
            Place(mark.rectTransform, new Vector2(0, .5f), Vector2.zero, new Vector2(points + 2, points + 2));
            var name = Label(row, appStore ? "App Store" : "Google Play", points, colors.Grey, FontStyle.Normal, TextAnchor.MiddleLeft);
            Span(name.rectTransform, points + 8, 0, 0, 0, true);
        }

        // The AdChoices mark, 15 points square as on every format.
        private static void AdChoices(Transform parent, Vector2 anchor, Vector2 position) {
            var mark = new GameObject("AdChoices", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorAdChoicesGraphic)).GetComponent<RectTransform>();
            mark.SetParent(parent, false);
            mark.GetComponent<EditorAdChoicesGraphic>().raycastTarget = false;
            Place(mark, anchor, position, new Vector2(15, 15));
        }

        // A video control in a dark disc at the top left of the picture.
        private static void Glyph(Transform parent, bool pause, Vector2 position) {
            var back = Box(parent, Shade(.35f), 15);
            Place(back.rectTransform, new Vector2(0, 1), position, new Vector2(30, 30));
            var glyph = new GameObject(pause ? "Pause" : "Sound", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorVideoGlyphGraphic)).GetComponent<EditorVideoGlyphGraphic>();
            glyph.transform.SetParent(back.transform, false);
            glyph.Pause         = pause;
            glyph.raycastTarget = false;
            Stretch(glyph.rectTransform);
        }

        private static RectTransform Cta(Transform parent, float points, string label, Color fill, Color color, float corner, string url) {
            var button = Box(parent, fill, corner);
            button.raycastTarget = true;
            Opens(button.gameObject.AddComponent<Button>(), url);
            var text = Label(button.transform, label, points, color, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
            return button.rectTransform;
        }

        // A press on the call to action opens the site, as a real ad's opens its store or page; the ad stays up.
        private static void Opens(Button button, string url) {
            if (string.IsNullOrEmpty(url)) return;

            button.onClick.AddListener(() => Application.OpenURL(url));
        }

        private static GameObject Cross(Transform parent, Color color) {
            var cross   = new GameObject("Cross", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorCrossGraphic));
            var graphic = cross.GetComponent<EditorCrossGraphic>();
            cross.transform.SetParent(parent, false);
            graphic.color         = color;
            graphic.raycastTarget = false;
            Stretch(cross.GetComponent<RectTransform>());
            return cross;
        }

        // The close in the safe area's top right corner, as AdMob places it, colored as every format's close disc over the given backdrop.
        private static (Button Button, Text Count, GameObject Cross) Close(RectTransform area, Color backdrop) {
            var (fill, ink) = Disc(backdrop);
            var back        = Box(area, fill, 15);
            Place(back.rectTransform, Vector2.one, new Vector2(-12, -12), new Vector2(30, 30));
            back.raycastTarget = true;
            // Pressed anywhere in 48 points round the disc, as a phone's least target.
            back.raycastPadding = -9 * Points() * Vector4.one;
            var count = Label(back.transform, "", 15, ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(count.rectTransform);
            var button = back.gameObject.AddComponent<Button>();
            // The disc keeps its colors: no tint on press or hold.
            button.transition = Selectable.Transition.None;
            return (button, count, Cross(back.transform, ink));
        }

        // The game's icon as the build target's would ship, for the bar back to the game: the target's own icons first, a plain one before an adaptive one,
        // then the Default Icon. One texture, or an adaptive icon's background and foreground; none when Player Settings has none.
        private static Texture[] GameIcon() {
            var target  = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            Texture[] layered = null;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(target)) {
                foreach (var icon in PlayerSettings.GetPlatformIcons(target, kind)) {
                    var textures = icon.GetTextures();
                    if (textures.Length == 1 && textures[0]) return textures;
                    if (textures.Length > 1 && textures[0] && textures[1]) layered ??= new Texture[] { textures[0], textures[1] };
                }
            }

            if (layered != null) return layered;

            foreach (var icon in PlayerSettings.GetIcons(target, IconKind.Any)) {
                if (icon) return new Texture[] { icon };
            }

            foreach (var icon in PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any)) {
                if (icon) return new Texture[] { icon };
            }

            return Array.Empty<Texture>();
        }

        private static string FormatName(AdFormat format) {
            switch (format) {
                case AdFormat.RewardedInterstitial: return "Rewarded Interstitial";
                case AdFormat.AppOpen:              return "App Open";
                default:                            return format.ToString();
            }
        }

        private static Vector2 Anchor(AdScreenPosition position) {
            switch (position) {
                case AdScreenPosition.TopLeft:     return new Vector2(0, 1);
                case AdScreenPosition.Top:         return new Vector2(.5f, 1);
                case AdScreenPosition.TopRight:    return new Vector2(1, 1);
                case AdScreenPosition.Center:      return new Vector2(.5f, .5f);
                case AdScreenPosition.BottomLeft:  return new Vector2(0, 0);
                case AdScreenPosition.Bottom:      return new Vector2(.5f, 0);
                case AdScreenPosition.BottomRight: return new Vector2(1, 0);
                default:                           throw new ArgumentOutOfRangeException(nameof(position), position, null);
            }
        }

        private static GameObject Canvas(string name, int sortingOrder) {
            var root   = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            Object.DontDestroyOnLoad(root);
            return root;
        }

        // The device's safe area, which the Device Simulator sets and the Game view leaves as the whole screen.
        private static RectTransform SafeArea(Transform parent) {
            var area = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            area.SetParent(parent, false);
            var safe = Screen.safeArea;
            area.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            area.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            area.offsetMin = area.offsetMax = Vector2.zero;
            return area;
        }

        private static Image Box(Transform parent, Color color, float corner = 0) {
            var image = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color         = color;
            image.raycastTarget = false;
            if (corner > 0) {
                image.sprite = EditorShapes.Rounded(Mathf.Max(1, Mathf.RoundToInt(corner * Points())));
                image.type   = Image.Type.Sliced;
            }

            return image;
        }

        private static Text Label(Transform parent, string text, float points, Color color, FontStyle style, TextAnchor alignment) {
            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font               = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize           = Mathf.RoundToInt(points * Points());
            label.fontStyle          = style;
            label.alignment          = alignment;
            label.color              = color;
            label.text               = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow   = VerticalWrapMode.Truncate;
            label.raycastTarget      = false;
            return label;
        }

        // At the anchor, its pivot there too, offset and sized in device points.
        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size) {
            rect.anchorMin        = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position * Points();
            rect.sizeDelta        = size * Points();
        }

        // Across the parent, the sides in device points from its edges; at its bottom with the given height, at its top when asked,
        // or between the two offsets when stretched.
        private static void Span(RectTransform rect, float left, float right, float bottom, float height, bool stretched = false, bool atTop = false) {
            var scale = Points();
            if (stretched) {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(left, bottom) * scale;
                rect.offsetMax = new Vector2(-right, -height) * scale;
                return;
            }

            var y = atTop ? 1 : 0;
            rect.anchorMin        = new Vector2(0, y);
            rect.anchorMax        = new Vector2(1, y);
            rect.pivot            = new Vector2(.5f, y);
            rect.sizeDelta        = new Vector2(-(left + right), height) * scale;
            rect.anchoredPosition = new Vector2((left - right) / 2, atTop ? -bottom : bottom) * scale;
        }

        private static void Stretch(RectTransform rect, float inset = 0) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = -Vector2.one * inset;
        }

        private static Palette Theme() => EditorGUIUtility.isProSkin ? dark : light;

        // Black at the given alpha as a phone blends it, in gamma: a project in linear space blends a translucent black lighter, so the alpha rises to match.
        private static Color Shade(float alpha) {
            return new Color(0, 0, 0, QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 - Mathf.Pow(1 - alpha, 2.2f) : alpha);
        }

        /// <summary>The Editor theme's surface, the panels' own: what the half-screen cover shows.</summary>
        internal static Color SurfaceColor => Theme().Surface;

        // Pixels per device point, as on a phone 360 points across, the most common width: the Game view's dpi is the monitor's,
        // which would draw a phone-sized view's placeholders a third of their size.
        private static float Points() {
            return Mathf.Min(Screen.width, Screen.height) / 360f;
        }

        private sealed class Palette {
            public Color Page, Surface, Ink, Grey, Line, Primary, OnPrimary;
        }

        // What a placeholder fills its ad with: the mediation as the advertiser, with the icon its pack ships, and a pitch naming the format.
        private readonly struct Content {
            public readonly AdFormat  Format;
            public readonly string    Advertiser;
            public readonly Texture2D Icon;
            public readonly string    Body;
            public readonly Color     From;
            public readonly Color     To;
            // Where its call to action leads: the mediation's own site, as a real ad leads to its advertiser's.
            public readonly string    Url;

            public Content(Type mediation, AdFormat format) {
                Format     = format;
                Advertiser = AdMediationNames.Of(mediation);
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(mediation.Assembly);
                Icon = package != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(package.assetPath + IconPath) : null;
                Body = $"This {FormatName(format)} test ad stands in for {Advertiser}'s. On a device, the real ad shows here.";
                (From, To) = Brand(Advertiser);
                Url        = Site(Advertiser);
            }

            private static string Site(string advertiser) {
                switch (advertiser) {
                    case "AdMob":     return "https://admob.google.com/home/";
                    case "MAX":       return "https://www.applovin.com/en/monetization";
                    case "LevelPlay": return "https://unity.com/products/levelplay";
                    default:          return null;
                }
            }

            // The mediations' brand colors, darkest first, so their ads tell apart at a glance; a slate for any other.
            private static (Color, Color) Brand(string advertiser) {
                switch (advertiser) {
                    case "AdMob":     return (new Color(.09f, .28f, .66f), new Color(.26f, .52f, .96f));
                    case "MAX":       return (new Color(.03f, .1f, .22f), new Color(0, .6f, .72f));
                    case "LevelPlay": return (new Color(.12f, .09f, .25f), new Color(.47f, .33f, .95f));
                    default:          return (new Color(.16f, .18f, .24f), new Color(.36f, .4f, .52f));
                }
            }
        }
    }

    /// <summary>
    /// A full-screen placeholder's close: for a rewarding format, five seconds counted down in real time, as the game is paused under it, on the reward pill, then the end card;
    /// for an app open ad, its close held two seconds behind a spinner, as AdMob may hold it; for the others, the close at once. Only a format with its reward label rewards.
    /// </summary>
    internal sealed class EditorFullScreenPlaceholder : MonoBehaviour {
        private IAdShowListener listener;
        private Button          button;
        private Text            count;
        private GameObject      cross;
        private Text            reward;
        private RectTransform   progress;
        private GameObject      endCard;
        private GameObject      waiting;
        private float           seconds;
        private float           startAt;
        private bool            closed;

        public void Begin(IAdShowListener listener, float closeAfter, Button close, Text count, GameObject cross, Text reward, RectTransform progress, GameObject endCard, GameObject waiting = null) {
            this.listener = listener;
            button        = close;
            this.count    = count;
            this.cross    = cross;
            this.reward   = reward;
            this.progress = progress;
            this.endCard  = endCard;
            this.waiting  = waiting;
            seconds       = closeAfter;
            startAt       = Time.realtimeSinceStartup;
            button.onClick.AddListener(Close);
            Update();
        }

        private void Update() {
            var left = seconds - (Time.realtimeSinceStartup - startAt);
            var done = left <= 0;
            button.interactable = done;
            if (count) count.text = done ? "" : Mathf.CeilToInt(left).ToString();
            if (cross) cross.SetActive(done);
            if (reward) reward.text = done ? "Reward granted" : Mathf.CeilToInt(left) == 1 ? "Reward in 1 second" : $"Reward in {Mathf.CeilToInt(left)} seconds";
            if (progress) progress.anchorMax = new Vector2(seconds > 0 ? Mathf.Clamp01(1 - left / seconds) : 1, 1);
            if (endCard) endCard.SetActive(done);
            if (waiting) waiting.SetActive(!done);
        }

        private void Close() {
            if (closed) return;

            closed = true;
            if (reward) listener.OnRewarded();
            Destroy(gameObject);
            listener.OnClosed();
        }

        // Leaving Play mode with the ad up still ends its show.
        private void OnDestroy() {
            if (closed) return;

            closed = true;
            listener.OnClosed();
        }
    }
}