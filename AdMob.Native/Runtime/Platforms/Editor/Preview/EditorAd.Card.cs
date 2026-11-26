using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    // The full-screen card, as the device draws it: OverlayAdContentView's
    // BuildCard and FitCard on Android, ran_arrangeCard and ran_fitCard on
    // iOS. One rounded white card in the middle of the panel - the picture
    // across its top, then the icon, the headline, the advertiser and rating,
    // the body, all centred, and a wide pill to act. In landscape the picture
    // takes the card's left. The end card leads with the app. The picture
    // yields first when the screen is short, then the body's lines, then the
    // icon.
    internal sealed partial class EditorAd {
        private const int   CardCornerDp              = 20;
        private const int   CardMarginDp              = 16;
        private const int   CardMaxWidthDp            = 440;
        private const int   CardLandscapeMaxWidthDp   = 760;
        private const int   CardLandscapeMaxHeightDp  = 420;
        private const float CardLandscapeMediaShare   = .58f;
        private const float CardPortraitMediaShare    = .6f;
        private const int   CardPaddingDp             = 20;
        private const int   CardIconDp                = 56;
        private const int   CardSmallIconDp           = 40;
        private const int   EndCardIconDp             = 88;
        private const int   CardCallToActionHeightDp  = 52;
        private const int   CardControlInsetDp        = 12;
        private const int   CardControlTopDp          = 8;
        private const int   CardBodyMaxLines          = 3;
        private const float IconCornerRatio           = .22f;
        // The preview's creative is drawn at 16:9, the shape most native
        // media arrives in.
        private const float CardMediaAspect           = 16f / 9f;
        private const int   AppBarHeightDp            = 40;
        private const int   AppBarGapDp               = 8;
        private const float AppBarWidthShare          = .82f;
        private const int   AppBarIconDp              = 26;
        private const float AppBarSpinDegreesPerSecond = 360f;
        private const string AppBarContinueText       = "Continue to app";
        private const string CardObjectName           = "Card";
        private const string AppBarObjectName         = "App Bar";

        private static readonly Color cardColor          = Color.white;
        private static readonly Color cardActionColor    = new(0x1A / 255f, 0x73 / 255f, 0xE8 / 255f, 1f);
        private static readonly Color cardSecondaryColor = new(0x5F / 255f, 0x63 / 255f, 0x68 / 255f, 1f);
        private static readonly Color lightDiscColor     = new(.89f, .89f, .89f, .9f);
        private static readonly Color darkGlyphColor     = new(.12f, .12f, .12f, 1f);
        private static readonly Color cardEdgeColor      = new(0xDA / 255f, 0xDC / 255f, 0xE0 / 255f, 1f);
        private static readonly Color cardDarkEdgeColor  = new(1f, 1f, 1f, .2f);

        private bool          cardLayout;
        private bool          cardLandscape;
        private RectTransform card;
        private RectTransform cardEdge;
        private LayoutElement cardMedia;
        private LayoutElement cardIcon;
        private LayoutElement cardBody;
        private Text          cardBodyText;
        private RectTransform cardInfo;
        private RectTransform appBar;
        private RectTransform appBarSpinner;
        private GameObject    appBarReady;

        private RectTransform CreateCard(
            RectTransform panel
          , EditorAdConfig config) {
            cardLandscape = Screen.width > Screen.height;
            var endCard = config.Layout is FullScreenLayout.EndCard;

            // A hairline behind the card, as the device strokes its card: grey
            // on a light panel, where the white card has no edge of its own;
            // faint white on a dark one, where a dark picture would merge
            // into the panel.
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
            edge.color = IsLight(config.BackgroundColor) ? cardEdgeColor : cardDarkEdgeColor;
            edge.raycastTarget = false;

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
            Round(shape, CardCornerDp);
            shape.color = cardColor;
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
            layout.childForceExpandWidth = !cardLandscape;
            layout.childForceExpandHeight = cardLandscape;

            var iconSize = endCard ? EndCardIconDp : CardIconDp;
            if (cardLandscape) {
                cardMedia = CardMedia(card);
                cardInfo = CardColumn(card, CardPaddingDp, CardPaddingDp);
                cardInfo.GetComponent<LayoutElement>().flexibleWidth = 1f;
                cardIcon = CardIcon(cardInfo, iconSize);
                CardIdentity(cardInfo, endCard);
            } else if (endCard) {
                // The app first, then its picture, then the pitch and the
                // button.
                var head = CardColumn(card, CardPaddingDp + 8, 12);
                cardIcon = CardIcon(head, iconSize);
                CardIdentity(head, true);
                cardMedia = CardMedia(card);
                cardInfo = CardColumn(card, 4, CardPaddingDp);
            } else {
                cardMedia = CardMedia(card);
                cardInfo = CardColumn(card, CardPaddingDp, CardPaddingDp);
                cardIcon = CardIcon(cardInfo, iconSize);
                CardIdentity(cardInfo, false);
            }
            cardBodyText = CardText(cardInfo, BodyText, 14, cardSecondaryColor);
            cardBodyText.lineSpacing = 1.15f;
            cardBody = cardBodyText.GetComponent<LayoutElement>();
            CardCallToAction(cardInfo, config.AdUnitId);

            // The badges on the card's corners, over its picture: the card
            // is the ad and the panel around it is not.
            adChoicesRect = CreateBadges(card);
            // On the card, outside its layout.
            attributionRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            adChoicesRect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            SetTopLeftRect(
                attributionRect
              , new(CardControlInsetDp, -CardControlInsetDp)
              , new(AttributionWidthDp, AttributionHeightDp));
            PlaceAdChoices(adChoicesRect, 0f);
            return card;
        }

        private LayoutElement CardMedia(RectTransform parent) {
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

        private void CardIdentity(RectTransform parent, bool endCard) {
            headlineText = CardText(
                parent, HeadlineText, endCard ? 22 : 20, primaryTextOnLightColor);
            headlineText.fontStyle = FontStyle.Bold;
            CardText(parent, AdvertiserText, 13, cardSecondaryColor);
            CardText(parent, StarRatingText, RatingFontSize, attributionColor);
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
        private static void Round(Image image, float radius) {
            image.sprite = RoundedRectSprite(radius);
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
            var topReserve = safeTop + (hasAppBar
                    ? AppBarHeightDp + 2 * AppBarGapDp
                    : ControlSizeDp + 2 * CardControlTopDp);
            var bottomReserve = safeBottom + CardMarginDp;
            var availableWidth = Mathf.Max(
                0f, width - safeLeft - safeRight - 2 * CardMarginDp);
            var cardWidth = Mathf.Min(
                availableWidth
              , cardLandscape ? CardLandscapeMaxWidthDp : CardMaxWidthDp);
            var maxHeight = Mathf.Max(0f, height - topReserve - bottomReserve);

            ResetCardGiveWay();
            // The body's cap needs its true width, so one pass first.
            card.sizeDelta = new(cardWidth, maxHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            ClampCardBody(CardBodyMaxLines);
            float cardHeight;
            if (cardLandscape) {
                cardHeight = Mathf.Min(maxHeight, CardLandscapeMaxHeightDp);
                var mediaWidth = Mathf.Min(
                    cardWidth * CardLandscapeMediaShare
                  , cardHeight * CardMediaAspect);
                cardMedia.minWidth = cardMedia.preferredWidth = mediaWidth;
                cardMedia.flexibleWidth = 0f;
                for (var step = 0; ; ++step) {
                    card.sizeDelta = new(cardWidth, cardHeight);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(card);
                    if (LayoutUtility.GetPreferredHeight(cardInfo) <= cardHeight
                            || !CardGiveWay(step)) {
                        break;
                    }
                }
            } else {
                var naturalMediaHeight = cardWidth / CardMediaAspect;
                var mediaCap = maxHeight * CardPortraitMediaShare;
                var mediaHeight = 0f;
                for (var step = 0; ; ++step) {
                    cardMedia.minHeight = cardMedia.preferredHeight = 0f;
                    card.sizeDelta = new(cardWidth, maxHeight);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(card);
                    var textHeight = LayoutUtility.GetPreferredHeight(card);
                    mediaHeight = Mathf.Min(
                        naturalMediaHeight
                      , Mathf.Min(mediaCap, maxHeight - textHeight));
                    if (mediaHeight >= MediaMinSizeDp || !CardGiveWay(step)) {
                        break;
                    }
                }
                cardMedia.minHeight = cardMedia.preferredHeight =
                        Mathf.Max(0f, mediaHeight);
                LayoutRebuilder.ForceRebuildLayoutImmediate(card);
                cardHeight = Mathf.Min(
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

            PlaceCardControls(safeTop, safeLeft, safeRight);
            if (hasAppBar) {
                if (appBar == null) CreateAppBar(panel);
                PlaceAppBar(width, safeTop);
            }
        }

        private void ResetCardGiveWay() {
            cardBodyText.gameObject.SetActive(true);
            cardBody.preferredHeight = -1f;
            cardBody.minHeight = -1f;
            SizeCardIcon(
                config.Layout is FullScreenLayout.EndCard
                        ? EndCardIconDp
                        : CardIconDp);
        }

        // One step of giving way, in order: the body to one line, the body
        // gone, the icon smaller. False when nothing is left to give.
        private bool CardGiveWay(int step) {
            switch (step) {
                case 0:
                    ClampCardBody(1);
                    return true;
                case 1:
                    cardBodyText.gameObject.SetActive(false);
                    return true;
                case 2:
                    SizeCardIcon(CardSmallIconDp);
                    return true;
                default:
                    return false;
            }
        }

        // uGUI has no line count, so the cap is a height: lines times the
        // font's line height.
        private void ClampCardBody(int lines) {
            var lineHeight = cardBodyText.fontSize * 1.15f * 1.2f;
            var natural = cardBodyText.preferredHeight;
            var height = Mathf.Min(
                natural > 0f ? natural : lineHeight
              , lines * lineHeight);
            cardBody.minHeight = cardBody.preferredHeight = height;
        }

        private void SizeCardIcon(float size) {
            cardIcon.minWidth = cardIcon.preferredWidth = size;
            cardIcon.minHeight = cardIcon.preferredHeight = size;
            var image = cardIcon.GetComponent<Image>();
            if (image != null) Round(image, size * IconCornerRatio);
        }

        // Over a dark panel the discs turn light with a dark cross, as
        // Google draws its close.
        private void StyleCardControls(EditorAdConfig config) {
            var light = !IsLight(config.BackgroundColor);
            foreach (var control in new[] {
                         closeButton != null ? closeButton.gameObject : null
                       , countdownControl }) {
                if (control == null || !light) continue;

                var image = control.GetComponent<Image>();

                image.color = lightDiscColor;
                foreach (var graphic in control.GetComponentsInChildren<Graphic>(true)) {
                    if (graphic != image) graphic.color = darkGlyphColor;
                }
            }
            SyncAppBar();
        }

        private void PlaceCardControls(
            float safeTop
          , float safeLeft
          , float safeRight) {
            foreach (var (control, onLeft) in new[] {
                         (closeButton != null ? closeButton.gameObject : null
                        , closeControlOnLeft)
                       , (countdownControl, countdownControlOnLeft) }) {
                if (control == null) continue;

                var rect = control.GetComponent<RectTransform>();
                var size = new Vector2(ControlSizeDp, ControlSizeDp);
                var y = -(safeTop + CardControlTopDp);
                if (onLeft) {
                    SetTopLeftRect(rect, new(safeLeft + CardControlInsetDp, y), size);
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
        // draws its own: white, its left end round, running off the screen's
        // right edge - the game's icon and name, a spinner while the close is
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
            shape.color = Color.white;
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
                barObject.transform, Application.productName, 15, primaryTextOnLightColor);
            name.alignment = TextAnchor.MiddleLeft;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            var nameLayout = name.GetComponent<LayoutElement>();
            nameLayout.flexibleWidth = 1f;

            var spinnerObject = new GameObject(
                "Spinner", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            spinnerObject.transform.SetParent(barObject.transform, false);
            var spinnerImage = spinnerObject.GetComponent<Image>();
            spinnerImage.sprite = ArcSprite();
            spinnerImage.color = cardActionColor;
            spinnerImage.raycastTarget = false;
            var spinnerLayout = spinnerObject.GetComponent<LayoutElement>();
            spinnerLayout.minWidth = spinnerLayout.preferredWidth = 20f;
            spinnerLayout.minHeight = spinnerLayout.preferredHeight = 20f;
            appBarSpinner = spinnerObject.GetComponent<RectTransform>();

            var go = CardText(
                barObject.transform, AppBarContinueText + "  ›", 14, cardSecondaryColor);
            go.alignment = TextAnchor.MiddleRight;
            go.horizontalOverflow = HorizontalWrapMode.Overflow;
            appBarReady = go.gameObject;
            SyncAppBar();
        }

        private void PlaceAppBar(float width, float safeTop) {
            var overhang = AppBarHeightDp / 2f;
            SetTopRightRect(
                appBar
              , new(overhang, -(safeTop + AppBarGapDp))
              , new(width * AppBarWidthShare + overhang, AppBarHeightDp));
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