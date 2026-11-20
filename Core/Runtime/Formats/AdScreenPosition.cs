using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>Where a banner or an MRec sits in the safe area: the places every mediation's SDK offers.</summary>
    public enum AdScreenPosition {
        [InspectorName("Top Left")] TopLeft
      , Top
      , [InspectorName("Top Right")] TopRight
      , Center
      , [InspectorName("Bottom Left")] BottomLeft
      , Bottom
      , [InspectorName("Bottom Right")] BottomRight
    }
}