using System.Collections.Generic;
using RiseOn.Serializables;
using RiseOn.Utils;
using Sirenix.OdinInspector;
using UnityEngine;

namespace RiseOn.Ads {
    /// <summary>
    /// The project's ads setup, edited in Project Settings > RiseOn > Ads and kept in a Resources asset: the game's manager, the mediations, and for each format
    /// its mediations, the Remote Config key that may replace them at launch, and how it shows.
    /// </summary>
    internal sealed class AdsSettings : ScriptableObject {
        public const string ResourceName = "RiseOnAdsSettings";

        // The sections that stay open come before those that fold.
        [SerializeField, SectionGroup("Display"), LabelText("Cover")]
        [Tooltip("What shows under a full-screen ad or a chained collapsible where the ad does not reach, such as the margins a notch leaves; the game never shows through.")]
        private Color coverColor = Color.black;

        [SerializeField, SectionGroup("Display"), LabelText("Editor Sorting Order")]
        [Tooltip("The lowest sorting order of the Editor's ad layers: banners and MRecs, then native in-feed cells and MRecs, the collapsible's cover, the collapsible, the full-screen cover and full-screen ads, each one higher. Keep the game's canvases below it.")]
        private int editorSortingOrder = 1000;

        [SerializeField, SectionGroup("On Return To Game"), HideLabel, InlineProperty]
        private AdReturnSettings returnToGame = new();

        // The Editor keeps it an instance of the game's one manager class, so the page offers no type to pick; a game
        // without one has none.
        [SerializeReference, SectionReference, ShowIf("@manager != null"), LabelText("Manager")]
        [Tooltip("The project's one partial AdsManagerBase class, taken by the pack, with its fields.")]
        private AdsManagerBase manager;

        [SerializeField, MediationList, LabelText("Mediations")]
        private List<SerRef<IAdMediation>> mediations = new();

        // The Formats section.
        [SerializeField, HideLabel]
        private AdFormatSetup formats = new();

        /// <summary>The game's manager with the fields the page edited; the pack runs it.</summary>
        public AdsManagerBase Manager => manager;

        /// <summary>How each format shows.</summary>
        public AdFormatSettings Formats => formats.Settings;

        /// <summary>The ad shown when the player comes back to the game.</summary>
        public AdReturnSettings ReturnToGame => returnToGame;

        // Each rule's Remote Config key with the type its value needs, for the page's check.
        internal IEnumerable<(string Setting, int KeyId, System.Type Type)> RemoteKeyUses {
            get {
                foreach (var use in Formats.RemoteKeyUses) yield return use;
                foreach (var use in returnToGame.RemoteKeyUses) yield return use;
            }
        }

        public IEnumerable<IAdMediation> Mediations {
            get {
                foreach (var entry in mediations) {
                    if (entry.Value != null) yield return entry.Value;
                }
            }
        }

        // The Editor's drawer edits the list in place.
        internal List<SerRef<IAdMediation>> EditableMediations => mediations;

        // Every format's lists, by the platforms each serves, for the Editor.
        internal IEnumerable<(string Platforms, AdFormat Format, AdPriorityList List)> PriorityLists {
            get {
                foreach (var (format, value) in formats.Priorities) {
                    if (value == null) continue;

                    foreach (var (platforms, list) in value.All) {
                        if (list != null) yield return (platforms, format, list);
                    }
                }
            }
        }

        // Keeps the first mediation of each id, for the Editor's fix.
        internal void RemoveExtraMediations() {
            var seen = new HashSet<string>();
            mediations.RemoveAll(entry => entry.Value != null && !seen.Add(entry.Value.Id));
        }

        /// <summary>The running platform's mediations per format, best first; in the Editor, each split list's picked platform's.</summary>
        public IEnumerable<(AdFormat Format, AdPriorityList List)> Priorities {
            get {
                foreach (var (format, value) in formats.Priorities) yield return (format, value?.Current);
            }
        }

        /// <summary>Each format's Remote Config key, the same on every platform; one other than none replaces the format's list at launch while the Remote Config pack is installed.</summary>
        public EnumMap<AdFormat, AdRemoteKey> RemoteKeys => formats.RemoteKeys;

        /// <summary>Whether any format has a Remote Config key.</summary>
        public bool HasRemoteKeys {
            get {
                foreach (var (_, key) in formats.RemoteKeys) {
                    if (key.Id != 0) return true;
                }

                return false;
            }
        }

        public Color CoverColor => coverColor;
        public int EditorSortingOrder => editorSortingOrder;

        /// <summary>The project's asset, or an empty setup when the project never saved one.</summary>
        public static AdsSettings Load() {
            var asset = Resources.Load<AdsSettings>(ResourceName);
            return asset ? asset : CreateInstance<AdsSettings>();
        }
    }
}