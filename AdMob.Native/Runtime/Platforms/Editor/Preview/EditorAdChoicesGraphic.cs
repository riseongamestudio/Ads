using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    // The AdChoices mark as the Digital Advertising Alliance draws it: a
    // cyan rounded triangle in outline, open at its bottom left, with an i
    // inside, on a light chip.
    internal sealed class EditorAdChoicesGraphic : MaskableGraphic {
        private const int CapSegments = 12;
        private const float IconSizeRatio = 0.8f;
        private const float EdgeHalfWidthRatio = 0.07f;
        private const float StemHalfWidthRatio = 0.06f;
        private const float DotRadiusRatio = 0.075f;

        private static readonly Color backgroundColor =
                new(1f, 1f, 1f, 0.9f);
        private static readonly Color iconColor =
                new(0f, 0.68f, 0.8f, 1f);

        protected override void OnPopulateMesh(VertexHelper vertexHelper) {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            EditorGraphicUtility.AddSolidQuad(
                vertexHelper
              , rect
              , backgroundColor);

            var size =
                    Mathf.Min(rect.width, rect.height)
                            * IconSizeRatio;
            Vector2 At(float x, float y) =>
                    rect.center + new Vector2(x, y) * size;

            var edge = size * EdgeHalfWidthRatio;
            // The left edge runs on into a tail; the lower edge stops short
            // of it.
            AddLine(vertexHelper, At(-0.3f, 0.36f), At(-0.3f, -0.42f), edge);
            AddLine(vertexHelper, At(-0.3f, 0.36f), At(0.36f, 0f), edge);
            AddLine(vertexHelper, At(0.36f, 0f), At(-0.13f, -0.27f), edge);
            EditorGraphicUtility.AddCircle(
                vertexHelper
              , At(-0.06f, 0.13f)
              , size * DotRadiusRatio
              , iconColor
              , CapSegments);
            AddLine(
                vertexHelper
              , At(-0.06f, -0.02f)
              , At(-0.06f, -0.2f)
              , size * StemHalfWidthRatio);
        }

        // A stroke with round ends.
        private void AddLine(
            VertexHelper vertexHelper
          , Vector2 from
          , Vector2 to
          , float halfWidth) {
            var side =
                    Vector2.Perpendicular((to - from).normalized)
                            * halfWidth;
            var start = vertexHelper.currentVertCount;
            EditorGraphicUtility.AddVertex(
                vertexHelper
              , from - side
              , iconColor
              , Vector2.zero);
            EditorGraphicUtility.AddVertex(
                vertexHelper
              , from + side
              , iconColor
              , Vector2.up);
            EditorGraphicUtility.AddVertex(
                vertexHelper
              , to + side
              , iconColor
              , Vector2.one);
            EditorGraphicUtility.AddVertex(
                vertexHelper
              , to - side
              , iconColor
              , Vector2.right);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start + 2, start + 3, start);
            EditorGraphicUtility.AddCircle(
                vertexHelper
              , from
              , halfWidth
              , iconColor
              , CapSegments);
            EditorGraphicUtility.AddCircle(
                vertexHelper
              , to
              , halfWidth
              , iconColor
              , CapSegments);
        }
    }
}