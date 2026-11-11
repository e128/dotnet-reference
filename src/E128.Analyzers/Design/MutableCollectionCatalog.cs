using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers.Design;

/// <summary>
///     One catalogue of mutable collection types and the read-only interface that replaces each
///     one. The exposure rule and the read-only return rules both read this list.
/// </summary>
internal static class MutableCollectionCatalog
{
    // A plain class because netstandard2.0 analysis assemblies have no IsExternalInit for records.
    internal sealed class Entry
    {
        internal Entry(
            string metadataName,
            string typeName,
            MutableCollectionKind kind,
            string displayTemplate,
            ImmutableArray<string> readOnlyInterfaces)
        {
            MetadataName = metadataName;
            TypeName = typeName;
            Kind = kind;
            DisplayTemplate = displayTemplate;
            ReadOnlyInterfaces = readOnlyInterfaces;
        }

        internal string MetadataName { get; }

        internal string TypeName { get; }

        internal MutableCollectionKind Kind { get; }

        internal string DisplayTemplate { get; }

        internal ImmutableArray<string> ReadOnlyInterfaces { get; }
    }

    internal static readonly ImmutableArray<Entry> Entries =
    [
        new(
            "System.Collections.Generic.List`1",
            "List",
            MutableCollectionKind.List,
            "IReadOnlyList<{0}>",
            ["IReadOnlyList", "IReadOnlyCollection"]),
        new(
            "System.Collections.Generic.Dictionary`2",
            "Dictionary",
            MutableCollectionKind.Dictionary,
            "IReadOnlyDictionary<{0}, {1}>",
            ["IReadOnlyDictionary"]),
        new(
            "System.Collections.Generic.HashSet`1",
            "HashSet",
            MutableCollectionKind.Set,
            "IReadOnlySet<{0}>",
            ["IReadOnlySet"]),
        new(
            "System.Collections.Generic.SortedSet`1",
            "SortedSet",
            MutableCollectionKind.Set,
            "IReadOnlySet<{0}>",
            ["IReadOnlySet"]),
        new(
            "System.Collections.Generic.SortedDictionary`2",
            "SortedDictionary",
            MutableCollectionKind.Dictionary,
            "IReadOnlyDictionary<{0}, {1}>",
            ["IReadOnlyDictionary"]),
        new(
            "System.Collections.Generic.SortedList`2",
            "SortedList",
            MutableCollectionKind.Dictionary,
            "IReadOnlyDictionary<{0}, {1}>",
            ["IReadOnlyDictionary"]),
        new(
            "System.Collections.ObjectModel.Collection`1",
            "Collection",
            MutableCollectionKind.List,
            "IReadOnlyList<{0}>",
            ["IReadOnlyList", "IReadOnlyCollection"]),
    ];

    /// <summary>
    ///     Matches a constructed type to its catalogue entry by name and namespace.
    /// </summary>
    internal static bool TryMatch(ITypeSymbol type, [NotNullWhen(true)] out Entry? entry)
    {
        entry = null;

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var containingNamespace = namedType.ContainingNamespace;
        if (!Namespaces.Equals(containingNamespace, "System.Collections.Generic")
            && !Namespaces.Equals(containingNamespace, "System.Collections.ObjectModel"))
        {
            return false;
        }

        foreach (var candidate in Entries)
        {
            if (string.Equals(namedType.Name, candidate.TypeName, StringComparison.Ordinal))
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     True when the type is a mutable collection of the given kind.
    /// </summary>
    internal static bool IsMutable(MutableCollectionKind kind, ITypeSymbol type)
    {
        return TryMatch(type, out var entry) && entry.Kind == kind;
    }

    /// <summary>
    ///     True when the type is one of the read-only interfaces that replaces the given kind.
    /// </summary>
    internal static bool IsReadOnlyInterface(MutableCollectionKind kind, ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType
            || !Namespaces.Equals(namedType.ContainingNamespace, "System.Collections.Generic"))
        {
            return false;
        }

        foreach (var entry in Entries)
        {
            if (entry.Kind != kind)
            {
                continue;
            }

            foreach (var interfaceName in entry.ReadOnlyInterfaces)
            {
                if (string.Equals(namedType.Name, interfaceName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
