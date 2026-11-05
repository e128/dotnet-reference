using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128178: Reports a second member of a type that maps to a JSON property name already used by
///     another member. System.Text.Json throws <c lang="csharp">InvalidOperationException</c> at the
///     first serialize or deserialize call when two members of one type carry the same
///     <c lang="csharp">[JsonPropertyName]</c> value.
/// </summary>
/// <remarks>
///     The rule matches the attribute by symbol, so a user-defined attribute with the same name does
///     not trigger it. The rule reports the later member in source order. It also reports a member
///     that reuses a JSON name declared on a base type, except when the member overrides that base
///     member, because an override replaces the base member in the contract.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateJsonPropertyNameAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128178";

    private const string JsonPropertyNameMetadataName = "System.Text.Json.Serialization.JsonPropertyNameAttribute";
    private const string JsonIgnoreMetadataName = "System.Text.Json.Serialization.JsonIgnoreAttribute";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Duplicate JsonPropertyName Value",
        "Member '{0}' and member '{1}' both map to JSON name '{2}' in type '{3}'",
        "E128-Reliability",
        DiagnosticSeverity.Error,
        true,
        "System.Text.Json throws InvalidOperationException when two members of one type map to the same " +
        "JSON property name. Give each serialized member a unique JsonPropertyName value.");

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
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var jsonPropertyNameTypes = MetadataTypes.Find(context.Compilation, JsonPropertyNameMetadataName);

        // Without the attribute type the rule has nothing to look for, so it stays off entirely.
        if (jsonPropertyNameTypes.IsEmpty)
        {
            return;
        }

        var jsonIgnoreTypes = MetadataTypes.Find(context.Compilation, JsonIgnoreMetadataName);

        context.RegisterSymbolAction(
            symbolContext => AnalyzeType(symbolContext, jsonPropertyNameTypes, jsonIgnoreTypes),
            SymbolKind.NamedType);
    }

    private static void AnalyzeType(
        SymbolAnalysisContext context,
        ImmutableArray<INamedTypeSymbol> jsonPropertyNameTypes,
        ImmutableArray<INamedTypeSymbol> jsonIgnoreTypes)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return;
        }

        // One symbol action covers every declaration of a partial type, because Roslyn merges the
        // partial declarations into one symbol before the action runs.
        var candidates = new List<(ISymbol Member, AttributeData Attribute, string JsonName)>();
        foreach (var member in type.GetMembers())
        {
            if (GetJsonPropertyName(member, jsonPropertyNameTypes, jsonIgnoreTypes, out var jsonName) is { } attribute)
            {
                candidates.Add((member, attribute, jsonName));
            }
        }

        if (candidates.Count == 0)
        {
            return;
        }

        var inheritedNames = CollectInheritedNames(type, jsonPropertyNameTypes, jsonIgnoreTypes);
        var declaredNames = new Dictionary<string, ISymbol>(StringComparer.Ordinal);

        foreach (var (member, attribute, jsonName) in candidates)
        {
            if (declaredNames.TryGetValue(jsonName, out var earlier))
            {
                Report(context, type, member, attribute, jsonName, earlier);
            }
            else if (inheritedNames.TryGetValue(jsonName, out var baseMember) && !IsOverrideOf(member, baseMember))
            {
                Report(context, type, member, attribute, jsonName, baseMember);
            }

            declaredNames[jsonName] = member;
        }
    }

    private static Dictionary<string, ISymbol> CollectInheritedNames(
        INamedTypeSymbol type,
        ImmutableArray<INamedTypeSymbol> jsonPropertyNameTypes,
        ImmutableArray<INamedTypeSymbol> jsonIgnoreTypes)
    {
        var names = new Dictionary<string, ISymbol>(StringComparer.Ordinal);

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            foreach (var member in baseType.GetMembers())
            {
                if (GetJsonPropertyName(member, jsonPropertyNameTypes, jsonIgnoreTypes, out var jsonName) is null
                    || names.ContainsKey(jsonName))
                {
                    continue;
                }

                names.Add(jsonName, member);
            }
        }

        return names;
    }

    private static AttributeData? GetJsonPropertyName(
        ISymbol member,
        ImmutableArray<INamedTypeSymbol> jsonPropertyNameTypes,
        ImmutableArray<INamedTypeSymbol> jsonIgnoreTypes,
        out string jsonName)
    {
        jsonName = string.Empty;

        if (member is not (IPropertySymbol or IFieldSymbol) || IsExcluded(member, jsonIgnoreTypes))
        {
            return null;
        }

        foreach (var attribute in member.GetAttributes())
        {
            if (!MetadataTypes.Contains(jsonPropertyNameTypes, attribute.AttributeClass))
            {
                continue;
            }

            return TryReadName(attribute, out jsonName) ? attribute : null;
        }

        return null;
    }

    // A static member and an indexer never reach the JSON contract. An explicit interface
    // implementation is not public, so System.Text.Json skips it. A member marked JsonIgnore is
    // removed from the contract, so it cannot collide with another member.
    private static bool IsExcluded(ISymbol member, ImmutableArray<INamedTypeSymbol> jsonIgnoreTypes)
    {
        if (member.IsStatic)
        {
            return true;
        }

        if (member is IPropertySymbol property
            && (property.IsIndexer || property.ExplicitInterfaceImplementations.Length > 0))
        {
            return true;
        }

        foreach (var attribute in member.GetAttributes())
        {
            if (MetadataTypes.Contains(jsonIgnoreTypes, attribute.AttributeClass))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryReadName(AttributeData attribute, out string jsonName)
    {
        jsonName = string.Empty;

        if (attribute.ConstructorArguments.Length != 1
            || attribute.ConstructorArguments[0].Value is not string name
            || name.Length == 0)
        {
            return false;
        }

        jsonName = name;
        return true;
    }

    // An override replaces the base member in the contract, so the two members never coexist. Walk
    // the override chain because the attribute can sit on a virtual ancestor of the base member.
    private static bool IsOverrideOf(ISymbol member, ISymbol baseMember)
    {
        if (member is not IPropertySymbol property)
        {
            return false;
        }

        for (var overridden = property.OverriddenProperty; overridden is not null; overridden = overridden.OverriddenProperty)
        {
            if (SymbolEqualityComparer.Default.Equals(overridden, baseMember))
            {
                return true;
            }
        }

        return false;
    }

    private static void Report(
        SymbolAnalysisContext context,
        INamedTypeSymbol type,
        ISymbol member,
        AttributeData attribute,
        string jsonName,
        ISymbol earlier)
    {
        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                GetLocation(attribute, member),
                member.Name,
                earlier.Name,
                jsonName,
                type.Name));
    }

    private static Location GetLocation(AttributeData attribute, ISymbol member)
    {
        return attribute.ApplicationSyntaxReference?.GetSyntax() is { } syntax
            ? syntax.GetLocation()
            : member.Locations.Length > 0 ? member.Locations[0] : Location.None;
    }
}
