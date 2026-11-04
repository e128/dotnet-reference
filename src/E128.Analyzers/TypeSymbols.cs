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
               && named.TypeArguments.Length == 1
            ? named.TypeArguments[0]
            : type;
    }

    /// <summary>
    ///     Returns true when <paramref name="type" /> is <paramref name="baseType" /> or inherits from it.
    /// </summary>
    internal static bool DerivesFrom(ITypeSymbol? type, ITypeSymbol baseType)
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
}
