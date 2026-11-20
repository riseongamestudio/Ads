using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>The ad the game's manager shows when the player comes back to the game, the page's On Return To Game section; the same on both platforms.</summary>
    [Serializable]
    public sealed class AdReturnSettings {
        // As most of the studio's games run it: the native one when loaded, else the network's.
        [SerializeField, LabelText("Format")]
        [Tooltip("The ad shown when the player comes back to the game, under its own rules: remove-ads, First After and Min Interval; "
               + "Native App Open, else App Open takes the native one when it is loaded and the app open ad otherwise; None for no ad on return.")]
        private AdReturnFormat format = AdReturnFormat.NativeAppOpenElseAppOpen;

        // Every one of the studio's games but OrganiCat shows it from the first launch.
        [SerializeField, LabelText("From Launch"), MinValue(1)]
        [Tooltip("The ad shows from this launch of the game on the device, on launch and on return alike: 1 from the first launch, 2 to leave the first launch to the game.")]
        private int fromLaunch = 1;

        // As most of the studio's games and Google's cold start guidance: over the loading screen, when the ad loads before the game is ready.
        [SerializeField, LabelText("On Launch")]
        [Tooltip("The ad also shows at launch, over the game's first scene, when it loads before the game leaves that scene; a later one waits for the player's next return. "
               + "A game with one scene turns it off, as nothing ends the wait.")]
        private AdRemoteSetting<bool> onLaunch = new(true);

        // Every one of the studio's games shows it on each return.
        [SerializeField, LabelText("After Away (s)"), Tooltip("Seconds the game has to stay in the background before its return shows the ad; 0 for every return.")]
        private AdRemoteSetting<float> afterAwaySeconds = new(0);

        /// <summary>The formats the ad on return can be, best first: the first loaded shows, else the last; empty for no ad.</summary>
        public IReadOnlyList<AdFullScreenFormat> Formats => format switch {
            AdReturnFormat.AppOpen                  => appOpenOnly
          , AdReturnFormat.NativeAppOpen            => nativeAppOpenOnly
          , AdReturnFormat.NativeAppOpenElseAppOpen => nativeAppOpenElseAppOpen
          , _                                       => Array.Empty<AdFullScreenFormat>()
        };

        private static readonly AdFullScreenFormat[] appOpenOnly              = { AdFullScreenFormat.AppOpen };
        private static readonly AdFullScreenFormat[] nativeAppOpenOnly        = { AdFullScreenFormat.NativeAppOpen };
        private static readonly AdFullScreenFormat[] nativeAppOpenElseAppOpen = { AdFullScreenFormat.NativeAppOpen, AdFullScreenFormat.AppOpen };

        /// <summary>The launch of the game on the device from which the ad shows, 1 for the first.</summary>
        public int FromLaunch => Mathf.Max(1, fromLaunch);

        /// <summary>Whether the ad also shows at launch, over the game's first scene.</summary>
        public bool OnLaunch => onLaunch.Value;

        public float AfterAwaySeconds => afterAwaySeconds.Value;

        internal IEnumerable<(string Setting, int KeyId, Type Type)> RemoteKeyUses {
            get {
                yield return ("On Return To Game On Launch", onLaunch.KeyId, typeof(bool));
                yield return ("On Return To Game After Away", afterAwaySeconds.KeyId, typeof(float));
            }
        }
    }

    /// <summary>The formats an ad on return can be.</summary>
    public enum AdReturnFormat {
        None
      , [InspectorName("App Open")] AppOpen
      , [InspectorName("Native App Open")] NativeAppOpen
      , [InspectorName("Native App Open, else App Open")] NativeAppOpenElseAppOpen
    }
}