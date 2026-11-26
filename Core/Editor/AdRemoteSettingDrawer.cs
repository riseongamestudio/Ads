using RiseOn.Utils.Editor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// A manager setting on one row: the page's value, then a drop-down of the Remote Config pack's keys of the setting's type, No Remote Key first.<br/>
    /// A picked key replaces the value, which then shows dimmed; a key since removed shows as missing. Without that pack the drop-down stays, locked, saying why.
    /// </summary>
    internal sealed class AdRemoteSettingDrawer<T> : OdinValueDrawer<AdRemoteSetting<T>> {
#if ADS_SUPPORT_REMOTECONFIG
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
            var key     = new Rect(field.xMax - AdRemoteKeyDrawer.Width, field.y, AdRemoteKeyDrawer.Width, field.height);
            field.xMax = key.x - AdRemoteKeyDrawer.Gap;
            remote     = current != 0;
            // The pick lands after this draw, from the drop-down's window, so the page repaints to show it.
            var window = GUIHelper.CurrentWindow;
            ChoiceFieldUtils.Value(key, AdRemoteKeys.Choices(typeof(T), current), current, picked => {
                id.RecordForUndo("Pick a remote key");
                id.ValueEntry.WeakSmartValue = picked;
                if (window) window.Repaint();
            }, search: false);
            GUI.Label(key, new GUIContent("", keyTooltip));
#else
            var key = new Rect(field.xMax - AdRemoteKeyDrawer.Width, field.y, AdRemoteKeyDrawer.Width, field.height);
            field.xMax = key.x - AdRemoteKeyDrawer.Gap;
#endif
            using (new EditorGUI.DisabledScope(remote)) Value(field);
#if !ADS_SUPPORT_REMOTECONFIG
            // After the value: the row's label draws with the control after it, and would dim with a locked one.
            AdRemoteKeyDrawer.DrawNoPack(key);
#endif
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