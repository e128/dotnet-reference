using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Style;

/// <summary>
///     Code fix for E128043: removes the null-forgiving operator (<c lang="csharp">!</c>),
///     leaving the inner expression intact.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullForgivingOperatorCodeFixProvider))]
[Shared]
public sealed class NullForgivingOperatorCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [NullForgivingOperatorAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        RegisterNodeFix<PostfixUnaryExpressionSyntax>(
            context,
            root,
            "Remove null-forgiving operator",
            nameof(NullForgivingOperatorCodeFixProvider),
            (postfixUnary, ct) => ApplyFixAsync(context.Document, postfixUnary, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode diagnosticNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        if (diagnosticNode is not PostfixUnaryExpressionSyntax postfix)
        {
            return document;
        }

        var innerExpression = postfix.Operand.WithTriviaFrom(postfix);
        var newRoot = root.ReplaceNode(postfix, innerExpression);
        return document.WithSyntaxRoot(newRoot);
    }
}
