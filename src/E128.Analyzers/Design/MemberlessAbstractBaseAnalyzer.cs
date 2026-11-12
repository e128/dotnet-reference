using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128219: Reports an abstract class or record base that declares no members while every
///     type that derives from it lives in the same assembly. The <c lang="csharp">closed</c> modifier names
///     that shape, and a switch over the closed hierarchy then fails the build on a missing
///     case. A public base is never reported, because an assembly outside this compilation
///     may derive from it and a closed modifier would break that derivation.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberlessAbstractBaseAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128219";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Memberless abstract base is a closed hierarchy",
        "Abstract base '{0}' declares no members and all derivation stays in this assembly — declare it closed instead of abstract so a switch over the hierarchy is exhaustive",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var candidates = new ConcurrentBag<(INamedTypeSymbol Type, Location Location)>();
            var derivations = new ConcurrentBag<(INamedTypeSymbol Base, IAssemblySymbol Assembly)>();

            startContext.RegisterSymbolAction(
                ctx => CollectCandidates(ctx, candidates),
                SymbolKind.NamedType);

            startContext.RegisterSymbolAction(
                ctx => CollectDerivations(ctx, derivations),
                SymbolKind.NamedType);

            startContext.RegisterCompilationEndAction(
                ctx => ReportClosedHierarchies(ctx, candidates, derivations));
        });
    }

    private static void CollectCandidates(
        SymbolAnalysisContext context,
        ConcurrentBag<(INamedTypeSymbol, Location)> candidates)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind is not TypeKind.Class
            || !type.IsAbstract
            || type.DeclaredAccessibility is not Accessibility.Internal
            || !IsMemberlessDeclaration(type))
        {
            return;
        }

        if (type.Locations.FirstOrDefault() is not { } location)
        {
            return;
        }

        candidates.Add((type, location));
    }

    private static bool IsMemberlessDeclaration(INamedTypeSymbol type)
    {
        var references = type.DeclaringSyntaxReferences;
        if (references.Length is 0)
        {
            return false;
        }

        foreach (var reference in references)
        {
            if (reference.GetSyntax(CancellationToken.None)
                    is not TypeDeclarationSyntax { Members.Count: 0 } declaration)
            {
                return false;
            }

            // The closed modifier implies abstract, so a closed base is already the target shape.
            if (declaration.Modifiers.IndexOf(SyntaxKind.ClosedKeyword) >= 0)
            {
                return false;
            }

            // A positional record synthesizes members from its parameter list, so it carries state.
            if (declaration is RecordDeclarationSyntax { ParameterList: not null })
            {
                return false;
            }
        }

        return true;
    }

    private static void CollectDerivations(
        SymbolAnalysisContext context,
        ConcurrentBag<(INamedTypeSymbol, IAssemblySymbol)> derivations)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        var baseType = type.BaseType;
        if (baseType is null || baseType.SpecialType is SpecialType.System_Object || baseType.TypeKind is not TypeKind.Class)
        {
            return;
        }

        derivations.Add((baseType.OriginalDefinition, type.ContainingAssembly));
    }

    private static void ReportClosedHierarchies(
        CompilationAnalysisContext context,
        ConcurrentBag<(INamedTypeSymbol Type, Location Location)> candidates,
        ConcurrentBag<(INamedTypeSymbol Base, IAssemblySymbol Assembly)> derivations)
    {
        foreach (var (type, location) in candidates)
        {
            var derivedAssemblies = derivations
                .Where(d => SymbolEqualityComparer.Default.Equals(d.Base, type))
                .Select(static d => d.Assembly)
                .Distinct(AssemblyComparer.Instance)
                .ToImmutableArray();

            if (derivedAssemblies.Length is 0)
            {
                continue;
            }

            // A derived type in a second assembly means the hierarchy is open and the closed
            // modifier would break that derivation.
            var singleAssembly = derivedAssemblies.Length is 1
                                 && AssemblyComparer.Instance.Equals(derivedAssemblies[0], type.ContainingAssembly);
            if (!singleAssembly)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.Name));
        }
    }

    private sealed class AssemblyComparer : IEqualityComparer<IAssemblySymbol>
    {
        internal static readonly AssemblyComparer Instance = new();

        private AssemblyComparer()
        {
        }

        public bool Equals(IAssemblySymbol? x, IAssemblySymbol? y)
        {
            return SymbolEqualityComparer.Default.Equals(x, y);
        }

        public int GetHashCode(IAssemblySymbol obj)
        {
            return SymbolEqualityComparer.Default.GetHashCode(obj);
        }
    }
}
