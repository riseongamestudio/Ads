using System;
using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    internal sealed class EditorMediaGraphic : MaskableGraphic {
        private const int SunSegments = 18;
        private const float SunRadiusRatio = 0.09f;
        private const float SunXRatio = 0.78f;
        private const float SunYRatio = 0.74f;
        private const float BackPeakXRatio = 0.48f;
        private const float BackPeakYRatio = 0.70f;
        private const float BackEndXRatio = 0.88f;
        private const float FrontStartXRatio = 0.24f;
        private const float FrontPeakXRatio = 0.68f;
        private const float FrontPeakYRatio = 0.58f;

        private static readonly Color topColor =
                new(0.10f, 0.15f, 0.24f, 1f);
        private static readonly Color bottomColor =
                new(0.08f, 0.43f, 0.48f, 1f);
        private static readonly Color backMountainColor =
                new(0.22f, 0.61f, 0.62f, 0.88f);
        private static readonly Color frontMountainColor =
                new(0.10f, 0.28f, 0.38f, 0.94f);
        private static readonly Color sunColor =
                new(1f, 0.76f, 0.24f, 0.95f);

        protected override void OnPopulateMesh(VertexHelper vertexHelper) {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            EditorGraphicUtility.AddGradientQuad(
                vertexHelper
              , rect
              , bottomColor
              , topColor);

            var sunRadius =
                    Mathf.Min(rect.width, rect.height)
                            * SunRadiusRatio;
            EditorGraphicUtility.AddCircle(
                vertexHelper
              , new(
                    Mathf.Lerp(rect.xMin, rect.xMax, SunXRatio)
                  , Mathf.Lerp(rect.yMin, rect.yMax, SunYRatio))
              , sunRadius
              , sunColor
              , SunSegments);

            EditorGraphicUtility.AddTriangle(
                vertexHelper
              , new(rect.xMin, rect.yMin)
              , new(
                    Mathf.Lerp(
                        rect.xMin
                      , rect.xMax
                      , BackPeakXRatio)
                  , Mathf.Lerp(
                        rect.yMin
                      , rect.yMax
                      , BackPeakYRatio))
              , new(
                    Mathf.Lerp(
                        rect.xMin
                      , rect.xMax
                      , BackEndXRatio)
                  , rect.yMin)
              , backMountainColor);
            EditorGraphicUtility.AddTriangle(
                vertexHelper
              , new(
                    Mathf.Lerp(
                        rect.xMin
                      , rect.xMax
                      , FrontStartXRatio)
                  , rect.yMin)
              , new(
                    Mathf.Lerp(
                        rect.xMin
                      , rect.xMax
                      , FrontPeakXRatio)
                  , Mathf.Lerp(
                        rect.yMin
                      , rect.yMax
                      , FrontPeakYRatio))
              , new(rect.xMax, rect.yMin)
              , frontMountainColor);
        }
    }
}