using System;
using System.Collections.Generic;
using System.Linq;
using RiseOn.Serializables;
using RiseOn.Utils.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace RiseOn.Ads.Editor {
    internal sealed class MediationListDrawer : OdinAttributeDrawer<MediationListAttribute, List<SerRef<IAdMediation>>> {
        private readonly SectionRows rows = new();

        // The key under which the list's package changes show in it.
        private const string Owner = "RiseOn.Ads.Mediations";

        // The pack's adapters, each listed whether its package is in the project or not, with its folder in the Ads repo, beside Core.
        private static readonly PackAdapters adapters = new(Owner, typeof(AdsSettings)
                                                          , new PackAdapter("AdMob", "com.riseon.ads.admob", "AdMob")
                                                          , new PackAdapter("LevelPlay", "com.riseon.ads.levelplay", "LevelPlay")
                                                          , new PackAdapter("MAX", "com.riseon.ads.max", "Max"));

        protected override void Initialize() {
            Section.Restore(Property);
        }

        /// <summary>Starts reading the adapters' versions, unless read; the Ads page calls it as it opens.</summary>
        internal static void LoadVersions() => adapters.Load();

        protected override void DrawPropertyLayout(GUIContent label) {
            var settings = Property.Tree.WeakTargets.OfType<AdsSettings>().FirstOrDefault();
            if (!settings) return;

            // Each mediation is long, so the list folds.
            using var section = Section.Foldout(Property, label.text, label.tooltip, buttons: 1, padded: false, barValue: true);
            using (new EditorGUI.DisabledScope(adapters.IsLoading)) {
                if (section.Button(0, SdfIconType.ArrowClockwise, "Read the adapters' versions again")) adapters.Reload();
            }

            var shown = Rows(settings);
            adapters.DrawUpdateAll(section.BarValue, shown.Where(row => row.Adapter != null && row.Index >= 0).Select(row => (row.Adapter.Value, row.Package)));
            if (!section.Open) return;

            PackageJobs.DrawRunning(Owner);
            rows.Draw(shown.Count, index => DrawRow(shown[index]), "None");
        }

        // A row: a mediation the settings hold, with its index there, or one of the pack's adapters not in the project; each of the pack's adapters
        // with what installs, updates or removes it.
        private readonly struct Row {
            public readonly int          Index;
            public readonly PackAdapter? Adapter;
            public readonly PackageRow   Package;
            public readonly string       Name;

            public Row(int index, PackAdapter? adapter, PackageRow package, string name) {
                Index   = index;
                Adapter = adapter;
                Package = package;
                Name    = name;
            }
        }

        // Every mediation the settings hold, which the page adopts for every class in the project, and every adapter of the pack's not in it, by name.
        private List<Row> Rows(AdsSettings settings) {
            var shown = new List<Row>();
            var held  = new HashSet<string>();
            for (var i = 0; i < settings.EditableMediations.Count; i++) {
                if (settings.EditableMediations[i].Value is not { } mediation) continue;

                var name    = AdMediationNames.Of(mediation.GetType());
                var package = PackageInfo.FindForAssembly(mediation.GetType().Assembly);
                var adapter = adapters.Of(package);
                var index   = i;
                if (adapter is { } found) held.Add(found.Package);
                shown.Add(new Row(i, adapter, adapter is { } own ? adapters.Installed(own, package, () => Uninstall(settings, index, package.name)) : null, name));
            }

            foreach (var adapter in adapters.All.Where(adapter => !held.Contains(adapter.Package))) shown.Add(new Row(-1, adapter, adapters.Missing(adapter), adapter.Name));
            return shown.OrderBy(row => row.Name).ToList();
        }

        // The name in the label column, folding the mediation's fields under it; one of the pack's adapters has its version and buttons in the value
        // column, as a network's row.
        private void DrawRow(Row row) {
            var line  = EditorGUILayout.GetControlRect();
            var value = SectionRows.MarkFirstRow(line, out var prefix);
            if (row.Package != null) PackageRow.Draw(value, row.Package);
            if (row.Index < 0) {
                GUI.Label(prefix, row.Name);
                return;
            }

            var property = Property.Children[row.Index].Children[0];
            var expanded = EditorGUI.Foldout(row.Package == null ? line : prefix, property.State.Expanded, row.Name, true);
            // The row is not the value's own drawer, so its Alt+click is passed down here.
            if (expanded != property.State.Expanded && Event.current.alt) Section.SetExpandedRecursively(property, expanded);
            property.State.Expanded = expanded;

            // Under the mediation's name, as Unity indents what a foldout opens, with the fade the page's sections use.
            if (Section.BeginFade(property, property.State.Expanded)) {
                var width = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = width - Section.Indent;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(Section.Indent);
                EditorGUILayout.BeginVertical();
                foreach (var child in property.Children) child.Draw();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                EditorGUIUtility.labelWidth = width;
            }

            Section.EndFade();
        }

        // Takes the mediation out of every format first, asking in the Editor's own dialog, then removes the package through the Package Manager,
        // as Unity's own Remove does; the settings drop the mediation as its class goes.
        private void Uninstall(AdsSettings settings, int index, string package) {
            var mediation = settings.EditableMediations[index].Value;
            var name      = AdMediationNames.Of(mediation.GetType());
            var uses      = settings.PriorityLists.Where(entry => entry.List.MediationIds.Contains(mediation.Id)).ToList();
            var formats   = string.Join("\n", uses.GroupBy(entry => entry.Platforms).Select(platform => $"{platform.Key}: {string.Join(", ", platform.Select(entry => ChoiceFieldUtils.NameOf(entry.Format)))}"));
            var message   = $"Removes {name}'s package from the project. {name}'s ad unit ids and format settings on this page go with it; "
                          + "the SDK's own settings files under Assets stay, and an SDK package goes too only when the manifest does not name it."
                          + (uses.Count == 0 ? "" : $"\n\nThese formats use {name} and drop it too:\n{formats}");
            if (!EditorUtility.DisplayDialog($"Uninstall {name}", message, "Uninstall", "Cancel")) return;

            Undo.RecordObject(settings, "Uninstall ad mediation");
            settings.EditableMediations.RemoveAt(index);
            foreach (var entry in uses) entry.List.EditableIds.Remove(mediation.Id);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            PackageJobs.Change(Owner, $"Uninstalling {name}", null, new[] { package }, worked => {
                // An undo would bring back a mediation whose class went with the package.
                if (worked) Undo.ClearUndo(settings);
            });
        }
    }
}