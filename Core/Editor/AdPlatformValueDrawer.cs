using System;
using System.Linq;
using RiseOn.Utils.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// A value that may differ between Android and iOS: one row while both share it, with a button at its end to split it; split, a row for each platform under the label's row, whose button links them back into Android's value.
    /// </summary>
    internal sealed class AdPlatformValueDrawer<T> : OdinValueDrawer<AdPlatformValue<T>> {
        protected override void DrawPropertyLayout(GUIContent label) {
            AdPlatformValueRows.Draw(Property, label);
        }
    }

    /// <inheritdoc cref="AdPlatformValueDrawer{T}"/>
    internal static class AdPlatformValueRows {

        /// <param name="key">Draws the value's Remote Config key at the end of its first row, the label's row once split, as one key serves both platforms; null for a value with none.</param>
        public static void Draw(InspectorProperty property, GUIContent label, Action<Rect> key = null) {
            label ??= GUIContent.none;
            var split  = property.Children["split"];
            var shared = property.Children["value"];
            var iosRow = property.Children["ios"];

            if (!(bool) split.ValueEntry.WeakSmartValue) {
                AdButtonRow.Draw(() => shared.Draw(label), rect => {
                    if (!Section.IconButton(rect, SdfIconType.ViewStacked, "Set a value for each platform, Android's and iOS's, starting from this one")) return;

                    iosRow.ValueEntry.WeakSmartValue = Copy(shared.ValueEntry.WeakSmartValue);
                    split.ValueEntry.WeakSmartValue  = true;
                }, key);
                return;
            }

            AdButtonRow.Draw(() => EditorGUILayout.LabelField(label), rect => {
                if (Section.IconButton(rect, SdfIconType.Link, "Link the platforms back to one value for both: Android's")) split.ValueEntry.WeakSmartValue = false;
            }, key);
            using (new EditorGUI.IndentLevelScope()) {
                AdButtonRow.Draw(() => shared.Draw(PlatformMarks.Android));
                AdButtonRow.Draw(() => iosRow.Draw(PlatformMarks.IOS));
            }
        }

        private const float MarkGap = 2;

        // No margins of its own: a group without a style takes its members', and would sit their margin lower than the bar.
        private static GUIStyle barLine;

        /// <summary>
        /// The mediations on the one line of the bar of the section just begun, in its value column down to the bar's right end: the split button, the row, then the key;
        /// once split, the link button, then Android's row and iOS's side by side, each after its platform's mark, then the key.<br/>
        /// Laid out over the bar rather than in an area of its rect: an area takes the rect of the Layout event, before the bar has one.
        /// </summary>
        public static void DrawOnBar(InspectorProperty property, Action<Rect, InspectorProperty> value, Action<Rect> key) {
            var split      = property.Children["split"];
            var shared     = property.Children["value"];
            var iosRow     = property.Children["ios"];
            var splitNow   = (bool) split.ValueEntry.WeakSmartValue;
            var labelWidth = EditorGUIUtility.labelWidth;
            barLine ??= new GUIStyle();
            // Back up over the bar and take exactly its height, so the section keeps its own; then past its title: the section narrowed the label
            // column by its inset, and the bar's value starts a label gap past the page's. The button and the rest of the line are rects of the
            // row itself with a field's margins, so each sits that margin under the bar's top: a line of 18 on a bar of 22. A value drawn as a
            // property instead would sit wherever the groups its drawer opens put it.
            GUILayout.Space(-Section.Height);
            EditorGUILayout.BeginHorizontal(barLine, GUILayout.Height(Section.Height));
            GUILayout.Space(labelWidth + Section.Inset + Section.LabelGap);
            var button = GUILayoutUtility.GetRect(Section.ButtonWidth, EditorGUIUtility.singleLineHeight, AdButtonRow.Cell, GUILayout.Width(Section.ButtonWidth), GUILayout.ExpandWidth(false));
            GUILayout.Space(Section.LabelGap);
            var line = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, AdButtonRow.Cell, GUILayout.MinWidth(0), GUILayout.ExpandWidth(true));
            // The bar's own inset at its end, as for a value without buttons.
            GUILayout.Space(Section.Inset);
            EditorGUILayout.EndHorizontal();

            // The line shared out by hand in thirds of the room its fields have, the marks and the gaps set aside: split, a third to each
            // platform's value and one to the key; one value takes two thirds and the room of the marks between them. The shares hold
            // whatever the values read, so every bar's key keeps one column.
            var marks = PlatformMarks.Width(PlatformMarks.Android) + PlatformMarks.Width(PlatformMarks.IOS) + 2 * MarkGap;
            var third = Mathf.Max(0, line.width - marks - 2 * AdRemoteKeyDrawer.Gap) / 3;
            var key0  = key != null ? line.xMax - third : line.xMax + AdRemoteKeyDrawer.Gap;
            var end   = key0 - AdRemoteKeyDrawer.Gap;
            if (!splitNow) {
                value(Rect.MinMaxRect(line.x, line.y, end, line.yMax), shared);
            } else {
                var each = Mathf.Max(0, end - line.x - marks - AdRemoteKeyDrawer.Gap) / 2;
                var x    = Half(line.x, PlatformMarks.Android, shared);
                Half(x + AdRemoteKeyDrawer.Gap, PlatformMarks.IOS, iosRow);

                // A platform's mark, then its value; where the value ends.
                float Half(float from, GUIContent mark, InspectorProperty row) {
                    var markWidth = PlatformMarks.Width(mark);
                    PlatformMarks.Draw(new Rect(from, line.y, markWidth, line.height), mark);
                    var field = new Rect(from + markWidth + MarkGap, line.y, each, line.height);
                    value(field, row);
                    return field.xMax;
                }
            }

            if (key != null) key(Rect.MinMaxRect(key0, line.y, line.xMax, line.yMax));
            if (!splitNow) {
                if (Section.IconButton(button, SdfIconType.ViewStacked, "Set a value for each platform, Android's and iOS's, starting from this one")) {
                    iosRow.ValueEntry.WeakSmartValue = Copy(shared.ValueEntry.WeakSmartValue);
                    split.ValueEntry.WeakSmartValue  = true;
                }
            } else if (Section.IconButton(button, SdfIconType.Link, "Link the platforms back to one value for both: Android's")) {
                split.ValueEntry.WeakSmartValue = false;
            }
        }

        // A list is the one value held by reference: iOS's starts as a list of its own.
        private static object Copy(object value) {
            return value is AdPriorityList list ? new AdPriorityList(list.MediationIds.ToArray()) : value;
        }
    }
}