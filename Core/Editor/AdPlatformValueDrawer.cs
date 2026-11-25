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
        private static readonly GUIContent android = new("Android");
        private static readonly GUIContent ios     = new("iOS");

        protected override void DrawPropertyLayout(GUIContent label) {
            label ??= GUIContent.none;
            var split  = Property.Children["split"];
            var shared = Property.Children["value"];
            var iosRow = Property.Children["ios"];

            if (!(bool) split.ValueEntry.WeakSmartValue) {
                AdButtonRow.Draw(() => shared.Draw(label), rect => {
                    if (!Section.IconButton(rect, SdfIconType.ViewStacked, "Set a value for each platform, Android's and iOS's, starting from this one")) return;

                    iosRow.ValueEntry.WeakSmartValue = Copy(shared.ValueEntry.WeakSmartValue);
                    split.ValueEntry.WeakSmartValue  = true;
                });
                return;
            }

            AdButtonRow.Draw(() => EditorGUILayout.LabelField(label), rect => {
                if (Section.IconButton(rect, SdfIconType.Link, "Link the platforms back to one value for both: Android's")) split.ValueEntry.WeakSmartValue = false;
            });
            using (new EditorGUI.IndentLevelScope()) {
                AdButtonRow.Draw(() => shared.Draw(android));
                AdButtonRow.Draw(() => iosRow.Draw(ios));
            }
        }

        // A list is the one value held by reference: iOS's starts as a list of its own.
        private static object Copy(object value) {
            return value is AdPriorityList list ? new AdPriorityList(list.MediationIds.ToArray()) : value;
        }
    }
}