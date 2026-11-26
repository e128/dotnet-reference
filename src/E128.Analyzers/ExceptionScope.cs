using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class ExceptionScope
{
    /// <summary>
    ///     Returns true when a parent of <paramref name="node" /> below <paramref name="boundary" />
    ///     is a try statement with at least one catch clause. The walk stops at
    ///     <paramref name="boundary" /> without testing it.
    /// </summary>
    internal static bool IsInsideTryWithCatch(SyntaxNode node, SyntaxNode boundary)
    {
        for (var current = node.Parent; current is not null && current != boundary; current = current.Parent)
        {
            if (current is TryStatementSyntax { Catches.Count: > 0 })
            {
                return true;
            }
        }

        return false;
    }
}
