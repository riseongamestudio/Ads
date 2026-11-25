using System;
using System.Collections.Generic;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// A setting that may differ between Android and iOS: one value for both until it is split, then one for each.<br/>
    /// The page draws it as one row, or as a row per platform.
    /// </summary>
    [Serializable]
    internal sealed class AdPlatformValue<T> {
        [SerializeField]
        private bool split;

        // Every platform's while not split; Android's once split.
        [SerializeField]
        private T value;

        [SerializeField]
        private T ios;

        public AdPlatformValue() { }

        public AdPlatformValue(T value) {
            this.value = value;
        }

        /// <summary>The running platform's value; in the Editor, the active build target's while split, Android's for a target other than iOS.</summary>
        public T Current => split && IsIOS ? ios : value;

        // Every value it holds, by the platforms it serves, for the Editor.
        internal IEnumerable<(string Platforms, T Value)> All {
            get {
                if (!split) {
                    yield return ("Android, iOS", value);
                    yield break;
                }

                yield return ("Android", value);
                yield return ("iOS", ios);
            }
        }

        private static bool IsIOS {
            get {
#if UNITY_EDITOR
                return UnityEditor.EditorUserBuildSettings.activeBuildTarget is UnityEditor.BuildTarget.iOS;
#else
                return Application.platform is RuntimePlatform.IPhonePlayer;
#endif
            }
        }
    }
}