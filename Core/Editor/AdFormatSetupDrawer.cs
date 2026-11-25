using System;
using System.Collections.Generic;
using System.Linq;
using RiseOn.Utils.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// The page's Formats section: a section per format with the mediations that serve it, its Remote Config key with that pack installed, and how it shows.<br/>
    /// A map saved before the enum gained a format draws as every enum map does, with its button to take the new keys.
    /// </summary>
    [DrawerPriority(0, 0, 2)]
    internal sealed class AdFormatSetupDrawer : OdinValueDrawer<AdFormatSetup> {
#if ADS_SUPPORT_REMOTECONFIG
        private const string Tip = "Each format: the mediations that serve it, tried left to right, with none the format off; the Remote Config key that can replace them at launch; and how it shows. "
                                 + "The mediations can split into a list per platform, the Editor playing the active build target's.";
#else
        private const string Tip = "Each format: the mediations that serve it, tried left to right, with none the format off; and how it shows. "
                                 + "The mediations can split into a list per platform, the Editor playing the active build target's.";
#endif

        private static readonly GUIContent mediations = new("Mediations", "Which mediations serve the format, tried left to right; with none the format is off.");
#if ADS_SUPPORT_REMOTECONFIG
        private static readonly GUIContent remoteKey = new("Mediations Remote Key", AdRemoteKeyDrawer.Tooltip);
#endif

        // Each format's settings, by the field of AdFormatSettings that holds them; the formats with nothing to set are not here.
        private static readonly Dictionary<AdFormat, string> settingsFields = new() {
            { AdFormat.Banner, "banner" }
          , { AdFormat.MRec, "mRec" }
          , { AdFormat.NativeInterstitial, "nativeInterstitial" }
          , { AdFormat.NativeAppOpen, "nativeAppOpen" }
          , { AdFormat.NativeEndCard, "nativeEndCard" }
          , { AdFormat.NativeCollapsible, "nativeCollapsible" }
          , { AdFormat.NativeMRec, "nativeMRec" }
          , { AdFormat.NativeInFeed, "nativeInFeed" }
        };

        // Each format's section keeps its fold on its entry of the map, so an Alt+click on Formats folds them all; the natives start folded.
        protected override void Initialize() {
            Section.Restore(Property);
            foreach (var entry in Property.Children["priorities"].Children["entries"].Children) {
                if (entry.Children["key"].ValueEntry.WeakSmartValue is AdFormat format) Section.Restore(entry, !format.IsNative());
            }
        }

        protected override void DrawPropertyLayout(GUIContent label) {
            using var section = Section.Foldout(Property, "Formats", Tip);
            if (!section.Open) return;

            var priorities = Property.Children["priorities"];
            var entries    = priorities.Children["entries"].Children.ToDictionary(entry => entry.Children["key"].ValueEntry.WeakSmartValue as AdFormat? ?? (AdFormat) (-1));
            var formats    = Enum.GetValues(typeof(AdFormat)).Cast<AdFormat>().ToList();
            if (formats.Any(format => !entries.ContainsKey(format))) {
                priorities.Draw(mediations);
                return;
            }

#if ADS_SUPPORT_REMOTECONFIG
            var settings = Property.Tree.WeakTargets.OfType<AdsSettings>().FirstOrDefault();
#endif
            var looks = Property.Children["settings"];
            foreach (var format in formats) {
                using var sub = Section.Foldout(entries[format], ChoiceFieldUtils.NameOf(format));
                if (!sub.Open) continue;

                entries[format].Children["value"].Draw(mediations);
#if ADS_SUPPORT_REMOTECONFIG
                // One key for both platforms, so no split button.
                if (settings) AdButtonRow.Draw(() => AdRemoteKeyDrawer.Draw(EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), remoteKey), settings, format));
#endif
                if (!settingsFields.TryGetValue(format, out var field)) continue;

                // One value for both platforms; the empty square keeps each row's end in line with the Mediations row's.
                foreach (var row in looks.Children[field].Children) AdButtonRow.Draw(() => row.Draw());
            }
        }
    }
}