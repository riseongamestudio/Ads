using RiseOn.Utils.Editor;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// An ad unit id as every Android and iOS pair of the settings pages draws: <see cref="PlatformFieldUtils"/>.<br/>
    /// A field with an <see cref="AdUnitTestAttribute"/> has a Test toggle after each platform's id, and keeps the attribute's test units stored in the value.
    /// </summary>
    internal sealed class AdUnitIdDrawer : OdinValueDrawer<AdUnitId> {
        private static readonly GUIContent test = new("Test", "Serve the network's test ads on this platform from its test unit, not from the id written here");

        protected override void DrawPropertyLayout(GUIContent label) {
            var ids = new Ids(Property);
            var changed = ids.Test == null
                ? PlatformFieldUtils.Draw(label, ref ids.Android, ref ids.IOS)
                : PlatformFieldUtils.Draw(label, ref ids.Android, ref ids.IOS, test, ref ids.AndroidTest, ref ids.IOSTest);
            if (changed) ids.Write(Property);
        }

        /// <summary>The ids of <paramref name="property"/>, an <see cref="AdUnitId"/>, in one row of <paramref name="rect"/>: a format section's bar.</summary>
        internal static void Draw(Rect rect, InspectorProperty property) {
            var ids = new Ids(property);
            var changed = ids.Test == null
                ? PlatformFieldUtils.Draw(rect, ref ids.Android, ref ids.IOS)
                : PlatformFieldUtils.Draw(rect, ref ids.Android, ref ids.IOS, test, ref ids.AndroidTest, ref ids.IOSTest);
            if (changed) ids.Write(property);
        }

        // The value's fields, read once per draw.
        private sealed class Ids {
            public readonly AdUnitTestAttribute Test;
            public string Android;
            public string IOS;
            public bool   AndroidTest;
            public bool   IOSTest;

            public Ids(InspectorProperty property) {
                Android     = (string) property.Children["android"].ValueEntry.WeakSmartValue;
                IOS         = (string) property.Children["ios"].ValueEntry.WeakSmartValue;
                AndroidTest = (bool) property.Children["androidTest"].ValueEntry.WeakSmartValue;
                IOSTest     = (bool) property.Children["iosTest"].ValueEntry.WeakSmartValue;
                Test        = property.GetAttribute<AdUnitTestAttribute>();
                StoreTestIds(property);
            }

            public void Write(InspectorProperty property) {
                property.Children["android"].ValueEntry.WeakSmartValue     = Android;
                property.Children["ios"].ValueEntry.WeakSmartValue         = IOS;
                property.Children["androidTest"].ValueEntry.WeakSmartValue = AndroidTest;
                property.Children["iosTest"].ValueEntry.WeakSmartValue     = IOSTest;
                StoreTestIds(property);
            }

            // Only for a platform switched on, so that a page merely shown changes no asset; the attribute's units change with the pack, and
            // the value keeps the ones a build serves.
            private void StoreTestIds(InspectorProperty property) {
                if (Test == null) return;

                if (AndroidTest) Store("androidTestId", Test.Android);
                if (IOSTest) Store("iosTestId", Test.IOS);

                void Store(string name, string id) {
                    var entry = property.Children[name].ValueEntry;
                    if ((string) entry.WeakSmartValue != id) entry.WeakSmartValue = id;
                }
            }
        }
    }
}