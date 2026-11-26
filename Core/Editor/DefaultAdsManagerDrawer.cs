using RiseOn.Utils;
using RiseOn.Utils.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>The Manager section while DefaultAdsManager runs: its script on the bar as for any manager, with Create Own right of it, which makes the game a manager of its own from it.</summary>
    // Above Utils' drawer for [SectionReference], which it replaces for this one class.
    [DrawerPriority(2, 0, 0)]
    internal sealed class DefaultAdsManagerDrawer : OdinAttributeDrawer<SectionReferenceAttribute, DefaultAdsManager> {
        private static readonly GUIContent create = new("Create Own", "Copies DefaultAdsManager into the game as its own class, with the values below, for rules the game changes, and points the game's scripts that call DefaultAdsManager at it; the pack then runs that class.");

        protected override void Initialize() {
            Section.Restore(Property);
        }

        protected override void DrawPropertyLayout(GUIContent label) {
            label ??= Property.Label;
            using var section = Section.Foldout(Property, label.text, label.tooltip, barValue: true);
            var bar    = section.BarValue;
            var width  = Mathf.Ceil(GUI.skin.button.CalcSize(create).x);
            var button = new Rect(bar.xMax - width, bar.y, width, bar.height);
            ScriptUtils.Field(Rect.MinMaxRect(bar.x, bar.y, button.x - AdRemoteKeyDrawer.Gap, bar.yMax), GUIContent.none, typeof(DefaultAdsManager));
            if (GUI.Button(button, create)) {
                AdsSettingsProvider.CreateOwnManager();
                GUIUtility.ExitGUI();
            }

            if (!section.Open) return;

            foreach (var child in Property.Children) child.Draw();
        }
    }
}