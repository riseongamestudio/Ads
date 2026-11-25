using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// A tool for the game's own pacing of a format: not before some seconds from launch, then not again before some seconds from its last display, nor before some seconds from a rewarded ad's.<br/>
    /// A field of the game's manager, so the page edits it, and each value takes a Remote Config key there while that pack is installed.<br/>
    /// It counts from <see cref="AdsSdk.SecondsSinceDisplay"/>, so it holds no state: two formats that share a pacing, such as an interstitial and a native interstitial shown at the same break, ask it for both.
    /// </summary>
    [Serializable]
    public sealed class AdPacing {
        [SerializeField, LabelText("First After (s)"), Tooltip("Seconds from launch before the format's first show; 0 for none.")]
        private AdRemoteSetting<float> firstSeconds;

        [SerializeField, LabelText("Min Interval (s)"), Tooltip("Fewest seconds from the format's last display to its next show; 0 for none.")]
        private AdRemoteSetting<float> minIntervalSeconds;

        [SerializeField, LabelText("After Rewarded (s)"), Tooltip("Fewest seconds from a rewarded ad's display to the format's next show, as the player just chose to watch one; 0 for none.")]
        private AdRemoteSetting<float> afterRewardedSeconds;

        public AdPacing() : this(0, 0) { }

        public AdPacing(float firstSeconds, float minIntervalSeconds, float afterRewardedSeconds = 0) {
            this.firstSeconds         = new AdRemoteSetting<float>(firstSeconds);
            this.minIntervalSeconds   = new AdRemoteSetting<float>(minIntervalSeconds);
            this.afterRewardedSeconds = new AdRemoteSetting<float>(afterRewardedSeconds);
        }

        public float FirstSeconds => firstSeconds.Value;
        public float MinIntervalSeconds => minIntervalSeconds.Value;
        public float AfterRewardedSeconds => afterRewardedSeconds.Value;

        /// <summary>The format may show now: past First After from launch before its first display, past Min Interval after one, and past After Rewarded after a rewarded ad's.</summary>
        public bool Allows(AdFormat format) {
            if (AdsSdk.SecondsSinceDisplay(AdFormat.Rewarded) is { } sinceRewarded && sinceRewarded < AfterRewardedSeconds) return false;

            return AdsSdk.SecondsSinceDisplay(format) is { } since ? since >= MinIntervalSeconds : Time.realtimeSinceStartupAsDouble >= FirstSeconds;
        }

        /// <summary>As <see cref="Allows(AdFormat)"/>.</summary>
        public bool Allows(AdFullScreenFormat format) => Allows(format.ToFormat());
    }
}