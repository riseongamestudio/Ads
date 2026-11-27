using UnityEngine;

namespace RiseOn.Ads.AdMob.Native.Platforms.Editor {
    // The wrapper serializes every call under its own lock and the Editor is
    // single-threaded, so this client needs no locking of its own.
    internal sealed class RectAdClient : IRectAdClient {
        private const int EditorAdSuccessCode = 0;

        private readonly IRectAdCallbacks callbacks;
        private readonly string adUnitId;
        private readonly string format;
        private Vector2Int positionPx;
        private EditorAdConfig config;
        private EditorAd preview;

        internal RectAdClient(
            RectAd.Settings settings
          , IRectAdCallbacks callbacks) {
            this.callbacks  = callbacks;
            adUnitId        = settings.AdUnitId;
            format          = settings.Format;
        }

        public void SetSize(Vector2Int sizePx, int roundCornerPx) {
            ReleasePreview();
            config = EditorAdConfig.CreateRectAd(
                adUnitId
              , positionPx
              , sizePx
              , format
              , roundCornerPx);
        }

        public void SetPosition(Vector2Int newPositionPx) {
            positionPx = newPositionPx;
            config?.SetPosition(newPositionPx);
            if (preview) preview.SetPosition(newPositionPx);
        }

        public void Show() {
            if (preview) {
                preview.SetVisible(true);
                callbacks.OnDisplayed();
                return;
            }

            var snapshot = config?.Snapshot();
            if (snapshot == null) return;

            preview = EditorAd.Show(snapshot, () => preview = null);
            // The preview holds no cache: it builds an ad the moment one is
            // asked for, so "one warm ad, room for one" is the honest
            // reading of a state that never had a queue.
            callbacks.OnLoadingCompleted(
                EditorAdSuccessCode
              , string.Empty
              , 1
              , 1);
            callbacks.OnDisplayed();
        }

        // A hidden rect holds no ad, so the next show builds a fresh one.
        public void Hide() {
            ReleasePreview();
        }

        public void Release() {
            config = null;
            ReleasePreview();
        }

        private void ReleasePreview() {
            var released = preview;
            preview = null;
            if (released) released.Release();
        }
    }
}