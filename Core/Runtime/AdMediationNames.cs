using System;
using System.Reflection;
using Sirenix.OdinInspector;

namespace RiseOn.Ads {
    /// <summary>What the page and the Editor's placeholders call a mediation: its type's TypeRegistryItem name, else its id or type name.</summary>
    internal static class AdMediationNames {
        public static string Of(AdsSettings settings, string id) {
            if (settings) {
                foreach (var mediation in settings.Mediations) {
                    if (mediation.Id == id) return Of(mediation.GetType());
                }
            }

            return id;
        }

        public static string Of(Type type) => type.GetCustomAttribute<TypeRegistryItemAttribute>()?.Name ?? type.Name;
    }
}