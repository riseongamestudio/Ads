using System;
using System.Collections.Generic;
using System.Linq;
using RiseOn.Utils.Editor;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// A mediation's Networks section, which its Editor assembly draws under the mediation's settings: a row for each network the SDK lists, with
    /// the adapter's version in the project, the newest one, and Install, Update and Uninstall, as the SDK's own manager has them.<br/>
    /// The list loads the first time the section opens, as it may ask a server; the bar's button loads it again.
    /// </summary>
    public abstract class AdNetworkCatalog {
        // Every catalog of an installed adapter, so the Ads page reads them all as it opens, not each as its section opens.
        private static readonly List<AdNetworkCatalog> all = new();

        private readonly SectionRows rows = new();
        private List<PackageRow> networks;
        private string problem;
        private bool isLoading;

        // The selected rows by name, which outlives a reload, and the row a Shift press selects from.
        private readonly HashSet<string> selected = new();
        private int anchor = -1;

        protected AdNetworkCatalog() => all.Add(this);

        /// <summary>Starts reading every catalog not read yet; the Ads page calls it as it opens.</summary>
        internal static void LoadAll() {
            foreach (var catalog in all) {
                if (catalog.networks == null && !catalog.isLoading && catalog.problem == null) catalog.StartLoad();
            }
        }

        /// <summary>Unique among mediations: the section's fold is kept under it for the session, and a package change it starts shows in it.</summary>
        protected abstract string Key { get; }

        protected abstract string Tooltip { get; }

        /// <summary>Shown under a spinner while the list loads, such as "Reading AppLovin's networks".</summary>
        protected abstract string LoadingText { get; }

        /// <summary>
        /// Reads the list, then calls <paramref name="done"/> once, on the main thread, with the networks, and with a problem to show when the list
        /// lacks some of what the SDK offers, or null.
        /// </summary>
        protected abstract void Load(Action<List<PackageRow>, string> done);

        /// <summary>Installs the newest version of every network in <paramref name="chosen"/>, over any version in the project, as one change shown as <paramref name="what"/>.</summary>
        protected abstract void Install(IReadOnlyList<PackageRow> chosen, string what);

        /// <summary>Removes every network in <paramref name="chosen"/>, each one with <see cref="PackageRow.Uninstall"/>, as one change shown as <paramref name="what"/>.</summary>
        protected abstract void Uninstall(IReadOnlyList<PackageRow> chosen, string what);

        public void Draw() {
            // The title keeps to the label column, so the bar's actions have its value column.
            using var section = Section.Foldout(Key, "Networks", Tooltip, open: false, buttons: 1, padded: false, barValue: true);
            using (new EditorGUI.DisabledScope(isLoading)) {
                if (section.Button(0, SdfIconType.ArrowClockwise, "Read the list again")) StartLoad();
            }

            var chosen = networks?.FindAll(network => selected.Contains(network.Name)) ?? new List<PackageRow>();
            // One row selected has its own buttons; the bar acts for several.
            if (section.Open && chosen.Count > 1) DrawBulk(section.BarValue, chosen);
            else DrawUpdateAll(section.BarValue);

            // Read with the bar in sight, the section open or not: a compile drops the list while the page stays open.
            if (networks == null && !isLoading && problem == null) StartLoad();
            if (!section.Open) return;

            PackageJobs.DrawRunning(Key);
            if (isLoading) DrawLine(SpinnerUtils.Content(LoadingText), EditorStyles.label);
            else if (problem != null) DrawLine(new GUIContent(problem), EditorStyles.wordWrappedMiniLabel);

            var shown = networks ?? new List<PackageRow>();
            if (shown.Count > 0 || !isLoading) rows.Draw(shown.Count, index => DrawRow(shown[index]), "None", isSelected: index => selected.Contains(shown[index].Name), select: (index, modifiers) => Select(shown, index, modifiers));
        }

        // As Windows Explorer and the Package Manager window's list: a press selects the row alone, or clears a row selected alone; with Ctrl or
        // Cmd it adds or takes out the row; with Shift it selects every row from the last one pressed, and with Ctrl or Cmd too it adds them to
        // the selection.
        private void Select(List<PackageRow> shown, int index, EventModifiers modifiers) {
            var name    = shown[index].Name;
            var isAdded = (modifiers & (EventModifiers.Control | EventModifiers.Command)) != 0;
            if ((modifiers & EventModifiers.Shift) != 0 && anchor >= 0 && anchor < shown.Count) {
                if (!isAdded) selected.Clear();
                for (var i = Math.Min(anchor, index); i <= Math.Max(anchor, index); i++) selected.Add(shown[i].Name);
                return;
            }

            if (isAdded) {
                if (!selected.Remove(name)) selected.Add(name);
                anchor = index;
                return;
            }

            var isAlone = selected.Count == 1 && selected.Contains(name);
            selected.Clear();
            if (!isAlone) selected.Add(name);
            anchor = index;
        }

        // While rows are selected, the bar's actions are for them: Install for those missing, Update for those behind, Uninstall for those the
        // SDK can remove, each with how many it takes, from the bar's end.
        private void DrawBulk(Rect value, List<PackageRow> chosen) {
            var missing   = chosen.FindAll(network => network.Install != null && network.Installed == null);
            var behind    = chosen.FindAll(network => network.Install != null && network.IsOutdated);
            var removable = chosen.FindAll(network => network.Uninstall != null && network.Installed != null);
            var actions = new List<(string Label, List<PackageRow> Networks, Action<List<PackageRow>> Act)> {
                ("Install", missing, list => Install(list, $"Installing {Names(list)}")),
                ("Update", behind, list => Install(list, $"Updating {Names(list)}")),
                ("Uninstall", removable, list => Uninstall(list, $"Uninstalling {Names(list)}")),
            };

            var right = value.xMax;
            for (var i = actions.Count - 1; i >= 0; i--) {
                var (text, list, act) = actions[i];
                if (list.Count == 0) continue;

                var label = new GUIContent($"{text} {list.Count}", $"{text} {Names(list)}");
                var width = EditorStyles.miniButton.CalcSize(label).x;
                var button = new Rect(right - width, value.y, width, value.height);
                right = button.x - Section.LabelGap;
                using (new EditorGUI.DisabledScope(isLoading || PackageJobs.IsRunning)) {
                    if (!GUI.Button(button, label, EditorStyles.miniButton)) continue;
                }

                act(list);
                selected.Clear();
                GUIUtility.ExitGUI();
            }
        }

        private static string Names(List<PackageRow> list) => string.Join(", ", list.ConvertAll(network => network.Name));

        // At the bar's end, as the Integration Manager's Upgrade All is over its list: grayed out while every adapter is on its newest version.
        private void DrawUpdateAll(Rect value) {
            var outdated = networks?.FindAll(network => network.Install != null && network.IsOutdated) ?? new List<PackageRow>();
            var label    = new GUIContent("Update All", outdated.Count == 0 ? "Every adapter in the project is on its newest version" : $"Update {string.Join(", ", outdated.ConvertAll(network => network.Name))}");
            var width    = EditorStyles.miniButton.CalcSize(label).x;
            using (new EditorGUI.DisabledScope(outdated.Count == 0 || isLoading || PackageJobs.IsRunning)) {
                if (!GUI.Button(new Rect(value.xMax - width, value.y, width, value.height), label, EditorStyles.miniButton)) return;
            }

            Install(outdated, $"Updating {Names(outdated)}");
            GUIUtility.ExitGUI();
        }

        /// <summary>Starts a package change shown in this section.</summary>
        protected void Change(string what, string[] add, string[] remove) => PackageJobs.Change(Key, what, add, remove);

        private void StartLoad() {
            isLoading = true;
            problem   = null;
            Load((loaded, why) => {
                networks  = loaded;
                problem   = why;
                isLoading = false;
                InternalEditorUtility.RepaintAllViews();
            });
        }

        // A line of the section's own, inset as a row's content.
        private static void DrawLine(GUIContent content, GUIStyle style) {
            GUILayout.Space(Section.RowPadding);
            var line = EditorGUILayout.GetControlRect(false, style.CalcHeight(content, EditorGUIUtility.currentViewWidth - 4 * Section.Inset));
            line.xMin += Section.Inset;
            line.xMax -= Section.Inset;
            EditorGUIUtility.SetIconSize(new Vector2(Section.IconSize, Section.IconSize));
            GUI.Label(line, content, style);
            EditorGUIUtility.SetIconSize(Vector2.zero);
            GUILayout.Space(Section.RowPadding);
        }

        // The name in the label column, then the version and the buttons.
        private static void DrawRow(PackageRow network) {
            var value = SectionRows.MarkFirstRow(EditorGUILayout.GetControlRect(), out var prefix);
            GUI.Label(prefix, new GUIContent(network.Name, network.Tooltip));
            PackageRow.Draw(value, network);
        }
    }
}