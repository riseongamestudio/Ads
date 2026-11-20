using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Ads.Platforms.Editor {
    /// <summary>
    /// The pack's placeholders as Play mode draws them, each on a canvas of its own at its layer of <see cref="AdsEditorLayers"/>, sized in device points as on a phone 360 points across: a full-screen ad with its close button, held back five seconds for a rewarding format, which then rewards; a banner and an MRec where the page's Formats section places them in the safe area.<br/>
    /// Each shows the name of the mediation that would serve it, and nothing else.
    /// </summary>
    internal sealed class EditorPlaceholdersUI : IEditorPlaceholders {
        private const float RewardSeconds = 5;

        private static readonly Color ground = new(.13f, .13f, .15f, 1);
        private static readonly Color ink    = new(.92f, .92f, .94f, 1);
        private static readonly Color paper  = new(.95f, .95f, .96f, 1);
        private static readonly Color line   = new(.2f, .2f, .22f, 1);

        private GameObject banner;
        private GameObject mRec;

        public void ShowFullScreen(string mediation, AdFormat format, IAdShowListener listener) => EditorThread.Run(() => {
            var root = Canvas("Ad Placeholder", AdsEditorLayers.FullScreenAd);
            var panel = Box(root.transform, ground);
            Stretch(panel.rectTransform, Vector2.zero, Vector2.one);
            panel.raycastTarget = true;

            var label = Label(panel.transform, mediation, 40, ink);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);

            var close = root.AddComponent<EditorFullScreenPlaceholder>();
            close.Begin(listener, format.IsRewarding() ? RewardSeconds : 0, Button(root.transform));
            listener.OnDisplayed();
        });

        // An adaptive banner spans the safe area; its height stays the standard one, as the network's own varies with the device.
        public void ShowBanner(string mediation, AdBannerSettings settings) => EditorThread.Run(() => {
            if (banner) Object.Destroy(banner);
            banner = Bar("Banner Placeholder", mediation, new Vector2(320, 50), settings.Adaptive, settings.Position, 18);
        });

        public void HideBanner() => EditorThread.Run(() => {
            if (banner) Object.Destroy(banner);
            banner = null;
        });

        public void ShowMRec(string mediation, AdMRecSettings settings) => EditorThread.Run(() => {
            if (mRec) Object.Destroy(mRec);
            mRec = Bar("MRec Placeholder", mediation, new Vector2(300, 250), false, settings.Position, 28);
        });

        public void HideMRec() => EditorThread.Run(() => {
            if (mRec) Object.Destroy(mRec);
            mRec = null;
        });

        // A box of the given device points at its place in the safe area, or as wide as the safe area; on a device two at the same place overlap, and so do these.
        private static GameObject Bar(string name, string mediation, Vector2 points, bool fullWidth, AdScreenPosition position, int fontPoints) {
            var root   = Canvas(name, AdsEditorLayers.OverGame);
            var area   = SafeArea(root.transform);
            var scale  = Points();
            var box    = Box(area, paper);
            var rect   = box.rectTransform;
            var anchor = Anchor(position);
            rect.anchorMin        = fullWidth ? new Vector2(0, anchor.y) : anchor;
            rect.anchorMax        = fullWidth ? new Vector2(1, anchor.y) : anchor;
            rect.pivot            = anchor;
            rect.sizeDelta        = new Vector2(fullWidth ? 0 : points.x * scale, points.y * scale);
            rect.anchoredPosition = Vector2.zero;
            box.raycastTarget     = true;
            box.gameObject.AddComponent<Outline>().effectColor = line;

            var label = Label(box.transform, mediation, fontPoints, line);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            return root;
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
                default:                           throw new System.ArgumentOutOfRangeException(nameof(position), position, null);
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
            Stretch(area, new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height), new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height));
            return area;
        }

        private static Image Box(Transform parent, Color color) {
            var image = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color         = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text Label(Transform parent, string text, float points, Color color) {
            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize      = Mathf.RoundToInt(points * Points());
            label.alignment     = TextAnchor.MiddleCenter;
            label.color         = color;
            label.text          = text;
            label.raycastTarget = false;
            return label;
        }

        // The close button in the safe area's top right corner, as most networks place it.
        private static (Button Button, Text Label) Button(Transform parent) {
            var area  = SafeArea(parent);
            var scale = Points();
            var image = Box(area, new Color(1, 1, 1, .15f));
            var rect  = image.rectTransform;
            rect.anchorMin        = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta        = new Vector2(40, 40) * scale;
            rect.anchoredPosition = new Vector2(-12, -12) * scale;
            image.raycastTarget   = true;

            var label = Label(image.transform, "", 22, ink);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            return (image.gameObject.AddComponent<Button>(), label);
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max) {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // Pixels per device point, as on a phone 360 points across, the most common width: the Game view's dpi is the monitor's,
        // which would draw a phone-sized view's placeholders a third of their size.
        private static float Points() {
            return Mathf.Min(Screen.width, Screen.height) / 360f;
        }
    }

    /// <summary>A full-screen placeholder's close: a countdown on the button first for a rewarding format, in real time, as the game is paused under it.</summary>
    internal sealed class EditorFullScreenPlaceholder : MonoBehaviour {
        private IAdShowListener listener;
        private Button          button;
        private Text            label;
        private float           rewardAt;
        private bool            rewards;
        private bool            closed;

        public void Begin(IAdShowListener listener, float rewardSeconds, (Button Button, Text Label) close) {
            this.listener = listener;
            (button, label) = close;
            rewards  = rewardSeconds > 0;
            rewardAt = Time.realtimeSinceStartup + rewardSeconds;
            button.onClick.AddListener(Close);
            Update();
        }

        private void Update() {
            var left = rewardAt - Time.realtimeSinceStartup;
            button.interactable = left <= 0;
            label.text          = left > 0 ? Mathf.CeilToInt(left).ToString() : "X";
        }

        private void Close() {
            if (closed) return;

            closed = true;
            if (rewards) listener.OnRewarded();
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