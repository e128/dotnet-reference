using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
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
public sealed class StringFormatToInterpolationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [StringFormatToInterpolationAnalyzer.DiagnosticId];

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

        if (node is not InvocationExpressionSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Convert to string interpolation",
                createChangedDocument: ct => ApplyFixAsync(context.Document, node, ct),
                equivalenceKey: nameof(StringFormatToInterpolationCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode invocationNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        var invocation = (InvocationExpressionSyntax)invocationNode;
        var interpolated = BuildFromInvocation(invocation, semanticModel, cancellationToken);
        if (interpolated is null)
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
        var contents = new System.Collections.Generic.List<InterpolatedStringContentSyntax>();
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
            else if (formatString[i] == '}' && i + 1 < formatString.Length && formatString[i + 1] == '}')
            {
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
        System.Collections.Generic.List<InterpolatedStringContentSyntax> contents)
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
        System.Collections.Generic.List<InterpolatedStringContentSyntax> contents)
    {
        var i = start;
        while (i < formatString.Length && formatString[i] != '{' && formatString[i] != '}')
        {
            i++;
        }

        contents.Add(CreateTextContent(formatString.Substring(start, i - start)));
        return i;
    }

    private static InterpolatedStringTextSyntax CreateTextContent(string text)
    {
        return SyntaxFactory.InterpolatedStringText(
            SyntaxFactory.Token(
                SyntaxTriviaList.Empty,
                SyntaxKind.InterpolatedStringTextToken,
                text,
                text,
                SyntaxTriviaList.Empty));
    }
}
