using System;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
/// E128052: Flags public/internal methods and properties that expose mutable collection types
/// (<c>List&lt;T&gt;</c>, <c>Dictionary&lt;TKey,TValue&gt;</c>, <c>HashSet&lt;T&gt;</c>, etc.)
/// where immutable interfaces (<c>IReadOnlyList&lt;T&gt;</c>, <c>IReadOnlyDictionary&lt;TKey,TValue&gt;</c>,
/// <c>IReadOnlySet&lt;T&gt;</c>) would be sufficient.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MutableCollectionExposureAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128052";
    internal const string SuggestedTypeKey = "SuggestedType";

    private static readonly ImmutableArray<string> ExcludedContainingTypeSuffixes =
    [
        "Builder",
        "State",
        "Stats",
        "Accumulator",
        "Options",
    ];

    /// <summary>
    /// Mutable type metadata names -> suggested immutable interface display names.
    /// Keyed by the unbound generic metadata name (e.g. "System.Collections.Generic.List`1").
    /// Value is (metadata name of immutable interface, display template like "IReadOnlyList&lt;{0}&gt;").
    /// </summary>
    private static readonly ImmutableArray<(string MetadataName, string DisplayTemplate)> MutableTypeEntries =
    [
        ("System.Collections.Generic.List`1", "IReadOnlyList<{0}>"),
        ("System.Collections.Generic.Dictionary`2", "IReadOnlyDictionary<{0}, {1}>"),
        ("System.Collections.Generic.HashSet`1", "IReadOnlySet<{0}>"),
        ("System.Collections.Generic.SortedSet`1", "IReadOnlySet<{0}>"),
        ("System.Collections.Generic.SortedDictionary`2", "IReadOnlyDictionary<{0}, {1}>"),
        ("System.Collections.Generic.SortedList`2", "IReadOnlyDictionary<{0}, {1}>"),
        ("System.Collections.ObjectModel.Collection`1", "IReadOnlyList<{0}>"),
    ];

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Use immutable collection interface instead of mutable concrete type",
        messageFormat: "'{0}' exposes mutable '{1}' — use '{2}' instead",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Public and internal API surfaces should expose immutable collection interfaces " +
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

        context.RegisterCompilationStartAction(compilationContext =>
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
        var builder = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);

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

        if (!IsPublicOrInternal(method))
        {
            return;
        }

        if (method.MethodKind != MethodKind.Ordinary)
        {
            return;
        }

        if (method.ContainingType?.TypeKind == TypeKind.Interface)
        {
            return;
        }

        if (method.IsAbstract || method.IsOverride)
        {
            return;
        }

        if (IsExcludedContainingType(method.ContainingType))
        {
            return;
        }

        if (IsInterfaceImplementation(method))
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

        foreach (var location in method.Locations)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                location,
                ImmutableDictionary.CreateRange(
                [
                    new System.Collections.Generic.KeyValuePair<string, string?>(SuggestedTypeKey, suggestion),
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

        if (!IsPublicOrInternal(property))
        {
            return;
        }

        if (property.ContainingType?.TypeKind == TypeKind.Interface)
        {
            return;
        }

        if (property.IsOverride)
        {
            return;
        }

        if (HasMutableSetAccessor(property))
        {
            return;
        }

        if (IsExcludedContainingType(property.ContainingType))
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
                    new System.Collections.Generic.KeyValuePair<string, string?>(SuggestedTypeKey, suggestion),
                ]),
                property.Name,
                propertyType.ToDisplayString(),
                suggestion));
        }
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

    private static bool IsInterfaceImplementation(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member is IMethodSymbol interfaceMethod)
                {
                    var impl = containingType.FindImplementationForInterfaceMember(interfaceMethod);
                    if (SymbolEqualityComparer.Default.Equals(impl, method))
                    {
                        return true;
                    }
                }
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
        return args.Length == 1
            ? string.Format(CultureInfo.InvariantCulture, template, args[0].ToDisplayString())
            : args.Length == 2
            ? string.Format(CultureInfo.InvariantCulture, template, args[0].ToDisplayString(), args[1].ToDisplayString())
            : null;
    }
}
