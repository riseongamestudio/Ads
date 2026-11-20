using System;
using Cysharp.Threading.Tasks;
using GoogleMobileAds.Api;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads.AdMob {
    internal sealed partial class AdMobMediation {
        [SerializeField, AdUnitTest("ca-app-pub-3940256099942544/9214589741", "ca-app-pub-3940256099942544/2435281174"), AdFormatSection("Banner")]
        private AdUnitId banner;

        [SerializeField, AdUnitTest("ca-app-pub-3940256099942544/6300978111", "ca-app-pub-3940256099942544/2934735716"), AdFormatSection("MRec")]
        private AdUnitId mRec;

        private BannerSlot bannerSlot;
        private BannerSlot mRecSlot;

        public void ShowBanner() => bannerSlot?.Show();
        public void HideBanner() => bannerSlot?.Hide();
        public void ShowMRec()   => mRecSlot?.Show();
        public void HideMRec()   => mRecSlot?.Hide();

        private void StartBanner() {
            var settings = context.Formats.Banner;
            var size     = settings.Adaptive ? AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth) : AdSize.Banner;
            bannerSlot = StartBannerSlot(AdFormat.Banner, banner, size, ToGma(settings.Position));
        }

        private void StartMRec() {
            mRecSlot = StartBannerSlot(AdFormat.MRec, mRec, AdSize.MediumRectangle, ToGma(context.Formats.MRec.Position));
        }

        private static AdPosition ToGma(AdScreenPosition position) {
            switch (position) {
                case AdScreenPosition.TopLeft:     return AdPosition.TopLeft;
                case AdScreenPosition.Top:         return AdPosition.Top;
                case AdScreenPosition.TopRight:    return AdPosition.TopRight;
                case AdScreenPosition.Center:      return AdPosition.Center;
                case AdScreenPosition.BottomLeft:  return AdPosition.BottomLeft;
                case AdScreenPosition.Bottom:      return AdPosition.Bottom;
                case AdScreenPosition.BottomRight: return AdPosition.BottomRight;
                default:                           throw new ArgumentOutOfRangeException(nameof(position), position, null);
            }
        }

        private BannerSlot StartBannerSlot(AdFormat format, AdUnitId unitId, AdSize size, AdPosition position) {
            if (unitId.IsEmpty) {
                LogNoUnitId(format);
                return null;
            }

            var slot = new BannerSlot(this, format, unitId.Value, size, position);
            slot.Load();
            return slot;
        }

        private void DisposeBanners() {
            bannerSlot?.Dispose();
            mRecSlot?.Dispose();
            bannerSlot = null;
            mRecSlot   = null;
        }

        /// <summary>A banner view that refreshes itself, hidden until shown. Lives on Unity's thread; the SDK's events are posted there.</summary>
        private sealed class BannerSlot {
            private readonly AdMobMediation owner;
            private readonly AdFormat        format;
            private readonly string          unitId;
            private readonly AdSize          size;
            private readonly AdPosition      position;

            private BannerView view;
            private bool       shown;
            private bool       disposed;
            private int        attempt;

            public BannerSlot(AdMobMediation owner, AdFormat format, string unitId, AdSize size, AdPosition position) {
                this.owner    = owner;
                this.format   = format;
                this.unitId   = unitId;
                this.size     = size;
                this.position = position;
            }

            public void Show() {
                shown = true;
                view?.Show();
            }

            public void Hide() {
                shown = false;
                view?.Hide();
            }

            public void Load() {
                if (disposed) return;

                Destroy();
                view = new BannerView(unitId, size, position);

                var loading = view;
                loading.OnBannerAdLoaded     += () => UniTask.Post(() => HandleLoaded(loading));
                loading.OnBannerAdLoadFailed += error => UniTask.Post(() => HandleLoadFailed(loading, error));
                loading.OnAdPaid             += value => owner.ReportPaid(format, unitId, value, loading.GetResponseInfo());

                loading.LoadAd(owner.NewRequest());

                // A loaded view shows itself; this one waits for Show.
                if (!shown) loading.Hide();
            }

            public void Dispose() {
                disposed = true;
                Destroy();
            }

            private void Destroy() {
                view?.Destroy();
                view = null;
            }

            private void HandleLoaded(BannerView loaded) {
                if (loaded != view) return;

                attempt = 0;
                if (!shown) loaded.Hide();
                owner.context?.ReportLoaded(format);
            }

            private void HandleLoadFailed(BannerView failed, LoadAdError error) {
                if (failed != view) return;

                owner.context?.ReportLoadFailed(format, new AdError(error.GetCode(), error.GetMessage()));
                owner.RetryLater(ref attempt, Load);
            }
        }
    }
}