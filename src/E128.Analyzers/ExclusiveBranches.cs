using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class ExclusiveBranches
{
    /// <summary>
    ///     True when one node sits in one branch and the other node sits in a different branch of the
    ///     same <c lang="csharp">if</c>, or in a different section of the same
    ///     <c lang="csharp">switch</c>, so the two never run in one pass.
    /// </summary>
    internal static bool Are(SyntaxNode first, SyntaxNode second)
    {
        if (AreInOppositeIfBranches(first, second))
        {
            return true;
        }

        foreach (var switchStatement in first.Ancestors().OfType<SwitchStatementSyntax>())
        {
            var firstSection = FindSection(switchStatement, first);
            var secondSection = FindSection(switchStatement, second);

            // Two enumerations in different sections never run in the same switch pass. An empty
            // section falls through, and it holds no enumeration of its own.
            if (firstSection is not null && secondSection is not null
                && !ReferenceEquals(firstSection, secondSection))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AreInOppositeIfBranches(SyntaxNode first, SyntaxNode second)
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

    private static SwitchSectionSyntax? FindSection(SwitchStatementSyntax switchStatement, SyntaxNode node)
    {
        foreach (var section in switchStatement.Sections)
        {
            if (section.Span.Contains(node.Span))
            {
                return section;
            }
        }

        return null;
    }
}
