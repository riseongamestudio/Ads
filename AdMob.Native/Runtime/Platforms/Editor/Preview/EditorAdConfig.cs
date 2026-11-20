using System;
using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    internal sealed class EditorAdConfig {
        private const float DEFAULT_HEIGHT_RATIO      = 0.5f;

        internal string                    AdUnitId          { get; }
        internal EditorAdMode Mode              { get; }
        internal CloseSettings             Close             { get; private set; }
        internal float                     HeightRatio       { get; }
        internal Color                     BackgroundColor   { get; }
        /// <summary>Rect ads only: the cell's corner radius in pixels.</summary>
        internal int                       RoundCornerPx     { get; }
        internal Vector2Int                PositionPx        { get; private set; }
        internal Vector2Int                SizePx            { get; }
        internal bool                      AllowsVideo       { get; }

        private EditorAdConfig(
            string adUnitId
          , EditorAdMode mode
          , in CloseSettings controls
          , float heightRatio
          , Color backgroundColor
          , int roundCornerPx
          , Vector2Int positionPx
          , Vector2Int sizePx
          , bool allowsVideo) {
            AdUnitId        = adUnitId;
            Mode            = mode;
            Close = controls;
            HeightRatio     = heightRatio;
            BackgroundColor = backgroundColor;
            RoundCornerPx   = Mathf.Max(0, roundCornerPx);
            PositionPx      = positionPx;
            SizePx          = sizePx;
            AllowsVideo     = allowsVideo;
        }

        internal static EditorAdConfig CreateFullScreen(
            string adUnitId
          , bool fullScreen
          , float heightRatio
          , Color backgroundColor
          , in CloseSettings controls) {
            return new(
                adUnitId
              , fullScreen
                    ? EditorAdMode.FullScreen
                    : EditorAdMode.Collapsible
              , controls
              , ResolveRatio(heightRatio, DEFAULT_HEIGHT_RATIO)
              , backgroundColor
              , 0
              , default
              , default
              , true);
        }

        internal static EditorAdConfig CreateRectAd(
            string adUnitId
          , Vector2Int positionPx
          , Vector2Int sizePx
          , Color backgroundColor
          , int roundCornerPx) {
            return new(
                adUnitId
              , EditorAdMode.RectAd
              , default
              , default
              , backgroundColor
              , roundCornerPx
              , positionPx
              , sizePx
              , true);
        }

        internal void SetClose(in CloseSettings controls) {
            Close = controls;
        }

        internal void SetPosition(Vector2Int positionPx) {
            PositionPx = positionPx;
        }

        internal EditorAdConfig Snapshot() {
            return new(
                AdUnitId
              , Mode
              , Close
              , HeightRatio
              , BackgroundColor
              , RoundCornerPx
              , PositionPx
              , SizePx
              , AllowsVideo);
        }

        private static float ResolveAlpha(float value, float defaultValue) {
            if (!float.IsFinite(value) || value < 0f) return defaultValue;
            return Mathf.Clamp01(value);
        }

        private static float ResolveRatio(float value, float defaultValue) {
            if (!float.IsFinite(value)) return defaultValue;
            return Mathf.Clamp01(value);
        }

        private static float ResolveNonNegative(float value) {
            return !float.IsFinite(value) || value < 0f
                    ? 0f
                    : value;
        }
    }
}