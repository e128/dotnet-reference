using Microsoft.CodeAnalysis;

namespace E128.Analyzers;

internal static class TypeSymbols
{
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
