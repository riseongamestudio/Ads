using System;
using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    internal sealed class EditorAdChoicesGraphic : MaskableGraphic {
        private const int InfoDotSegments = 12;
        private const float IconSizeRatio = 0.72f;
        private const float Half = 0.5f;
        private const float InfoXOffsetRatio = 0.24f;
        private const float InfoBarWidthRatio = 0.12f;
        private const float InfoBarHeightRatio = 0.34f;
        private const float InfoBarYOffsetRatio = 0.34f;
        private const float InfoDotYOffsetRatio = 0.28f;
        private const float InfoDotRadiusRatio = 0.62f;

        private static readonly Color backgroundColor =
                new(1f, 1f, 1f, 0.92f);
        private static readonly Color iconColor =
                new(0.10f, 0.56f, 0.82f, 1f);
        private static readonly Color infoColor =
                Color.white;

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
            var center = rect.center;
            var half = size * Half;
            EditorGraphicUtility.AddTriangle(
                vertexHelper
              , new(center.x - half, center.y - half)
              , new(center.x + half, center.y)
              , new(center.x - half, center.y + half)
              , iconColor);

            var barWidth = size * InfoBarWidthRatio;
            var barHeight = size * InfoBarHeightRatio;
            EditorGraphicUtility.AddSolidQuad(
                vertexHelper
              , new Rect(
                    center.x
                            - half * InfoXOffsetRatio
                            - barWidth * Half
                  , center.y
                            - half * InfoBarYOffsetRatio
                  , barWidth
                  , barHeight)
              , infoColor);
            EditorGraphicUtility.AddCircle(
                vertexHelper
              , new(
                    center.x - half * InfoXOffsetRatio
                  , center.y
                            + half * InfoDotYOffsetRatio)
              , barWidth * InfoDotRadiusRatio
              , infoColor
              , InfoDotSegments);
        }
    }
}