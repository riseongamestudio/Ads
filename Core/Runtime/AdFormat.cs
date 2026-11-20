using UnityEngine;

namespace RiseOn.Ads {
    public enum AdFormat {
        Banner
      , [InspectorName("MRec")] MRec
      , Interstitial
      , Rewarded
      , RewardedInterstitial
      , AppOpen
      , [InspectorName("Native In-Feed")] NativeInFeed
      , NativeCollapsible
      , NativeInterstitial
      , NativeAppOpen
      , NativeEndCard
      , [InspectorName("Native MRec")] NativeMRec
    }

    /// <summary>The formats that take the whole screen and run as steps of an <see cref="AdChain"/>, each with its <see cref="AdFormat"/>'s value.</summary>
    public enum AdFullScreenFormat {
        Interstitial         = (int)AdFormat.Interstitial
      , Rewarded             = (int)AdFormat.Rewarded
      , RewardedInterstitial = (int)AdFormat.RewardedInterstitial
      , AppOpen              = (int)AdFormat.AppOpen
      , NativeInterstitial   = (int)AdFormat.NativeInterstitial
      , NativeAppOpen        = (int)AdFormat.NativeAppOpen
      , NativeEndCard        = (int)AdFormat.NativeEndCard
    }

    public static class AdFormatExt {
        /// <summary>The same format as an <see cref="AdFormat"/>, for the members that take any format.</summary>
        public static AdFormat ToFormat(this AdFullScreenFormat format) => (AdFormat)format;

        /// <summary>The same format as an <see cref="AdFullScreenFormat"/>; none for a format that does not take the whole screen.</summary>
        public static AdFullScreenFormat? ToFullScreen(this AdFormat format) => format.IsFullScreen() ? (AdFullScreenFormat)format : null;

        /// <summary>Whether the format takes the whole screen and runs as a step of an <see cref="AdChain"/>.</summary>
        public static bool IsFullScreen(this AdFormat format) => format switch {
            AdFormat.Interstitial         => true
          , AdFormat.Rewarded             => true
          , AdFormat.RewardedInterstitial => true
          , AdFormat.AppOpen              => true
          , AdFormat.NativeInterstitial   => true
          , AdFormat.NativeAppOpen        => true
          , AdFormat.NativeEndCard        => true
          , _                             => false
        };

        /// <summary>Whether <paramref name="mediation"/> serves the format: it implements the format's interface, and a full-screen one says it supports it.</summary>
        public static bool IsServedBy(this AdFormat format, IAdMediation mediation) => format switch {
            AdFormat.Banner              => mediation is IAdMediationBanner
          , AdFormat.MRec                => mediation is IAdMediationMRec
          , AdFormat.NativeInFeed        => mediation is IAdMediationNativeInFeed
          , AdFormat.NativeMRec          => mediation is IAdMediationNativeMRec
          , AdFormat.NativeCollapsible   => mediation is IAdMediationNativeCollapsible
          , _ when format.IsFullScreen() => mediation is IAdMediationFullScreen fullScreen && fullScreen.Supports(format)
          , _                            => false
        };

        /// <summary>Whether the format rewards the player once watched.</summary>
        public static bool IsRewarding(this AdFormat format) => format is AdFormat.Rewarded or AdFormat.RewardedInterstitial;

        /// <summary>Whether the format is drawn by the native ad views rather than the network's own.</summary>
        public static bool IsNative(this AdFormat format) => format switch {
            AdFormat.NativeInFeed       => true
          , AdFormat.NativeMRec         => true
          , AdFormat.NativeCollapsible  => true
          , AdFormat.NativeInterstitial => true
          , AdFormat.NativeAppOpen      => true
          , AdFormat.NativeEndCard      => true
          , _                           => false
        };
    }
}