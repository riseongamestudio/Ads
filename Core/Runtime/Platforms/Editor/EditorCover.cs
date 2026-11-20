using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Ads.Platforms.Editor {
    // One cover as the preview draws it: a flat color on a canvas of its own,
    // at the sorting order its kind is given.
    //
    // It swallows touches over its area the way the device's window does, and
    // nothing outside it: a half-screen cover leaves the strip above it empty,
    // so touches there reach the game. It survives scene loads, because the
    // device's cover is not part of any scene either - a cover raised over a
    // level change is still up when the next level arrives.
    internal sealed class EditorCover : MonoBehaviour {
        private const string AREA_OBJECT_NAME = "Area";

        private Canvas canvas;
        private Image area;

        // Created hidden: the half-screen cover waits for Reveal, and the
        // full-screen one is revealed straight after.
        internal static EditorCover Create(string objectName, int sortingOrder) {
            var coverObject = new GameObject(
                objectName
              , typeof(RectTransform)
              , typeof(Canvas)
              , typeof(GraphicRaycaster)
              , typeof(EditorCover));
            DontDestroyOnLoad(coverObject);

            var cover = coverObject.GetComponent<EditorCover>();
            cover.canvas = coverObject.GetComponent<Canvas>();
            cover.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cover.canvas.sortingOrder = sortingOrder;
            cover.canvas.enabled = false;

            var areaObject = new GameObject(
                AREA_OBJECT_NAME
              , typeof(RectTransform)
              , typeof(CanvasRenderer)
              , typeof(Image));
            areaObject.transform.SetParent(coverObject.transform, false);

            cover.area = areaObject.GetComponent<Image>();
            // Even when fully transparent: the device's window still takes
            // the touches that land on it.
            cover.area.raycastTarget = true;
            return cover;
        }

        // Measured from the bottom edge, as the device measures it, in Screen
        // pixels. At or below 0, or at or above the screen, means all of it.
        internal void Apply(Color color, float heightPx) {
            area.color = color;

            var screenHeight = (float)Screen.height;
            var heightRatio = heightPx > 0f && heightPx < screenHeight
                    ? heightPx / screenHeight
                    : 1f;
            var rect = area.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new(1f, heightRatio);
            rect.pivot = new(.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // The canvas, not the GameObject: hidden, the cover keeps its place,
        // and draws and catches nothing.
        internal void SetRevealed(bool revealed) {
            canvas.enabled = revealed;
        }

        internal void Close() {
            Destroy(gameObject);
        }
    }
}