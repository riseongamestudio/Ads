using System;
using System.Collections.Generic;
using RiseOn.Utils.Editor;
using UnityEditor;
using UnityEngine;
#if ADS_SUPPORT_REMOTECONFIG
using System.Reflection;
using RiseOn.Firebase.RemoteConfig;
#endif

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// A format's Remote Config key for its mediations, the Mediations Remote Key row of its section under Formats: a drop-down of that pack's string keys, No Remote Key first; a key since removed shows as missing.<br/>
    /// One key for every platform.<br/>
    /// Without the pack the drop-down stays, locked, saying why, as every Remote Config key of the page does.
    /// </summary>
    internal static class AdRemoteKeyDrawer {
        public const string Tooltip = "A Remote Config string key whose value replaces this format's mediations at launch: mediation ids best first, split by commas, such as \"max,admob\", or \"off\" to turn the format off. "
                                    + "No Remote Key, or an empty value, keeps the page's. One key for both platforms; a Remote Config condition on the platform gives each its own value.";

        /// <summary>Draws <paramref name="format"/>'s key in <paramref name="rect"/>; a pick goes straight into the settings asset, with undo.</summary>
        public static void Draw(Rect rect, AdsSettings settings, AdFormat format) {
#if ADS_SUPPORT_REMOTECONFIG
            var current = settings.RemoteKeys[format].Id;
            ChoiceFieldUtils.Value(rect, AdRemoteKeys.Choices(typeof(string), current), current, picked => {
                Undo.RecordObject(settings, "Pick a remote key");
                settings.RemoteKeys[format] = new AdRemoteKey { Id = picked };
                EditorUtility.SetDirty(settings);
            });
            GUI.Label(rect, new GUIContent("", Tooltip));
#else
            DrawNoPack(rect);
#endif
        }

#if !ADS_SUPPORT_REMOTECONFIG
        private static readonly GUIContent noRemoteConfig = new("No Remote Config", "The project lacks com.riseon.firebase.remoteconfig, so there is no Remote Config key to pick and the page's value holds. Install that pack to pick one.");

        /// <summary>A Remote Config key's drop-down in a project without that pack: locked, saying why.</summary>
        public static void DrawNoPack(Rect rect) {
            using (new EditorGUI.DisabledScope(true)) EditorGUI.DropdownButton(rect, noRemoteConfig, FocusType.Passive, EditorStyles.popup);
            GUI.Label(rect, new GUIContent("", noRemoteConfig.tooltip));
        }
#endif

    }

    /// <summary>The Remote Config pack's keys by the type of their value: string keys name a format's mediations, float and bool keys a format's rule; none without the pack.</summary>
    internal static class AdRemoteKeys {
        private static readonly Dictionary<Type, List<(string Name, int Value)>> byType = new();

        /// <summary>Each key of values of <paramref name="type"/>, by its name and number; the keys change with a recompile only, which clears this.</summary>
        public static IReadOnlyList<(string Name, int Value)> Of(Type type) {
            if (!byType.TryGetValue(type, out var keys)) byType[type] = keys = Find(type);
            return keys;
        }

        public static bool Is(int id, Type type) {
            foreach (var key in Of(type)) {
                if (key.Value == id) return true;
            }

            return false;
        }

        /// <summary>The drop-down's choices: No Remote Key first, the keys of <paramref name="type"/>, and <paramref name="current"/> as missing when it is not one of them.</summary>
        public static List<(string Name, int Value)> Choices(Type type, int current) {
            var choices = new List<(string Name, int Value)> { ("No Remote Key", 0) };
            choices.AddRange(Of(type));
            if (current != 0 && !Is(current, type)) choices.Add(($"Missing ({current})", current));
            return choices;
        }

        private static List<(string Name, int Value)> Find(Type type) {
            var keys = new List<(string Name, int Value)>();
#if ADS_SUPPORT_REMOTECONFIG
            // The pack's source generator gives each key a property of its value's type and an enum member of the same name.
            var valueType = typeof(RemoteValue<>).MakeGenericType(type);
            foreach (var property in typeof(RemoteKeys).GetProperties(BindingFlags.Public | BindingFlags.Static)) {
                if (property.PropertyType == valueType && Enum.TryParse<RemoteKey>(property.Name, out var key)) keys.Add((property.Name, (int) key));
            }
#endif
            return keys;
        }
    }
}