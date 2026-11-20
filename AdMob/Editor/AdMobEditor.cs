using RiseOn.Ads.Editor;
using RiseOn.Consent.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.AdMob.Editor {
    /// <summary>
    /// The Google Mobile Ads settings file (Assets > Google Mobile Ads > Settings), drawn under the mediation: the project's AdMob app ids, kept by com.riseon.consent, which writes them into this file, then the file's Android options under short labels, Google's words in the tooltips.<br/>
    /// Its tracking text is left out, as com.riseon.consent writes it into Info.plist; its language is only the language of Google's own window.
    /// </summary>
    [InitializeOnLoad]
    internal static class AdMobEditor {
        // GoogleMobileAdsSettings is internal to Google's Editor assembly; its file is read by name.
        private const string SettingsResource = "GoogleMobileAdsSettings";

        private static readonly AdMobNetworks networks = new();

        static AdMobEditor() {
            AdsEditorHooks.RegisterMediationDrawer(typeof(AdMobMediation), _ => {
                ConsentAppIdField.Draw("App ID");
                AdsSdkSettingsGui.Draw(Resources.Load<ScriptableObject>(SettingsResource), Draw);
                networks.Draw();
            });
        }

        private static void Draw(SerializedObject settings) {
            DrawSdk(settings);
            AdsSdkSettingsGui.Toggle(settings.FindProperty("disableOptimizeInitialization"), new GUIContent("Background Init", "Initializes the SDK on a background thread. Off is Google's \"Disable initialization optimization\"."), inverted: true);
            AdsSdkSettingsGui.Toggle(settings.FindProperty("disableOptimizeAdLoading"), new GUIContent("Background Loading", "Loads ads on a background thread. Off is Google's \"Disable ad loading optimization\"."), inverted: true);
            AdsSdkSettingsGui.Toggle(settings.FindProperty("enableGradleBuildPreProcessor"), new GUIContent("Gradle Fixes", "Google's \"Enable Gradle build pre-processor\": edits the Gradle files under Plugins/Android to fix common build errors."));
            AdsSdkSettingsGui.Toggle(settings.FindProperty("enableKotlinXCoroutinesPackagingOption"), new GUIContent("Coroutines Fix", "Google's \"Enable kotlinx.coroutines packaging option\": fixes the Android build error \"2 files found with path 'META-INF/kotlinx_coroutines_core.version'\"."));
        }

        // Google's switch and its choice, as the one opt-in its window offers: off keeps the Google Mobile Ads SDK.
        private static void DrawSdk(SerializedObject settings) {
            var overriding = settings.FindProperty("overrideDefaultGmaAndroidSdk");
            var selected   = settings.FindProperty("selectedGmaAndroidSdk");
            var label      = new GUIContent("Next-Gen SDK", "Google's \"Upgrade to the GMA Next-Gen SDK\" on Android, for better latency and stability; off keeps the Google Mobile Ads SDK, and it can be switched back at any time.");

            EditorGUI.BeginChangeCheck();
            var nextGen = EditorGUILayout.Toggle(label, overriding.boolValue && selected.intValue is 1);
            if (!EditorGUI.EndChangeCheck()) return;

            overriding.boolValue = nextGen;
            selected.intValue    = nextGen ? 1 : 0;
        }
    }
}