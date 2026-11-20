using System;
using UnityEngine;

namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    // The wrapper serializes every call under its own lock and the Editor is
    // single-threaded, so this client needs no locking of its own. Readiness
    // is reported through OnStateChanged like the device platforms, so the
    // wrapper's cache flags are the one source of truth.
    internal sealed class OverlayAdClient : IOverlayAdClient {
        private const string AdNotReadyError = "Ad not ready";

        private readonly IOverlayAdCallbacks callbacks;
        private readonly EditorAdConfig config;
        private EditorAd preview;
        private bool adReady;
        private bool adLoading;

        internal OverlayAdClient(
            OverlayAdSettings settings
          , IOverlayAdCallbacks callbacks) {
            this.callbacks = callbacks;
            config = EditorAdConfig.CreateFullScreen(
                settings.AdUnitId
              , settings.FullScreen
              , settings.HeightRatio
              , AdColor.Unpack(settings.BackgroundColor)
              , settings.Close);
        }

        // Screen pixels: the unit the Editor's own surface is measured in.
        public float PanelHeight
            => config.Mode is EditorAdMode.FullScreen
                ? Screen.height
                : EditorAd.ResolveHalfScreenPanelHeight(config.HeightRatio);

        public void SetClose(in CloseSettings controls) {
            config?.SetClose(controls);
        }

        public void Load() {
            if (adLoading || adReady) return;

            adLoading = true;
            callbacks.OnStateChanged(false, true);

            adLoading = false;
            adReady   = true;
            callbacks.OnStateChanged(true, false);
            callbacks.OnLoadingCompleted(0, string.Empty, 1, 1);
        }

        public void Show(int showId, bool slideIn, bool slideOutOnClose) {
            if (!adReady) {
                callbacks.OnShowCompleted(showId, AdNotReadyError, false, 0);
                return;
            }

            adReady = false;
            callbacks.OnStateChanged(false, adLoading);
            try {
                preview = EditorAd.Show(
                    config.Snapshot()
                  , () => {
                        preview = null;
                        // Not a flat zero: OnDisplayed below triggers the
                        // wrapper's replacement Load, which this client
                        // serves instantly - so by the time the preview
                        // closes the seat is usually already refilled.
                        // Reporting zero here overwrote that truth and
                        // stranded IsReady at false for the whole session,
                        // because the follow-up Load found adReady already
                        // true and returned without republishing state.
                        callbacks.OnShowCompleted(
                            showId, string.Empty, true, adReady ? 1 : 0);
                    }
                  , callbacks.OnDisplayed
                  , slideIn
                  , slideOutOnClose);
                // A panel that slides reports itself once it has arrived;
                // anything else is on screen already.
                if (!slideIn || config.Mode is not EditorAdMode.Collapsible) {
                    callbacks.OnDisplayed();
                }
            } catch (Exception exception) {
                Debug.LogException(exception);
                callbacks.OnShowCompleted(
                    showId, exception.Message, true, adReady ? 1 : 0);
            }
        }

        public void Hide(bool slideOut) {
            var current = preview;
            if (current) current.Dismiss(slideOut);
        }

        public void Release() {
            var current = preview;
            preview = null;
            if (current) current.Release();
        }
    }
}