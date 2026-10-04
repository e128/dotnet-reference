using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Testing;

/// <summary>
///     One production call site in a candidate test method. Detectors that read argument syntax or resolve the
///     called member need the invocation beside its symbol.
/// </summary>
internal sealed class ProductionCallFact
{
    internal ProductionCallFact(InvocationExpressionSyntax invocation, IMethodSymbol symbol)
    {
        Invocation = invocation;
        Symbol = symbol;
    }

    internal InvocationExpressionSyntax Invocation { get; }

    internal IMethodSymbol Symbol { get; }
}
