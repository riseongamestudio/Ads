#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace RiseOn.Ads {
    /// <summary>
    /// How the Editor assemblies draw each SDK's own settings file under its mediation on the ads settings page.<br/>
    /// The settings types live in Editor assemblies a runtime type cannot reference, so the runtime side only asks to be drawn.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class AdsEditorHooks {
        private static readonly Dictionary<Type, Action<object>> mediationDrawers = new();

        public static void RegisterMediationDrawer(Type mediationType, Action<object> draw) {
            mediationDrawers[mediationType] = draw;
        }

        public static void DrawMediation(object mediation) {
            if (mediation != null && mediationDrawers.TryGetValue(mediation.GetType(), out var draw)) draw(mediation);
        }
    }
}
#endif