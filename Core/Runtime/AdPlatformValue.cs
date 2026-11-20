using System;
using System.Collections.Generic;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// A setting that may differ between Android and iOS: one value for both until it is split, then one for each, with the platform whose value the Editor uses.<br/>
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

        // Of the two split values, the one the Editor plays with.
        [SerializeField]
        private AdPlatform editor;

        public AdPlatformValue() { }

        public AdPlatformValue(T value) {
            this.value = value;
        }

        /// <summary>The running platform's value; in the Editor, the picked platform's while split.</summary>
        public T Current => !split ? value : Application.platform switch {
            RuntimePlatform.IPhonePlayer => ios
          , RuntimePlatform.Android      => value
          , _                            => editor is AdPlatform.IOS ? ios : value
        };

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
    }
}