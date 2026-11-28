using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StringFormatToInterpolationCodeFixProvider))]
[Shared]
public sealed class StringFormatToInterpolationCodeFixProvider : E128CodeFixProviderBase
{
    private const int DoubledBraceLength = 2;

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [StringFormatToInterpolationAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var rootAndModel = await FixSyntax.GetRootAndSemanticModelAsync(context.Document, context.CancellationToken).ConfigureAwait(false);
        if (rootAndModel is null)
        {
            return;
        }

        var semanticModel = rootAndModel.Value.SemanticModel;
        var diagnostic = context.Diagnostics[0];

        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation
            || BuildFromInvocation(invocation, semanticModel, context.CancellationToken) is not { } interpolated)
        {
            return;
        }

        CodeFixRegistration.Register(
            context,
            diagnostic,
            "Convert to string interpolation",
            nameof(StringFormatToInterpolationCodeFixProvider),
            ct => ApplyFixAsync(context.Document, invocation, interpolated, ct));
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
        var literalStart = 0;
        var i = 0;

        while (i < formatString.Length)
        {
            if (formatString[i] == '{')
            {
                if (i + 1 < formatString.Length && formatString[i + 1] == '{')
                {
                    i += DoubledBraceLength;
                    continue;
                }

                AddLiteralText(formatString, literalStart, i, contents);
                i = ProcessPlaceholder(formatString, i, arguments, contents);
                if (i < 0)
                {
                    return null;
                }

                literalStart = i;
            }
            else if (formatString[i] != '}')
            {
                i++;
            }
            else if (i + 1 < formatString.Length && formatString[i + 1] == '}')
            {
                i += DoubledBraceLength;
            }
            else
            {
                return null;
            }
        }

        AddLiteralText(formatString, literalStart, formatString.Length, contents);

        return SyntaxFactory.InterpolatedStringExpression(
            SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken),
            SyntaxFactory.List(contents));
    }

    private static void AddLiteralText(
        string formatString,
        int start,
        int end,
        List<InterpolatedStringContentSyntax> contents)
    {
        if (start >= end)
        {
            return;
        }

        contents.Add(CreateTextContent(formatString.Substring(start, end - start)));
    }

    private static int ProcessPlaceholder(
        string formatString,
        int start,
        ExpressionSyntax[] arguments,
        List<InterpolatedStringContentSyntax> contents)
    {
        var closeIndex = formatString.IndexOf('}', start);
        if (closeIndex < 0)
        {
            return -1;
        }

        var placeholder = formatString.Substring(start + 1, closeIndex - start - 1);
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
                _ => character.ToString(CultureInfo.InvariantCulture)
            });
        }

        return escaped.ToString();
    }
}
