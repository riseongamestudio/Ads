using System.Runtime.CompilerServices;

// The platform assemblies implement the internal client SPI; nobody else
// gets to see it.
[assembly: InternalsVisibleTo("RiseOn.Ads.AdMob.Native.Platforms.Android")]
[assembly: InternalsVisibleTo("RiseOn.Ads.AdMob.Native.Platforms.IOS")]
[assembly: InternalsVisibleTo("RiseOn.Ads.AdMob.Native.Platforms.Editor")]