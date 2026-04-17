using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeneratedRegexOverlappingCodeFixProvider))]
[Shared]
public sealed class GeneratedRegexOverlappingCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GeneratedRegexAnalyzer.OverlappingQuantifierDiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not AttributeSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove overlapping \\s quantifier",
                ct => ApplyFixAsync(context.Document, node, ct),
                nameof(GeneratedRegexOverlappingCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode attributeNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var attribute = (AttributeSyntax)attributeNode;
        if (attribute.ArgumentList is null || !attribute.ArgumentList.Arguments.Any())
        {
            return document;
        }

        var firstArg = attribute.ArgumentList.Arguments[0];
        if (firstArg.NameColon is not null || firstArg.NameEquals is not null)
        {
            return document;
        }

        if (firstArg.Expression is not LiteralExpressionSyntax literal
            || !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return document;
        }

        var pattern = literal.Token.ValueText;
        var fixedPattern = RemoveOverlappingWhitespaceQuantifier(pattern);
        if (string.Equals(fixedPattern, pattern, StringComparison.Ordinal))
        {
            return document;
        }

        var newLiteral = CreateFixedLiteral(literal, fixedPattern);
        var newArg = firstArg.WithExpression(newLiteral);
        var newRoot = root.ReplaceNode(firstArg, newArg);
        return document.WithSyntaxRoot(newRoot);
    }

    internal static string RemoveOverlappingWhitespaceQuantifier(string pattern)
    {
        for (var i = 0; i < pattern.Length - 2; i++)
        {
            if (pattern[i] != '\\' || pattern[i + 1] != 's')
            {
                continue;
            }

            var qEnd = i + 2;
            if (qEnd >= pattern.Length || pattern[qEnd] is not ('*' or '+'))
            {
                continue;
            }

            qEnd++;
            if (qEnd < pattern.Length && pattern[qEnd] is '?')
            {
                qEnd++;
            }

            if (HasOverlappingElementForward(pattern, qEnd))
            {
                return pattern.Substring(0, i) + pattern.Substring(qEnd);
            }

            if (HasOverlappingElementBackward(pattern, i))
            {
                return pattern.Substring(0, i) + pattern.Substring(qEnd);
            }
        }

        return pattern;
    }

    private static bool HasOverlappingElementForward(string pattern, int index)
    {
        if (index >= pattern.Length)
        {
            return false;
        }

        var ch = pattern[index];

        return (ch == '.' && index + 1 < pattern.Length && pattern[index + 1] is '*' or '+')
               || (ch == '(' && ContainsDotQuantifierInGroup(pattern, index));
    }

    private static bool HasOverlappingElementBackward(string pattern, int backslashIndex)
    {
        if (backslashIndex == 0)
        {
            return false;
        }

        var prevIdx = backslashIndex - 1;
        var prev = pattern[prevIdx];

        if (prev == '?')
        {
            if (prevIdx == 0)
            {
                return false;
            }

            prevIdx--;
            prev = pattern[prevIdx];
        }

        if (prev is '*' or '+' && prevIdx > 0 && pattern[prevIdx - 1] == '.')
        {
            return true;
        }

        if (prev == ')')
        {
            var openParen = FindMatchingOpenParen(pattern, prevIdx);
            return openParen >= 0 && ContainsDotQuantifierInGroup(pattern, openParen);
        }

        if (prev is '*' or '+' && prevIdx > 0 && pattern[prevIdx - 1] == ')')
        {
            var openParen = FindMatchingOpenParen(pattern, prevIdx - 1);
            return openParen >= 0 && ContainsDotQuantifierInGroup(pattern, openParen);
        }

        return false;
    }

    private static bool ContainsDotQuantifierInGroup(string pattern, int openParenIndex)
    {
        if (openParenIndex < 0)
        {
            return false;
        }

        var skipNext = false;
        var depth = 0;
        for (var j = openParenIndex; j < pattern.Length; j++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            var ch = pattern[j];
            if (ch == '\\')
            {
                skipNext = true;
                continue;
            }

            if (ch == '(')
            {
                depth++;
            }
            else if (ch == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return false;
                }
            }
            else if (ch == '.' && depth == 1 && j + 1 < pattern.Length && pattern[j + 1] is '*' or '+' or '?')
            {
                return true;
            }
        }

        return false;
    }

    private static int FindMatchingOpenParen(string pattern, int closeIndex)
    {
        var depth = 0;
        for (var j = closeIndex; j >= 0; j--)
        {
            if (j > 0 && pattern[j - 1] == '\\')
            {
                continue;
            }

            if (pattern[j] == ')')
            {
                depth++;
            }
            else if (pattern[j] == '(')
            {
                depth--;
                if (depth == 0)
                {
                    return j;
                }
            }
        }

        return -1;
    }

    private static LiteralExpressionSyntax CreateFixedLiteral(
        LiteralExpressionSyntax original,
        string fixedValueText)
    {
        var text = original.Token.Text;
        SyntaxToken newToken;

        if (text.StartsWith("@", StringComparison.Ordinal))
        {
            var escapedText = "@\"" + fixedValueText.Replace("\"", "\"\"") + "\"";
            newToken = SyntaxFactory.Literal(
                original.Token.LeadingTrivia,
                escapedText,
                fixedValueText,
                original.Token.TrailingTrivia);
        }
        else
        {
            newToken = SyntaxFactory.Literal(fixedValueText)
                .WithLeadingTrivia(original.Token.LeadingTrivia)
                .WithTrailingTrivia(original.Token.TrailingTrivia);
        }

        return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, newToken);
    }
}
