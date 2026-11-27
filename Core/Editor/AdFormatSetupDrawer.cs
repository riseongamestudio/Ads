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
        private const string Tip = "Each format: the mediations that serve it, tried left to right, with none the format off; the Remote Config key that can replace them at launch; and how it shows. "
                                 + "The mediations can split into a list per platform, the Editor playing the active build target's.";

        private const string MediationsTip = "Which mediations serve the format, tried left to right; with none the format is off.";

        private static readonly GUIContent mediations = new("Mediations", MediationsTip);

        // Each format's settings, by the field of AdFormatSettings that holds them; the formats with nothing to set are not here.
        private static readonly Dictionary<AdFormat, string> settingsFields = new() {
            { AdFormat.Banner, "banner" }
          , { AdFormat.MRec, "mRec" }
          , { AdFormat.NativeInterstitial, "nativeInterstitial" }
          , { AdFormat.NativeAppOpen, "nativeAppOpen" }
          , { AdFormat.NativeEndCard, "nativeEndCard" }
          , { AdFormat.NativeCollapsible, "nativeCollapsible" }
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

            var settings = Property.Tree.WeakTargets.OfType<AdsSettings>().FirstOrDefault();
            var looks = Property.Children["settings"];
            // The bars' title column, the same on every bar so they line up: as wide as the longest format's name, its arrow included.
            var page   = EditorGUIUtility.labelWidth;
            var titles = Section.Inset + formats.Max(format => Mathf.Ceil(EditorStyles.foldout.CalcSize(new GUIContent(ChoiceFieldUtils.NameOf(format))).x));
            foreach (var format in formats) {
                // The mediations on the format's bar; a format with nothing more to set is its bar alone.
                var entry = entries[format];
                var look  = settingsFields.TryGetValue(format, out var field) ? looks.Children[field] : null;
                var name  = ChoiceFieldUtils.NameOf(format);
                EditorGUIUtility.labelWidth = titles;
                using var sub = look != null
                    ? Section.Foldout(entry, name, MediationsTip, barValue: true)
                    : new Section(name, MediationsTip, barValue: true, barOnly: true);
                // The mediations' Remote Config key at the end of the line, locked without that pack.
                AdPlatformValueRows.DrawOnBar(entry.Children["value"], (rect, row) => AdPriorityListDrawer.Draw(rect, settings, () => (AdPriorityList) row.ValueEntry.WeakSmartValue, format),
                    settings ? rect => AdRemoteKeyDrawer.Draw(rect, settings, format) : null);
                if (look == null || !sub.Open) continue;

                // The rows under the bar keep the page's label column, narrowed by the section's inset as the section narrows its own.
                EditorGUIUtility.labelWidth = page - Section.Inset;
                // One value for both platforms; the empty square keeps each row's end in line with the bar's.
                foreach (var row in look.Children) AdButtonRow.Draw(() => row.Draw());
            }

            EditorGUIUtility.labelWidth = page;
        }
    }
}