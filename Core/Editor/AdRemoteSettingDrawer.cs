using RiseOn.Utils.Editor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// A format's rule on one row: the page's value, then, with the Remote Config pack, a drop-down of that pack's keys of the setting's type, No Remote Key first.<br/>
    /// A picked key replaces the value, which then shows dimmed; a key since removed shows as missing.
    /// </summary>
    internal sealed class AdRemoteSettingDrawer<T> : OdinValueDrawer<AdRemoteSetting<T>> {
        private const float Gap = 4;

#if ADS_SUPPORT_REMOTECONFIG
        // As wide as leaves a key's name readable, and no wider, so the value keeps its room.
        private const float KeyWidth = 160;

        private static readonly string keyTooltip = $"A Remote Config {(typeof(T) == typeof(bool) ? "bool" : "float")} key whose value replaces this setting; No Remote Key keeps the page's value.";
#endif

        protected override void DrawPropertyLayout(GUIContent label) {
            var rect   = EditorGUILayout.GetControlRect(label != null);
            var field  = label == null ? rect : EditorGUI.PrefixLabel(rect, label);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var remote = false;
#if ADS_SUPPORT_REMOTECONFIG
            var id      = Property.Children["key"].Children["id"];
            var current = (int) id.ValueEntry.WeakSmartValue;
            var key     = new Rect(field.xMax - KeyWidth, field.y, KeyWidth, field.height);
            field.xMax = key.x - Gap;
            remote     = current != 0;
            // The pick lands after this draw, from the drop-down's window, so the page repaints to show it.
            var window = GUIHelper.CurrentWindow;
            ChoiceFieldUtils.Value(key, AdRemoteKeys.Choices(typeof(T), current), current, picked => {
                id.RecordForUndo("Pick a remote key");
                id.ValueEntry.WeakSmartValue = picked;
                if (window) window.Repaint();
            }, search: false);
            GUI.Label(key, new GUIContent("", keyTooltip));
#endif
            using (new EditorGUI.DisabledScope(remote)) Value(field);
            EditorGUI.indentLevel = indent;
        }

        // The page's value: a float at 0 or above, every one being seconds or a weight; or a bool.
        private void Value(Rect rect) {
            var entry = Property.Children["value"].ValueEntry;
            EditorGUI.BeginChangeCheck();
            object next = entry.WeakSmartValue switch {
                float number => Mathf.Max(EditorGUI.FloatField(rect, number), 0),
                bool on      => EditorGUI.Toggle(rect, on),
                var other    => other
            };
            if (EditorGUI.EndChangeCheck()) entry.WeakSmartValue = next;
        }
    }
}