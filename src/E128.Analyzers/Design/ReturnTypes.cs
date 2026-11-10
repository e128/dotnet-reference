using Microsoft.CodeAnalysis;

namespace E128.Analyzers.Design;

/// <summary>
///     Reads the declared return type of a member. The read-only collection rules share it.
/// </summary>
internal static class ReturnTypes
{
    /// <summary>
    ///     Returns the return type of a method or the type of a property. Another symbol returns
    ///     null.
    /// </summary>
    internal static ITypeSymbol? Get(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol method => method.ReturnType,
            IPropertySymbol property => property.Type,
            _ => null,
        };
    }
}
