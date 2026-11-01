using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StringFormatToInterpolationCodeFixProvider))]
[Shared]
public sealed class StringFormatToInterpolationCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [StringFormatToInterpolationAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        // An unsupported format string makes the rewrite impossible. Decline here so the user never
        // sees a fix that leaves the document unchanged.
        var interpolated = BuildFromInvocation(invocation, semanticModel, context.CancellationToken);
        if (interpolated is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Convert to string interpolation",
                ct => ApplyFixAsync(context.Document, invocation, interpolated, ct),
                nameof(StringFormatToInterpolationCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        InterpolatedStringExpressionSyntax interpolated,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newRoot = root.ReplaceNode(invocation, interpolated.WithTriviaFrom(invocation));
        return document.WithSyntaxRoot(newRoot);
    }

    private static InterpolatedStringExpressionSyntax? BuildFromInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return null;
        }

        var formatArgIndex = StringFormatToInterpolationAnalyzer.GetFormatStringArgumentIndex(method);
        if (formatArgIndex < 0)
        {
            return null;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (formatArgIndex >= arguments.Count)
        {
            return null;
        }

        var formatExpression = arguments[formatArgIndex].Expression;
        var constantValue = semanticModel.GetConstantValue(formatExpression, cancellationToken);
        if (!constantValue.HasValue || constantValue.Value is not string formatString)
        {
            return null;
        }

        var argExpressions = new ExpressionSyntax[arguments.Count - formatArgIndex - 1];
        for (var i = formatArgIndex + 1; i < arguments.Count; i++)
        {
            argExpressions[i - formatArgIndex - 1] = arguments[i].Expression;
        }

        return BuildInterpolatedString(formatString, argExpressions);
    }

    internal static InterpolatedStringExpressionSyntax? BuildInterpolatedString(
        string formatString,
        ExpressionSyntax[] arguments)
    {
        var contents = new List<InterpolatedStringContentSyntax>();
        var i = 0;

        while (i < formatString.Length)
        {
            if (formatString[i] == '{')
            {
                i = ProcessOpenBrace(formatString, i, arguments, contents);
                if (i < 0)
                {
                    return null;
                }
            }
            else if (formatString[i] == '}')
            {
                // A brace is either doubled (a literal brace) or unmatched. An unmatched brace
                // cannot be reproduced in an interpolated string, so the fix declines.
                if (i + 1 >= formatString.Length || formatString[i + 1] != '}')
                {
                    return null;
                }

                contents.Add(CreateTextContent("}"));
                i += 2;
            }
            else
            {
                i = ProcessLiteralText(formatString, i, contents);
            }
        }

        return SyntaxFactory.InterpolatedStringExpression(
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken),
            SyntaxFactory.List(contents));
    }

    private static int ProcessOpenBrace(
        string formatString,
        int i,
        ExpressionSyntax[] arguments,
        List<InterpolatedStringContentSyntax> contents)
    {
        if (i + 1 < formatString.Length && formatString[i + 1] == '{')
        {
            contents.Add(CreateTextContent("{"));
            return i + 2;
        }

        var closeIndex = formatString.IndexOf('}', i);
        if (closeIndex < 0)
        {
            return -1;
        }

        var placeholder = formatString.Substring(i + 1, closeIndex - i - 1);
        var interpolation = BuildInterpolation(placeholder, arguments);
        if (interpolation is null)
        {
            return -1;
        }

        contents.Add(interpolation);
        return closeIndex + 1;
    }

    private static InterpolationSyntax? BuildInterpolation(string placeholder, ExpressionSyntax[] arguments)
    {
        var colonIndex = placeholder.IndexOf(':');
        var indexPart = colonIndex >= 0 ? placeholder.Substring(0, colonIndex) : placeholder;

        if (!int.TryParse(indexPart, NumberStyles.None, CultureInfo.InvariantCulture, out var argIndex)
            || argIndex < 0
            || argIndex >= arguments.Length)
        {
            return null;
        }

        if (colonIndex >= 0)
        {
            var formatSpecifier = placeholder.Substring(colonIndex + 1);
            return SyntaxFactory.Interpolation(
                arguments[argIndex],
                null,
                SyntaxFactory.InterpolationFormatClause(
                    SyntaxFactory.Token(SyntaxKind.ColonToken),
                    SyntaxFactory.Token(
                        SyntaxTriviaList.Empty,
                        SyntaxKind.InterpolatedStringTextToken,
                        formatSpecifier,
                        formatSpecifier,
                        SyntaxTriviaList.Empty)));
        }

        return SyntaxFactory.Interpolation(arguments[argIndex]);
    }

    private static int ProcessLiteralText(
        string formatString,
        int start,
        List<InterpolatedStringContentSyntax> contents)
    {
        var i = start;
        while (i < formatString.Length && formatString[i] != '{' && formatString[i] != '}')
        {
            i++;
        }

        contents.Add(CreateTextContent(formatString.Substring(start, i - start)));
        return i;
    }

    // The token carries source text, so a backslash, a quote, or a line break must be escaped.
    // The format string holds the runtime value, and a raw value breaks the emitted literal.
    private static InterpolatedStringTextSyntax CreateTextContent(string text)
    {
        var escaped = EscapeText(text);

        return SyntaxFactory.InterpolatedStringText(
            SyntaxFactory.Token(
                SyntaxTriviaList.Empty,
                SyntaxKind.InterpolatedStringTextToken,
                escaped,
                text,
                SyntaxTriviaList.Empty));
    }

    private static string EscapeText(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            escaped.Append(character switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                _ => character.ToString()
            });
        }

        return escaped.ToString();
    }
}
