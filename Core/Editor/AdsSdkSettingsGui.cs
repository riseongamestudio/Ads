using System;
using System.Collections.Generic;
using RiseOn.Utils.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// An SDK's own settings file, drawn in place under its mediation: the mediation's Editor assembly draws the fields the game decides, each under a short label with the SDK's own words in its tooltip; fields another pack writes, or the SDK's window keeps for itself, stay out.
    /// </summary>
    /// <example><code>
    /// AdsSdkSettingsGui.Draw(sdkSettings, settings => AdsSdkSettingsGui.Toggle(settings.FindProperty("debug"), new GUIContent("Debug", "...")));
    /// </code></example>
    public static class AdsSdkSettingsGui {
        private static readonly Dictionary<UnityEngine.Object, SerializedObject> serializedObjects = new();

        /// <summary>Draws <paramref name="settings"/> with <paramref name="draw"/>, and saves the file on an edit.</summary>
        public static void Draw(ScriptableObject settings, Action<SerializedObject> draw) {
            if (settings == null) {
                HelpBoxUtils.Draw("SDK settings file not found", MessageType.Warning);
                return;
            }

            if (!serializedObjects.TryGetValue(settings, out var serialized) || serialized.targetObject == null) {
                serialized                  = new SerializedObject(settings);
                serializedObjects[settings] = serialized;
            }

            serialized.Update();
            draw(serialized);
            if (serialized.ApplyModifiedProperties()) Save(settings);
        }

        /// <summary>Writes the file, for an edit applied outside <see cref="Draw"/> too, as a drop-down's pick is.</summary>
        internal static void Save(UnityEngine.Object settings) {
            EditorUtility.SetDirty(settings);
            DeferredSave.Request(settings);
        }

        /// <summary>A bool field; <paramref name="inverted"/> ticks it while the field is false, for an SDK flag that turns a feature off.</summary>
        public static void Toggle(SerializedProperty property, GUIContent label, bool inverted = false) {
            EditorGUI.BeginChangeCheck();
            var on = EditorGUILayout.Toggle(label, property.boolValue != inverted);
            if (EditorGUI.EndChangeCheck()) property.boolValue = on != inverted;
        }
    }
}