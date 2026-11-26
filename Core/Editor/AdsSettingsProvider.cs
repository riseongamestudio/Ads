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
                yield return SettingsIssue.Info("The project has no ads manager of its own, so the pack's DefaultAdsManager runs, with the Manager section's values. "
                                              + "Create Own, on the Manager bar, copies it into the game, with those values, for rules the game changes.");
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

        /// <summary>Every class of the game's the pack could run, DefaultAdsManager aside; the source generator lets only one compile in an assembly.</summary>
        internal static List<Type> Candidates() {
            return TypeCache.GetTypesDerivedFrom<IAdsManager>().Where(type => type.IsClass && !type.IsAbstract && type != typeof(DefaultAdsManager)).ToList();
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
        /// Makes the settings hold an instance of the game's one manager class, or DefaultAdsManager while there is none, keeping the fields when it already does; the game's class taking over from DefaultAdsManager takes the fields of the same names, as Create Own promises.<br/>
        /// And one of every mediation class in the project, so an adapter installed is on the page without a step of its own; a mediation no format lists serves nothing.<br/>
        /// Leaves them alone while scripts fail to compile, when a class could be missing only for now; a class gone is the page's to report.
        /// </summary>
        internal static void Adopt(AdsSettings settings) {
            if (EditorUtility.scriptCompilationFailed) return;

            var held    = settings.EditableMediations.Where(entry => entry.Value != null).Select(entry => entry.Value.GetType()).ToHashSet();
            var missing = TypeCache.GetTypesDerivedFrom<IAdMediation>().Where(type => !type.IsAbstract && !type.IsGenericType && !held.Contains(type) && type.GetConstructor(Type.EmptyTypes) != null && InBuilds(type) && !Leaving(type)).ToList();
            foreach (var type in missing) settings.EditableMediations.Add(new SerRef<IAdMediation>((IAdMediation) Activator.CreateInstance(type)));
            if (missing.Count > 0) EditorUtility.SetDirty(settings);

            var candidates = Candidates();
            var runs       = candidates.Count == 0 ? typeof(DefaultAdsManager) : candidates.Count == 1 && Problem(candidates[0]) == null ? candidates[0] : null;
            if (runs != null && settings.Manager?.GetType() != runs) {
                var manager = Activator.CreateInstance(runs);
                if (settings.Manager is DefaultAdsManager taken) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(taken), manager);

                var asset = new SerializedObject(settings);
                asset.FindProperty("manager").managedReferenceValue = manager;
                asset.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssetIfDirty(settings);
        }

        // An adapter being uninstalled keeps its class until the domain reloads; taking it back would save it into the settings with no class.
        private static bool Leaving(Type type) => UnityEditor.PackageManager.PackageInfo.FindForAssembly(type.Assembly) is { } package && PackageJobs.Removes(package.name);

        internal static AdsSettings Find() => SettingsAssetUtils.Find<AdsSettings>(AdsSettings.ResourceName);

        internal static AdsSettings LoadOrCreate() => SettingsAssetUtils.LoadOrCreate<AdsSettings>(AdsSettings.ResourceName);

        // DefaultAdsManager.cs as the game's own class: its name, in the project's root namespace or none, a header saying whose it is.
        // The game's scripts that called DefaultAdsManager call it instead; the settings' values follow in Adopt once it compiles.
        // Writes nothing when the pack's file is not as expected, or when the copy or a turned call would not compile: see Unreachable.
        internal static void CreateOwnManager() {
            var path = EditorUtility.SaveFilePanelInProject("Own ads manager", "AdsManager", "cs", "A copy of the pack's DefaultAdsManager, the class taking the file's name; the Manager section's values go with it, and the game's calls to DefaultAdsManager turn to it.");
            if (!string.IsNullOrEmpty(path)) CreateOwnManager(path);
        }

        internal static void CreateOwnManager(string path) {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]*$")) {
                Debug.LogError($"{nameof(AdsSettingsProvider)}: {name} cannot be a class name");
                return;
            }

            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DefaultAdsManager).Assembly);
            var source  = package == null ? null : Path.Combine(package.resolvedPath, "Runtime", nameof(DefaultAdsManager) + ".cs");
            if (source == null || !File.Exists(source)) {
                Debug.LogError($"{nameof(AdsSettingsProvider)}: the pack's {nameof(DefaultAdsManager)}.cs is not where the pack keeps it, so there is nothing to copy");
                return;
            }

            var space = EditorSettings.projectGenerationRootNamespace;
            var own   = OwnManagerSource(File.ReadAllText(source), name, space);
            if (own == null) {
                Debug.LogError($"{nameof(AdsSettingsProvider)}: the pack's {nameof(DefaultAdsManager)}.cs is not in the shape Create Own copies, so it wrote nothing");
                return;
            }

            // The copy has DefaultAdsManager's members, so a call turned to it compiles as it did.
            var turned = new List<(string file, string text, bool bom)>();
            foreach (var file in Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)) {
                var bytes = File.ReadAllBytes(file);
                var bom   = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
                var calls = CallsTurned(Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0)), name, space);
                if (calls != null) turned.Add((file.Replace('\\', '/'), calls, bom));
            }

            if (Unreachable(path, name, own, turned.Select(call => call.file)) is { } reason) {
                Debug.LogError($"Ads: Create Own wrote nothing: {reason}");
                return;
            }

            File.WriteAllText(path, own, new UTF8Encoding(true));
            foreach (var (file, text, bom) in turned) File.WriteAllText(file, text, new UTF8Encoding(bom));
            AssetDatabase.Refresh();
            if (turned.Count > 0) Debug.Log($"Ads: these scripts called {nameof(DefaultAdsManager)} and now call {name}:\n{string.Join("\n", turned.Select(call => call.file))}");
        }

        // Why the copy at path, or a script calling it, would not compile, or the copy would miss the build; null when nothing stands in the way.
        private static string Unreachable(string path, string name, string own, IEnumerable<string> callers) {
            var player = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).ToDictionary(assembly => assembly.name);
            var target = Path.GetFileNameWithoutExtension(CompilationPipeline.GetAssemblyNameFromScriptPath(path));
            if (target == null || !player.TryGetValue(target, out var home)) {
                return $"{path} would compile in {target ?? "no assembly"}, which the build leaves out, such as an Editor folder's; save it where the game's runtime scripts are.";
            }

            // A type of that name the copy's assembly or a caller's sees would make the copy a second declaration, or the calls ambiguous.
            var all      = CompilationPipeline.GetAssemblies().ToDictionary(assembly => assembly.name);
            var origins  = callers.Select(file => Path.GetFileNameWithoutExtension(CompilationPipeline.GetAssemblyNameFromScriptPath(file))).ToList();
            var visible  = origins.Append(target).Where(origin => origin != null && all.ContainsKey(origin)).SelectMany(origin => References(all[origin]).Append(origin)).ToHashSet();
            var taken    = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => visible.Contains(assembly.GetName().Name)).SelectMany(Types)
                                    .Where(type => type.Name == name && !type.IsNested && (type.IsPublic || all.ContainsKey(type.Assembly.GetName().Name)))
                                    .Select(type => type.FullName).Distinct().ToList();
            if (taken.Count > 0) return $"the project already has a type named {name} ({string.Join(", ", taken)}); save the file under another name.";

            // What the copy's usings need: the pack, and each assembly of the pack's references that holds one of those namespaces.
            var spaces  = Regex.Matches(own, @"^using ([\w.]+);", RegexOptions.Multiline).Select(match => match.Groups[1].Value).ToHashSet();
            var loaded  = AppDomain.CurrentDomain.GetAssemblies().GroupBy(assembly => assembly.GetName().Name).ToDictionary(group => group.Key, group => group.First());
            var pack    = typeof(DefaultAdsManager).Assembly;
            var needs   = pack.GetReferencedAssemblies().Select(reference => reference.Name)
                              .Where(needed => !needed.StartsWith("UnityEngine") && loaded.TryGetValue(needed, out var assembly) && Types(assembly).Any(type => type.IsPublic && spaces.Contains(type.Namespace)))
                              .Prepend(pack.GetName().Name);
            var missing = needs.Where(needed => !References(home).Contains(needed)).ToList();
            if (missing.Count > 0) return $"{path} would compile in {target}, which does not reference {string.Join(", ", missing)}; save it in an assembly that does, or add the references to its assembly definition first.";

            // A turned call's assembly must see the copy's. An assembly definition cannot reference Assembly-CSharp, so then only the copy can move.
            var blind = callers.Where((file, i) => origins[i] != target && (origins[i] == null || !all.ContainsKey(origins[i]) || !References(all[origins[i]]).Contains(target))).ToList();
            if (blind.Count > 0) {
                var fix = target.StartsWith("Assembly-CSharp") ? "save it in an assembly definition they reference" : $"save it in an assembly they reference, or add {target} to their assembly definitions first";
                return $"these scripts call {nameof(DefaultAdsManager)} from assemblies that do not reference {target}, where {name} would be: {string.Join(", ", blind)}; {fix}.";
            }

            return null;
        }

        private static HashSet<string> References(UnityEditor.Compilation.Assembly assembly) {
            return assembly.allReferences.Select(Path.GetFileNameWithoutExtension).ToHashSet();
        }

        private static IEnumerable<Type> Types(System.Reflection.Assembly assembly) {
            try {
                return assembly.GetTypes();
            } catch (System.Reflection.ReflectionTypeLoadException e) {
                return e.Types.Where(type => type != null);
            }
        }

        private static readonly Regex defaultName = new(@"(?<![\w.])(?:global::)?(?:RiseOn\.Ads\.)?DefaultAdsManager\b");

        // A using directive, alias or static: it names types from the global namespace, so it takes the class's full name.
        private static readonly Regex usingDirective = new(@"^\s*using\s+(static\s+)?(\w+\s*=\s*)?[\w.:<>, ]+;");

        /// <summary>A script's text with its every DefaultAdsManager turned to the game's class, with its namespace's using where the script lacks it; null when it names none.</summary>
        internal static string CallsTurned(string text, string name, string space) {
            if (!defaultName.IsMatch(text)) return null;

            var newline = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines   = text.Split(new[] { newline }, StringSplitOptions.None).ToList();
            var full    = string.IsNullOrEmpty(space) ? name : $"{space}.{name}";
            var bare    = false;
            for (var i = 0; i < lines.Count; i++) {
                if (!defaultName.IsMatch(lines[i])) continue;

                var directive = usingDirective.IsMatch(lines[i]);
                lines[i] = defaultName.Replace(lines[i], directive ? full : name);
                bare |= !directive;
            }

            text = string.Join(newline, lines);
            if (!bare || string.IsNullOrEmpty(space) || Regex.IsMatch(text, $@"^\s*(using|namespace)\s+{Regex.Escape(space)}\s*[;{{.]", RegexOptions.Multiline)) return text;

            // In the usings' order, System's first, as OwnManagerSource places its own.
            var line    = $"using {space};";
            var usings  = lines.FindLastIndex(other => Regex.IsMatch(other, @"^using [\w.]+;$"));
            var at      = lines.FindIndex(other => Regex.IsMatch(other, @"^using [\w.]+;$") && !other.StartsWith("using System") && string.CompareOrdinal(other, line) > 0);
            if (usings < 0) lines.InsertRange(0, new[] { line, "" });
            else lines.Insert(at < 0 || at > usings ? usings + 1 : at, line);
            return string.Join(newline, lines);
        }

        private const string PackNamespace = "namespace RiseOn.Ads {";
        private const string PackHeader    = "/// The pack runs it while the project has no ads manager of its own; Create Own, beside its script on that page, copies it into the game, values and all, to change.";

        /// <summary>The game's copy of DefaultAdsManager's source, or null when the source lacks what the copy rewrites.</summary>
        internal static string OwnManagerSource(string source, string name, string space) {
            var lines = source.Replace("\r\n", "\n").Split('\n').ToList();
            var open  = lines.FindIndex(line => line == PackNamespace);
            var close = lines.FindLastIndex(line => line == "}");
            var usings = lines.FindLastIndex(line => line.StartsWith("using "));
            if (open < 0 || close <= open || usings < 0 || usings > open || !source.Contains(PackHeader)) return null;

            var header = $"/// The game's own, copied from the pack's DefaultAdsManager: change what the game needs. The pack seals it and compiles {name}.Ins, the running instance, into it.";
            for (var i = open + 1; i < close; i++) lines[i] = lines[i].Replace(nameof(DefaultAdsManager), name).Replace(PackHeader, header);
            if (string.IsNullOrEmpty(space)) {
                lines.RemoveAt(close);
                for (var i = open + 1; i < close; i++) lines[i] = lines[i].StartsWith("    ") ? lines[i].Substring(4) : lines[i];
                lines.RemoveAt(open);
            } else {
                lines[open] = $"namespace {space} {{";
            }

            // In the usings' order, System's first: before the first other one that sorts after it.
            var at = lines.FindIndex(line => line.StartsWith("using ") && !line.StartsWith("using System") && string.CompareOrdinal(line, "using RiseOn.Ads;") > 0);
            lines.Insert(at < 0 || at > usings ? usings + 1 : at, "using RiseOn.Ads;");
            return string.Join("\r\n", lines);
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

            foreach (var issue in issues.Where(issue => !issue.IsInfo)) Debug.LogWarning("Ads: " + issue.Message);
        }
    }
}