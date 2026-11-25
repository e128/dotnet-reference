using Microsoft.CodeAnalysis;

namespace E128.Analyzers;

/// <summary>
///     Reads the element type of a one-dimensional array, a <c lang="csharp">Span&lt;T&gt;</c>, or
///     a <c lang="csharp">ReadOnlySpan&lt;T&gt;</c>. The performance rules that rewrite an indexed
///     loop share this shape.
/// </summary>
internal static class SpanElementType
{
    internal static bool TryGet(
        ITypeSymbol? type,
        INamedTypeSymbol spanType,
        out ITypeSymbol? element,
        out bool isSpan)
    {
        return TryGet(type, spanType, spanType, out element, out isSpan);
    }

    internal static bool TryGet(
        ITypeSymbol? type,
        INamedTypeSymbol spanType,
        INamedTypeSymbol readOnlySpanType,
        out ITypeSymbol? element,
        out bool isSpan)
    {
        element = null;
        isSpan = false;

        switch (type)
        {
            case IArrayTypeSymbol { Rank: 1 } array:
                element = array.ElementType;
                return true;

            case INamedTypeSymbol named
                when TypeSymbols.MatchesOriginalDefinition(named, spanType)
                     || TypeSymbols.MatchesOriginalDefinition(named, readOnlySpanType):
                element = named.TypeArguments[0];
                isSpan = true;
                return true;

            default:
                return false;
        }
    }
}
