using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Rewrites a regex pattern literal in a <c lang="csharp">GeneratedRegex</c> attribute. The
///     nested-quantifier and overlapping-quantifier fixes share the edit.
/// </summary>
internal static class RegexLiteralFix
{
    /// <summary>
    ///     Reads the pattern from the first positional string argument. Returns false when the
    ///     attribute has no such argument.
    /// </summary>
    internal static bool TryGetPattern(AttributeSyntax attribute, out string pattern)
    {
        return TryGetPatternArgument(attribute, out _, out _, out pattern);
    }

    private static bool TryGetPatternArgument(
        AttributeSyntax attribute,
        out AttributeArgumentSyntax firstArg,
        out LiteralExpressionSyntax literal,
        out string pattern)
    {
        literal = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(string.Empty));
        firstArg = attribute.ArgumentList?.Arguments.FirstOrDefault() ?? SyntaxFactory.AttributeArgument(literal);
        pattern = string.Empty;
        if (attribute.ArgumentList is null || !attribute.ArgumentList.Arguments.Any())
        {
            return false;
        }

        firstArg = attribute.ArgumentList.Arguments[0];
        if (firstArg.NameColon is not null || firstArg.NameEquals is not null)
        {
            return false;
        }

        if (firstArg.Expression is not LiteralExpressionSyntax expression
            || !expression.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return false;
        }

        literal = expression;
        pattern = expression.Token.ValueText;
        return true;
    }

    /// <summary>
    ///     Applies <paramref name="rewrite" /> to the first positional string argument and returns
    ///     the document unchanged when the pattern does not change.
    /// </summary>
    internal static async Task<Document> ApplyRewriteAsync(
        Document document,
        SyntaxNode attributeNode,
        Func<string, string> rewrite,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var attribute = (AttributeSyntax)attributeNode;
        if (!TryGetPatternArgument(attribute, out var firstArg, out var literal, out var pattern))
        {
            return document;
        }

        var fixedPattern = rewrite(pattern);
        if (string.Equals(fixedPattern, pattern, StringComparison.Ordinal))
        {
            return document;
        }

        var newArg = firstArg.WithExpression(CreateFixedLiteral(literal, fixedPattern));
        var newRoot = root.ReplaceNode(firstArg, newArg);
        return document.WithSyntaxRoot(newRoot);
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
            // The one-argument overload takes the token text, so it needs the quotes and the
            // escapes. Without them the rewrite emits an unquoted literal.
            newToken = SyntaxFactory.Literal(
                    SymbolDisplay.FormatLiteral(fixedValueText, quote: true),
                    fixedValueText)
                .WithLeadingTrivia(original.Token.LeadingTrivia)
                .WithTrailingTrivia(original.Token.TrailingTrivia);
        }

        return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, newToken);
    }
}
