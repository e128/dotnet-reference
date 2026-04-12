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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GeneratedRegexNestedCodeFixProvider))]
[Shared]
public sealed class GeneratedRegexNestedCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GeneratedRegexAnalyzer.NestedQuantifierDiagnosticId];

    public override FixAllProvider? GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

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
                title: "Remove outer quantifier from nested group",
                createChangedDocument: ct => ApplyFixAsync(context.Document, node, ct),
                equivalenceKey: nameof(GeneratedRegexNestedCodeFixProvider)),
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
        var fixedPattern = RemoveOuterQuantifier(pattern);
        if (string.Equals(fixedPattern, pattern, StringComparison.Ordinal))
        {
            return document;
        }

        var newLiteral = CreateFixedLiteral(literal, fixedPattern);
        var newArg = firstArg.WithExpression(newLiteral);
        var newRoot = root.ReplaceNode(firstArg, newArg);
        return document.WithSyntaxRoot(newRoot);
    }

    internal static string RemoveOuterQuantifier(string pattern)
    {
        var skipNext = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (pattern[i] == '\\')
            {
                skipNext = true;
                continue;
            }

            if (pattern[i] != '(')
            {
                continue;
            }

            var closeIndex = FindMatchingCloseParen(pattern, i);
            if (closeIndex < 0)
            {
                continue;
            }

            var afterClose = closeIndex + 1;
            if (afterClose >= pattern.Length || pattern[afterClose] is not ('*' or '+'))
            {
                continue;
            }

            if (GroupContainsInnerQuantifier(pattern, i, closeIndex))
            {
                return pattern.Substring(0, afterClose) + pattern.Substring(afterClose + 1);
            }
        }

        return pattern;
    }

    private static bool GroupContainsInnerQuantifier(string pattern, int openIndex, int closeIndex)
    {
        var skipNext = false;
        var depth = 0;
        for (var j = openIndex; j <= closeIndex; j++)
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
            }
            else if (ch is '*' or '+' && depth == 1)
            {
                return true;
            }
        }

        return false;
    }

    private static int FindMatchingCloseParen(string pattern, int openIndex)
    {
        var depth = 0;
        var skipNext = false;
        for (var j = openIndex; j < pattern.Length; j++)
        {
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            if (pattern[j] == '\\')
            {
                skipNext = true;
                continue;
            }

            if (pattern[j] == '(')
            {
                depth++;
            }
            else if (pattern[j] == ')')
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
