using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>A platform whose setup the ads run with: the device's, or the one the Editor plays as.</summary>
    public enum AdPlatform {
        Android
      , [InspectorName("iOS")] IOS
    }
}