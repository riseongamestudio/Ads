using System;
using UnityEngine;

namespace RiseOn.Ads.AdMob.Native {
    /// <summary>
    /// A native ad in a rect the game places: one cell of a feed, or a block such as an MRec. It draws from the native pool of its unit, which every native format on the same unit shares.<br/>
    /// It holds an ad only while shown: hiding it gives back an ad it never displayed, and a hidden one keeps at most the ad it displayed and has not yet shown long enough, which no other placement could use.<br/>
    /// One that is never sized or shown still keeps its unit's pool warm with its <see cref="Settings.CacheSize"/>.
    /// </summary>
    public sealed class RectAd : BaseAd, IRectAdCallbacks {
        [Serializable]
        public struct Settings {
            public string AdUnitId;

            /// <summary>The placement every paid impression of this ad reports, such as <c>NATIVE_IN_FEED</c>.</summary>
            public string Format;

            /// <summary>Raw ads this ad asks its pool to keep warm; 0 means 1. The pool keeps the most any of its ads asks for.</summary>
            public int CacheSize;

            /// <summary>
            /// The cell's own backdrop, alpha included.<br/>
            /// Transparent is a real answer here: a feed cell that wants no backdrop asks for it.
            /// </summary>
            public Color BackgroundColor;
        }

        public event Action<AdError> OnDisplayFailed;

        private bool          sized;
        private Action        pendingOnDisplayed;
        private IRectAdClient client;

        public RectAd(in Settings settings) : base(settings.AdUnitId, settings.Format) {
            var platform = AdPlatformRegistry.Installed;
            if (platform == null) {
                Debug.Log($"{nameof(RectAd)} is not supported on {Application.platform}");
                return;
            }

            client = platform.CreateRectAd(settings, this);
        }

        /// <summary>
        /// Sizes the rect, with its corner radius; 0 keeps the corners square.<br/>
        /// Required before it can be placed or shown, and calling it again resizes it.<br/>
        /// Every asset keeps clear of the curve - inset by just enough that its own corner touches the arc - except the background picture of the scrim layout, which fills the rect and is clipped by the curve instead.
        /// </summary>
        public void SetSize(Vector2Int sizePx, int roundCornerPx = 0) {
            lock (nativeAdStateLock) {
                if (releasedManaged) return;

                pendingOnDisplayed = null;
                client?.SetSize(sizePx, Mathf.Max(0, roundCornerPx));
                sized = true;
            }
        }

        /// <summary>Where the rect's top-left corner goes, in pixels from the screen's top-left corner.</summary>
        public void SetPosition(Vector2Int positionPx) {
            lock (nativeAdStateLock) {
                if (releasedManaged || WarnIfNotSized(nameof(SetPosition))) return;

                client?.SetPosition(positionPx);
            }
        }

        /// <summary>Puts it on screen; <paramref name="onDisplayed"/> comes once an ad is on screen in it, which may be after an ad arrives.</summary>
        public void Show(Action onDisplayed = null) {
            lock (nativeAdStateLock) {
                if (releasedManaged || WarnIfNotSized(nameof(Show))) return;

                pendingOnDisplayed = onDisplayed;
                client?.Show();
            }
        }

        public void Hide() {
            lock (nativeAdStateLock) {
                if (releasedManaged || !sized) return;

                pendingOnDisplayed = null;
                client?.Hide();
            }
        }

        /// <summary>Takes it off screen for good and leaves its unit's pool, which goes with the last ad to leave it.</summary>
        public void Dispose() {
            IRectAdClient releasedClient;
            lock (nativeAdStateLock) {
                if (releasedManaged) return;

                releasedManaged    = true;
                releasedClient     = client;
                client             = null;
                pendingOnDisplayed = null;
            }

            releasedClient?.Release();
        }

        void IRectAdCallbacks.OnLoadingCompleted(int errorCode, string errorMessage, int cachedCount, int cacheSize)
            => DispatchFromNative(() => RaiseLoadingCompleted(errorCode, errorMessage, cachedCount, cacheSize));

        void IRectAdCallbacks.OnAdPaid(string source, string adUnitId, double value, string currencyCode)
            => DispatchFromNative(() => RaiseAdPaid(source, adUnitId, value, currencyCode));

        void IRectAdCallbacks.OnDisplayed() => DispatchFromNative(HandleDisplayed);

        void IRectAdCallbacks.OnShowNotReady()
            => DispatchFromNative(() => Debug.LogWarning($"{nameof(RectAd)} {Format}: Show Called While Not Ready"));

        void IRectAdCallbacks.OnDisplayFailed(int errorCode, string errorMessage)
            => DispatchFromNative(() => {
                var handler = OnDisplayFailed;
                if (handler == null) return;

                AdError error = new(errorCode, errorMessage);
                InvokeSafely(() => handler(error));
            });

        private bool WarnIfNotSized(string operation) {
            if (sized) return false;

            Debug.LogError($"{nameof(RectAd)} {Format}: {operation} called before {nameof(SetSize)}; it has no size yet.");
            return true;
        }

        private void HandleDisplayed() {
            Action onDisplayed;
            lock (nativeAdStateLock) {
                if (releasedManaged) return;

                onDisplayed        = pendingOnDisplayed;
                pendingOnDisplayed = null;
            }

            InvokeSafely(onDisplayed);
        }
    }
}