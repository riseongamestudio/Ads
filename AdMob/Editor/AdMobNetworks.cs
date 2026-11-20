using System;
using System.Collections.Generic;
using System.Linq;
using RiseOn.Ads.Editor;
using RiseOn.Utils.Editor;
using UnityEditor;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace RiseOn.Ads.AdMob.Editor {
    /// <summary>
    /// AdMob's mediation adapters, which Google publishes on OpenUPM as one package each, com.google.ads.mobile.mediation.*: the list is the
    /// Package Manager's search of the project's registries, and Install, Update and Uninstall change the package as Unity's Package Manager window does.
    /// </summary>
    internal sealed class AdMobNetworks : AdNetworkCatalog {
        private const string Prefix = "com.google.ads.mobile.mediation.";

        // Each row's package at its newest version, for Update All's one request.
        private readonly Dictionary<PackageRow, string> newest = new();

        // Each row's package, for a removal of several rows in one request.
        private readonly Dictionary<PackageRow, string> packages = new();

        protected override string Key => "RiseOn.Ads.AdMob.Networks";

        protected override string Tooltip => "The networks AdMob mediates through Google's adapters, each a package on OpenUPM; the manifest needs OpenUPM with the scope com.google.";

        protected override string LoadingText => "Searching the registries for Google's adapters";

        protected override void Load(Action<List<PackageRow>, string> done) {
            newest.Clear();
            packages.Clear();
            var search = Client.SearchAll(false);
            EditorApplication.update += Wait;

            void Wait() {
                if (!search.IsCompleted) return;

                EditorApplication.update -= Wait;
                var installed = PackageInfo.GetAllRegisteredPackages().Where(package => package.name.StartsWith(Prefix)).ToDictionary(package => package.name);
                var found     = search.Status is StatusCode.Success ? search.Result.Where(package => package.name.StartsWith(Prefix)).ToList() : new List<PackageInfo>();
                var names     = found.Select(package => package.name).ToHashSet();
                var networks  = found.Select(package => Row(package, installed.GetValueOrDefault(package.name), package.versions.latestCompatible ?? package.version)).ToList();
                // Offline, or from a registry the search left out: still listed, with no newer version to offer.
                networks.AddRange(installed.Values.Where(package => !names.Contains(package.name)).Select(package => Row(package, package, null)));

                string problem = null;
                if (search.Status is not StatusCode.Success) problem = $"Could not search the registries: {search.Error.message}";
                else if (found.Count == 0) problem = "No registry has Google's adapters: add OpenUPM with the scope com.google to the manifest.";
                done(networks.OrderBy(network => network.Name, StringComparer.OrdinalIgnoreCase).ToList(), problem);
            }
        }

        private PackageRow Row(PackageInfo listed, PackageInfo installed, string latest) {
            var name     = Name(listed.displayName);
            // Only from the search: an installed package's own record knows no other versions.
            var versions = latest == null ? null : listed.versions.compatible.Reverse().ToList();
            var network  = new PackageRow(
                name, installed?.version, latest
              , latest == null ? null : () => Change($"{(installed == null ? "Installing" : "Updating")} {name}", new[] { $"{listed.name}@{latest}" }, null)
              , installed is { isDirectDependency: true } ? () => Change($"Uninstalling {name}", null, new[] { listed.name }) : null
              , listed.name
              , versions: versions
              , installVersion: version => Change(installed == null ? $"Installing {name} {version}" : $"Changing {name} to {version}", new[] { $"{listed.name}@{version}" }, null));
            if (latest != null) newest[network] = $"{listed.name}@{latest}";
            packages[network] = listed.name;
            return network;
        }

        // One request for all, as Unity's Package Manager window changes several packages.
        protected override void Install(IReadOnlyList<PackageRow> chosen, string what) {
            Change(what, chosen.Select(network => newest[network]).ToArray(), null);
        }

        protected override void Uninstall(IReadOnlyList<PackageRow> chosen, string what) {
            Change(what, null, chosen.Select(network => packages[network]).ToArray());
        }

        // "Google Mobile Ads AppLovin Mediation for Unity" is AppLovin.
        private static string Name(string displayName) {
            var name = displayName.Replace("Google Mobile Ads ", "").Replace(" Mediation for Unity", "").Replace(" Mediation", "").Trim();
            return name.Length == 0 ? displayName : char.ToUpperInvariant(name[0]) + name[1..];
        }
    }
}