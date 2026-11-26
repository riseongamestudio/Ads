using System;

namespace RiseOn.Ads {
    // What the source generator compiles into a game's own manager, written here for the pack's: [Serializable] for the settings'
    // [SerializeReference], sealed, and its Ins. Create Own copies DefaultAdsManager.cs alone, and the generator gives the copy these.
    // DefaultAdsManager.cs keeps to the pack's public API and to the assemblies its usings name, as the copy compiles in the game's assembly.
    [Serializable]
    sealed partial class DefaultAdsManager {
        /// <summary>The instance the Ads pack runs while the project has no ads manager of its own; throws once it has one, or before the first scene loads.</summary>
        public static DefaultAdsManager Ins => AdsSdk.GetManager<DefaultAdsManager>();
    }
}