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
    /// An interstitial is one card in the middle of a black screen, and closes at once. A rewarding format is an app's store page with its video, the reward counted down
    /// for five seconds in a pill that closes it once earned, then its end card. An app open ad is a bar back to the game over the same card, the game dimmed behind,
    /// and closes from that bar once two seconds have passed. The card: the picture at its top, or its left on a wide screen, then the logo, the headline, the advertiser,
    /// the pitch and an Install pill, centred.<br/>
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

        // The advertiser's colors, the same in either appearance: they belong to the creative, not to the device.
        private static readonly Color action   = new(.1f, .45f, .91f, 1);
        private static readonly Color tag      = new(.62f, .62f, .62f, .95f);
        private static readonly Color pill     = new(.11f, .11f, .11f, .94f);
        private static readonly Color pillDisc = new(.24f, .24f, .25f, 1);
        private static readonly Color disc     = new(.85f, .85f, .85f, .95f);

        private GameObject banner;
        private GameObject mRec;

        public void ShowFullScreen(Type mediation, AdFormat format, IAdShowListener listener) => EditorThread.Run(() => {
            colors = Theme();
            var ad   = new Content(mediation, format);
            var root = Canvas("Ad Placeholder", AdsEditorLayers.FullScreenAd);
            // An app open ad dims the game rather than hiding it: the game is right there, and the card still reads on its own.
            var back = Box(root.transform, format is AdFormat.AppOpen ? new Color(0, 0, 0, .72f) : Color.black);
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

        // The card in the middle of the black screen, under the row the light close disc stands in.
        private static void Interstitial(RectTransform area, Content ad, EditorFullScreenPlaceholder holder, IAdShowListener listener) {
            Card(area, ad, 16, 52);
            var close = Close(area);
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
            Place(Label(app, Headline, 26, colors.Ink, FontStyle.Normal, TextAnchor.UpperLeft).rectTransform, new Vector2(0, 1), new Vector2(116, -2), new Vector2(228, 34));
            Place(Label(app, $"{ad.Advertiser} · {FormatName(ad.Format)}", 14, colors.Grey, FontStyle.Normal, TextAnchor.UpperLeft).rectTransform, new Vector2(0, 1), new Vector2(116, -38), new Vector2(228, 20));
            Store(app, new Vector2(116, 8), 13);

            // The store's figures between two hairlines: the rating, the category, the developer.
            var figures = Fixed(column, 66);
            Hairline(figures, 16, true);
            Hairline(figures, 16, false);
            Figure(figures, 0, "RATINGS", "4.8", "★★★★★");
            Figure(figures, 1, "CATEGORY", "Games", null);
            Figure(figures, 2, "DEVELOPER", ad.Advertiser, null);

            // The video, its sound and pause over its top left corner. On a tall screen its band takes the height the page leaves, the video
            // across its middle and the rest the same picture enlarged and dimmed, so the page has no dead gap and the band no bars.
            var video = new GameObject("Video", typeof(RectTransform), typeof(RectMask2D), typeof(EditorAspectHeight)).GetComponent<RectTransform>();
            video.SetParent(column, false);
            var band     = video.GetComponent<EditorAspectHeight>();
            var ambient  = Media(video, ad, false);
            var envelope = ambient.gameObject.AddComponent<AspectRatioFitter>();
            envelope.aspectMode  = AspectRatioFitter.AspectMode.EnvelopeParent;
            envelope.aspectRatio = 16 / 9f;
            Stretch(Box(video, new Color(0, 0, 0, .45f)).rectTransform);
            var media = Media(video, ad);
            media.anchorMin = new Vector2(0, .5f);
            media.anchorMax = new Vector2(1, .5f);
            media.offsetMin = media.offsetMax = Vector2.zero;
            var across = media.gameObject.AddComponent<AspectRatioFitter>();
            across.aspectMode  = AspectRatioFitter.AspectMode.WidthControlsHeight;
            across.aspectRatio = 16 / 9f;
            Glyph(media, false, new Vector2(10, -10));
            Glyph(media, true, new Vector2(46, -10));

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
            holder.Begin(listener, RewardSeconds, reward.Button, null, null, reward.Text, null, end);
        }

        // The pill a rewarded ad counts down in, at the top right: its text, then a close cross in a disc at its end; the whole pill closes the ad once the reward is earned.
        private static (Button Button, Text Text) RewardPill(RectTransform area) {
            var body = Box(area, pill, 18);
            body.raycastTarget = true;
            Place(body.rectTransform, Vector2.one, new Vector2(-10, -10), new Vector2(206, 36));
            var text = Label(body.transform, "", 14, new Color(.9f, .9f, .9f, 1), FontStyle.Normal, TextAnchor.MiddleLeft);
            Span(text.rectTransform, 16, 40, 0, 0, true);
            var end = Box(body.transform, pillDisc, 14);
            Place(end.rectTransform, new Vector2(1, .5f), new Vector2(-4, 0), new Vector2(28, 28));
            Cross(end.transform, Color.white, .25f, .06f);
            var button = body.gameObject.AddComponent<Button>();
            // Held without a sign, as Google holds it: no disabled tint.
            button.transition = Selectable.Transition.None;
            return (button, text);
        }

        // What a rewarded video ends on: the page dimmed, and in its middle a card of the app, its icon, name and store, and Install. Hidden until then.
        private static GameObject EndCard(RectTransform area, Content ad) {
            var group = Group(area);
            var veil  = Box(group, new Color(0, 0, 0, .66f)).rectTransform;
            var safe  = Screen.safeArea;
            Stretch(veil);
            veil.offsetMin = -safe.min;
            veil.offsetMax = new Vector2(Screen.width - safe.xMax, Screen.height - safe.yMax);

            var center = new Vector2(.5f, .5f);
            var card   = Box(group, colors.Page, 28).rectTransform;
            Place(card, center, new Vector2(0, 10), new Vector2(296, 372));
            Icon(card, ad.Icon, ad.Advertiser, center, new Vector2(0, 86), 104, 24);
            Place(Label(card, Headline, 28, colors.Ink, FontStyle.Normal, TextAnchor.MiddleCenter).rectTransform, center, new Vector2(0, -2), new Vector2(260, 38));
            Store(card, Vector2.zero, 14, center, new Vector2(18, -38));
            Place(Cta(card, 16, Install, colors.Primary, colors.OnPrimary, 24, ad.Url), center, new Vector2(0, -116), new Vector2(150, 48));
            // Shrunk to fit a screen on its side.
            Responsive(area, size => card.localScale = Vector3.one * Mathf.Min(1, (size.y - 24) / 392));
            group.gameObject.SetActive(false);
            return group.gameObject;
        }

        // One of the store page's figures, in the column at its index of three: the caption, the value, the stars under it when given; a divider before all but the first.
        private static void Figure(RectTransform parent, int index, string caption, string value, string stars) {
            var cell = new GameObject(caption, typeof(RectTransform)).GetComponent<RectTransform>();
            cell.SetParent(parent, false);
            cell.anchorMin = new Vector2(index / 3f, 0);
            cell.anchorMax = new Vector2((index + 1) / 3f, 1);
            cell.offsetMin = cell.offsetMax = Vector2.zero;
            Place(Label(cell, caption, 10, colors.Grey, FontStyle.Normal, TextAnchor.MiddleCenter).rectTransform, new Vector2(.5f, .5f), new Vector2(0, 18), new Vector2(110, 14));
            Place(Label(cell, value, 16, colors.Ink, FontStyle.Normal, TextAnchor.MiddleCenter).rectTransform, new Vector2(.5f, .5f), new Vector2(0, stars == null ? -6 : 0), new Vector2(110, 22));
            if (stars != null) Place(Label(cell, stars, 12, colors.Ink, FontStyle.Normal, TextAnchor.MiddleCenter).rectTransform, new Vector2(.5f, .5f), new Vector2(0, -18), new Vector2(110, 14));
            if (index == 0) return;

            var divider = Box(cell, colors.Line).rectTransform;
            Place(divider, new Vector2(0, .5f), Vector2.zero, new Vector2(1, 30));
        }

        // No cross, but a bar back to the game at the top, white with its left end round, running off the right edge: the game's icon and name, a spinner at its end
        // while the close is held, then Continue to app. The bar says where the way out leads, which a cross does not; under it the card, the game dimmed around both.
        private static void AppOpen(RectTransform area, Content ad, EditorFullScreenPlaceholder holder, IAdShowListener listener) {
            const float barHeight = 40;
            var back = Box(area, colors.Surface, barHeight / 2);
            back.raycastTarget = true;
            var button = back.gameObject.AddComponent<Button>();
            // Held without a sign, as AdMob holds it: no disabled tint.
            button.transition = Selectable.Transition.None;
            var bar = back.rectTransform;
            bar.anchorMin = new Vector2(.2f, 1);
            bar.anchorMax = Vector2.one;
            bar.pivot     = new Vector2(.5f, 1);
            bar.offsetMin = new Vector2(0, -(8 + barHeight)) * Points();
            bar.offsetMax = new Vector2(barHeight / 2, -8) * Points();

            var game = GameIcon();
            var tile = Icon(bar, game.Length > 0 ? game[0] : null, Application.productName, new Vector2(0, .5f), new Vector2(14, 0), 26, 6);
            if (game.Length > 1) {
                tile.GetComponentInChildren<RawImage>().uvRect = adaptiveVisible;
                var front = new GameObject("Foreground", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
                front.transform.SetParent(tile, false);
                front.texture       = game[1];
                front.uvRect        = adaptiveVisible;
                front.raycastTarget = false;
                Stretch(front.rectTransform);
            }

            var title = Label(bar, Application.productName, 15, colors.Ink, FontStyle.Normal, TextAnchor.MiddleLeft);
            Span(title.rectTransform, 50, 150, 0, 0, true);

            // While the close is held, a spinner where the way out will be; then the way out.
            var right   = barHeight / 2 + 16;
            var waiting = Group(bar);
            var spinner = new GameObject("Spinner", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorSpinnerGraphic)).GetComponent<EditorSpinnerGraphic>();
            spinner.transform.SetParent(waiting, false);
            spinner.color         = action;
            spinner.raycastTarget = false;
            Place(spinner.rectTransform, new Vector2(1, .5f), new Vector2(-right, 0), new Vector2(20, 20));
            // It turns about its own middle.
            spinner.rectTransform.pivot = new Vector2(.5f, .5f);
            var ready    = Group(bar);
            var go       = Label(ready, "Continue to app", 14, colors.Grey, FontStyle.Normal, TextAnchor.MiddleRight);
            Place(go.rectTransform, new Vector2(1, .5f), new Vector2(-(right + 18), 0), new Vector2(130, 24));
            var chevron = new GameObject("Chevron", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorChevronGraphic)).GetComponent<EditorChevronGraphic>();
            chevron.transform.SetParent(ready, false);
            chevron.color         = action;
            chevron.raycastTarget = false;
            Place(chevron.rectTransform, new Vector2(1, .5f), new Vector2(-right + 6, 0), new Vector2(22, 22));

            Card(area, ad, 16, 8 + barHeight + 16);
            holder.Begin(listener, AppOpenCloseSeconds, button, null, ready.gameObject, null, null, null, waiting.gameObject);
        }

        // The ad as one card, as the pack's native ads draw it, centred between the given points from the parent's bottom and top. A tall screen stacks the picture at the top,
        // as wide as the card and at most 60% of the room, over the logo, the headline, the advertiser, the pitch and an Install pill, all centred, the pill in reach of a
        // thumb; a wide screen puts the picture down the card's left. Short of room, the pitch goes first. The Ad tag and the AdChoices sit on the card's top corners,
        // and a faint rim keeps its shape where a dark picture meets a dark ground.
        private static void Card(RectTransform parent, Content ad, float bottom, float top) {
            const float pad = 20, logoSize = 56, pill = 52;
            var rim  = Box(parent, new Color(1, 1, 1, .2f), 21).rectTransform;
            var card = Box(parent, colors.Surface, 20);
            card.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var rect    = card.rectTransform;
            var picture = Media(rect, ad);
            var logo    = new GameObject("Logo", typeof(RectTransform)).GetComponent<RectTransform>();
            logo.SetParent(rect, false);
            Icon(logo, ad.Icon, ad.Advertiser, new Vector2(.5f, .5f), Vector2.zero, logoSize, 12);
            var headline   = Label(rect, Headline, 20, colors.Ink, FontStyle.Bold, TextAnchor.UpperCenter);
            var advertiser = Label(rect, $"{ad.Advertiser} · ★ 4.8", 13, colors.Grey, FontStyle.Normal, TextAnchor.UpperCenter);
            var body       = Label(rect, ad.Body, 14, colors.Grey, FontStyle.Normal, TextAnchor.UpperCenter);
            var install    = Cta(rect, 16, Install, colors.Primary, colors.OnPrimary, pill / 2, ad.Url);
            Tag(rect, "Ad", new Vector2(0, 1), new Vector2(12, -12));
            AdChoices(rect, Vector2.one, new Vector2(-6, -6), 18);

            Responsive(parent, size => {
                var wide   = size.x > size.y;
                var room   = size.y - bottom - top;
                var width  = Mathf.Min(size.x - 32, wide ? 760 : 440);
                var height = Mathf.Min(room, 420);
                var media  = wide ? Mathf.Min(width * .58f, height * 16 / 9) : width;
                var column = (wide ? width - media : width) - 2 * pad;
                var most   = body.fontSize * 3.5f / Points();
                var lines  = Measure(headline, column) + 4 + Measure(advertiser, column);
                var pitch  = Mathf.Min(Measure(body, column), most) + 8;
                // The logo, the lines, the pitch and the pill with their gaps, inside the padding.
                var text = logoSize + 12 + lines + pitch + 16 + pill;
                body.enabled = wide ? text <= height - 2 * pad : room - (text + 2 * pad) >= 120;
                if (!body.enabled) text -= pitch;

                if (wide) {
                    Place(rect, new Vector2(.5f, 0), new Vector2(0, bottom + (room - height) / 2), new Vector2(width, height));
                    Place(picture, new Vector2(0, 1), Vector2.zero, new Vector2(media, height));
                } else {
                    media  = Mathf.Min(width * 9 / 16, room * .6f, room - text - 2 * pad);
                    height = media + text + 2 * pad;
                    Place(rect, new Vector2(.5f, 0), new Vector2(0, bottom + (room - height) / 2), new Vector2(width, height));
                    Place(picture, new Vector2(.5f, 1), Vector2.zero, new Vector2(width, media));
                }

                Place(rim, new Vector2(.5f, 0), new Vector2(0, bottom + (room - height) / 2 - 1), new Vector2(width + 2, height + 2));
                // Down the column: under the picture on a tall screen, centred beside it on a wide one.
                var x = wide ? media / 2 : 0;
                var y = wide ? (height - text) / 2 : media + pad;
                Place(logo, new Vector2(.5f, 1), new Vector2(x, -y), new Vector2(logoSize, logoSize));
                y = Row(headline, x, y + logoSize + 12, column) + 4;
                y = Row(advertiser, x, y, column);
                if (body.enabled) y = Row(body, x, y + 8, column, most);
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

        // A box of the standard height at its place in the safe area, or as wide as it, framed by a hairline: the AdChoices down its left edge, the logo,
        // the headline over the advertiser and the rating, and the Install pill.
        private static GameObject Banner(Content ad, bool fullWidth, AdScreenPosition position) {
            var box   = Slot("Banner Placeholder", new Vector2(320, 50), fullWidth, position);
            var inner = (RectTransform) box.GetChild(0);

            AdChoices(inner, new Vector2(0, .5f), new Vector2(2, 0), 12, true);
            var logo = Icon(inner, ad.Icon, ad.Advertiser, new Vector2(0, .5f), new Vector2(18, 0), 36, 6);
            logo.GetComponent<Image>().color = colors.Line;
            var install = Cta(inner, 13, Install.ToUpperInvariant(), colors.Ink, colors.Surface, 16, ad.Url);
            Place(install, new Vector2(1, .5f), new Vector2(-8, 0), new Vector2(96, 32));
            var text = install.GetComponentInChildren<Text>();
            text.alignment               = TextAnchor.MiddleLeft;
            text.rectTransform.offsetMin = new Vector2(14, 0) * Points();
            var chevron = new GameObject("Chevron", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorChevronGraphic)).GetComponent<EditorChevronGraphic>();
            chevron.transform.SetParent(install, false);
            chevron.color         = colors.Surface;
            chevron.raycastTarget = false;
            Place(chevron.rectTransform, new Vector2(1, .5f), new Vector2(-8, 0), new Vector2(18, 18));

            TextColumn(inner, 62, 112, (Headline, 14, colors.Ink, FontStyle.Bold), ($"{ad.Advertiser} · ★ 4.8", 12, colors.Grey, FontStyle.Normal));
            return box.root.gameObject;
        }

        // The picture over the top half, the Ad tag on it, the AdChoices at its corner; under it the logo, the headline over the advertiser, and the Install bar across the foot.
        private static GameObject MRec(Content ad, AdScreenPosition position) {
            var box  = Slot("MRec Placeholder", new Vector2(300, 250), false, position);
            var card = box.gameObject.AddComponent<RectMask2D>();
            var picture = Media(card.transform, ad);
            picture.anchorMin = new Vector2(0, .46f);
            picture.anchorMax = Vector2.one;
            picture.offsetMin = picture.offsetMax = Vector2.zero;
            Tag(picture, "Ad", new Vector2(0, 1), new Vector2(8, -8));
            AdChoices(picture, Vector2.one, new Vector2(-4, -4), 14);

            var foot = Box(box, action).rectTransform;
            Span(foot, 0, 0, 0, 40);
            foot.GetComponent<Image>().raycastTarget = true;
            var go = foot.gameObject.AddComponent<Button>();
            go.transition = Selectable.Transition.None;
            Opens(go, ad.Url);
            var label = Label(foot, Install, 15, Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            Span(label.rectTransform, 16, 50, 0, 0, true);
            var chevron = new GameObject("Chevron", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorChevronGraphic)).GetComponent<EditorChevronGraphic>();
            chevron.transform.SetParent(foot, false);
            chevron.raycastTarget = false;
            Place(chevron.rectTransform, new Vector2(1, .5f), new Vector2(-12, 0), new Vector2(22, 22));

            var row = new GameObject("App", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(box, false);
            row.anchorMin = Vector2.zero;
            row.anchorMax = new Vector2(1, .46f);
            row.offsetMin = new Vector2(0, 40) * Points();
            row.offsetMax = Vector2.zero;
            Icon(row, ad.Icon, ad.Advertiser, new Vector2(0, .5f), new Vector2(12, 0), 40, 9);
            TextColumn(row, 62, 12, (Headline, 16, colors.Ink, FontStyle.Bold), ($"{ad.Advertiser} · ★ 4.8", 12, colors.Grey, FontStyle.Normal));
            return box.root.gameObject;
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

        // Lines of text stacked in the middle of the parent, between the given points from its sides.
        private static void TextColumn(RectTransform parent, float left, float right, params (string Text, float Points, Color Color, FontStyle Style)[] lines) {
            var column = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            column.SetParent(parent, false);
            Span(column, left, right, 0, 0, true);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment         = TextAnchor.MiddleLeft;
            layout.spacing                = 2 * Points();
            layout.childControlWidth      = layout.childControlHeight = true;
            layout.childForceExpandWidth  = true;
            layout.childForceExpandHeight = false;
            foreach (var line in lines) Label(column, line.Text, line.Points, line.Color, line.Style, TextAnchor.MiddleLeft);
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

        // The icon on a rounded tile that clips it, framed by a hairline as a store frames an app's icon; with no icon, the advertiser's initial.
        private static RectTransform Icon(Transform parent, Texture icon, string name, Vector2 anchor, Vector2 position, float size, float corner) {
            var hair  = 1 / Points();
            var frame = Box(parent, colors.Line, corner + hair);
            Place(frame.rectTransform, anchor, position + hair * (2 * anchor - Vector2.one), new Vector2(size + 2 * hair, size + 2 * hair));
            var tile = Box(parent, Color.white, corner);
            Place(tile.rectTransform, anchor, position, new Vector2(size, size));
            tile.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            if (icon) {
                var image = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(tile.transform, false);
                image.texture       = icon;
                image.raycastTarget = false;
                Stretch(image.rectTransform);
            } else {
                tile.color = colors.Line;
                var initial = Label(tile.transform, name.Length > 0 ? name.Substring(0, 1) : "", size * .5f, colors.Grey, FontStyle.Bold, TextAnchor.MiddleCenter);
                Stretch(initial.rectTransform);
            }

            return tile.rectTransform;
        }

        // A grey pill with white text, as Google tags an ad: Ad, or Advertisement.
        private static void Tag(Transform parent, string text, Vector2 anchor, Vector2 position) {
            var box = Box(parent, tag, 9).rectTransform;
            Place(box, anchor, position, new Vector2(text.Length * 7.2f + 18, 18));
            Stretch(Label(box, text, 12, Color.white, FontStyle.Normal, TextAnchor.MiddleCenter).rectTransform);
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

        private static void AdChoices(Transform parent, Vector2 anchor, Vector2 position, float size, bool vertical = false) {
            var mark = new GameObject("AdChoices", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorAdChoicesGraphic)).GetComponent<RectTransform>();
            mark.SetParent(parent, false);
            var graphic = mark.GetComponent<EditorAdChoicesGraphic>();
            graphic.raycastTarget = false;
            graphic.Vertical      = vertical;
            Place(mark, anchor, position, vertical ? new Vector2(size, size * 2) : new Vector2(size * 2, size));
        }

        // A video control in a dark disc at the top left of the picture.
        private static void Glyph(Transform parent, bool pause, Vector2 position) {
            var back = Box(parent, new Color(0, 0, 0, .35f), 15);
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

        private static GameObject Cross(Transform parent, Color color, float reach, float weight) {
            var cross   = new GameObject("Cross", typeof(RectTransform), typeof(CanvasRenderer), typeof(EditorCrossGraphic));
            var graphic = cross.GetComponent<EditorCrossGraphic>();
            cross.transform.SetParent(parent, false);
            graphic.color         = color;
            graphic.Reach         = reach;
            graphic.Weight        = weight;
            graphic.raycastTarget = false;
            Stretch(cross.GetComponent<RectTransform>());
            return cross;
        }

        // The close in the safe area's top right corner, as AdMob places it: a light disc with a dark cross.
        private static (Button Button, Text Count, GameObject Cross) Close(RectTransform area) {
            var back = Box(area, disc, 15);
            Place(back.rectTransform, Vector2.one, new Vector2(-12, -12), new Vector2(30, 30));
            back.raycastTarget = true;
            var count = Label(back.transform, "", 15, Color.black, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(count.rectTransform);
            return (back.gameObject.AddComponent<Button>(), count, Cross(back.transform, new Color(.1f, .1f, .1f, 1), .2f, .065f));
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