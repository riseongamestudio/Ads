using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>A plan as its list of steps alone, whose bar carries the plan's name and weight.</summary>
    internal sealed class AdCollapsiblePlanDrawer : OdinValueDrawer<AdCollapsiblePlan> {
        protected override void DrawPropertyLayout(GUIContent label) => Property.Children["steps"].Draw(null);
    }
}