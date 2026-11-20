using System;
using UnityEngine;

namespace RiseOn.Ads {
    public interface IAdMediationNativeInFeed : IAdMediation {
        /// <summary>A cell the game places in pixels from the screen's top-left corner; it holds an ad only while shown.</summary>
        INativeRect CreateInFeed(Vector2Int sizePx, int roundCornerPx);
    }

    /// <summary>A native ad in a rect the game places, from <see cref="IAdMediationNativeInFeed"/> or <see cref="IAdMediationNativeMRec"/>.</summary>
    public interface INativeRect {
        void SetPosition(Vector2Int positionPx);
        void Show(Action onDisplayed);
        void Hide();
        void Dispose();
    }

    /// <summary>The native MRec: native ad blocks the game places, each of its own size, on the format's own ad unit.</summary>
    public interface IAdMediationNativeMRec : IAdMediation {
        /// <summary>A block the game places in pixels from the screen's top-left corner; it holds an ad only while shown.</summary>
        INativeRect CreateNativeMRec(Vector2Int sizePx, int roundCornerPx);
    }

    /// <summary>The collapsible: a native ad over the bottom part of the screen that slides in and out.</summary>
    public interface IAdMediationNativeCollapsible : IAdMediation {
        /// <summary>The panel's height in the platform's own units, known before a show; the half-screen cover takes it as is.</summary>
        float PanelHeight { get; }

        /// <summary>Must be safe from any thread.</summary>
        bool IsCollapsibleReady { get; }

        /// <param name="slideIn">Slide in, or appear in place as the next ad of a chain does.</param>
        /// <param name="slideOutOnClose">Slide out when the player closes it, or vanish so the next ad takes its place.</param>
        /// <remarks><see cref="IAdShowListener.OnDisplayed"/> comes once the slide-in has finished.</remarks>
        void ShowCollapsible(in AdCloseSettings close, bool slideIn, bool slideOutOnClose, IAdShowListener listener);

        void HideCollapsible(bool slideOut);
    }
}