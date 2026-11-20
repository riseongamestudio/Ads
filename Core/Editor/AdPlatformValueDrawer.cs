using System;
using System.Linq;
using RiseOn.Utils.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// A value that may differ between Android and iOS: one row while both share it, with a button at its end to split it; split, a row for each platform
    /// under the label's row, which says which platform's value the Editor uses and whose button links them back into Android's value.
    /// </summary>
    internal sealed class AdPlatformValueDrawer<T> : OdinValueDrawer<AdPlatformValue<T>> {
        private const float PopupWidth = 90;

        private static readonly GUIContent android   = new("Android");
        private static readonly GUIContent ios       = new("iOS");
        private static readonly GUIContent editorUse = new("Editor uses", "Which platform's value Play mode in the Editor uses");
        private static readonly (string Name, AdPlatform Value)[] platforms = { ("Android", AdPlatform.Android), ("iOS", AdPlatform.IOS) };

        protected override void DrawPropertyLayout(GUIContent label) {
            label ??= GUIContent.none;
            var split  = Property.Children["split"];
            var shared = Property.Children["value"];
            var iosRow = Property.Children["ios"];
            var editor = Property.Children["editor"];

            if (!(bool) split.ValueEntry.WeakSmartValue) {
                AdButtonRow.Draw(() => shared.Draw(label), rect => {
                    if (!Section.IconButton(rect, SdfIconType.ViewStacked, "Set a value for each platform, Android's and iOS's, starting from this one")) return;

                    iosRow.ValueEntry.WeakSmartValue = Copy(shared.ValueEntry.WeakSmartValue);
                    split.ValueEntry.WeakSmartValue  = true;
                });
                return;
            }

            AdButtonRow.Draw(() => EditorPick(label, editor), rect => {
                if (Section.IconButton(rect, SdfIconType.Link, "Link the platforms back to one value for both: Android's")) split.ValueEntry.WeakSmartValue = false;
            });
            using (new EditorGUI.IndentLevelScope()) {
                AdButtonRow.Draw(() => shared.Draw(android));
                AdButtonRow.Draw(() => iosRow.Draw(ios));
            }
        }

        // The label, then in the value column "Editor uses" and a drop-down of the two platforms.
        private static void EditorPick(GUIContent label, InspectorProperty editor) {
            var value  = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), label);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var text = EditorStyles.label.CalcSize(editorUse).x;
            EditorGUI.LabelField(new Rect(value.x, value.y, text, value.height), editorUse);
            // The pick lands after this draw, from the drop-down's window, so the page repaints to show it.
            var window = GUIHelper.CurrentWindow;
            ChoiceFieldUtils.Value(new Rect(value.x + text + 4, value.y, PopupWidth, value.height), platforms, (AdPlatform) editor.ValueEntry.WeakSmartValue, picked => {
                editor.RecordForUndo("Pick the Editor's platform");
                editor.ValueEntry.WeakSmartValue = picked;
                if (window) window.Repaint();
            }, search: false);
            EditorGUI.indentLevel = indent;
        }

        // A list is the one value held by reference: iOS's starts as a list of its own.
        private static object Copy(object value) => value is AdPriorityList list ? new AdPriorityList(list.MediationIds.ToArray()) : value;
    }

    /// <summary>A row of the Formats section: its value, then a square at its first line's end in the column of the section's buttons, empty on a row with no button, so every row ends at one edge.</summary>
    internal static class AdButtonRow {
        // A field's margins above and below: a group takes the least of its members', so a square without them would leave the row none,
        // and the rows would touch each other and the section's border.
        private static GUIStyle cell;

        public static void Draw(Action draw, Action<Rect> button = null) {
            cell ??= new GUIStyle { margin = new RectOffset(0, 0, EditorStyles.textField.margin.top, EditorStyles.textField.margin.bottom) };
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            draw();
            EditorGUILayout.EndVertical();
            var rect = GUILayoutUtility.GetRect(Section.ButtonWidth, EditorGUIUtility.singleLineHeight, cell, GUILayout.Width(Section.ButtonWidth), GUILayout.ExpandWidth(false));
            button?.Invoke(rect);
            EditorGUILayout.EndHorizontal();
        }
    }
}