using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads.AdMob {
    /// <summary>A full-screen native placement of <c>com.riseon.ads.admob.native</c>.</summary>
    [Serializable]
    internal sealed class AdMobNativeFullScreenSettings {
        [AdUnitTest("ca-app-pub-3940256099942544/2247696110", "ca-app-pub-3940256099942544/3986624511")]
        public AdUnitId UnitId;

        [Tooltip("Ads this format asks its ad unit's pool to keep loaded. Formats on the same ad unit id share the pool, which keeps the most any of them asks for; raise it where one show follows another")]
        [MinValue(1)] public int CacheSize = 1;
    }

    internal sealed partial class AdMobMediation {
        [SerializeField, AdFormatSection("Native Interstitial", 3, Closed = true), HideLabel]
        private AdMobNativeFullScreenSettings nativeInterstitial = new();

        [SerializeField, AdFormatSection("Native App Open", 4, Closed = true), HideLabel]
        private AdMobNativeFullScreenSettings nativeAppOpen = new();

        [SerializeField, AdFormatSection("Native End Card", 5, Closed = true), HideLabel]
        private AdMobNativeFullScreenSettings nativeEndCard = new();

        [SerializeField, AdUnitTest("ca-app-pub-3940256099942544/2247696110", "ca-app-pub-3940256099942544/3986624511"), AdFormatSection("Native Collapsible", 2, Closed = true)]
        private AdUnitId nativeCollapsible;

        [SerializeField, AdFormatSection("Native Collapsible", 2), LabelText("Cache Size"), MinValue(1)]
        [Tooltip("Ads this format asks its ad unit's pool to keep loaded. Formats on the same ad unit id share the pool, which keeps the most any of them asks for; a chain of collapsibles needs one per ad")]
        private int collapsibleCacheSize = 2;

        [SerializeField, AdUnitTest("ca-app-pub-3940256099942544/2247696110", "ca-app-pub-3940256099942544/3986624511"), AdFormatSection("Native MRec", 6, Closed = true)]
        private AdUnitId nativeMRec;

        [SerializeField, AdFormatSection("Native MRec", 6), LabelText("Cache Size"), MinValue(1)]
        [Tooltip("Ads this format asks its ad unit's pool to keep loaded. Formats on the same ad unit id share the pool, which keeps the most any of them asks for")]
        private int mRecCacheSize = 1;

        [SerializeField, AdUnitTest("ca-app-pub-3940256099942544/2247696110", "ca-app-pub-3940256099942544/3986624511"), AdFormatSection("Native In-Feed", 1, Closed = true)]
        private AdUnitId nativeInFeed;

        [SerializeField, AdFormatSection("Native In-Feed", 1), LabelText("Cache Size"), MinValue(1)]
        [Tooltip("Ads this format asks its ad unit's pool to keep loaded. Formats on the same ad unit id share the pool, which keeps the most any of them asks for; raise it where a screen shows more cells at once")]
        private int inFeedCacheSize = 1;

        private NativeCollapsibleSlot collapsibleSlot;

        // Keep the in-feed's and the MRec's unit pools loaded from start-up, before any cell exists; never shown. Each cell is an ad of its own on
        // its format's pool, and the cells the game has not disposed are kept here for the mediation's own dispose.
        private Native.RectAd            inFeedWarmer;
        private Native.RectAd            mRecWarmer;
        private readonly List<RectCell>  cells = new();

        // ---- Full screen

        // The ids and cache are the mediation's; how the ad looks and closes is the page's Formats section's.
        private FullScreenSlot CreateNativeFullScreenSlot(AdFormat format, AdMobNativeFullScreenSettings settings, AdNativeFullScreenSettings look) {
            if (settings.UnitId.IsEmpty) {
                LogNoUnitId(format);
                return null;
            }

            var ad = new Native.FullScreenAd(new Native.FullScreenAd.Settings {
                AdUnitId        = settings.UnitId.Value
              , CacheSize       = settings.CacheSize
              , BackgroundColor = look.BackgroundColor
              , Format          = NativeFormatName(format)
              , Close           = ToNative(look.Close)
            });

            return new NativeFullScreenSlot(this, format, ad);
        }

        /// <summary>A full-screen native placement. Its events and its show work from any thread, and it reloads itself.</summary>
        private sealed class NativeFullScreenSlot : FullScreenSlot {
            private readonly Native.FullScreenAd ad;
            private          IAdShowListener     listener;

            public NativeFullScreenSlot(AdMobMediation owner, AdFormat format, Native.FullScreenAd ad) {
                this.ad = ad;

                ad.OnAdLoaded     += _ => owner.context?.ReportLoaded(format);
                ad.OnAdLoadFailed += error => owner.context?.ReportLoadFailed(format, ToCore(error));
                ad.OnAdPaid       += info => owner.ReportNativePaid(format, info);
                ad.OnAdDisplayed  += () => Volatile.Read(ref listener)?.OnDisplayed();
                ad.OnAdHidden     += () => Interlocked.Exchange(ref listener, null)?.OnClosed();
                ad.OnAdDisplayFailed += error => Interlocked.Exchange(ref listener, null)?.OnFailed(ToCore(error));
            }

            public override bool IsReady => ad.IsReady();

            public override void Load() => ad.Load();

            public override void Show(IAdShowListener showListener) {
                Volatile.Write(ref listener, showListener);
                ad.Show();
            }

            public override void Dispose() => ad.Release();
        }

        // ---- Collapsible

        public float PanelHeight => collapsibleSlot?.Ad.PanelHeight ?? 0;

        public bool IsCollapsibleReady => collapsibleSlot?.Ad.IsReady() == true;

        public void ShowCollapsible(in AdCloseSettings close, bool slideIn, bool slideOutOnClose, IAdShowListener listener) {
            if (collapsibleSlot == null) {
                listener.OnFailed(new AdError(-1, $"{AdFormat.NativeCollapsible} is not loaded by {MediationId}"));
                return;
            }

            collapsibleSlot.Show(close, slideIn, slideOutOnClose, listener);
        }

        public void HideCollapsible(bool slideOut) {
            collapsibleSlot?.Ad.Hide(slideOut);
        }

        private void StartNativeCollapsible() {
            if (nativeCollapsible.IsEmpty) {
                LogNoUnitId(AdFormat.NativeCollapsible);
                return;
            }

            var ad = new Native.HalfScreenAd(new Native.HalfScreenAd.Settings {
                AdUnitId        = nativeCollapsible.Value
              , HeightRatio     = context.Formats.NativeCollapsible.HeightRatio
              , CacheSize       = collapsibleCacheSize
              , BackgroundColor = context.Formats.NativeCollapsible.BackgroundColor
              , Format          = NativeFormatName(AdFormat.NativeCollapsible)
            });

            collapsibleSlot = new NativeCollapsibleSlot(this, ad);
            ad.Load();
        }

        private sealed class NativeCollapsibleSlot {
            public readonly Native.HalfScreenAd Ad;

            private IAdShowListener listener;

            public NativeCollapsibleSlot(AdMobMediation owner, Native.HalfScreenAd ad) {
                Ad = ad;

                const AdFormat format = AdFormat.NativeCollapsible;
                ad.OnAdLoaded        += _ => owner.context?.ReportLoaded(format);
                ad.OnAdLoadFailed    += error => owner.context?.ReportLoadFailed(format, ToCore(error));
                ad.OnAdPaid          += info => owner.ReportNativePaid(format, info);
                ad.OnAdDisplayed     += () => Volatile.Read(ref listener)?.OnDisplayed();
                ad.OnAdHidden        += () => Interlocked.Exchange(ref listener, null)?.OnClosed();
                ad.OnAdDisplayFailed += error => Interlocked.Exchange(ref listener, null)?.OnFailed(ToCore(error));
            }

            public void Show(in AdCloseSettings close, bool slideIn, bool slideOutOnClose, IAdShowListener showListener) {
                Volatile.Write(ref listener, showListener);
                Ad.SetClose(ToNative(close));
                Ad.Show(slideIn, slideOutOnClose);
            }
        }

        // ---- In-feed and MRec

        public INativeRect CreateInFeed(Vector2Int sizePx, int roundCornerPx) =>
            CreateRectCell(AdFormat.NativeInFeed, nativeInFeed, inFeedCacheSize, context.Formats.NativeInFeed.BackgroundColor, sizePx, roundCornerPx);

        public INativeRect CreateNativeMRec(Vector2Int sizePx, int roundCornerPx) =>
            CreateRectCell(AdFormat.NativeMRec, nativeMRec, mRecCacheSize, context.Formats.NativeMRec.BackgroundColor, sizePx, roundCornerPx);

        private INativeRect CreateRectCell(AdFormat format, AdUnitId unitId, int cacheSize, Color backgroundColor, Vector2Int sizePx, int roundCornerPx) {
            if (unitId.IsEmpty) return null;

            var ad = NewRectAd(format, unitId, cacheSize, backgroundColor);
            ad.SetSize(sizePx, roundCornerPx);
            ad.OnAdPaid        += info => ReportNativePaid(format, info);
            ad.OnDisplayFailed += error => LogDisplayFailed(format, error);

            var cell = new RectCell(this, ad);
            cells.Add(cell);
            return cell;
        }

        private void StartNativeInFeed() => inFeedWarmer = StartWarmer(AdFormat.NativeInFeed, nativeInFeed, inFeedCacheSize, context.Formats.NativeInFeed.BackgroundColor);

        private void StartNativeMRec() => mRecWarmer = StartWarmer(AdFormat.NativeMRec, nativeMRec, mRecCacheSize, context.Formats.NativeMRec.BackgroundColor);

        // Its loads stand for the format's: each cell hears the same ones from the pool.
        private Native.RectAd StartWarmer(AdFormat format, AdUnitId unitId, int cacheSize, Color backgroundColor) {
            if (unitId.IsEmpty) {
                LogNoUnitId(format);
                return null;
            }

            var warmer = NewRectAd(format, unitId, cacheSize, backgroundColor);
            ReportLoads(format, warmer);
            return warmer;
        }

        // One cell: an ad of its own, which holds an ad from its format's pool while shown.
        private sealed class RectCell : INativeRect {
            private readonly AdMobMediation owner;
            private          Native.RectAd  ad;

            public RectCell(AdMobMediation owner, Native.RectAd ad) {
                this.owner = owner;
                this.ad    = ad;
            }

            public void SetPosition(Vector2Int positionPx) => ad?.SetPosition(positionPx);

            // The native side reports the display on its own thread; the game hears it on Unity's.
            public void Show(Action onDisplayed) => ad?.Show(() => UniTask.Post(() => onDisplayed?.Invoke()));

            public void Hide() => ad?.Hide();

            public void Dispose() {
                if (ad == null) return;

                ad.Dispose();
                ad = null;
                owner.cells.Remove(this);
            }
        }

        // ---- Shared

        private static Native.RectAd NewRectAd(AdFormat format, AdUnitId unitId, int cacheSize, Color backgroundColor) {
            return new Native.RectAd(new Native.RectAd.Settings {
                AdUnitId        = unitId.Value
              , Format          = NativeFormatName(format)
              , CacheSize       = cacheSize
              , BackgroundColor = backgroundColor
            });
        }

        private void ReportLoads(AdFormat format, Native.BaseAd ad) {
            ad.OnAdLoaded     += _ => context?.ReportLoaded(format);
            ad.OnAdLoadFailed += error => context?.ReportLoadFailed(format, ToCore(error));
        }

        private static void LogDisplayFailed(AdFormat format, Native.AdError error) {
            Debug.LogWarning($"{LogTag}: {format} failed to display ({error})");
        }

        private void DisposeNative() {
            collapsibleSlot?.Ad.Release();
            collapsibleSlot = null;

            foreach (var cell in cells.ToArray()) cell.Dispose();
            inFeedWarmer?.Dispose();
            inFeedWarmer = null;
            mRecWarmer?.Dispose();
            mRecWarmer = null;
        }

        private void ReportNativePaid(AdFormat format, Native.AdInfo info) {
            context?.ReportPaid(new AdImpression(format, MediationId, info.Source, info.UnitId, info.Value, info.Currency));
        }

        /// <summary>The placement name the native pack reports with its impressions.</summary>
        private static string NativeFormatName(AdFormat format) {
            switch (format) {
                case AdFormat.NativeInterstitial:
                    return "NATIVE_INTERSTITIAL";

                case AdFormat.NativeAppOpen:
                    return "NATIVE_APP_OPEN";

                case AdFormat.NativeEndCard:
                    return "NATIVE_END_CARD";

                case AdFormat.NativeCollapsible:
                    return "NATIVE_COLLAPSIBLE";

                case AdFormat.NativeMRec:
                    return "NATIVE_MREC";

                case AdFormat.NativeInFeed:
                    return "NATIVE_IN_FEED";

                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        private static AdError ToCore(Native.AdError error) {
            return new(error.Code, error.Message);
        }

        private static Native.CloseSettings ToNative(in AdCloseSettings close) {
            return new Native.CloseSettings {
                Cooldown        = close.Cooldown
              , CloseSide       = ToNative(close.CloseSide)
              , TimerSide       = ToNative(close.TimerSide)
              , RedirectOnClose = close.RedirectOnClose
            };
        }

        private static Native.CloseSide ToNative(AdCloseSide side) {
            switch (side) {
                case AdCloseSide.Left:   return Native.CloseSide.Left;
                case AdCloseSide.Right:  return Native.CloseSide.Right;
                case AdCloseSide.Random: return Native.CloseSide.Random;
                default:                 throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        private static Native.TimerSide ToNative(AdTimerSide side) {
            switch (side) {
                case AdTimerSide.Left:            return Native.TimerSide.Left;
                case AdTimerSide.Right:           return Native.TimerSide.Right;
                case AdTimerSide.Random:          return Native.TimerSide.Random;
                case AdTimerSide.OppositeOfClose: return Native.TimerSide.OppositeOfClose;
                case AdTimerSide.SameAsClose:     return Native.TimerSide.SameAsClose;
                default:                          throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }
    }
}