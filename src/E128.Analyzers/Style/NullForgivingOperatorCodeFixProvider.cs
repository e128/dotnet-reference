using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Style;

/// <summary>
/// Code fix for E128043: removes the null-forgiving operator (<c>!</c>),
/// leaving the inner expression intact.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullForgivingOperatorCodeFixProvider))]
[Shared]
public sealed class NullForgivingOperatorCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [NullForgivingOperatorAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() =>
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

        if (node is not PostfixUnaryExpressionSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Remove null-forgiving operator",
                createChangedDocument: ct => ApplyFixAsync(context.Document, node, ct),
                equivalenceKey: nameof(NullForgivingOperatorCodeFixProvider)),
            diagnostic);
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

        // Replace expr! with expr, preserving the outer trivia.
        var innerExpression = postfix.Operand.WithTriviaFrom(postfix);
        var newRoot = root.ReplaceNode(postfix, innerExpression);
        return document.WithSyntaxRoot(newRoot);
    }
}
