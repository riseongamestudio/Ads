using RiseOn.Ads.Editor;
using RiseOn.Consent.Editor;
using RiseOn.Utils.Editor;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.LevelPlay.Editor {
    /// <summary>
    /// LevelPlay's two settings files, drawn under the mediation: the mediation settings and the mediated network settings, under short labels with LevelPlay's words in the tooltips.<br/>
    /// Automatic initialization and its app keys are left out, as the mediation initializes LevelPlay with its own; the AdMob app ids are the project's, kept by com.riseon.consent, which writes them into the file.
    /// </summary>
    [InitializeOnLoad]
    internal static class LevelPlayEditor {
        private const string NetworkManagerMenu = "Ads Mediation/Network Manager";
        private const string NetworkManagerTip  = "The networks LevelPlay mediates: their adapters are installed and updated in LevelPlay's Network Manager (Ads Mediation > Network Manager).";

        static LevelPlayEditor() {
            AdsEditorHooks.RegisterMediationDrawer(typeof(LevelPlayMediation), _ => {
                // LevelPlay's own getters, which make each file the first time, as its menu does.
                AdsSdkSettingsGui.Draw(LevelPlayMediationSettingsInspector.LevelPlayMediationSettings, DrawMediation);
                AdsSdkSettingsGui.Draw(LevelPlayMediationNetworkSettingsInspector.LevelPlayMediationNetworkSettings, DrawNetworks);
                DrawNetworkManager();
            });
        }

        // LevelPlay keeps its adapters' installs to its Network Manager, whose code is internal to its package: the section opens that window.
        private static void DrawNetworkManager() {
            using var section = Section.Foldout("RiseOn.Ads.LevelPlay.Networks", "Networks", NetworkManagerTip, open: false, buttons: 1);
            if (section.Button(0, SdfIconType.BoxArrowUpRight, "Open LevelPlay's Network Manager")) EditorApplication.ExecuteMenuItem(NetworkManagerMenu);
            if (!section.Open) return;

            EditorGUILayout.LabelField("LevelPlay installs and updates its networks' adapters in its Network Manager, opened by the button on the bar.", EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawMediation(SerializedObject settings) {
            AdsSdkSettingsGui.Toggle(settings.FindProperty("DeclareAD_IDPermission"), new GUIContent("AD_ID Permission", "Adds the AD_ID permission to the Android manifest, without which Android 13 and later give no advertising id. LevelPlay's \"Google Play Services Settings\"."));
            AdsSdkSettingsGui.Toggle(settings.FindProperty("EnableAdapterDebug"), new GUIContent("Adapter Debug", "The network adapters log what they do. LevelPlay's \"Enable Adapter Debug\"."));
            AdsSdkSettingsGui.Toggle(settings.FindProperty("EnableIntegrationHelper"), new GUIContent("Integration Helper", "LevelPlay checks every network adapter as it starts and logs what is missing. LevelPlay's \"Enable Integration Helper\"."));
        }

        private static void DrawNetworks(SerializedObject settings) {
            var admob = settings.FindProperty("EnableAdmob");
            EditorGUI.BeginChangeCheck();
            AdsSdkSettingsGui.Toggle(admob, new GUIContent("AdMob Network", "LevelPlay mediates AdMob: it writes the AdMob app id into the Android manifest and Info.plist for its AdMob adapter. LevelPlay's \"AdMob Integration\"."));
            // Turned on, the file takes the ids at once; the file applies first, as the sync reads it from disk.
            if (EditorGUI.EndChangeCheck() && admob.boolValue) {
                settings.ApplyModifiedProperties();
                ConsentAppIdField.Sync();
                settings.Update();
            }

            if (admob.boolValue) ConsentAppIdField.Draw();
        }
    }
}