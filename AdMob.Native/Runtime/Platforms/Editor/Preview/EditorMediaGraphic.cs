using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    // The test creative: AdMob's blues on a diagonal under soft light
    // discs, as a promo card is drawn. The discs stay inside the box, as
    // nothing here clips them.
    internal sealed class EditorMediaGraphic : MaskableGraphic {
        private const int GlowSegments = 48;

        private static readonly Color fromColor =
                new(0.09f, 0.28f, 0.66f, 1f);
        private static readonly Color toColor =
                new(0.26f, 0.52f, 0.96f, 1f);
        private static readonly Color glowColor =
                new(1f, 1f, 1f, 0.09f);

        // Each disc as its center's share of the box and its radius's
        // share of the box's shorter side.
        private static readonly Vector3[] glows = {
            new(0.78f, 0.70f, 0.18f)
          , new(0.20f, 0.26f, 0.16f)
          , new(0.62f, 0.16f, 0.10f)
        };

        protected override void OnPopulateMesh(VertexHelper vertexHelper) {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var middle = Color.Lerp(fromColor, toColor, 0.5f);
            var start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(
                new(rect.xMin, rect.yMin)
              , fromColor
              , new(0f, 0f));
            vertexHelper.AddVert(
                new(rect.xMin, rect.yMax)
              , middle
              , new(0f, 1f));
            vertexHelper.AddVert(
                new(rect.xMax, rect.yMax)
              , toColor
              , new(1f, 1f));
            vertexHelper.AddVert(
                new(rect.xMax, rect.yMin)
              , middle
              , new(1f, 0f));
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start + 2, start + 3, start);

            var side = Mathf.Min(rect.width, rect.height);
            foreach (var glow in glows) {
                EditorGraphicUtility.AddCircle(
                    vertexHelper
                  , new(
                        Mathf.Lerp(rect.xMin, rect.xMax, glow.x)
                      , Mathf.Lerp(rect.yMin, rect.yMax, glow.y))
                  , side * glow.z
                  , glowColor
                  , GlowSegments);
            }
        }
    }
}