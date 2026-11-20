using System;
using UnityEngine;
#if RISEON_ADS_REMOTECONFIG
using RiseOn.Firebase.RemoteConfig;
#endif

namespace RiseOn.Ads {
    /// <summary>
    /// A manager setting the page sets, which a Remote Config key of the same type replaces once one is picked, while that pack is installed: a float key for a number, a bool key for a bool.<br/>
    /// The page keeps a number at 0 or above: every one is seconds or a weight.
    /// </summary>
    [Serializable]
    public sealed class AdRemoteSetting<T> {
        [SerializeField]
        private T value;

        [SerializeField]
        private AdRemoteKey key;

        public AdRemoteSetting() { }

        public AdRemoteSetting(T value) {
            this.value = value;
        }

        /// <summary>The key's value once one is picked, Remote Config's in-app default until that pack completes; else the page's.</summary>
        public T Value {
            get {
#if RISEON_ADS_REMOTECONFIG
                // The page reports a key removed from Remote Config, or of another type, and stops the build.
                if (key.Id != 0) return ((RemoteKey) key.Id).Get<T>().Value;
#endif
                return value;
            }
        }

        // For the page's check of the keys.
        internal int KeyId => key.Id;
    }
}