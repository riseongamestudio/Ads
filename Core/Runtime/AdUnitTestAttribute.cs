using System;

namespace RiseOn.Ads {
    /// <summary>
    /// The network's own test unit for an <see cref="AdUnitId"/> field, per platform: the settings page draws a Test toggle beside each platform's id,
    /// and a platform switched on serves the network's test ads from this unit.<br/>
    /// Only for a network that publishes test units.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class AdUnitTestAttribute : Attribute {
        public readonly string Android;
        public readonly string Ios;

        public AdUnitTestAttribute(string android, string ios) {
            Android = android;
            Ios     = ios;
        }
    }
}