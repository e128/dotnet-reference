using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Testing;

/// <summary>
///     One assertion call site in a candidate test method. Detectors that read argument shape need the
///     invocation syntax. Detectors that read the called member need the resolved symbol.
/// </summary>
internal sealed class AssertionFact
{
    private ImmutableArray<SyntaxNode>? nodes;

    internal AssertionFact(InvocationExpressionSyntax invocation, IMethodSymbol symbol)
    {
        Invocation = invocation;
        Symbol = symbol;
    }

    internal InvocationExpressionSyntax Invocation { get; }

    internal IMethodSymbol Symbol { get; }

    // Six detectors walk the same argument subtree. Materialize it once per assertion instead of
    // once per detector.
    internal ImmutableArray<SyntaxNode> Nodes => nodes ??= LowValueTestAnalyzer.CollectAssertionNodes(Invocation);
}
