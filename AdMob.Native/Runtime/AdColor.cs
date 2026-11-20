using UnityEngine;

namespace RiseOn.Ads.AdMob.Native {
    /// <summary>
    /// One color, one int, across three languages.<br/>
    /// The wire carries packed ARGB because that is what both native sides build a color from - Color.argb over there, the shift-and-mask in RANRectAdPresentation here - and because a float per channel would cost four parameters where the ABI check watches one.
    /// </summary>
    internal static class AdColor {
        private const int ChannelMax = 255;

        internal static int Pack(Color color) {
            return Channel(color.a) << 24
                 | Channel(color.r) << 16
                 | Channel(color.g) << 8
                 | Channel(color.b);
        }

        internal static Color Unpack(int packed) {
            return new Color(
                (packed >> 16 & 0xFF) / (float)ChannelMax
              , (packed >> 8  & 0xFF) / (float)ChannelMax
              , (packed       & 0xFF) / (float)ChannelMax
              , (packed >> 24 & 0xFF) / (float)ChannelMax);
        }

        private static int Channel(float value) {
            return Mathf.Clamp(Mathf.RoundToInt(value * ChannelMax), 0, ChannelMax);
        }
    }
}