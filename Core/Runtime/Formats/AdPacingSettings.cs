using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// How often the game's manager lets a format show, on both platforms: not before some seconds from launch, then not again before some seconds from its last display,
    /// nor, for the interstitials, before some seconds from a rewarded ad's.
    /// </summary>
    [Serializable]
    public sealed class AdPacingSettings {
        [SerializeField, LabelText("First After (s)"), Tooltip("Seconds from launch before the format's first show; 0 for none.")]
        private AdRemoteSetting<float> firstSeconds;

        [SerializeField, LabelText("Min Interval (s)"), Tooltip("Fewest seconds from the format's last display to its next show; 0 for none.")]
        private AdRemoteSetting<float> minIntervalSeconds;

        // On the page for the interstitials only, as Shikaku Cat and OrganiCat hold them back after a rewarded ad; 0 for the others.
        [SerializeField, LabelText("After Rewarded (s)"), Tooltip("Fewest seconds from a rewarded ad's display to the format's next show, as the player just chose to watch one; 0 for none.")]
        private AdRemoteSetting<float> afterRewardedSeconds = new(0);

        public AdPacingSettings() : this(0, 0) { }

        public AdPacingSettings(float firstSeconds, float minIntervalSeconds) {
            this.firstSeconds       = new AdRemoteSetting<float>(firstSeconds);
            this.minIntervalSeconds = new AdRemoteSetting<float>(minIntervalSeconds);
        }

        public float FirstSeconds => firstSeconds.Value;
        public float MinIntervalSeconds => minIntervalSeconds.Value;

        /// <summary>Fewest seconds from a rewarded ad's display to the format's next show; set on the page for the interstitials only.</summary>
        public float AfterRewardedSeconds => afterRewardedSeconds.Value;

        // The formats whose section on the page has After Rewarded.
        internal static bool TakesAfterRewarded(AdFormat format) => format is AdFormat.Interstitial or AdFormat.NativeInterstitial;

        internal IEnumerable<(string Setting, int KeyId, Type Type)> RemoteKeyUses(AdFormat format) {
            yield return ($"{format} First After", firstSeconds.KeyId, typeof(float));
            yield return ($"{format} Min Interval", minIntervalSeconds.KeyId, typeof(float));
            if (TakesAfterRewarded(format)) yield return ($"{format} After Rewarded", afterRewardedSeconds.KeyId, typeof(float));
        }
    }
}