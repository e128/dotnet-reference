using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Style;

/// <summary>
///     One enumerator for the <c lang="csharp">#pragma warning</c> directives in a syntax tree. The
///     suppression rules read every directive from here.
/// </summary>
internal static class PragmaWarningDirectives
{
    internal static IEnumerable<(SyntaxTrivia Trivia, PragmaWarningDirectiveTriviaSyntax Directive)> Warning(
        SyntaxNode root)
    {
        foreach (var trivia in root.DescendantTrivia())
        {
            if (trivia.GetStructure() is PragmaWarningDirectiveTriviaSyntax directive)
            {
                yield return (trivia, directive);
            }
        }
    }

    internal static bool IsDisable(PragmaWarningDirectiveTriviaSyntax directive)
    {
        return directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword);
    }
}
