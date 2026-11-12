using Microsoft.CodeAnalysis;

namespace E128.Analyzers;

/// <summary>
///     Classifies the symbols that the compiler delivers for a partial member.
///     A symbol action receives the defining symbol and the implementing symbol
///     as separate events. A rule that reports one diagnostic per member calls
///     <see cref="IsImplementationPart"/> and skips the implementing event.
/// </summary>
internal static class PartialMembers
{
    /// <summary>
    ///     True when <paramref name="symbol"/> is the implementing source symbol of
    ///     a partial member. The defining symbol, and a member with no other part,
    ///     return false.
    /// </summary>
    internal static bool IsImplementationPart(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol method => method.PartialDefinitionPart is not null,
            IPropertySymbol property => property.PartialDefinitionPart is not null,
            IParameterSymbol parameter => parameter.ContainingSymbol is IMethodSymbol containingMethod
                                          && containingMethod.PartialDefinitionPart is not null,
            _ => false
        };
    }
}
