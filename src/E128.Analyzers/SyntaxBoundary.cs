using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Shared ancestor walks that stop at a known syntax boundary.
/// </summary>
internal static class SyntaxBoundary
{
    /// <summary>
    ///     Returns true when <paramref name="node" /> sits inside a lambda or a local function
    ///     before the walk reaches <paramref name="boundary" />. That body runs later, so an
    ///     enclosing statement list says nothing about its execution order.
    /// </summary>
    internal static bool IsInsideNestedFunction(SyntaxNode node, SyntaxNode boundary)
    {
        for (var current = node.Parent; current is not null && current != boundary; current = current.Parent)
        {
            if (current is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }
}
