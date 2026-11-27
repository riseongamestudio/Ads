using RiseOn.Utils.Editor;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// The Editor Sorting Order section: the field's value on the bar, and under it each layer of the Editor's ads with the sorting order it takes from that value,
    /// lowest first, as <see cref="AdsEditorLayers"/> stacks them.
    /// </summary>
    internal sealed class AdsEditorLayersTableAttributeDrawer : OdinAttributeDrawer<AdsEditorLayersTableAttribute, int> {
        private static readonly (GUIContent Layer, int Offset)[] layers = {
            (new GUIContent("Banner, MRec", "The banner, the MRec, and every view over the game a mediation draws: the field's value."), 0)
          , (new GUIContent("Native In-Feed, MRec", "The native in-feed cells and native MRec blocks."), 1)
          , (new GUIContent("Collapsible Cover", "The ground under a chained collapsible while one ad swaps for the next."), 2)
          , (new GUIContent("Native Collapsible", "The collapsible's panel."), 3)
          , (new GUIContent("Full-Screen Cover", "The ground under a chain of full-screen ads."), 4)
          , (new GUIContent("Full-Screen Ads", "Every full-screen ad, native ones and the placeholders included."), 5)
        };

        protected override void Initialize() {
            Section.Restore(Property, false);
        }

        protected override void DrawPropertyLayout(GUIContent label) {
            label ??= Property.Label;
            using var section = Section.Foldout(Property, label.text, label.tooltip, barValue: true);
            var entry = ValueEntry;
            EditorGUI.showMixedValue = entry.ValueState is PropertyValueState.PrimitiveValueConflict;
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.IntField(section.BarValue, entry.SmartValue);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck()) entry.SmartValue = value;

            if (!section.Open) return;

            foreach (var (layer, offset) in layers) {
                var order = entry.SmartValue + offset * AdsEditorLayers.Step;
                EditorGUILayout.LabelField(new GUIContent(layer.text, $"{layer.tooltip} Its sorting order: the field's value plus {offset * AdsEditorLayers.Step}."), new GUIContent(order.ToString()));
            }
        }
    }
}