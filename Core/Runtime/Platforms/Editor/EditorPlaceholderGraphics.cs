using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RiseOn.Ads.Platforms.Editor {
    /// <summary>A placeholder's creative: a diagonal gradient between two colors, under soft light discs, as a promo card is drawn.</summary>
    internal sealed class EditorMediaGraphic : MaskableGraphic {
        private static readonly Color glow = new(1, 1, 1, .09f);

        public Color From = new(.2f, .22f, .3f, 1);
        public Color To   = new(.35f, .38f, .5f, 1);

        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            var side = Mathf.Min(rect.width, rect.height);
            Vector2 At(float x, float y) => new(Mathf.Lerp(rect.xMin, rect.xMax, x), Mathf.Lerp(rect.yMin, rect.yMax, y));

            EditorShapes.Diagonal(mesh, rect, From, To);
            EditorShapes.Circle(mesh, At(.86f, .82f), side * .42f, glow, 48);
            EditorShapes.Circle(mesh, At(.1f, .12f), side * .3f, glow, 48);
            EditorShapes.Circle(mesh, At(.7f, .05f), side * .16f, glow, 48);
        }
    }

    /// <summary>
    /// The AdChoices mark as the pack's native ads show it on a device: a cyan i in a ring on a light square chip, the same in either appearance.
    /// </summary>
    internal sealed class EditorAdChoicesGraphic : MaskableGraphic {
        private static readonly Color chip = new(.918f, .918f, .918f, 1);
        private static readonly Color cyan = new(0, .68f, .8f, 1);

        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            var rect   = GetPixelAdjustedRect();
            var cell   = Mathf.Min(rect.width, rect.height);
            var middle = rect.center;
            var radius = cell * .32f;
            EditorShapes.Quad(mesh, rect, chip, chip);
            EditorShapes.Ring(mesh, middle, radius, cell * .056f, cyan);
            EditorShapes.Circle(mesh, middle + new Vector2(0, radius * .45f), cell * .06f, cyan, 10);
            EditorShapes.Line(mesh, middle + new Vector2(0, radius * .1f), middle - new Vector2(0, radius * .5f), cell * .048f, cyan);
        }
    }

    /// <summary>
    /// A close cross of two strokes, drawn rather than typed, as fonts draw ✕ each their own way.<br/>
    /// One mark for every close of every format, AdMob.Native's and the device's included: Google's own, measured on a device, ink across 44% of its disc in strokes a tenth of it.
    /// </summary>
    internal sealed class EditorCrossGraphic : MaskableGraphic {
        // Shares of the disc's side: a stroke's centre line from the middle to its end, and half its width; the round ends add the half width to the ink.
        private const float Reach      = .17f;
        private const float HalfStroke = .05f;

        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            var rect   = GetPixelAdjustedRect();
            var reach  = Mathf.Min(rect.width, rect.height) * Reach;
            var stroke = Mathf.Min(rect.width, rect.height) * HalfStroke;
            EditorShapes.Line(mesh, rect.center + new Vector2(-reach, -reach), rect.center + new Vector2(reach, reach), stroke, color);
            EditorShapes.Line(mesh, rect.center + new Vector2(-reach, reach), rect.center + new Vector2(reach, -reach), stroke, color);
        }
    }

    /// <summary>A chevron pointing right, the way on to something: two round-ended strokes.</summary>
    internal sealed class EditorChevronGraphic : MaskableGraphic {
        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            var rect   = GetPixelAdjustedRect();
            var side   = Mathf.Min(rect.width, rect.height);
            var stroke = side * .07f;
            var tip    = rect.center + new Vector2(side * .14f, 0);
            EditorShapes.Line(mesh, tip + new Vector2(-side * .26f, side * .28f), tip, stroke, color);
            EditorShapes.Line(mesh, tip + new Vector2(-side * .26f, -side * .28f), tip, stroke, color);
        }
    }

    /// <summary>A video's controls as a player draws them over the picture: a speaker, or the pause bars.</summary>
    internal sealed class EditorVideoGlyphGraphic : MaskableGraphic {
        public bool Pause;

        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            var side = Mathf.Min(rect.width, rect.height);
            Vector2 At(float x, float y) => rect.center + new Vector2(x, y) * side;
            if (Pause) {
                EditorShapes.Quad(mesh, Rect.MinMaxRect(At(-.22f, -.3f).x, At(-.22f, -.3f).y, At(-.07f, .3f).x, At(-.07f, .3f).y), color, color);
                EditorShapes.Quad(mesh, Rect.MinMaxRect(At(.07f, -.3f).x, At(.07f, -.3f).y, At(.22f, .3f).x, At(.22f, .3f).y), color, color);
                return;
            }

            EditorShapes.Quad(mesh, Rect.MinMaxRect(At(-.36f, -.12f).x, At(-.36f, -.12f).y, At(-.2f, .12f).x, At(-.2f, .12f).y), color, color);
            EditorShapes.Triangle(mesh, At(-.2f, .12f), At(.04f, .32f), At(.04f, -.32f), color);
            EditorShapes.Triangle(mesh, At(-.2f, .12f), At(.04f, -.32f), At(-.2f, -.12f), color);
            EditorShapes.Arc(mesh, At(.04f, 0), side * .18f, -50, 50, side * .035f, color);
            EditorShapes.Arc(mesh, At(.04f, 0), side * .32f, -50, 50, side * .035f, color);
        }
    }

    /// <summary>The store's mark beside its name: Google Play's four-colored triangle, or the App Store's blue disc with its A.</summary>
    internal sealed class EditorStoreGraphic : MaskableGraphic {
        private static readonly Color green  = new(0, .63f, .45f, 1);
        private static readonly Color blue   = new(.26f, .52f, .96f, 1);
        private static readonly Color red    = new(.92f, .26f, .21f, 1);
        private static readonly Color yellow = new(.98f, .74f, .02f, 1);
        private static readonly Color sky    = new(.05f, .59f, .96f, 1);

        public bool AppStore;

        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            var rect = GetPixelAdjustedRect();
            var side = Mathf.Min(rect.width, rect.height);
            Vector2 At(float x, float y) => rect.center + new Vector2(x, y) * side;
            if (AppStore) {
                EditorShapes.Circle(mesh, rect.center, side * .5f, sky, 32);
                var stroke = side * .055f;
                EditorShapes.Line(mesh, At(-.2f, -.24f), At(.06f, .24f), stroke, Color.white);
                EditorShapes.Line(mesh, At(.2f, -.24f), At(-.06f, .24f), stroke, Color.white);
                EditorShapes.Line(mesh, At(-.27f, -.08f), At(.27f, -.08f), stroke, Color.white);
                return;
            }

            var top    = At(-.36f, .46f);
            var bottom = At(-.36f, -.46f);
            var tip    = At(.44f, 0);
            var middle = At(-.02f, 0);
            EditorShapes.Triangle(mesh, top, bottom, middle, blue);
            EditorShapes.Triangle(mesh, top, middle, tip, green);
            EditorShapes.Triangle(mesh, bottom, tip, middle, red);
            EditorShapes.Triangle(mesh, tip, At(.18f, .15f), At(.18f, -.15f), yellow);
        }
    }

    /// <summary>A loading spinner: three quarters of a ring, turning once a second in real time, as the game may be paused under it.</summary>
    internal sealed class EditorSpinnerGraphic : MaskableGraphic {
        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            var rect   = GetPixelAdjustedRect();
            var radius = Mathf.Min(rect.width, rect.height) * .4f;
            EditorShapes.Arc(mesh, rect.center, radius, 0, 270, radius * .14f, color);
        }

        private void Update() {
            rectTransform.localEulerAngles = new Vector3(0, 0, -Time.realtimeSinceStartup * 360 % 360);
        }
    }

    /// <summary>Hands its size to a placeholder's layout once built and each time it changes, as when the Game view turns from tall to wide.</summary>
    internal sealed class EditorResized : UIBehaviour {
        public Action<Vector2> Resized;

        private Vector2 laidOut;

        private void LateUpdate() => Check();

        public void Check() {
            var size = ((RectTransform) transform).rect.size;
            if (size == laidOut || size.x <= 0 || size.y <= 0) return;

            laidOut = size;
            Resized?.Invoke(size);
        }
    }

    /// <summary>For a layout group: a height that follows the width at a ratio, as a picture of fixed proportions takes.</summary>
    internal sealed class EditorAspectHeight : UIBehaviour, ILayoutElement {
        public float Ratio = 9 / 16f;
        // Takes the column's spare height too, past its ratio.
        public bool Flexible;

        private RectTransform Rect => (RectTransform) transform;

        public float minWidth       => -1;
        public float preferredWidth => -1;
        public float flexibleWidth  => -1;
        public float minHeight      => Rect.rect.width * Ratio;
        public float preferredHeight => Rect.rect.width * Ratio;
        public float flexibleHeight => Flexible ? 1 : -1;
        public int   layoutPriority => 1;

        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
    }

    internal static class EditorShapes {
        private static readonly Dictionary<int, Sprite> rounded = new();

        public static void Quad(VertexHelper mesh, Rect rect, Color bottom, Color top) {
            var start = mesh.currentVertCount;
            Vertex(mesh, new Vector2(rect.xMin, rect.yMin), bottom);
            Vertex(mesh, new Vector2(rect.xMin, rect.yMax), top);
            Vertex(mesh, new Vector2(rect.xMax, rect.yMax), top);
            Vertex(mesh, new Vector2(rect.xMax, rect.yMin), bottom);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }

        // From the bottom left corner to the top right one.
        public static void Diagonal(VertexHelper mesh, Rect rect, Color from, Color to) {
            var middle = Color.Lerp(from, to, .5f);
            var start  = mesh.currentVertCount;
            Vertex(mesh, new Vector2(rect.xMin, rect.yMin), from);
            Vertex(mesh, new Vector2(rect.xMin, rect.yMax), middle);
            Vertex(mesh, new Vector2(rect.xMax, rect.yMax), to);
            Vertex(mesh, new Vector2(rect.xMax, rect.yMin), middle);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }

        public static void Triangle(VertexHelper mesh, Vector2 first, Vector2 second, Vector2 third, Color color) {
            var start = mesh.currentVertCount;
            Vertex(mesh, first, color);
            Vertex(mesh, second, color);
            Vertex(mesh, third, color);
            mesh.AddTriangle(start, start + 1, start + 2);
        }

        public static void Circle(VertexHelper mesh, Vector2 center, float radius, Color color, int segments = 24) {
            var start = mesh.currentVertCount;
            Vertex(mesh, center, color);
            for (var i = 0; i < segments; i++) {
                var angle = i * Mathf.PI * 2 / segments;
                Vertex(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color);
            }

            for (var i = 0; i < segments; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % segments);
        }

        // A band along a circle between two angles in degrees, counterclockwise from the right.
        public static void Arc(VertexHelper mesh, Vector2 center, float radius, float from, float to, float halfWidth, Color color, int segments = 32) {
            var start = mesh.currentVertCount;
            for (var i = 0; i <= segments; i++) {
                var angle = Mathf.Lerp(from, to, i / (float) segments) * Mathf.Deg2Rad;
                var along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vertex(mesh, center + along * (radius - halfWidth), color);
                Vertex(mesh, center + along * (radius + halfWidth), color);
            }

            for (var i = 0; i < segments; i++) {
                var at = start + i * 2;
                mesh.AddTriangle(at, at + 1, at + 3);
                mesh.AddTriangle(at + 3, at + 2, at);
            }
        }

        public static void Ring(VertexHelper mesh, Vector2 center, float radius, float halfWidth, Color color) => Arc(mesh, center, radius, 0, 360, halfWidth, color, 48);

        // A bar from one point to another, as wide as twice the half-width.
        public static void Stroke(VertexHelper mesh, Vector2 from, Vector2 to, float halfWidth, Color color) {
            var side  = Vector2.Perpendicular((to - from).normalized) * halfWidth;
            var start = mesh.currentVertCount;
            Vertex(mesh, from - side, color);
            Vertex(mesh, from + side, color);
            Vertex(mesh, to + side, color);
            Vertex(mesh, to - side, color);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }

        // A stroke with round ends.
        public static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float halfWidth, Color color) {
            Stroke(mesh, from, to, halfWidth, color);
            Circle(mesh, from, halfWidth, color, 12);
            Circle(mesh, to, halfWidth, color, 12);
        }

        // A white box with corners of the radius in pixels, nine-sliced at it, its edge smoothed over one pixel; drawn once per radius. A tab rounds the bottom
        // right corner alone, as the Ad tag flush in a square corner.
        public static Sprite Rounded(int radius, bool tab = false) {
            var key = tab ? -radius : radius;
            if (rounded.TryGetValue(key, out var sprite) && sprite != null) return sprite;

            var size   = radius * 2 + 2;
            var half   = size / 2f;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++) {
                for (var x = 0; x < size; x++) {
                    var dx    = Mathf.Max(Mathf.Abs(x + .5f - half) - (half - radius), 0);
                    var dy    = Mathf.Max(Mathf.Abs(y + .5f - half) - (half - radius), 0);
                    var square = tab && (x + .5f < half || y + .5f > half);
                    var alpha = square ? 1f : Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte) Mathf.RoundToInt(alpha * 255));
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply();
            sprite           = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, Vector4.one * radius);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            rounded[key]     = sprite;
            return sprite;
        }

        private static void Vertex(VertexHelper mesh, Vector2 position, Color color) {
            var vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color    = color;
            mesh.AddVert(vertex);
        }
    }
}