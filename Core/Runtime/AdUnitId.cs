using System;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// One ad unit id per platform. The Editor uses the Android one, or the iOS one when Android's is empty; it loads none of the formats it fakes.<br/>
    /// A platform whose Test is on uses the network's test unit instead of the id written for it: the one the field's <see cref="AdUnitTestAttribute"/>
    /// names, which the settings page stores here.
    /// </summary>
    [Serializable]
    public struct AdUnitId {
        [SerializeField] private string android;
        [SerializeField] private string ios;
        [SerializeField] private bool   androidTest;
        [SerializeField] private bool   iosTest;
        [SerializeField] private string androidTestId;
        [SerializeField] private string iosTestId;

        public AdUnitId(string android, string ios) {
            this.android  = android;
            this.ios      = ios;
            androidTest   = false;
            iosTest       = false;
            androidTestId = null;
            iosTestId     = null;
        }

        public string Value => Application.platform switch {
            RuntimePlatform.IPhonePlayer => Ios
          , RuntimePlatform.Android      => Android
          , _                            => string.IsNullOrEmpty(Android) ? Ios : Android
        };

        private string Android => androidTest ? androidTestId : android;
        private string Ios => iosTest ? iosTestId : ios;

        public bool IsEmpty => string.IsNullOrEmpty(Value);
    }
}