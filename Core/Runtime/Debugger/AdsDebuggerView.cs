using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RiseOn.Consent;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RiseOn.Ads {
    /// <summary>The window behind <see cref="AdsDebugger"/>, built in code so that the pack ships no prefab.</summary>
    internal sealed class AdsDebuggerView : MonoBehaviour {
        // Above every canvas of the game; the pack's covers are native and stay above it.
        private const int SortingOrder = short.MaxValue;

        private const int   FontSize     = 30;
        private const int   NoteSize     = 26;
        private const float ButtonHeight = 84;

        private static readonly Color DimColor    = new(0, 0, 0, 0.7f);
        private static readonly Color PanelColor  = new(0.13f, 0.13f, 0.15f);
        private static readonly Color ButtonColor = new(0.22f, 0.42f, 0.86f);
        private static readonly Color MutedColor  = new(0.72f, 0.72f, 0.75f);
        private static readonly Color WarnColor   = new(1f, 0.75f, 0.35f);

        private readonly CancellationTokenSource lifetime = new();
        // The SDKs' tools answer from their own threads; Update applies the answers.
        private readonly List<Action> answers = new();

        private Text       idValue;
        private Text       copyLabel;
        private Button     copyButton;
        private string     advertisingId;
        private GameObject ownEventSystem;

        public static AdsDebuggerView Create() {
            var root = new GameObject(nameof(AdsDebugger), typeof(RectTransform));
            DontDestroyOnLoad(root);
            var view = root.AddComponent<AdsDebuggerView>();
            view.Build();
            return view;
        }

        private void Build() {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = Screen.width < Screen.height ? 0 : 1;
            gameObject.AddComponent<GraphicRaycaster>();

            // Takes every touch the window does not, so that none reaches the game.
            Stretch(Child(transform, "Blocker")).gameObject.AddComponent<Image>().color = DimColor;

            var safe = Child(transform, "Safe Area");
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;

            var panel = Child(safe, "Panel");
            panel.anchorMin = new Vector2(0.04f, 0.5f);
            panel.anchorMax = new Vector2(0.96f, 0.5f);
            panel.pivot     = new Vector2(0.5f, 0.5f);
            panel.gameObject.AddComponent<Image>().color = PanelColor;
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding                = new RectOffset(32, 32, 32, 32);
            layout.spacing                = 18;
            layout.childControlWidth      = true;
            layout.childControlHeight     = true;
            layout.childForceExpandWidth  = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddText(panel, font, "RiseOn Ads Debugger", FontSize + 8, Color.white, FontStyle.Bold);

            AddText(panel, font, "Advertising ID: register it as a test device on the AdMob and LevelPlay dashboards", NoteSize, MutedColor);
            idValue    = AddText(panel, font, "Reading", FontSize, Color.white);
            copyButton = AddButton(panel, font, "Copy", Copy, out copyLabel);
            copyButton.interactable = false;

            if (Application.platform is RuntimePlatform.IPhonePlayer) {
                var tracking = ConsentFlow.State?.TrackingStatus;
                AddText(panel, font, $"Tracking (ATT): {(tracking.HasValue ? tracking.Value.ToString() : "not asked yet")}", NoteSize, MutedColor);
                // Restricted: Screen Time or device management holds the switch, so the app's settings have none to turn on.
                if (tracking is TrackingStatus.Denied) {
                    AddButton(panel, font, "Allow tracking in Settings", () => Application.OpenURL("app-settings:"), out _);
                }
            }

            var mediations = AdsSdk.RunningMediations;
            if (mediations.Count == 0) AddText(panel, font, "No mediation is running yet: the pack starts them once the consent flow allows ads", NoteSize, WarnColor);
            foreach (var mediation in mediations) {
                if (mediation is not IAdMediationDebugger debugger) {
                    AddText(panel, font, $"{mediation.Id}: no debugging tool", NoteSize, MutedColor);
                    continue;
                }

                Text note = null;
                AddButton(panel, font, $"{mediation.Id}: {debugger.DebuggerName}", () => Open(debugger, note), out _);
                note = AddText(panel, font, "", NoteSize, WarnColor);
                note.gameObject.SetActive(false);
            }

            AddButton(panel, font, "Close", AdsDebugger.Hide, out _);

            EnsureEventSystem();
            ReadIdAsync().Forget();

            static Text AddText(Transform parent, Font font, string value, int size, Color color, FontStyle style = FontStyle.Normal) {
                var text = Child(parent, "Text").gameObject.AddComponent<Text>();
                text.font               = font;
                text.fontSize           = size;
                text.fontStyle          = style;
                text.color              = color;
                text.text               = value;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow   = VerticalWrapMode.Overflow;
                text.raycastTarget      = false;
                return text;
            }

            static Button AddButton(Transform parent, Font font, string value, UnityAction onClick, out Text label) {
                var rect = Child(parent, value);
                rect.gameObject.AddComponent<Image>().color             = ButtonColor;
                rect.gameObject.AddComponent<LayoutElement>().minHeight = ButtonHeight;
                var button = rect.gameObject.AddComponent<Button>();
                button.onClick.AddListener(onClick);

                label               = Stretch(Child(rect, "Label")).gameObject.AddComponent<Text>();
                label.font          = font;
                label.fontSize      = FontSize;
                label.color         = Color.white;
                label.alignment     = TextAnchor.MiddleCenter;
                label.text          = value;
                label.raycastTarget = false;
                return button;
            }
        }

        private void Update() {
            Action[] pending;
            lock (answers) {
                if (answers.Count == 0) return;
                pending = answers.ToArray();
                answers.Clear();
            }

            foreach (var answer in pending) answer();
        }

        private void OnDestroy() {
            lifetime.Cancel();
            lifetime.Dispose();
            if (ownEventSystem != null) Destroy(ownEventSystem);
        }

        private void Copy() {
            GUIUtility.systemCopyBuffer = advertisingId;
            copyLabel.text              = "Copied";
        }

        private void Open(IAdMediationDebugger debugger, Text note) {
            note.gameObject.SetActive(false);
            debugger.OpenDebugger(problem => {
                lock (answers) {
                    answers.Add(() => {
                        note.text = problem;
                        note.gameObject.SetActive(problem != null);
                    });
                }
            });
        }

        private async UniTaskVoid ReadIdAsync() {
            var (id, problem) = await AdvertisingId.ReadAsync(lifetime.Token);
            advertisingId           = id;
            idValue.text            = id ?? problem;
            idValue.color           = id == null ? WarnColor : Color.white;
            copyButton.interactable = id != null;
        }

        // A cheat menu drawn without uGUI leaves the scene with no event system, and the window's buttons would hear no touch.
        private void EnsureEventSystem() {
            if (FindAnyObjectByType<EventSystem>() != null) return;

            ownEventSystem = new GameObject(nameof(EventSystem), typeof(EventSystem));
            DontDestroyOnLoad(ownEventSystem);
#if ENABLE_INPUT_SYSTEM && RISEON_ADS_INPUTSYSTEM
            ownEventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            ownEventSystem.AddComponent<StandaloneInputModule>();
#endif
        }

        private static RectTransform Child(Transform parent, string name) {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        private static RectTransform Stretch(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}