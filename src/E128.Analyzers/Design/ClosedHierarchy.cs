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
///     Shared analysis for the rules that suggest the <c lang="csharp">closed</c> modifier on an
///     internal abstract base whose every derivation lives in the same assembly. E128219 owns the
///     memberless base, and E128225 owns the base that declares members.
/// </summary>
internal static class ClosedHierarchy
{
    private const string ClosedAttributeName = "IsClosedTypeAttribute";

    private const string ClosedAttributeNamespace = "System.Runtime.CompilerServices";

    internal static void Register(AnalysisContext context, DiagnosticDescriptor rule, bool memberless)
    {
        context.RegisterCompilationStartAction(startContext =>
        {
            var candidates = new ConcurrentBag<(INamedTypeSymbol Type, Location Location)>();
            var derivations = new ConcurrentBag<(INamedTypeSymbol Base, IAssemblySymbol Assembly)>();

            startContext.RegisterSymbolAction(
                symbolContext => CollectCandidates(symbolContext, candidates, memberless),
                SymbolKind.NamedType);

            startContext.RegisterSymbolAction(
                symbolContext => CollectDerivations(symbolContext, derivations),
                SymbolKind.NamedType);

            startContext.RegisterCompilationEndAction(
                compilationContext => ReportCandidates(compilationContext, rule, candidates, derivations));
        });
    }

    /// <summary>
    ///     True when the type already carries the <c lang="csharp">closed</c> modifier, in source
    ///     or through the compiler attribute.
    /// </summary>
    internal static bool IsClosed(INamedTypeSymbol type)
    {
        if (type.DeclaringSyntaxReferences.Any(static reference =>
                reference.GetSyntax(CancellationToken.None)
                    is TypeDeclarationSyntax { Modifiers: var modifiers }
                && modifiers.IndexOf(SyntaxKind.ClosedKeyword) >= 0))
        {
            return true;
        }

        return type.GetAttributes().Any(static attribute =>
            string.Equals(attribute.AttributeClass?.Name, ClosedAttributeName, StringComparison.Ordinal)
            && string.Equals(
                attribute.AttributeClass?.ContainingNamespace?.ToDisplayString(),
                ClosedAttributeNamespace,
                StringComparison.Ordinal));
    }

    private static void CollectCandidates(
        SymbolAnalysisContext context,
        ConcurrentBag<(INamedTypeSymbol Type, Location Location)> candidates,
        bool memberless)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind is not TypeKind.Class
            || !type.IsAbstract
            || type.DeclaredAccessibility is not Accessibility.Internal
            || IsClosed(type)
            || !HasMatchingDeclaration(type, memberless))
        {
            return;
        }

        if (type.Locations.FirstOrDefault() is not { } location)
        {
            return;
        }

        candidates.Add((type, location));
    }

    private static bool HasMatchingDeclaration(INamedTypeSymbol type, bool memberless)
    {
        var references = type.DeclaringSyntaxReferences;
        if (references.Length is 0)
        {
            return false;
        }

        foreach (var reference in references)
        {
            if (reference.GetSyntax(CancellationToken.None) is not TypeDeclarationSyntax declaration
                || !MatchesMemberShape(declaration, memberless))
            {
                return false;
            }

            if (declaration is RecordDeclarationSyntax record && (!memberless || record.ParameterList is not null))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesMemberShape(TypeDeclarationSyntax declaration, bool memberless)
    {
        return memberless
            ? declaration.Members.Count is 0
            : declaration.Members.Count is not 0;
    }

    private static void CollectDerivations(
        SymbolAnalysisContext context,
        ConcurrentBag<(INamedTypeSymbol Base, IAssemblySymbol Assembly)> derivations)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        var baseType = type.BaseType;
        if (baseType is null || baseType.SpecialType is SpecialType.System_Object || baseType.TypeKind is not TypeKind.Class)
        {
            return;
        }

        derivations.Add((baseType.OriginalDefinition, type.ContainingAssembly));
    }

    private static void ReportCandidates(
        CompilationAnalysisContext context,
        DiagnosticDescriptor rule,
        ConcurrentBag<(INamedTypeSymbol Type, Location Location)> candidates,
        ConcurrentBag<(INamedTypeSymbol Base, IAssemblySymbol Assembly)> derivations)
    {
        foreach (var (type, location) in candidates)
        {
            var derivedAssemblies = derivations
                .Where(derivation => SymbolEqualityComparer.Default.Equals(derivation.Base, type))
                .Select(static derivation => derivation.Assembly)
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

            context.ReportDiagnostic(Diagnostic.Create(rule, location, type.Name));
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
