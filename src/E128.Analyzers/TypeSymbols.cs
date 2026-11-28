using System;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers;

internal static class TypeSymbols
{
    /// <summary>
    ///     Returns the underlying type of a <c lang="csharp">Nullable&lt;T&gt;</c>, or <paramref name="type" /> itself otherwise.
    /// </summary>
    internal static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named
               && named.TypeArguments.Length is 1
            ? named.TypeArguments[0]
            : type;
    }

    /// <summary>
    ///     Returns true when <paramref name="type" /> is <paramref name="baseType" /> or inherits from it.
    ///     A null <paramref name="baseType" /> matches nothing.
    /// </summary>
    internal static bool DerivesFrom(ITypeSymbol? type, ITypeSymbol? baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns true when <paramref name="type" /> is <paramref name="target" />. A null target
    ///     matches nothing.
    /// </summary>
    internal static bool Matches(ITypeSymbol? type, INamedTypeSymbol? target)
    {
        return target is not null && SymbolEqualityComparer.Default.Equals(type, target);
    }

    /// <summary>
    ///     Returns true when the original definition of <paramref name="type" /> is
    ///     <paramref name="definition" />. A null definition matches nothing.
    /// </summary>
    internal static bool MatchesOriginalDefinition(ITypeSymbol? type, INamedTypeSymbol? definition)
    {
        return definition is not null
               && type is not null
               && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, definition);
    }

    /// <summary>
    ///     Returns true when <paramref name="type" /> is a type with the given simple name and
    ///     namespace. When <paramref name="requireTopLevel" /> is true, a nested type does not match.
    /// </summary>
    internal static bool IsNamed(
        ITypeSymbol? type,
        string name,
        string dottedNamespace,
        bool requireTopLevel = true)
    {
        return type is INamedTypeSymbol named
               && (!requireTopLevel || named.ContainingType is null)
               && string.Equals(named.Name, name, StringComparison.Ordinal)
               && Namespaces.Equals(named.ContainingNamespace, dottedNamespace);
    }

    /// <summary>
    ///     Walks <paramref name="type" /> and its base types. Returns true when the predicate matches.
    ///     When <paramref name="includeSelf" /> is false, the walk starts at the direct base type.
    /// </summary>
    internal static bool WalkBaseTypes(
        ITypeSymbol? type,
        Func<ITypeSymbol, bool> predicate,
        bool includeSelf = true)
    {
        return FindBaseType(type, predicate, includeSelf) is not null;
    }

    /// <summary>
    ///     Returns the first type in the base-type chain that matches the predicate, or null.
    ///     When <paramref name="includeSelf" /> is false, the walk starts at the direct base type.
    /// </summary>
    internal static ITypeSymbol? FindBaseType(
        ITypeSymbol? type,
        Func<ITypeSymbol, bool> predicate,
        bool includeSelf = true)
    {
        for (var current = includeSelf ? type : type?.BaseType; current is not null; current = current.BaseType)
        {
            if (predicate(current))
            {
                return current;
            }
        }

        return null;
    }
}
