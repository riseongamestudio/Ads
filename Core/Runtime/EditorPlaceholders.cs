using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using RiseOn.Consent;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// The Editor's side of the pack's placeholders: what a full-screen ad, a banner and an MRec look like in Play mode, filled as test ads of the mediation that would serve them, given by its type.<br/>
    /// Every member may be called from any thread.
    /// </summary>
    internal interface IEditorPlaceholders {
        void ShowFullScreen(Type mediation, AdFormat format, IAdShowListener listener);
        void ShowBanner(Type mediation, AdBannerSettings settings);
        void HideBanner();
        void ShowMRec(Type mediation, AdMRecSettings settings);
        void HideMRec();
    }

    internal static class EditorPlaceholders {
        public static IEditorPlaceholders Installed { get; private set; }

        public static void Install(IEditorPlaceholders placeholders) {
            Installed = placeholders;
        }
    }

    /// <summary>
    /// A mediation as the Editor plays it: the pack's placeholders in place of its SDK's for the formats it serves that are not native, ready as soon as it starts.<br/>
    /// The native formats keep the mediation's own preview.
    /// </summary>
    internal sealed class EditorStandIn : IAdMediationFullScreen, IAdMediationBanner, IAdMediationMRec {
        private readonly IAdMediation     mediation;
        private readonly AdFormatSettings formats;

        public EditorStandIn(IAdMediation mediation, AdFormatSettings formats) {
            this.mediation = mediation;
            this.formats   = formats;
        }

        public string Id => mediation.Id;
        public bool IsInitialized => true;

        public void ApplyConsent(in ConsentState consent) { }

        public UniTask InitializeAsync(IAdMediationContext context, System.Collections.Generic.IReadOnlyCollection<AdFormat> formats, CancellationToken cancellationToken) => UniTask.CompletedTask;

        public void Dispose() { }

        public bool Supports(AdFormat format) => mediation is IAdMediationFullScreen fullScreen && fullScreen.Supports(format);
        public bool IsReady(AdFormat format) => Supports(format);

        // The Editor's player loop never stops, so whatever thread a placeholder calls back on, the call arrives.
        public bool CallsBackOffLoop(AdFormat format) => true;
        public bool ShowsFromAnyThread(AdFormat format) => true;

        public void Show(AdFormat format, string placement, IAdShowListener listener) {
            if (EditorPlaceholders.Installed is { } placeholders) placeholders.ShowFullScreen(mediation.GetType(), format, listener);
            else listener.OnFailed(new AdError(-1, "no Editor placeholders"));
        }

        public void ShowBanner() => EditorPlaceholders.Installed?.ShowBanner(mediation.GetType(), formats.Banner);
        public void HideBanner() => EditorPlaceholders.Installed?.HideBanner();
        public void ShowMRec() => EditorPlaceholders.Installed?.ShowMRec(mediation.GetType(), formats.MRec);
        public void HideMRec() => EditorPlaceholders.Installed?.HideMRec();
    }
}