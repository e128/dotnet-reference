using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class ExclusiveBranches
{
    /// <summary>
    ///     True when one node sits in the then branch and the other in the else branch of the same
    ///     <c lang="csharp">if</c>, so the two never run in one pass.
    /// </summary>
    internal static bool Are(SyntaxNode first, SyntaxNode second)
    {
        foreach (var ifStatement in first.Ancestors().OfType<IfStatementSyntax>())
        {
            var firstInThen = ifStatement.Statement.Span.Contains(first.Span);
            var firstInElse = ifStatement.Else is { } firstElse && firstElse.Span.Contains(first.Span);
            var secondInThen = ifStatement.Statement.Span.Contains(second.Span);
            var secondInElse = ifStatement.Else is { } secondElse && secondElse.Span.Contains(second.Span);

            if ((firstInThen && secondInElse) || (firstInElse && secondInThen))
            {
                return true;
            }
        }

        return false;
    }
}
