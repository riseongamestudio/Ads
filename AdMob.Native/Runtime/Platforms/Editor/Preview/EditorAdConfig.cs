using System;
using UnityEngine;
using UnityEngine.UI;

namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    internal sealed class EditorAdConfig {
        private const float DefaultHeightRatio      = 0.5f;

        internal string                    AdUnitId          { get; }
        internal EditorAdMode Mode              { get; }
        internal CloseSettings             Close             { get; private set; }
        internal float                     HeightRatio       { get; }
        /// <summary>The format's name, written on the creative.</summary>
        internal string                    FormatName        { get; }
        /// <summary>Rect ads only: the cell's corner radius in pixels.</summary>
        internal int                       RoundCornerPx     { get; }
        internal Vector2Int                PositionPx        { get; private set; }
        internal Vector2Int                SizePx            { get; }
        internal bool                      AllowsVideo       { get; }
        /// <summary>Full screen only: how the card is arranged.</summary>
        internal FullScreenLayout          Layout            { get; }

        private EditorAdConfig(
            string adUnitId
          , EditorAdMode mode
          , in CloseSettings controls
          , float heightRatio
          , string formatName
          , int roundCornerPx
          , Vector2Int positionPx
          , Vector2Int sizePx
          , bool allowsVideo
          , FullScreenLayout layout) {
            AdUnitId        = adUnitId;
            Mode            = mode;
            Close = controls;
            HeightRatio     = heightRatio;
            FormatName      = formatName;
            RoundCornerPx   = Mathf.Max(0, roundCornerPx);
            PositionPx      = positionPx;
            SizePx          = sizePx;
            AllowsVideo     = allowsVideo;
            Layout          = layout;
        }

        internal static EditorAdConfig CreateFullScreen(
            string adUnitId
          , bool fullScreen
          , float heightRatio
          , in CloseSettings controls
          , FullScreenLayout layout) {
            return new(
                adUnitId
              , fullScreen
                    ? EditorAdMode.FullScreen
                    : EditorAdMode.Collapsible
              , controls
              , ResolveRatio(heightRatio, DefaultHeightRatio)
              , !fullScreen
                    ? "Native Collapsible"
                    : layout switch {
                        FullScreenLayout.AppOpen => "Native App Open"
                      , FullScreenLayout.EndCard => "Native End Card"
                      , _                        => "Native Interstitial"
                    }
              , 0
              , default
              , default
              , true
              , layout);
        }

        internal static EditorAdConfig CreateRectAd(
            string adUnitId
          , Vector2Int positionPx
          , Vector2Int sizePx
          , string format
          , int roundCornerPx) {
            return new(
                adUnitId
              , EditorAdMode.RectAd
              , default
              , default
              , format == "NATIVE_MREC" ? "Native MRec" : "Native In-Feed"
              , roundCornerPx
              , positionPx
              , sizePx
              , true
              , default);
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
              , FormatName
              , RoundCornerPx
              , PositionPx
              , SizePx
              , AllowsVideo
              , Layout);
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