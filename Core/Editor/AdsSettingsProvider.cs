using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RiseOn.Serializables;
using RiseOn.Utils.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace RiseOn.Ads.Editor {
    /// <summary>
    /// Project Settings > RiseOn > Ads: what blocks the build, then the settings asset as Odin draws it, the manager's section first.<br/>
    /// The settings asset's Inspector only opens this page.
    /// </summary>
    internal static class AdsSettingsProvider {
        internal const string PagePath = "Project/RiseOn/Ads";

        private static readonly SettingsTree tree = new();

        [MenuItem("RiseOn/Ads")]
        internal static void Open() {
            SettingsService.OpenProjectSettings(PagePath);
        }

        [SettingsProvider]
        private static SettingsProvider Create() {
            return new(PagePath, SettingsScope.Project) {
                guiHandler = _ => {
                    using var page = SettingsPage.Begin();
                    Draw(LoadOrCreate());
                }
              , activateHandler   = (_, _) => {
                    AdNetworkCatalog.LoadAll();
                    MediationListDrawer.LoadVersions();
                }
              , deactivateHandler = tree.Dispose
              , keywords = new[] { "ads", "admob", "max", "levelplay", "banner", "interstitial", "rewarded", "app open" }
            };
        }

        /// <summary>The whole page for one settings asset.</summary>
        internal static void Draw(AdsSettings settings) {
            Adopt(settings);
            SettingsIssue.Draw(Issues(settings));

            tree.Draw(settings);
        }

        /// <summary>Everything that would leave the game without ads, for the page and the build.</summary>
        internal static IEnumerable<SettingsIssue> Issues(AdsSettings settings) {
            if (SettingsAssetUtils.OutsideResources<AdsSettings>(AdsSettings.ResourceName) is { } path) {
                yield return new SettingsIssue(BuildTarget.NoTarget, true, $"{path} is outside a Resources folder, so the game cannot load it and shows no ad.", "Move it", () => SettingsAssetUtils.MoveToResources<AdsSettings>(AdsSettings.ResourceName));
            }

            var candidates = Candidates();
            if (candidates.Count > 1) {
                yield return new SettingsIssue(BuildTarget.NoTarget, true, $"{string.Join(", ", candidates.Select(type => type.FullName))} all implement IAdsManager, so none runs; keep one.");
            } else if (candidates.Count == 0) {
                yield return new SettingsIssue(BuildTarget.NoTarget, false, "No partial class implements IAdsManager, so the game has no place for its ad rules on this page; the pack's AdsManager sample holds the rules most games run.", "New script", CreateScript);
            } else if (Problem(candidates[0]) is { } problem) {
                yield return new SettingsIssue(BuildTarget.NoTarget, true, problem + ".");
            } else if (settings.Manager?.GetType() != candidates[0]) {
                // Adopt waits while scripts fail to compile.
                yield return new SettingsIssue(BuildTarget.NoTarget, true, $"The settings hold no {candidates[0].Name} yet, so its fields cannot be edited.", "Take it", () => Adopt(settings));
            }

            // Both would start the same SDK, and every list naming the id would take both.
            var twice = settings.Mediations.GroupBy(mediation => mediation.Id).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
            if (twice.Count > 0) {
                yield return new SettingsIssue(BuildTarget.NoTarget, true, $"The mediations list {string.Join(", ", twice)} more than once; each SDK runs once.", "Remove extras", () => {
                    Undo.RecordObject(settings, "Remove extra ad mediations");
                    settings.RemoveExtraMediations();
                    EditorUtility.SetDirty(settings);
                });
            }

#if ADS_SUPPORT_REMOTECONFIG
            // The game would throw reading them at launch, and start no mediation.
            var lost = new List<AdFormat>();
            foreach (var (format, key) in settings.RemoteKeys) {
                if (key.Id != 0 && !AdRemoteKeys.Is(key.Id, typeof(string))) lost.Add(format);
            }

            if (lost.Count > 0) yield return new SettingsIssue(BuildTarget.NoTarget, true, $"Formats: the Remote Config keys of {string.Join(", ", lost)} are no longer string keys of Remote Config, which removed them or changed their type; pick others.");

            var lostSettings = RemoteKeyUses(settings).Where(use => use.KeyId != 0 && !AdRemoteKeys.Is(use.KeyId, use.Type)).Select(use => use.Setting).ToList();
            if (lostSettings.Count > 0) {
                yield return new SettingsIssue(BuildTarget.NoTarget, true, $"The Remote Config keys of {string.Join(", ", lostSettings)} are no longer keys of the setting's type in Remote Config, which removed them or changed their type; pick others.");
            }
#endif

            // Not blocking: a test build serves test ads on purpose.
            var tests = TestUnits(settings);
            if (tests.Count > 0) yield return new SettingsIssue(BuildTarget.NoTarget, false, $"Test is on for {string.Join(", ", tests)}: a build serves the network's test ads there and earns nothing; turn it off before a store build.");

            if (SerializationUtility.HasManagedReferencesWithMissingTypes(settings)) {
                yield return new SettingsIssue(BuildTarget.NoTarget, true, "A class the settings hold (a mediation, or a manager since replaced) was renamed or deleted.", "Clear it", () => SerializationUtility.ClearAllManagedReferencesWithMissingTypes(settings));
            }
        }

        // Every AdRemoteSetting the asset holds, the manager's fields included, with its key and the type its value needs.
        private static IEnumerable<(string Setting, int KeyId, Type Type)> RemoteKeyUses(AdsSettings settings) {
            var property = new SerializedObject(settings).GetIterator();
            while (property.Next(true)) {
                if (!property.type.StartsWith("AdRemoteSetting") || property.FindPropertyRelative("key.id") is not { } key || property.FindPropertyRelative("value") is not { } value) continue;

                var type = value.propertyType switch {
                    SerializedPropertyType.Float   => typeof(float)
                  , SerializedPropertyType.Boolean => typeof(bool)
                  , SerializedPropertyType.Integer => typeof(int)
                  , SerializedPropertyType.String  => typeof(string)
                  , _                              => null
                };
                if (type != null) yield return (Nice(property.propertyPath), key.intValue, type);
            }

            // "manager.interstitialPacing.firstSeconds" as "Manager Interstitial Pacing First Seconds".
            static string Nice(string path) => string.Join(" ", path.Replace(".Array.data[", "[").Split('.').Select(ObjectNames.NicifyVariableName));
        }

        // Each ad unit id whose Test is on, as "admob Banner (Android)".
        private static List<string> TestUnits(AdsSettings settings) {
            var found      = new List<string>();
            var mediations = settings.Mediations.ToList();
            var property   = new SerializedObject(settings).GetIterator();
            while (property.Next(true)) {
                if (property.type != nameof(AdUnitId)) continue;

                foreach (var (flag, platform) in new[] { ("androidTest", "Android"), ("iosTest", "iOS") }) {
                    if (property.FindPropertyRelative(flag) is { boolValue: true }) found.Add($"{Name(property.propertyPath)} ({platform})");
                }
            }

            return found;

            // "mediations.Array.data[0].value.nativeInterstitial.UnitId" as "admob Native Interstitial Unit Id"; value is the SerRef's own field.
            string Name(string path) {
                var match = Regex.Match(path, @"^mediations\.Array\.data\[(\d+)\]\.(?:value\.)?(.+)$");
                if (!match.Success) return ObjectNames.NicifyVariableName(path);

                var index = int.Parse(match.Groups[1].Value);
                var owner = index < mediations.Count ? mediations[index].Id : "?";
                return owner + " " + string.Join(" ", match.Groups[2].Value.Split('.').Select(ObjectNames.NicifyVariableName));
            }
        }

        /// <summary>Every class the pack could run; the source generator lets only one compile in an assembly.</summary>
        internal static List<Type> Candidates() {
            return TypeCache.GetTypesDerivedFrom<IAdsManager>().Where(type => type.IsClass && !type.IsAbstract).ToList();
        }

        private static string Problem(Type type) {
            if (type.GetConstructor(Type.EmptyTypes) == null) return $"{type.Name} needs a constructor without parameters";
            if (!InBuilds(type)) return $"{type.Name} is in {type.Assembly.GetName().Name}, an Editor-only assembly: Play mode runs it, builds do not have it; move it to a runtime assembly";
            return null;
        }

        // TypeCache sees Editor assemblies too, and Play mode loads them, so only a build would miss the class.
        private static bool InBuilds(Type type) {
            playerAssemblies ??= new HashSet<string>(CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).Select(assembly => assembly.name));
            return playerAssemblies.Contains(type.Assembly.GetName().Name);
        }

        // GetAssemblies takes some 150 ms and the page asks on every GUI event; a recompile reloads the domain, which
        // clears this with the assemblies it lists.
        private static HashSet<string> playerAssemblies;

        /// <summary>
        /// Makes the settings hold an instance of the game's one manager class, keeping the fields when it already does, and one of every mediation class in the project, so an adapter installed is on the page without a step of its own; a mediation no format lists serves nothing.<br/>
        /// Leaves them alone while scripts fail to compile, when a class could be missing only for now; a class gone is the page's to report.
        /// </summary>
        internal static void Adopt(AdsSettings settings) {
            if (EditorUtility.scriptCompilationFailed) return;

            var held    = settings.EditableMediations.Where(entry => entry.Value != null).Select(entry => entry.Value.GetType()).ToHashSet();
            var missing = TypeCache.GetTypesDerivedFrom<IAdMediation>().Where(type => !type.IsAbstract && !type.IsGenericType && !held.Contains(type) && type.GetConstructor(Type.EmptyTypes) != null && InBuilds(type) && !Leaving(type)).ToList();
            foreach (var type in missing) settings.EditableMediations.Add(new SerRef<IAdMediation>((IAdMediation) Activator.CreateInstance(type)));
            if (missing.Count > 0) EditorUtility.SetDirty(settings);

            var candidates = Candidates();
            if (candidates.Count == 1 && Problem(candidates[0]) == null && settings.Manager?.GetType() != candidates[0]) {
                var asset = new SerializedObject(settings);
                asset.FindProperty("manager").managedReferenceValue = Activator.CreateInstance(candidates[0]);
                asset.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssetIfDirty(settings);
        }

        // An adapter being uninstalled keeps its class until the domain reloads; taking it back would save it into the settings with no class.
        private static bool Leaving(Type type) => UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly) is { } package && PackageJobs.Removes(package.name);

        internal static AdsSettings Find() => SettingsAssetUtils.Find<AdsSettings>(AdsSettings.ResourceName);

        internal static AdsSettings LoadOrCreate() => SettingsAssetUtils.LoadOrCreate<AdsSettings>(AdsSettings.ResourceName);

        private static void CreateScript() {
            var path = EditorUtility.SaveFilePanelInProject("New ads manager", "AdsManager", "cs", "The class takes the file's name.");
            if (string.IsNullOrEmpty(path)) return;

            var name = Path.GetFileNameWithoutExtension(path);
            if (!Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$")) {
                Debug.LogError($"{nameof(AdsSettingsProvider)}: {name} cannot be a class name");
                return;
            }

            var space  = EditorSettings.projectGenerationRootNamespace;
            var indent = string.IsNullOrEmpty(space) ? "" : "    ";
            var lines  = new List<string> { "using RiseOn.Ads;", "" };
            if (indent.Length > 0) lines.Add($"namespace {space} {{");
            lines.AddRange(new[] {
                $"{indent}// The project's one ads manager: the game's ad rules, calling AdsSdk under them; its fields show on Project Settings > RiseOn > Ads."
              , $"{indent}// Partial: the Ads pack seals it and compiles {name}.Ins, the running instance, into it. The pack's AdsManager sample holds the rules most games run."
              , $"{indent}public partial class {name} : IAdsManager {{"
              , $"{indent}    // The Editor's pause under a full-screen ad, where no SDK pauses Unity: the time scale and the audio by default."
              , $"{indent}    void IAdsManager.PauseInEditor(bool paused) => EditorGamePause.Apply(paused);"
              , $"{indent}}}"
            });
            if (indent.Length > 0) lines.Add("}");

            File.WriteAllText(path, string.Join("\r\n", lines), new UTF8Encoding(true));
            AssetDatabase.ImportAsset(path);
        }

        // After every script reload: a manager class was written, renamed or replaced.
        [InitializeOnLoadMethod]
        private static void AdoptAfterReload() {
            EditorApplication.delayCall += () => {
                var settings = Find();
                if (settings) Adopt(settings);
            };
        }
    }

    [CustomEditor(typeof(AdsSettings))]
    internal sealed class AdsSettingsInspector : SettingsAssetInspector {
        protected override string PagePath => AdsSettingsProvider.PagePath;
    }

    /// <summary>Stops a build that would show no ad, and warns of what the page warns of, such as test units left on.</summary>
    internal sealed class AdsBuildCheck : IPreprocessBuildWithReport {
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report) {
            var settings = AdsSettingsProvider.Find();
            if (!settings) return;

            AdsSettingsProvider.Adopt(settings);
            var issues   = AdsSettingsProvider.Issues(settings).ToList();
            var blocking = issues.Where(issue => issue.Blocking).Select(issue => issue.Message).ToList();
            if (blocking.Count > 0) throw new BuildFailedException("Ads: fix these before building, Project Settings > RiseOn > Ads has a button for some:\n- " + string.Join("\n- ", blocking));

            foreach (var issue in issues) Debug.LogWarning("Ads: " + issue.Message);
        }
    }
}