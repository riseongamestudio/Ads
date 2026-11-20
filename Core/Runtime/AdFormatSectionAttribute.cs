using System.Diagnostics;
using Sirenix.OdinInspector;

namespace RiseOn.Ads {
    /// <summary>
    /// Groups a format's members into one section of its mediation: the format's <see cref="AdUnitId"/> on the bar after its name, the rest under the bar.<br/>
    /// A format with more than its ids folds; one with only its ids is the bar alone.<br/>
    /// A group holding one settings object takes the object's <see cref="AdUnitId"/> for the bar and its other fields for the rest.<br/>
    /// Every format's section sits in the mediation's <see cref="Group"/> section, which one member declares with <c>[SectionGroup(AdFormatSectionAttribute.Group)]</c>.
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    public sealed class AdFormatSectionAttribute : PropertyGroupAttribute {
        /// <summary>The section starts folded the first time it shows in a session: for a format seldom used.</summary>
        public bool Closed;

        /// <summary>The mediation's section that holds its formats' sections.</summary>
        public const string Group = "Formats";

        /// <summary>The tooltip of <see cref="Group"/>.</summary>
        public const string GroupTooltip = "Each format's ad unit ids, Android and iOS, on its bar, with the format's own settings under it. "
                                         + "Formats on the same ad unit id share its loaded ads, and keep loaded the most any of them asks for.";

        public AdFormatSectionAttribute(string title, float order = 0) : base(Group + "/" + title, order) { }

        protected override void CombineValuesWith(PropertyGroupAttribute other) {
            Closed |= ((AdFormatSectionAttribute) other).Closed;
        }
    }
}