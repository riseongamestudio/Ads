using RiseOn.Ads.Editor;
using RiseOn.Consent.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.Max.Editor {
    /// <summary>
    /// The AppLovin settings file, drawn under the mediation: the SDK key, Ad Review, and the AdMob app ids its Google adapter needs, the project's, kept by com.riseon.consent, which writes them into the file.<br/>
    /// MAX's own consent flow, set in its Integration Manager, stays off: com.riseon.consent stops the build while it is on.
    /// </summary>
    [InitializeOnLoad]
    internal static class MaxEditor {
        private static readonly MaxNetworks networks = new();

        static MaxEditor() {
            AdsEditorHooks.RegisterMediationDrawer(typeof(MaxMediation), _ => {
                AdsSdkSettingsGui.Draw(AppLovinSettings.Instance, Draw);
                networks.Draw();
            });
        }

        private static void Draw(SerializedObject settings) {
            EditorGUILayout.PropertyField(settings.FindProperty("sdkKey"), new GUIContent("SDK Key", "AppLovin's SDK key, from the MAX dashboard: Account > General > Keys."));
            AdsSdkSettingsGui.Toggle(settings.FindProperty("qualityServiceEnabled"), new GUIContent("Ad Review", "AppLovin's \"Enable MAX Ad Review\": AppLovin scans the creatives the game shows."));
            ConsentAppIdField.Draw();
        }
    }
}