using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128052: Flags public/internal methods and properties that expose mutable collection types
///     (<c lang="csharp">List&lt;T&gt;</c>, <c lang="csharp">Dictionary&lt;TKey,TValue&gt;</c>, <c lang="csharp">HashSet&lt;T&gt;</c>, etc.)
///     where immutable interfaces (<c lang="csharp">IReadOnlyList&lt;T&gt;</c>, <c lang="csharp">IReadOnlyDictionary&lt;TKey,TValue&gt;</c>,
///     <c lang="csharp">IReadOnlySet&lt;T&gt;</c>) would be sufficient.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MutableCollectionExposureAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128052";
    internal const string SuggestedTypeKey = "SuggestedType";

    private const int TwoTypeArguments = 2;

    private static readonly ImmutableArray<string> ExcludedContainingTypeSuffixes =
    [
        "Builder",
        "State",
        "Stats",
        "Accumulator",
        "Options"
    ];

    // Mutable type metadata names -> suggested immutable interface display names.
    // MutableCollectionCatalog owns the list.
    private static readonly ImmutableArray<(string MetadataName, string DisplayTemplate)> MutableTypeEntries =
    [
        .. MutableCollectionCatalog.Entries.Select(
            static entry => (entry.MetadataName, entry.DisplayTemplate))
    ];

    internal static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use immutable collection interface instead of mutable concrete type",
        "'{0}' exposes mutable '{1}' — use '{2}' instead",
        "E128-Design",
        DiagnosticSeverity.Info,
        true,
        "Public and internal API surfaces should expose immutable collection interfaces " +
        "(IReadOnlyList<T>, IReadOnlyDictionary<TKey,TValue>, IReadOnlySet<T>) instead of " +
        "mutable concrete types (List<T>, Dictionary<TKey,TValue>, HashSet<T>). " +
        "This enforces immutability by default at the API boundary.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var typeMap = BuildTypeMap(compilationContext.Compilation);
            if (typeMap.IsEmpty)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeMethod(symbolContext, typeMap),
                SymbolKind.Method);

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeProperty(symbolContext, typeMap),
                SymbolKind.Property);
        });
    }

    private static ImmutableDictionary<INamedTypeSymbol, string> BuildTypeMap(Compilation compilation)
    {
        var builder = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, string>(SymbolEqualityComparer.Default, StringComparer.Ordinal);

        foreach (var (metadataName, displayTemplate) in MutableTypeEntries)
        {
            var type = compilation.GetTypeByMetadataName(metadataName);
            if (type is not null)
            {
                builder[type] = displayTemplate;
            }
        }

        return builder.ToImmutable();
    }

    private static void AnalyzeMethod(
        SymbolAnalysisContext context,
        ImmutableDictionary<INamedTypeSymbol, string> typeMap)
    {
        var method = (IMethodSymbol)context.Symbol;

        if (!IsCandidateMethod(method))
        {
            return;
        }

        if (method.ReturnType is not INamedTypeSymbol returnType || !returnType.IsGenericType)
        {
            return;
        }

        var suggestion = GetImmutableSuggestion(returnType, typeMap);
        if (suggestion is null)
        {
            return;
        }

        // The interface walk below scans every interface member and resolves each implementation.
        // It runs only for a method that would otherwise report.
        if (InterfaceMembers.ImplementsMember(method))
        {
            return;
        }

        foreach (var location in method.Locations)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                ImmutableDictionary.CreateRange(
                    [
                        new KeyValuePair<string, string?>(SuggestedTypeKey, suggestion)
                    ]),
                method.Name,
                returnType.ToDisplayString(),
                suggestion));
        }
    }

    private static void AnalyzeProperty(
        SymbolAnalysisContext context,
        ImmutableDictionary<INamedTypeSymbol, string> typeMap)
    {
        var property = (IPropertySymbol)context.Symbol;

        if (!IsCandidateProperty(property))
        {
            return;
        }

        if (property.Type is not INamedTypeSymbol propertyType || !propertyType.IsGenericType)
        {
            return;
        }

        var suggestion = GetImmutableSuggestion(propertyType, typeMap);
        if (suggestion is null)
        {
            return;
        }

        foreach (var location in property.Locations)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                ImmutableDictionary.CreateRange(
                    [
                        new KeyValuePair<string, string?>(SuggestedTypeKey, suggestion)
                    ]),
                property.Name,
                propertyType.ToDisplayString(),
                suggestion));
        }
    }

    private static bool IsCandidateMethod(IMethodSymbol method)
    {
        return IsPublicOrInternal(method)
            && method.MethodKind is MethodKind.Ordinary
            && method.ContainingType?.TypeKind is not TypeKind.Interface
            && !method.IsAbstract
            && !method.IsOverride
            && !IsExcludedContainingType(method.ContainingType);
    }

    private static bool IsCandidateProperty(IPropertySymbol property)
    {
        return IsPublicOrInternal(property)
            && property.ContainingType?.TypeKind is not TypeKind.Interface
            && !property.IsOverride
            && !HasMutableSetAccessor(property)
            && !IsExcludedContainingType(property.ContainingType);
    }

    private static bool HasMutableSetAccessor(IPropertySymbol property)
    {
        // init-only setters are effectively immutable after construction — don't skip those
        return property.SetMethod is not null && !property.SetMethod.IsInitOnly;
    }

    private static bool IsPublicOrInternal(ISymbol symbol)
    {
        return symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal;
    }

    private static bool IsExcludedContainingType(INamedTypeSymbol? containingType)
    {
        if (containingType is null)
        {
            return false;
        }

        var name = containingType.Name;
        foreach (var suffix in ExcludedContainingTypeSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static string? GetImmutableSuggestion(
        INamedTypeSymbol type,
        ImmutableDictionary<INamedTypeSymbol, string> typeMap)
    {
        var originalDefinition = type.OriginalDefinition;
        if (!typeMap.TryGetValue(originalDefinition, out var template))
        {
            return null;
        }

        var args = type.TypeArguments;
        if (args.Length is 1)
        {
            return string.Format(CultureInfo.InvariantCulture, template, args[0].ToDisplayString());
        }

        if (args.Length is not TwoTypeArguments)
        {
            return null;
        }

        return string.Format(CultureInfo.InvariantCulture, template, args[0].ToDisplayString(), args[1].ToDisplayString());
    }
}
