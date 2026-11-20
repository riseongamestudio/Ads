using System.Collections.Generic;
using System.Linq;
using RiseOn.Utils.Editor;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>Draws <see cref="AdFormatSectionAttribute"/>: a <see cref="Section"/> with the format's ids on its bar.</summary>
    internal sealed class AdFormatSectionAttributeDrawer : OdinGroupDrawer<AdFormatSectionAttribute> {
        protected override void Initialize() {
            Section.Restore(Property, !Attribute.Closed);
        }

        protected override void DrawPropertyLayout(GUIContent label) {
            Split(out var ids, out var rest);
            using var section = rest.Count > 0
                ? Section.Foldout(Property, Attribute.GroupName, barValue: ids != null)
                : new Section(Attribute.GroupName, barValue: ids != null, barOnly: true);
            if (ids != null) AdUnitIdDrawer.Draw(section.BarValue, ids);
            if (!section.Open) return;

            foreach (var child in rest) child.Draw();
        }

        // The format's ids, a member of the group or of the one settings object it holds, and the members that go under the bar.
        private void Split(out InspectorProperty ids, out List<InspectorProperty> rest) {
            var members = Property.Children.ToList();
            if (members.Count == 1 && IsSettingsObject(members[0])) members = members[0].Children.ToList();

            var found = members.FirstOrDefault(IsIds);
            ids  = found;
            rest = members.Where(member => member != found).ToList();
        }

        private static bool IsIds(InspectorProperty property) => property.ValueEntry?.TypeOfValue == typeof(AdUnitId);

        // A class of a mediation's that holds a format's ids and settings, not a value such as a color.
        private static bool IsSettingsObject(InspectorProperty property) {
            var type = property.ValueEntry?.TypeOfValue;
            return !IsIds(property) && type is { IsClass: true } && type != typeof(string) && type.Namespace != "UnityEngine" && property.Children.Count > 0;
        }
    }
}