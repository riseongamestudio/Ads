using RiseOn.Utils.Editor;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// An ad unit id as every Android and iOS pair of the settings pages draws: <see cref="PlatformFieldUtils"/>. A field with an
    /// <see cref="AdUnitTestAttribute"/> has a Test toggle after each platform's id, and keeps the attribute's test units stored in the value.
    /// </summary>
    internal sealed class AdUnitIdDrawer : OdinValueDrawer<AdUnitId> {
        private static readonly GUIContent test = new("Test", "Serve the network's test ads on this platform from its test unit, not from the id written here");

        protected override void DrawPropertyLayout(GUIContent label) {
            var ids = new Ids(Property);
            var changed = ids.Test == null
                ? PlatformFieldUtils.Draw(label, ref ids.Android, ref ids.Ios)
                : PlatformFieldUtils.Draw(label, ref ids.Android, ref ids.Ios, test, ref ids.AndroidTest, ref ids.IosTest);
            if (changed) ids.Write(Property);
        }

        /// <summary>The ids of <paramref name="property"/>, an <see cref="AdUnitId"/>, in one row of <paramref name="rect"/>: a format section's bar.</summary>
        internal static void Draw(Rect rect, InspectorProperty property) {
            var ids = new Ids(property);
            var changed = ids.Test == null
                ? PlatformFieldUtils.Draw(rect, ref ids.Android, ref ids.Ios)
                : PlatformFieldUtils.Draw(rect, ref ids.Android, ref ids.Ios, test, ref ids.AndroidTest, ref ids.IosTest);
            if (changed) ids.Write(property);
        }

        // The value's fields, read once per draw.
        private sealed class Ids {
            public readonly AdUnitTestAttribute Test;
            public string Android;
            public string Ios;
            public bool   AndroidTest;
            public bool   IosTest;

            public Ids(InspectorProperty property) {
                Android     = (string) property.Children["android"].ValueEntry.WeakSmartValue;
                Ios         = (string) property.Children["ios"].ValueEntry.WeakSmartValue;
                AndroidTest = (bool) property.Children["androidTest"].ValueEntry.WeakSmartValue;
                IosTest     = (bool) property.Children["iosTest"].ValueEntry.WeakSmartValue;
                Test        = property.GetAttribute<AdUnitTestAttribute>();
                StoreTestIds(property);
            }

            public void Write(InspectorProperty property) {
                property.Children["android"].ValueEntry.WeakSmartValue     = Android;
                property.Children["ios"].ValueEntry.WeakSmartValue         = Ios;
                property.Children["androidTest"].ValueEntry.WeakSmartValue = AndroidTest;
                property.Children["iosTest"].ValueEntry.WeakSmartValue     = IosTest;
                StoreTestIds(property);
            }

            // Only for a platform switched on, so that a page merely shown changes no asset; the attribute's units change with the pack, and
            // the value keeps the ones a build serves.
            private void StoreTestIds(InspectorProperty property) {
                if (Test == null) return;

                if (AndroidTest) Store("androidTestId", Test.Android);
                if (IosTest) Store("iosTestId", Test.Ios);

                void Store(string name, string id) {
                    var entry = property.Children[name].ValueEntry;
                    if ((string) entry.WeakSmartValue != id) entry.WeakSmartValue = id;
                }
            }
        }
    }
}