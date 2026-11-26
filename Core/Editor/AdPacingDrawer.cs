using RiseOn.Utils.Editor;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>Every <see cref="AdPacing"/> field of the game's manager as a section of the page: its label on the bar, its three rows under it.</summary>
    internal sealed class AdPacingDrawer : OdinValueDrawer<AdPacing> {
        protected override void Initialize() {
            Section.Restore(Property);
        }

        protected override void DrawPropertyLayout(GUIContent label) {
            using var section = Section.Foldout(Property, label?.text ?? Property.NiceName, label?.tooltip);
            if (!section.Open) return;

            foreach (var child in Property.Children) child.Draw();
        }
    }
}