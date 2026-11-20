using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AppLovinMax.Scripts.IntegrationManager.Editor;
using RiseOn.Ads.Editor;
using RiseOn.Utils.Editor;
using UnityEditor;

namespace RiseOn.Ads.Max.Editor {
    /// <summary>
    /// The networks MAX mediates, as AppLovin's Integration Manager lists and installs them, through its public API: the list from AppLovin's server, each adapter added or removed as the Integration Manager does it, in the manifest when the plugin is a package, else in Assets.
    /// </summary>
    internal sealed class MaxNetworks : AdNetworkCatalog {
        protected override string Key => "RiseOn.Ads.Max.Networks";

        protected override string Tooltip => "The networks MAX mediates, as AppLovin's Integration Manager lists them: its adapters, installed and updated as there.";

        protected override string LoadingText => "Reading AppLovin's networks";

        // The Integration Manager's record behind each row, which its add and remove take.
        private readonly Dictionary<PackageRow, Network> sources = new();

        protected override void Load(Action<List<PackageRow>, string> done) {
            sources.Clear();
            AppLovinEditorCoroutine.StartCoroutine(AppLovinIntegrationManager.Instance.LoadPluginData(data => {
                if (data?.MediatedNetworks == null) {
                    done(new List<PackageRow>(), "Could not read AppLovin's network list: check the connection.");
                    return;
                }

                done(data.MediatedNetworks.Select(Row).ToList(), null);
            }));
        }

        private PackageRow Row(Network network) {
            var installed = string.IsNullOrEmpty(network.CurrentVersions?.Unity) ? null : Text(network.CurrentVersions);
            var tooltip   = network.RequiresUpdate ? "AppLovin says this adapter does not work with the MAX plugin in the project: update it." : null;
            var row       = new PackageRow(
                network.DisplayName, installed, Text(network.LatestVersions)
              , () => Add(new[] { network }, $"{(installed == null ? "Installing" : "Updating")} {network.DisplayName}")
              , !AppLovinIntegrationManager.IsPluginInPackageManager || Listed(network).Length > 0 ? () => Remove(new[] { network }, $"Uninstalling {network.DisplayName}") : null
              , tooltip
              , network.CurrentToLatestVersionComparisonResult is MaxSdkUtils.VersionComparisonResult.Lesser);
            sources[row] = network;
            return row;
        }

        // An adapter has a version per platform; the Integration Manager shows them run together, as "android_25.5.0.0_ios_13.13.0.0".
        private static string Text(Versions versions) {
            if (versions == null) return null;

            var platforms = new List<string>();
            if (!string.IsNullOrEmpty(versions.Android)) platforms.Add($"Android {versions.Android}");
            if (!string.IsNullOrEmpty(versions.Ios)) platforms.Add($"iOS {versions.Ios}");
            return platforms.Count == 0 ? versions.Unity : string.Join(", ", platforms);
        }

        // One after another, as the Integration Manager's Upgrade All adds them.
        protected override void Install(IReadOnlyList<PackageRow> chosen, string what) {
            Add(chosen.Select(row => sources[row]).ToArray(), what);
        }

        protected override void Uninstall(IReadOnlyList<PackageRow> chosen, string what) {
            Remove(chosen.Select(row => sources[row]).ToArray(), what);
        }

        private static IPackageManagerClient Client => AppLovinIntegrationManager.IsPluginInPackageManager ? new AppLovinUpmPackageManager() : new AppLovinAssetsPackageManager();

        // As a package, the pack's own request for the same packages ends the job; in Assets, the import does.
        private void Add(Network[] networks, string what) {
            PackageJobs.Begin(Key, what);
            AppLovinEditorCoroutine.StartCoroutine(Run());

            IEnumerator Run() {
                // The coroutine's first step comes an update later, after the spinner is drawn: AppLovin's resolve then holds the main thread
                // until the packages change.
                if (AppLovinIntegrationManager.IsPluginInPackageManager) {
                    // That resolve reports nothing, not even a failure; adding the same packages again is a request that ends once they
                    // resolve, and says why when they do not. Run whole in one step, so the spinner the resolve's registration ends is
                    // started again before the next repaint.
                    foreach (var network in networks) {
                        var add = Client.AddNetwork(network, false);
                        while (add.MoveNext()) { }
                    }

                    PackageJobs.Change(Key, what, networks.SelectMany(network => network.Packages).Select(package => $"{package.Name}@{package.Version}").ToArray(), null);
                    yield break;
                }

                foreach (var network in networks) yield return Client.AddNetwork(network, false);
                AssetDatabase.Refresh();
                PackageJobs.End();
            }
        }

        // The network's packages the manifest names, which a removal takes out; one that came in as another package's dependency is not.
        private static string[] Listed(Network network) {
            return network.Packages.Select(package => package.Name).Where(name => UnityEditor.PackageManager.PackageInfo.FindForPackageName(name) is { isDirectDependency: true }).ToArray();
        }

        private void Remove(Network[] networks, string what) {
            if (AppLovinIntegrationManager.IsPluginInPackageManager) {
                // What AppLovin's removal takes out of the manifest, but as a request that reports when it is done, or why it failed.
                PackageJobs.Change(Key, what, null, networks.SelectMany(Listed).ToArray());
                return;
            }

            PackageJobs.Begin(Key, what);
            foreach (var network in networks) Client.RemoveNetwork(network);
            AssetDatabase.Refresh();
            PackageJobs.End();
        }
    }
}