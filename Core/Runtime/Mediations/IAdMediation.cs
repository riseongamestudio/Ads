using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using RiseOn.Consent;

namespace RiseOn.Ads {
    /// <summary>
    /// One mediation SDK behind Ads.<br/>
    /// A mediation also implements the format interfaces it serves (<see cref="IAdMediationBanner"/>, <see cref="IAdMediationFullScreen"/>...), which the pack casts to.
    /// </summary>
    public interface IAdMediation {
        /// <summary>A short, stable id ("admob", "max"...) that priority lists and remote config name it by.</summary>
        string Id { get; }

        bool IsInitialized { get; }

        /// <summary>Hands the player's choices to the SDK; called before <see cref="InitializeAsync"/> and again whenever they change.</summary>
        void ApplyConsent(in ConsentState consent);

        /// <param name="formats">The formats some priority list names this mediation for; it loads only these.</param>
        UniTask InitializeAsync(IAdMediationContext context, IReadOnlyCollection<AdFormat> formats, CancellationToken cancellationToken);

        void Dispose();
    }

    /// <summary>For the mediations whose SDK has a debugging tool of its own, which <see cref="AdsDebugger"/> opens.</summary>
    public interface IAdMediationDebugger {
        /// <summary>The tool's name as the SDK's docs call it, such as "Ad Inspector".</summary>
        string DebuggerName { get; }

        /// <summary>Called on Unity's thread; <paramref name="done"/> gets null once the tool opened, else what the tester has to do first, from any thread.</summary>
        void OpenDebugger(Action<string> done);
    }

    /// <summary>For the mediations whose SDK wants to hear when the app pauses and resumes.</summary>
    public interface IAdMediationLifecycle {
        void OnApplicationPause(bool paused);
    }

    /// <summary>What the pack gives its mediations. Every member may be called from any thread.</summary>
    public interface IAdMediationContext {
        /// <summary>How each format shows, which the mediation turns into its SDK's terms.</summary>
        AdFormatSettings Formats { get; }

        void ReportPaid(in AdImpression impression);
        void ReportLoaded(AdFormat format);
        void ReportLoadFailed(AdFormat format, in AdError error);
    }
}