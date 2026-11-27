using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    // The full-screen card, as the device draws it: OverlayAdContentView's
    // BuildCard and FitCard on Android, ran_arrangeCard and ran_fitCard on
    // iOS. The interstitial over the whole screen: the picture as large as it
    // goes in the middle, the app's strip at the foot. The end card as the
    // app's store page: the picture as a banner over its dimmed copy, then the
    // listing and the button at the foot. The app open ad a rounded card in
    // the middle of the dimmed game. In landscape the picture takes the left.
    // The picture yields first when the screen is short, then the body's
    // lines, then the icon.
    internal sealed partial class EditorAd {
        private const int   CardCornerDp              = 20;
        private const int   CardMarginDp              = 16;
        private const int   CardMaxWidthDp            = 440;
        private const int   CardLandscapeMaxWidthDp   = 760;
        private const int   CardLandscapeMaxHeightDp  = 420;
        private const float CardLandscapeMediaShare   = .58f;
        private const float CardPortraitMediaShare    = .6f;
        // The end card's picture, at most: the app is what the page is about.
        private const float EndCardMediaShare         = .4f;
        // The interstitial's listing row: two lines of name, two of pitch.
        private const int   StripHeadlineMaxLines     = 2;
        private const int   StripBodyMaxLines         = 2;
        private const int   CardPaddingDp             = 20;
        private const int   CardIconDp                = 56;
        private const int   CardSmallIconDp           = 40;
        private const int   EndCardIconDp             = 88;
        private const int   CardCallToActionHeightDp  = 52;
        private const int   CardControlInsetDp        = 12;
        private const int   CardControlTopDp          = 8;
        private const int   CardBodyMaxLines          = 3;
        private const int   CardHeadlineMaxLines      = 3;
        private const float IconCornerRatio           = .22f;
        // The preview's creative is drawn at 16:9, the shape most native
        // media arrives in.
        private const float CardMediaAspect           = 16f / 9f;
        // The way out, so a full finger target.
        private const int   AppBarHeightDp            = 48;
        private const int   AppBarGapDp               = 8;
        private const int   AppBarNameGapDp           = 24;
        private const int   AppBarIconDp              = 26;
        private const float AppBarSpinDegreesPerSecond = 360f;
        private const string AppBarContinueText       = "Continue to app";
        private const string CardObjectName           = "Card";
        private const string AppBarObjectName         = "App Bar";

        // The card's rim on the dark ground behind it, as the Ads placeholders draw it.
        private static readonly Color cardEdgeColor = new(1f, 1f, 1f, .2f);
        // The Ads placeholders' AdChoices inset from a card's corner.
        private const int CardAdChoicesInsetDp = 6;
        // The text and the button of a face spread over the screen, at most,
        // so a tablet's lines stay readable while the picture spans it.
        private const int BleedTextMaxWidthDp = 560;

        private bool          cardLayout;
        private Text          halfBodyText;
        private bool          cardLandscape;
        private RectTransform card;
        private RectTransform cardEdge;
        private LayoutElement cardMedia;
        private LayoutElement cardIcon;
        private LayoutElement cardBody;
        private Text          cardBodyText;
        private LayoutElement cardHeadline;
        private int           cardHeadlineSize;
        private RectTransform cardInfo;
        private RectTransform cardFoot;
        // The tone the end card's banner stands on, behind the card, as the
        // device's dimmed, blurred copy of the creative (PlaceCardBadges).
        private RectTransform cardBackdrop;
        // The interstitial's app strip, read from the left.
        private bool          cardStrip;
        private RectTransform appBar;
        private Text          appBarName;
        private RectTransform appBarSpinner;
        private GameObject    appBarReady;

        // The interstitial and the end card: the card's arrangement spread
        // over the whole screen on the theme's surface, as the networks' own
        // full-screen ads; only the app open ad keeps the card over the game.
        private static bool Bleeds(EditorAdConfig config) =>
                config.Layout is not FullScreenLayout.AppOpen;

        private RectTransform CreateCard(
            RectTransform panel
          , EditorAdConfig config) {
            cardLandscape = Screen.width > Screen.height;
            var endCard = config.Layout is FullScreenLayout.EndCard;
            var bleeds = Bleeds(config);
            cardFoot = null;
            cardStrip = bleeds && !endCard && !cardLandscape;
            cardEdge = null;

            // A faint rim behind the card, so a dark picture or a dark card
            // keeps its edge on the dimmed game.
            if (!bleeds) CreateCardEdge(panel);

            var cardObject = new GameObject(
                CardObjectName
              , typeof(RectTransform)
              , typeof(Image)
              , typeof(Mask)
              , typeof(EditorAdaptiveLayoutGroup));
            cardObject.transform.SetParent(panel, false);
            card = cardObject.GetComponent<RectTransform>();
            card.anchorMin = card.anchorMax = new(.5f, .5f);
            card.pivot = new(.5f, .5f);
            var shape = cardObject.GetComponent<Image>();
            if (!bleeds) Round(shape, CardCornerDp);
            shape.color = Theme.Surface;
            if (endCard) {
                // See-through, as the device's: the banner's backdrop stands
                // behind it, on the surface the panel paints.
                cardObject.GetComponent<Mask>().showMaskGraphic = false;
                if (cardBackdrop == null) {
                    cardBackdrop = new GameObject(
                        CardObjectName + " Backdrop"
                      , typeof(RectTransform)
                      , typeof(Image)).GetComponent<RectTransform>();
                    cardBackdrop.SetParent(panel, false);
                    cardBackdrop.anchorMin = cardBackdrop.anchorMax = new(0f, 1f);
                    cardBackdrop.pivot = new(0f, 1f);
                    var backdrop = cardBackdrop.GetComponent<Image>();
                    backdrop.color = new Color32(0x26, 0x2A, 0x33, 0xFF);
                    backdrop.raycastTarget = false;
                }
                cardBackdrop.SetSiblingIndex(card.GetSiblingIndex());
            }
            // The card is the ad: a press anywhere on it is the ad's click,
            // and the panel around it is not.
            var cardButton = cardObject.AddComponent<Button>();
            cardButton.targetGraphic = shape;
            cardButton.transition = Selectable.Transition.None;
            cardButton.onClick.AddListener(
                () => TryOpenUrl(TestAdClickUrl, ClickLogText));
            panel.GetComponent<Button>().interactable = false;
            var layout = cardObject.GetComponent<EditorAdaptiveLayoutGroup>();
            layout.IsVertical = !cardLandscape;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            // The columns stretch; the picture keeps the creative's width.
            layout.childForceExpandWidth = false;
            // In landscape the picture keeps the creative's height, centred
            // beside the column.
            layout.childForceExpandHeight = false;
            if (cardLandscape) layout.childAlignment = TextAnchor.MiddleCenter;

            var iconSize = endCard ? EndCardIconDp : CardIconDp;
            if (cardLandscape) {
                cardMedia = CardMedia(card);
                // On the full-screen face the text's column keeps the
                // controls' strip at its top.
                cardInfo = CardColumn(
                    card
                  , bleeds
                          ? Mathf.Max(CardPaddingDp, Mathf.RoundToInt(ControlSizeDp + 2 * CardControlTopDp))
                          : CardPaddingDp
                  , CardPaddingDp);
                cardInfo.GetComponent<LayoutElement>().flexibleWidth = 1f;
                if (endCard) {
                    EndCardListing(cardInfo, iconSize);
                } else {
                    cardIcon = CardIcon(cardInfo, iconSize);
                    CardIdentity(cardInfo, false);
                }
            } else if (endCard) {
                // The end card is the app's store page, as the networks draw
                // the card after a video: the creative as the page's banner
                // across the top, then the listing read from the left - the
                // big icon beside the name and the advertiser, the figures a
                // store shows, the pitch - and the button at the foot.
                cardMedia = CardMedia(card);
                cardInfo = CardColumn(card, CardPaddingDp, 0);
                EndCardListing(cardInfo, iconSize);
            } else if (bleeds) {
                // The interstitial is the creative's stage, as Google's own
                // full-screen native ads are: the picture as large as it goes
                // in the middle of what the app's strip leaves; the strip at
                // the foot, read from the left as a store's listing row - the
                // icon beside the name, the pitch, then the button.
                CardSpacer(card, 1f);
                cardMedia = CardMedia(card);
                CardSpacer(card, 1f);
                cardInfo = CardColumn(card, CardPaddingDp, 0);
                cardInfo.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
                var row = new GameObject(
                    "Row"
                  , typeof(RectTransform)
                  , typeof(HorizontalLayoutGroup)
                  , typeof(LayoutElement)).GetComponent<RectTransform>();
                row.SetParent(cardInfo, false);
                var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.spacing = 12;
                rowLayout.childControlWidth = rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
                cardIcon = CreateIcon(row, iconSize);
                var identity = CardColumn(row, 0, 0);
                identity.GetComponent<VerticalLayoutGroup>().padding = new(0, 0, 0, 0);
                identity.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
                CardIdentity(identity, false);
                foreach (var text in identity.GetComponentsInChildren<Text>()) {
                    text.alignment = TextAnchor.UpperLeft;
                }
            } else {
                cardMedia = CardMedia(card);
                cardInfo = CardColumn(card, CardPaddingDp, CardPaddingDp);
                cardIcon = CardIcon(cardInfo, iconSize);
                CardIdentity(cardInfo, false);
            }
            cardBodyText = CardText(cardInfo, BodyText, 14, Theme.Grey);
            cardBodyText.lineSpacing = 1.15f;
            if (cardStrip || endCard) cardBodyText.alignment = TextAnchor.UpperLeft;
            cardBody = cardBodyText.GetComponent<LayoutElement>();
            if (bleeds && !cardLandscape) {
                // The button at the foot of the screen.
                if (endCard) CardSpacer(card, 1f);
                // Apart from the pitch above it, however tight the face.
                cardFoot = CardColumn(card, CardPaddingDp, CardPaddingDp + 8);
                CardCallToAction(cardFoot, config.AdUnitId);
            } else {
                CardCallToAction(cardInfo, config.AdUnitId);
            }

            // The badges on the card's corners, over its picture: the card
            // is the ad and the panel around it is not.
            adChoicesRect = CreateBadges(card);
            // On the card, outside its layout.
            attributionRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            adChoicesRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            SetTopLeftRect(
                attributionRect
              , new(CardMediaInset(bleeds), -CardMediaInset(bleeds))
              , new(AttributionWidthDp, AttributionHeightDp));
            PlaceAdChoices(adChoicesRect, 0f);
            return card;
        }

        private static Transform CardSpacer(RectTransform parent, float weight) {
            var spacer = new GameObject("Space", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            var layout = spacer.GetComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 0f;
            layout.flexibleHeight = weight;
            return spacer.transform;
        }

        private void CreateCardEdge(RectTransform panel) {
            var edgeObject = new GameObject(
                CardObjectName + " Edge"
              , typeof(RectTransform)
              , typeof(Image));
            edgeObject.transform.SetParent(panel, false);
            cardEdge = edgeObject.GetComponent<RectTransform>();
            cardEdge.anchorMin = cardEdge.anchorMax = new(.5f, .5f);
            cardEdge.pivot = new(.5f, .5f);
            var edge = edgeObject.GetComponent<Image>();
            Round(edge, CardCornerDp + 1);
            edge.color = cardEdgeColor;
            edge.raycastTarget = false;
        }

        private LayoutElement CardMedia(RectTransform parent) {
            var inset = Mathf.RoundToInt(CardMediaInset(Bleeds(config)));
            if (inset > 0) {
                // In a frame that holds it off the edges it meets
                // (CardMediaInset).
                var frame = new GameObject(
                    "MediaFrame"
                  , typeof(RectTransform)
                  , typeof(HorizontalLayoutGroup)).GetComponent<RectTransform>();
                frame.SetParent(parent, false);
                var frameLayout = frame.GetComponent<HorizontalLayoutGroup>();
                frameLayout.padding = cardLandscape
                        ? new(inset, 0, inset, inset)
                        : new(inset, inset, inset, 0);
                frameLayout.childAlignment = TextAnchor.MiddleCenter;
                frameLayout.childControlWidth = frameLayout.childControlHeight = true;
                frameLayout.childForceExpandWidth = frameLayout.childForceExpandHeight = false;
                parent = frame;
            }
            var media = CreateMedia(
                parent
              , MediaText(config)
              , 0f
              , out mediaLabel);
            media.flexibleHeight = 0f;
            // The card's edge frames the picture; a rim of its own would
            // draw a second one inside it.
            foreach (Transform child in media.transform) {
                if (child.name == "Stroke") child.gameObject.SetActive(false);
            }
            return media;
        }

        private static RectTransform CardColumn(
            RectTransform parent
          , int top
          , int bottom) {
            var columnObject = new GameObject(
                DetailsObjectName
              , typeof(RectTransform)
              , typeof(VerticalLayoutGroup)
              , typeof(LayoutElement));
            columnObject.transform.SetParent(parent, false);
            var column = columnObject.GetComponent<VerticalLayoutGroup>();
            column.padding = new(CardPaddingDp, CardPaddingDp, top, bottom);
            column.childAlignment = TextAnchor.MiddleCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            columnObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            return columnObject.GetComponent<RectTransform>();
        }

        private LayoutElement CardIcon(RectTransform parent, int size) {
            // A holder centres the icon in the stretched column.
            var holder = new GameObject(
                IconObjectName
              , typeof(RectTransform)
              , typeof(HorizontalLayoutGroup)
              , typeof(LayoutElement));
            holder.transform.SetParent(parent, false);
            var row = holder.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            row.padding = new(0, 0, 0, 12);

            return CreateIcon(holder.transform, size);
        }

        // The end card's listing, read from the left: the icon beside the
        // name and the advertiser, then the store's figures - the rating's
        // stars, the price, the store - apart by hairlines. The pitch follows
        // (CreateCard).
        private void EndCardListing(RectTransform info, int iconSize) {
            info.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            var row = CardRow(info, 16);
            cardIcon = CreateIcon(row, iconSize);
            var identity = CardColumn(row, 0, 0);
            var identityLayout = identity.GetComponent<VerticalLayoutGroup>();
            identityLayout.padding = new(0, 0, 0, 0);
            identityLayout.childAlignment = TextAnchor.MiddleLeft;
            CardIdentity(identity, true);
            foreach (var text in identity.GetComponentsInChildren<Text>()) {
                text.alignment = TextAnchor.UpperLeft;
            }

            var figures = CardRow(info, 0);
            figures.GetComponent<HorizontalLayoutGroup>().padding = new(0, 0, 16, 0);
            figures.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var store = UnityEditor.EditorUserBuildSettings.activeBuildTarget is UnityEditor.BuildTarget.iOS
                    ? "App Store"
                    : "Google Play";
            var values = new[] { StarRatingText, "Free", store };
            for (var index = 0; index < values.Length; ++index) {
                if (index > 0) {
                    var hairline = new GameObject(
                        "Hairline"
                      , typeof(RectTransform)
                      , typeof(Image)
                      , typeof(LayoutElement));
                    hairline.transform.SetParent(figures, false);
                    var line = hairline.GetComponent<LayoutElement>();
                    line.minWidth = line.preferredWidth = 1f;
                    line.minHeight = line.preferredHeight = 20f;
                    var image = hairline.GetComponent<Image>();
                    image.color = DarkTheme
                            ? new Color32(0x44, 0x47, 0x46, 0xFF)
                            : new Color32(0xDA, 0xDC, 0xE0, 0xFF);
                    image.raycastTarget = false;
                }
                var figure = CardText(figures, values[index], 15, index == 0 ? Theme.Grey : Theme.Ink);
                figure.alignment = TextAnchor.MiddleCenter;
                figure.GetComponent<LayoutElement>().flexibleWidth = 1f;
            }
        }

        // A row in the card's column, its children centred down it, the given
        // points apart.
        private static RectTransform CardRow(RectTransform parent, int spacing) {
            var row = new GameObject(
                "Row"
              , typeof(RectTransform)
              , typeof(HorizontalLayoutGroup)
              , typeof(LayoutElement)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.spacing = spacing;
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
            return row;
        }

        private void CardIdentity(RectTransform parent, bool endCard) {
            cardHeadlineSize = endCard ? 22 : 20;
            headlineText = CardText(
                parent, HeadlineText, cardHeadlineSize, Theme.Ink);
            headlineText.fontStyle = FontStyle.Bold;
            cardHeadline = headlineText.GetComponent<LayoutElement>();
            // The advertiser a step under the headline and over the body: the
            // ink at 70%, where the body is the grey.
            var advertiserColor = Theme.Ink;
            advertiserColor.a = .7f;
            CardText(parent, AdvertiserText, 13, advertiserColor);
            // The end card shows its rating among the store's figures.
            if (!endCard) CardText(parent, StarRatingText, 13, Theme.Grey);
        }

        private static Text CardText(
            Transform parent
          , string value
          , int fontSize
          , Color color) {
            var label = CreateOverlayText(
                parent
              , value
              , fontSize
              , TextAnchor.UpperCenter
              , color);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.gameObject.AddComponent<LayoutElement>();
            return label;
        }

        private void CardCallToAction(RectTransform parent, string adUnitId) {
            callToActionLayoutElement = CreateCallToAction(
                parent
              , adUnitId
              , CardCallToActionHeightDp
              , 16
              , out callToActionText);
            var image = callToActionLayoutElement.GetComponent<Image>();
            callToActionText.fontStyle = FontStyle.Bold;
            // The gap above the button, as a spacer the column lays out.
            var spacer = new GameObject("Gap", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            spacer.transform.SetSiblingIndex(image.transform.GetSiblingIndex());
            var gap = spacer.GetComponent<LayoutElement>();
            gap.minHeight = gap.preferredHeight = 16f;
        }

        // Paints the image as a rounded rectangle of this radius, the same
        // sliced sprite the rect ad's corners use; see ApplyRoundedCorners
        // for the multiplier.
        private static void Round(Image image, float radius, bool tab = false) {
            image.sprite = RoundedRectSprite(radius, tab);
            image.type = Image.Type.Sliced;
            var canvas = image.canvas;
            image.pixelsPerUnitMultiplier =
                    canvas != null ? canvas.referencePixelsPerUnit : 100f;
        }

        // The device's rule: the card sits in what the controls leave, its
        // width capped, and in portrait the picture takes what the text
        // leaves, up to its own proportions and its share of the card.
        private void RefreshCard(float uiScale) {
            if (Screen.width > Screen.height != cardLandscape) {
                // The arrangement itself turns: built again, as the device
                // builds its face again for a new screen.
                Destroy(card.gameObject);
                if (cardEdge != null) Destroy(cardEdge.gameObject);
                content = CreateCard(panel, config);
                // Back under the controls, where the first build put them.
                card.SetAsFirstSibling();
                if (cardEdge != null) cardEdge.SetAsFirstSibling();
            }

            var safe = Screen.safeArea;
            var safeTop = (Screen.height - safe.yMax) / uiScale;
            var safeBottom = safe.yMin / uiScale;
            var safeLeft = safe.xMin / uiScale;
            var safeRight = (Screen.width - safe.xMax) / uiScale;
            var width = Screen.width / uiScale;
            var height = Screen.height / uiScale;
            var hasAppBar = HasAppBar(config);
            var bleeds = Bleeds(config);
            // The controls never take a strip across the top. A landscape
            // full-screen face has two gaps of its own beside its picture:
            // one control stands right of the picture, the other at the
            // screen's right edge, both over the text's column. A landscape
            // card takes a column at each side for them; a full-screen
            // portrait face lets them stand over its picture's corners or
            // beside it. Only the app open card without its bar keeps the
            // strip.
            const float controlColumn = ControlSizeDp + 2 * CardControlInsetDp;
            var sideReserve = cardLandscape && !hasAppBar && !bleeds ? controlColumn : 0f;
            var topReserve = safeTop + (hasAppBar
                    ? AppBarHeightDp + 2 * AppBarGapDp
                    : cardLandscape || bleeds
                            ? 0f
                            : ControlSizeDp + 2 * CardControlTopDp);
            var margin = bleeds ? 0f : CardMarginDp;
            var bottomReserve = safeBottom + margin;
            var availableWidth = Mathf.Max(
                0f, width - safeLeft - safeRight - 2 * Mathf.Max(margin, sideReserve));
            var cardWidth = bleeds
                    ? availableWidth
                    : Mathf.Min(
                          availableWidth
                        , cardLandscape ? CardLandscapeMaxWidthDp : CardMaxWidthDp);
            var maxHeight = Mathf.Max(0f, height - topReserve - bottomReserve);
            if (bleeds && !cardLandscape) {
                // The text and the button no wider than a tablet reads well.
                var side = Mathf.RoundToInt(
                    Mathf.Max(0f, (cardWidth - BleedTextMaxWidthDp) / 2f) + CardPaddingDp);
                foreach (var column in new[] { cardInfo, cardFoot }) {
                    if (column == null) continue;

                    var group = column.GetComponent<VerticalLayoutGroup>();
                    group.padding.left = group.padding.right = side;
                }
            }

            ResetCardGiveWay();
            // The body's cap needs its true width, so one pass first.
            card.sizeDelta = new(cardWidth, maxHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            ClampCardBody(cardStrip ? StripBodyMaxLines : CardBodyMaxLines);
            ClampCardHeadline(cardStrip ? StripHeadlineMaxLines : CardHeadlineMaxLines);
            float cardHeight;
            if (cardLandscape) {
                cardHeight = bleeds ? maxHeight : Mathf.Min(maxHeight, CardLandscapeMaxHeightDp);
                var mediaInset = Mathf.Round(CardMediaInset(bleeds));
                var mediaRoom = cardHeight - 2f * mediaInset;
                var mediaWidth = Mathf.Min(
                    cardWidth * CardLandscapeMediaShare
                  , mediaRoom * CardMediaAspect);
                // The creative's own height at that width: no letterbox.
                var mediaHeight = Mathf.Min(mediaRoom, mediaWidth / CardMediaAspect);
                cardMedia.minWidth = cardMedia.preferredWidth = mediaWidth;
                cardMedia.minHeight = cardMedia.preferredHeight = mediaHeight;
                cardMedia.flexibleWidth = 0f;
                cardMedia.flexibleHeight = 0f;
                var infoLayout = cardInfo.GetComponent<LayoutElement>();
                infoLayout.minHeight = infoLayout.preferredHeight = -1f;
                for (var step = 0; ; ++step) {
                    card.sizeDelta = new(cardWidth, cardHeight);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(card);
                    if (LayoutUtility.GetPreferredHeight(cardInfo) <= cardHeight
                            || !CardGiveWay(step)) {
                        break;
                    }
                }
                // The face no taller than what it holds, centred in its room:
                // the picture then reaches the face's top, where the badge and
                // the AdChoices mark sit.
                cardHeight = Mathf.Min(
                    cardHeight
                  , Mathf.Max(
                        mediaHeight + 2f * mediaInset
                      , LayoutUtility.GetPreferredHeight(cardInfo)));
                infoLayout.minHeight = infoLayout.preferredHeight = cardHeight;
            } else {
                var mediaInset = Mathf.Round(CardMediaInset(bleeds));
                var naturalMediaHeight = (cardWidth - 2f * mediaInset) / CardMediaAspect;
                // The end card's picture gives the app the middle; the
                // interstitial's is the face.
                var mediaCap = maxHeight * (!bleeds
                        ? CardPortraitMediaShare
                        : config.Layout is FullScreenLayout.EndCard ? EndCardMediaShare : 1f);
                // The controls stand in the face's top corners. The picture
                // either keeps under the strip they stand in, at the face's
                // whole width, or rises to the face's top between the columns
                // they stand in; the larger picture wins.
                const float strip = ControlSizeDp + 2 * CardControlTopDp;
                var cardGroup = card.GetComponent<EditorAdaptiveLayoutGroup>();
                var mediaHeight = 0f;
                var top = 0;
                for (var step = 0; ; ++step) {
                    cardGroup.padding.top = 0;
                    cardMedia.minHeight = cardMedia.preferredHeight = 0f;
                    card.sizeDelta = new(cardWidth, maxHeight);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(card);
                    var room = maxHeight - LayoutUtility.GetPreferredHeight(card);
                    mediaHeight = Mathf.Min(
                        naturalMediaHeight
                      , Mathf.Min(mediaCap, room - (bleeds ? strip : 0f)));
                    top = bleeds ? (int) strip : 0;
                    if (bleeds) {
                        var between = Mathf.Min(
                            (cardWidth - 2 * controlColumn) / CardMediaAspect
                          , Mathf.Min(mediaCap, room));
                        var underWidth = Mathf.Min(cardWidth, mediaHeight * CardMediaAspect);
                        if (between * CardMediaAspect * between > underWidth * mediaHeight) {
                            mediaHeight = between;
                            top = 0;
                        }
                    }
                    if (mediaHeight >= MediaMinSizeDp || !CardGiveWay(step)) {
                        break;
                    }
                }
                cardGroup.padding.top = top;
                cardMedia.minHeight = cardMedia.preferredHeight =
                        Mathf.Max(0f, mediaHeight);
                // No wider than the creative at that height.
                cardMedia.minWidth = cardMedia.preferredWidth =
                        Mathf.Min(cardWidth - 2f * mediaInset, mediaHeight * CardMediaAspect);
                LayoutRebuilder.ForceRebuildLayoutImmediate(card);
                cardHeight = bleeds
                        ? maxHeight
                        : Mathf.Min(
                              maxHeight
                            , LayoutUtility.GetPreferredHeight(card));
            }

            card.sizeDelta = new(cardWidth, cardHeight);
            // Centred in what the controls leave, as the device pads its
            // container.
            card.anchoredPosition = new(
                (safeLeft - safeRight) / 2f
              , (bottomReserve - topReserve) / 2f);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            if (cardEdge != null) {
                cardEdge.sizeDelta = card.sizeDelta + new Vector2(2f, 2f);
                cardEdge.anchoredPosition = card.anchoredPosition;
            }

            PlaceCardBadges();
            PlaceCardControls(safeTop, safeLeft, safeRight, cardLandscape && bleeds);
            if (hasAppBar) {
                if (appBar == null) CreateAppBar(panel);
                PlaceAppBar(width, safeTop);
            }
        }

        private void PlaceCardBadges() {
            PlaceBadgesOnPicture(card, cardMedia.GetComponent<RectTransform>());
            if (cardBackdrop == null) return;

            // Across the screen from its top edge to the foot of the picture,
            // or in landscape down from its left edge to the picture's right:
            // the controls stand on it, clear of the creative.
            var corners = new Vector3[4];
            cardMedia.GetComponent<RectTransform>().GetWorldCorners(corners);
            var panelRect = panel.rect;
            var bottomRight = panel.InverseTransformPoint(corners[3]);
            cardBackdrop.anchoredPosition = Vector2.zero;
            cardBackdrop.sizeDelta = cardLandscape
                    ? new(bottomRight.x - panelRect.xMin, panelRect.height)
                    : new(panelRect.width, panelRect.yMax - bottomRight.y);
            cardBackdrop.gameObject.SetActive(cardMedia.gameObject.activeSelf);
        }

        // How far the app open card's picture stands in from each edge it
        // meets: by what the card's rounding takes from a corner, so its own
        // corners touch the curve and not a pixel of the creative is cut -
        // the native policy lets a picture lose only width, a video nothing.
        // Nothing on the full-screen faces, whose corners are square.
        private static float CardMediaInset(bool bleeds) =>
                bleeds ? 0f : Mathf.Ceil(CardCornerDp * rectAdCornerInsetRatio);

        // The badge and the AdChoices mark on the picture's top corners,
        // their centres on one line: the picture is the ad and what is
        // around it is not.
        // The Ad tag's shape: corners slightly round; flush in a square
        // corner, a tab - square along the corner's edges, round where it
        // reaches into the ad.
        private void ShapeBadge(bool tab) =>
                Round(attributionRect.GetComponent<Image>(), BadgeCornerDp, tab);

        // Flush on the picture's top corners, which are square everywhere.
        private void PlaceBadgesOnPicture(RectTransform frame, RectTransform media) {
            var corners = new Vector3[4];
            media.GetWorldCorners(corners);
            var topLeft = frame.InverseTransformPoint(corners[1]);
            var topRight = frame.InverseTransformPoint(corners[2]);
            var frameRect = frame.rect;
            var left = topLeft.x - frameRect.xMin;
            var right = frameRect.xMax - topRight.x;
            var top = frameRect.yMax - topLeft.y;
            SetTopLeftRect(
                attributionRect
              , new(left, -top)
              , new(AttributionWidthDp, AttributionHeightDp));
            ShapeBadge(true);
            SetTopRightRect(
                adChoicesRect
              , new(-right, -top)
              , new(AdChoicesSizeDp, AdChoicesSizeDp));
        }

        private void ResetCardGiveWay() {
            cardBodyText.gameObject.SetActive(true);
            cardBody.preferredHeight = -1f;
            cardBody.minHeight = -1f;
            cardBodyText.text = BodyText;
            headlineText.fontSize = cardHeadlineSize;
            headlineText.text = HeadlineText;
            cardHeadline.preferredHeight = -1f;
            cardHeadline.minHeight = -1f;
            SizeCardIcon(
                config.Layout is FullScreenLayout.EndCard
                        ? EndCardIconDp
                        : CardIconDp);
        }

        // One step of giving way, in order: the body a line at a time to
        // one, the headline to two lines, the headline a size smaller, the
        // body gone, the icon smaller. The headline holds its lines before
        // the body; it is the ad's name. False when nothing is left to give.
        private bool CardGiveWay(int step) {
            switch (step) {
                case 0:
                    ClampCardBody(CardBodyMaxLines - 1);
                    return true;
                case 1:
                    ClampCardBody(1);
                    return true;
                case 2:
                    ClampCardHeadline(CardHeadlineMaxLines - 1);
                    return true;
                case 3:
                    headlineText.fontSize = Mathf.RoundToInt(cardHeadlineSize * .9f);
                    ClampCardHeadline(CardHeadlineMaxLines - 1);
                    return true;
                case 4:
                    cardBodyText.gameObject.SetActive(false);
                    return true;
                case 5:
                    SizeCardIcon(CardSmallIconDp);
                    return true;
                default:
                    return false;
            }
        }

        // uGUI has no line count, so the cap is a height: lines times the
        // font's line height; what runs past it ends in "…", as the device's.
        private void ClampCardBody(int lines) {
            ClampCardText(cardBodyText, cardBody, BodyText, lines, 1.15f);
        }

        private void ClampCardHeadline(int lines) {
            ClampCardText(headlineText, cardHeadline, HeadlineText, lines, 1f);
        }

        private static void ClampCardText(
            Text label
          , LayoutElement layout
          , string value
          , int lines
          , float spacing) {
            label.text = value;
            var lineHeight = label.fontSize * spacing * 1.2f;
            var natural = label.preferredHeight;
            var height = Mathf.Min(
                natural > 0f ? natural : lineHeight
              , lines * lineHeight);
            layout.minHeight = layout.preferredHeight = height;
            Ellipsize(label, value, label.rectTransform.rect.width, height);
        }

        // uGUI cuts a text that runs past its box without a mark; the value
        // is trimmed until it fits with "…" after it.
        private static void Ellipsize(Text label, string value, float width, float height) {
            if (width <= 0f || height <= 0f) return;

            var settings = label.GetGenerationSettings(new Vector2(width, height));
            var generator = new TextGenerator();
            generator.Populate(value, settings);
            if (generator.characterCountVisible >= value.Length) return;

            for (var count = generator.characterCountVisible; count > 0; --count) {
                var candidate = value[..count].TrimEnd() + "…";
                generator.Populate(candidate, settings);
                if (generator.characterCountVisible < candidate.Length) continue;

                label.text = candidate;
                return;
            }
            label.text = "…";
        }

        private void SizeCardIcon(float size) {
            cardIcon.minWidth = cardIcon.preferredWidth = size;
            cardIcon.minHeight = cardIcon.preferredHeight = size;
            var image = cardIcon.GetComponent<Image>();
            if (image != null) Round(image, size * IconCornerRatio);
        }

        // The discs and their glyphs in the backdrop's colours.
        private void StyleControls() {
            foreach (var control in new[] {
                         closeButton != null ? closeButton.gameObject : null
                       , countdownControl }) {
                if (control == null) continue;

                var image = control.GetComponent<Image>();

                image.color = DiscColor;
                foreach (var graphic in control.GetComponentsInChildren<Graphic>(true)) {
                    if (graphic != image) graphic.color = GlyphColor;
                }
            }
            SyncAppBar();
        }

        // nearPicture: the left control stands right of the picture.
        private void PlaceCardControls(
            float safeTop
          , float safeLeft
          , float safeRight
          , bool nearPicture) {
            var left = safeLeft;
            if (nearPicture && cardMedia.gameObject.activeSelf) {
                var parent = (RectTransform) card.parent;
                var corners = new Vector3[4];
                cardMedia.GetComponent<RectTransform>().GetWorldCorners(corners);
                left = parent.InverseTransformPoint(corners[2]).x - parent.rect.xMin;
            }
            foreach (var (control, onLeft) in new[] {
                         (closeButton != null ? closeButton.gameObject : null
                        , closeControlOnLeft)
                       , (countdownControl, countdownControlOnLeft) }) {
                if (control == null) continue;

                var rect = control.GetComponent<RectTransform>();
                var size = new Vector2(ControlSizeDp, ControlSizeDp);
                var y = -(safeTop + CardControlTopDp);
                if (onLeft) {
                    SetTopLeftRect(rect, new(left + CardControlInsetDp, y), size);
                } else {
                    SetTopRightRect(rect, new(-(safeRight + CardControlInsetDp), y), size);
                }
            }
        }

        private static bool HasAppBar(EditorAdConfig config) {
            return config.Layout is FullScreenLayout.AppOpen
                && !config.Close.RedirectOnClose;
        }

        // The bar back to the game, at the top of an app open ad, as AdMob
        // draws its own: the theme's surface, its left end round, running off
        // the screen's right edge - the game's icon and name, a spinner while the close is
        // held, then Continue to app and a chevron.
        private void CreateAppBar(RectTransform panel) {
            var barObject = new GameObject(
                AppBarObjectName
              , typeof(RectTransform)
              , typeof(Image)
              , typeof(Button)
              , typeof(HorizontalLayoutGroup));
            barObject.transform.SetParent(panel, false);
            appBar = barObject.GetComponent<RectTransform>();
            var shape = barObject.GetComponent<Image>();
            Round(shape, AppBarHeightDp / 2f);
            shape.color = Theme.Surface;
            var row = barObject.GetComponent<HorizontalLayoutGroup>();
            row.padding = new(14, AppBarHeightDp / 2 + 8, 0, 0);
            row.spacing = 10;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            barObject.GetComponent<Button>().onClick.AddListener(() => {
                if (countdownRemaining > 0f) return;
                Dismiss(false);
            });

            var gameIcon = CreateIcon(barObject.transform, AppBarIconDp);
            // The game's own icon where the project has one.
            var icons = UnityEditor.PlayerSettings.GetIcons(
                UnityEditor.Build.NamedBuildTarget.Unknown
              , UnityEditor.IconKind.Any);
            var picture = gameIcon.GetComponentInChildren<RawImage>();
            if (picture != null && icons.Length > 0 && icons[0] != null) {
                picture.texture = icons[0];
            }

            var name = CardText(
                barObject.transform, Application.productName, 15, Theme.Ink);
            name.alignment = TextAnchor.MiddleLeft;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            appBarName = name;

            // The spinner and Continue to app share one slot as wide as the
            // wider, so the bar keeps its length when one replaces the other.
            var slot = new GameObject("State", typeof(RectTransform), typeof(LayoutElement));
            slot.transform.SetParent(barObject.transform, false);
            var slotLayout = slot.GetComponent<LayoutElement>();

            var spinnerObject = new GameObject(
                "Spinner", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            spinnerObject.transform.SetParent(slot.transform, false);
            var spinnerRect = spinnerObject.GetComponent<RectTransform>();
            spinnerRect.anchorMin = spinnerRect.anchorMax = new(1f, .5f);
            spinnerRect.pivot = new(.5f, .5f);
            spinnerRect.sizeDelta = new(20f, 20f);
            spinnerRect.anchoredPosition = new(-10f, 0f);
            var spinnerImage = spinnerObject.GetComponent<Image>();
            spinnerImage.sprite = ArcSprite();
            spinnerImage.color = Theme.Primary;
            spinnerImage.raycastTarget = false;
            var spinnerLayout = spinnerObject.GetComponent<LayoutElement>();
            spinnerLayout.minWidth = spinnerLayout.preferredWidth = 20f;
            spinnerLayout.minHeight = spinnerLayout.preferredHeight = 20f;
            appBarSpinner = spinnerObject.GetComponent<RectTransform>();

            // The action, so it weighs as much as the name beside it.
            var go = CardText(
                slot.transform, AppBarContinueText + "  ›", 15, Theme.Ink);
            go.alignment = TextAnchor.MiddleRight;
            go.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(go.rectTransform);
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            // Wider by the gap after the name, the slot's content held right.
            slotLayout.minWidth = slotLayout.preferredWidth =
                    Mathf.Max(20f, go.preferredWidth) + AppBarNameGapDp - row.spacing;
            slotLayout.minHeight = slotLayout.preferredHeight = 20f;
            appBarReady = go.gameObject;
            SyncAppBar();
        }

        // The bar hugs what it holds; its right end runs off the screen.
        private void PlaceAppBar(float width, float safeTop) {
            var overhang = AppBarHeightDp / 2f;
            var row = appBar.GetComponent<HorizontalLayoutGroup>();
            // A long name gives way first.
            var nameWidth = Mathf.Min(appBarName.preferredWidth, width * .4f);
            var nameLayout = appBarName.GetComponent<LayoutElement>();
            nameLayout.minWidth = nameLayout.preferredWidth = nameWidth;
            var barWidth = row.padding.left + AppBarIconDp + 2 * row.spacing
                    + nameWidth
                    + LayoutUtility.GetPreferredWidth(appBar.Find("State") as RectTransform)
                    + row.padding.right;
            SetTopRightRect(
                appBar
              , new(overhang, -(safeTop + AppBarGapDp))
              , new(barWidth, AppBarHeightDp));
            LayoutRebuilder.ForceRebuildLayoutImmediate(appBar);
        }

        // Where the app bar is the way out, it shows the countdown's state
        // and the corner controls stay out of sight. The countdown keeps
        // running underneath, because Update reads it.
        private void SyncAppBar() {
            if (config == null || !cardLayout || !HasAppBar(config)) return;

            var done = countdownRemaining <= 0f;
            if (closeButton != null) {
                closeButton.transform.localScale = Vector3.zero;
            }
            if (countdownControl != null) {
                countdownControl.transform.localScale = Vector3.zero;
            }
            if (appBarSpinner != null) appBarSpinner.gameObject.SetActive(!done);
            if (appBarReady != null) appBarReady.SetActive(done);
        }

        private void SpinAppBar() {
            if (appBarSpinner == null || !appBarSpinner.gameObject.activeSelf) return;

            appBarSpinner.Rotate(
                0f, 0f, -AppBarSpinDegreesPerSecond * Time.unscaledDeltaTime);
        }

        // Three quarters of a ring, as the platform spinners draw it.
        private static Sprite arcSprite;

        private static Sprite ArcSprite() {
            if (arcSprite != null) return arcSprite;

            const int size = 64;
            const float outer = size * .5f - 1f;
            const float stroke = 7f;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; ++y) {
                for (var x = 0; x < size; ++x) {
                    var p = new Vector2(x + .5f - size * .5f, y + .5f - size * .5f);
                    var angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                    if (angle < 0f) angle += 360f;
                    var ring = Mathf.Abs(p.magnitude - (outer - stroke * .5f));
                    var alpha = angle <= 270f
                            ? Mathf.Clamp01(stroke * .5f + .5f - ring)
                            : 0f;
                    pixels[y * size + x] = new Color32(
                        255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                wrapMode   = TextureWrapMode.Clamp
              , filterMode = FilterMode.Bilinear
              , hideFlags  = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            arcSprite = Sprite.Create(
                texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f));
            arcSprite.hideFlags = HideFlags.HideAndDontSave;
            return arcSprite;
        }
    }
}