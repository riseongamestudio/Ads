using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RiseOn.Ads.Generator {
    /// <summary>
    /// Compiles into the game's assembly, for the one class deriving from AdsManagerBase, an <c>Ins</c> and the registration the pack starts it from; a second such class is an error.<br/>
    /// Nothing is written to disk, so nobody can edit it.
    /// </summary>
    [Generator]
    public sealed class RiseOnAdsGenerator : ISourceGenerator {
        private const string PackAssembly = "RiseOn.Ads";
        private const string ManagerType   = "RiseOn.Ads.AdsManagerBase";

        private static readonly DiagnosticDescriptor notPartial = new(
            "RISEONADS001", "Ads manager not partial", "{0} derives from AdsManagerBase, so it must be a partial class, neither generic nor nested, for the Ads pack to find it and give it Ins",
            "RiseOn.Ads", DiagnosticSeverity.Error, true);

        private static readonly DiagnosticDescriptor notAlone = new(
            "RISEONADS003", "Several ads managers", "{0} derives from AdsManagerBase, and so does {1}; the Ads pack runs one manager, so only one class may",
            "RiseOn.Ads", DiagnosticSeverity.Error, true);

        public void Initialize(GeneratorInitializationContext context) {
            context.RegisterForSyntaxNotifications(() => new Receiver());
        }

        public void Execute(GeneratorExecutionContext context) {
            if (context.Compilation.AssemblyName is PackAssembly) return;

            var manager = context.Compilation.GetTypeByMetadataName(ManagerType);
            if (manager == null || context.SyntaxReceiver is not Receiver receiver) return;

            // Every class of this assembly that the pack could run, with the declaration that names it first.
            var found = new Dictionary<INamedTypeSymbol, ClassDeclarationSyntax>(SymbolEqualityComparer.Default);
            var mine  = new List<INamedTypeSymbol>();
            foreach (var declaration in receiver.Classes) {
                var model = context.Compilation.GetSemanticModel(declaration.SyntaxTree);
                if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol type || type.IsAbstract || !Derives(type, manager)) continue;
                if (found.ContainsKey(type)) continue;

                found.Add(type, declaration);
                mine.Add(type);
            }

            // Assemblies this one references can hold one too; sibling assemblies the Editor checks.
            var all = mine.Concat(Referenced(context.Compilation, manager)).ToList();
            foreach (var type in mine) {
                var declaration = found[type];
                var other       = all.FirstOrDefault(each => !SymbolEqualityComparer.Default.Equals(each, type));
                if (other != null) {
                    context.ReportDiagnostic(Diagnostic.Create(notAlone, declaration.Identifier.GetLocation(), type.ToDisplayString(), other.ToDisplayString()));
                    continue;
                }

                var partial = type.DeclaringSyntaxReferences.Select(reference => reference.GetSyntax()).OfType<ClassDeclarationSyntax>()
                    .All(each => each.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));
                if (!partial || type.IsGenericType || type.ContainingType != null) {
                    context.ReportDiagnostic(Diagnostic.Create(notPartial, declaration.Identifier.GetLocation(), type.Name));
                    continue;
                }

                context.AddSource($"{type.ToDisplayString().Replace('.', '_')}.AdsManagerBase.g.cs", SourceText.From(Partial(type), Encoding.UTF8));
            }
        }

        // Only assemblies that reference the pack can hold a manager, which keeps the search off the engine's own.
        private static IEnumerable<INamedTypeSymbol> Referenced(Compilation compilation, INamedTypeSymbol manager) {
            foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols) {
                if (assembly.Name is PackAssembly || !assembly.Modules.Any(module => module.ReferencedAssemblySymbols.Any(each => each.Name is PackAssembly))) continue;

                foreach (var type in Types(assembly.GlobalNamespace)) {
                    if (!type.IsAbstract && Derives(type, manager)) yield return type;
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol space) {
            foreach (var type in space.GetTypeMembers()) yield return type;
            foreach (var child in space.GetNamespaceMembers()) {
                foreach (var type in Types(child)) yield return type;
            }
        }

        // Ins, and the registration that lets the pack find the class and keeps IL2CPP from stripping it.
        private static string Partial(INamedTypeSymbol type) {
            var space  = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString();
            var indent = space == null ? "" : "    ";
            var code   = new StringBuilder();
            code.Append("// <auto-generated/>\n");
            if (space != null) code.Append($"namespace {space} {{\n");
            // The settings hold it by [SerializeReference], which wants [Serializable] on the class itself.
            if (!IsSerializable(type)) code.Append($"{indent}[global::System.Serializable]\n");
            code.Append($"{indent}partial class {type.Name} {{\n");
            code.Append($"{indent}    /// <summary>The instance the Ads pack runs; throws before the first scene loads.</summary>\n");
            code.Append($"{indent}    public static {type.Name} Ins => global::RiseOn.Ads.AdsSdk.GetManager<{type.Name}>();\n\n");
            code.Append($"{indent}    [global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.AfterAssembliesLoaded)]\n");
            code.Append($"{indent}    private static void RegisterWithRiseOnAds() => global::RiseOn.Ads.AdsSdk.Register(typeof({type.Name}), () => new {type.Name}());\n");
            code.Append($"{indent}}}\n");
            if (space != null) code.Append("}\n");
            return code.ToString();
        }

        // A second [Serializable] would not compile.
        private static bool IsSerializable(INamedTypeSymbol type) {
            return type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.SerializableAttribute");
        }

        private static bool Derives(INamedTypeSymbol type, INamedTypeSymbol root) {
            for (var current = type.BaseType; current != null; current = current.BaseType) {
                if (SymbolEqualityComparer.Default.Equals(current, root)) return true;
            }

            return false;
        }

        private sealed class Receiver : ISyntaxReceiver {
            public readonly List<ClassDeclarationSyntax> Classes = new();

            public void OnVisitSyntaxNode(SyntaxNode node) {
                if (node is ClassDeclarationSyntax { BaseList: not null } declaration) Classes.Add(declaration);
            }
        }
    }
}