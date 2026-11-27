using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128259: A type name is the plural of a sibling type in the same namespace. The
///     plural reads as a collection of the sibling, so an operation type named this way
///     hides its real role.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PluralTwinTypeNameAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128259";

    private const string EnumerableMetadataName = "System.Collections.IEnumerable";

    private const int SSuffixLength = 1;

    private const int EsSuffixLength = 2;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Type name is the plural of a sibling type",
        "Type '{0}' is the plural of sibling type '{1}': rename it so the plural does not read as a collection",
        "E128-Design",
        DiagnosticSeverity.Info,
        true,
        "A type whose name is the plural of a sibling type reads as a collection of that sibling. An operation type named this way hides its real role. Rename it to name the operation.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(Analyze);
    }

    private static void Analyze(CompilationAnalysisContext context)
    {
        var enumerable = context.Compilation.GetTypeByMetadataName(EnumerableMetadataName);

        Visit(context.Compilation.Assembly.GlobalNamespace, enumerable, context);
    }

    private static void Visit(
        INamespaceSymbol namespaceSymbol,
        INamedTypeSymbol? enumerable,
        CompilationAnalysisContext context)
    {
        ReportInNamespace(namespaceSymbol, enumerable, context);

        foreach (var child in namespaceSymbol.GetNamespaceMembers())
        {
            Visit(child, enumerable, context);
        }
    }

    private static void ReportInNamespace(
        INamespaceSymbol namespaceSymbol,
        INamedTypeSymbol? enumerable,
        CompilationAnalysisContext context)
    {
        var byName = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);

        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            if (type.Arity is 0 && FirstSourceLocation(type) is not null)
            {
                byName[type.Name] = type;
            }
        }

        foreach (var candidate in byName.Values)
        {
            if (IsCollection(candidate, enumerable))
            {
                continue;
            }

            var sibling = FindSibling(candidate.Name, byName);
            var location = sibling is null ? null : FirstSourceLocation(candidate);

            if (sibling is null || location is null)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, location, candidate.Name, sibling.Name));
        }
    }

    private static INamedTypeSymbol? FindSibling(
        string name,
        Dictionary<string, INamedTypeSymbol> byName)
    {
        var singular = FindSingular(name, SSuffixLength, "s")
                       ?? FindSingular(name, EsSuffixLength, "es");

        return singular is not null && byName.TryGetValue(singular, out var sibling)
            ? sibling
            : null;
    }

    private static string? FindSingular(string name, int suffixLength, string suffix)
    {
        return name.Length > suffixLength && name.EndsWith(suffix, StringComparison.Ordinal)
            ? name.Substring(0, name.Length - suffixLength)
            : null;
    }

    private static bool IsCollection(INamedTypeSymbol type, INamedTypeSymbol? enumerable)
    {
        if (enumerable is null)
        {
            return false;
        }

        foreach (var @interface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, enumerable))
            {
                return true;
            }
        }

        return SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, enumerable);
    }

    private static Location? FirstSourceLocation(INamedTypeSymbol type)
    {
        foreach (var location in type.Locations)
        {
            if (location.IsInSource)
            {
                return location;
            }
        }

        return null;
    }
}
