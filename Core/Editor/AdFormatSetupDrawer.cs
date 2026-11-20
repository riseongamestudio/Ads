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
        private const string Tip = "Each format: the mediations that serve it, tried left to right, with none the format off; the Remote Config key that can replace them at launch; "
                                 + "whether remove-ads takes it; how it shows and the manager's rules for it. The mediations can split into a list per platform, the Editor using the one its row picks.";
#else
        private const string Tip = "Each format: the mediations that serve it, tried left to right, with none the format off; whether remove-ads takes it; how it shows and the manager's rules for it. "
                                 + "The mediations can split into a list per platform, the Editor using the one its row picks.";
#endif

        private static readonly GUIContent mediations = new("Mediations", "Which mediations serve the format, tried left to right; with none the format is off.");
        private static readonly GUIContent remoteKey  = new("Mediations Remote Key", AdRemoteKeyDrawer.Tooltip);
        private static readonly GUIContent removeAds  = new("Included In Remove Ads", "The remove-ads purchase takes this format away: once the player bought it, the format no longer shows, and a rewarding one grants its reward with no ad.");
        private static readonly GUIContent endCard    = new("Use Native End Card", "After the format's ad, a native end card in the same chain when one is loaded and its own pacing allows, for ShowAsync and the Show methods of the game's manager.");

        // Each format's settings, by the field of AdFormatSettings that holds them; the formats with nothing to set are not here.
        private static readonly Dictionary<AdFormat, string> settingsFields = new() {
            { AdFormat.Banner, "banner" }
          , { AdFormat.MRec, "mRec" }
          , { AdFormat.Interstitial, "interstitial" }
          , { AdFormat.RewardedInterstitial, "rewardedInterstitial" }
          , { AdFormat.AppOpen, "appOpen" }
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

            var settings = Property.Tree.WeakTargets.OfType<AdsSettings>().FirstOrDefault();
            var looks    = Property.Children["settings"];
            // A map saved before the enum gained a format leaves that one without the row, not taken, until the map takes the key.
            var removes  = Entries(looks.Children["removeAds"]);
            var endCards = Entries(looks.Children["endCards"]);
            foreach (var format in formats) {
                using var sub = Section.Foldout(entries[format], ChoiceFieldUtils.NameOf(format));
                if (!sub.Open) continue;

                entries[format].Children["value"].Draw(mediations);
#if ADS_SUPPORT_REMOTECONFIG
                // One key for both platforms, so no split button.
                if (settings) AdButtonRow.Draw(() => AdRemoteKeyDrawer.Draw(EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), remoteKey), settings, format));
#endif
                if (removes.TryGetValue(format, out var remove)) AdButtonRow.Draw(() => remove.Draw(removeAds));
                if (AdFormatSettings.TakesEndCard(format) && endCards.TryGetValue(format, out var card)) AdButtonRow.Draw(() => card.Draw(endCard));
                if (!settingsFields.TryGetValue(format, out var field)) continue;

                // One value for both platforms; the empty square keeps each row's end in line with the Mediations row's. A pacing is its rows, After Rewarded on the interstitials only.
                var own = looks.Children[field];
                foreach (var child in own.ValueEntry.WeakSmartValue is AdPacingSettings ? new[] { own } : own.Children.ToArray()) {
                    var rows = child.ValueEntry.WeakSmartValue is AdPacingSettings
                        ? child.Children.Where(row => row.Name != "afterRewardedSeconds" || AdPacingSettings.TakesAfterRewarded(format)).ToArray()
                        : new[] { child };
                    foreach (var row in rows) AdButtonRow.Draw(() => row.Draw());
                }
            }

            static Dictionary<AdFormat, InspectorProperty> Entries(InspectorProperty map) =>
                map.Children["entries"].Children.Where(entry => entry.Children["key"].ValueEntry.WeakSmartValue is AdFormat)
                   .ToDictionary(entry => (AdFormat) entry.Children["key"].ValueEntry.WeakSmartValue, entry => entry.Children["value"]);
        }
    }
}