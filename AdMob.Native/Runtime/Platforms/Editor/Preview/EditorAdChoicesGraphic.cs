using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    // The AdChoices mark as the device shows it: a cyan i in a ring on a
    // light square chip.
    internal sealed class EditorAdChoicesGraphic : MaskableGraphic {
        private const int CapSegments = 12;
        private const float IconSizeRatio = 0.8f;
        private const float EdgeHalfWidthRatio = 0.07f;
        private const float StemHalfWidthRatio = 0.06f;
        private const float DotRadiusRatio = 0.075f;

        private const int RingSegments = 28;
        private const float RingRadiusRatio = 0.4f;

        private static readonly Color backgroundColor =
                new(.918f, .918f, .918f, 1f);
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
            for (var segment = 0; segment < RingSegments; ++segment) {
                var from = segment * Mathf.PI * 2f / RingSegments;
                var to = (segment + 1) * Mathf.PI * 2f / RingSegments;
                AddLine(
                    vertexHelper
                  , At(Mathf.Cos(from) * RingRadiusRatio, Mathf.Sin(from) * RingRadiusRatio)
                  , At(Mathf.Cos(to) * RingRadiusRatio, Mathf.Sin(to) * RingRadiusRatio)
                  , edge);
            }
            EditorGraphicUtility.AddCircle(
                vertexHelper
              , At(0f, 0.18f)
              , size * DotRadiusRatio
              , iconColor
              , CapSegments);
            AddLine(
                vertexHelper
              , At(0f, 0.04f)
              , At(0f, -0.2f)
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