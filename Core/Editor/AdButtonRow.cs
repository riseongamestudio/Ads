using System;
using RiseOn.Utils.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// A row of the Formats section: its value, then a square at its first line's end in the column of the section's buttons, empty on a row with no button, so every row ends at one edge.<br/>
    /// A row with a Remote Config key has its drop-down on that line too, before the square.
    /// </summary>
    internal static class AdButtonRow {
        // A field's margins above and below: a group takes the least of its members', so a square without them would leave the row none,
        // and the rows would touch each other and the section's border.
        private static GUIStyle cell;

        public static void Draw(Action draw, Action<Rect> button = null, Action<Rect> key = null) {
            cell ??= new GUIStyle { margin = new RectOffset(0, 0, EditorStyles.textField.margin.top, EditorStyles.textField.margin.bottom) };
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            draw();
            EditorGUILayout.EndVertical();
            if (key != null) {
                GUILayout.Space(AdRemoteKeyDrawer.Gap);
                key(GUILayoutUtility.GetRect(AdRemoteKeyDrawer.Width, EditorGUIUtility.singleLineHeight, cell, GUILayout.Width(AdRemoteKeyDrawer.Width), GUILayout.ExpandWidth(false)));
            }

            var rect = GUILayoutUtility.GetRect(Section.ButtonWidth, EditorGUIUtility.singleLineHeight, cell, GUILayout.Width(Section.ButtonWidth), GUILayout.ExpandWidth(false));
            button?.Invoke(rect);
            EditorGUILayout.EndHorizontal();
        }
    }
}