using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
/// Code fix for E128040: replaces literal 0 with <c>Environment.ProcessorCount</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConcurrencyLimitCodeFixProvider))]
[Shared]
public sealed class ConcurrencyLimitCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ConcurrencyLimitAnalyzer.DiagnosticId];

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

        // The diagnostic location may be on the whole expression (ObjectCreation, Invocation)
        // or on a literal (assignment RHS). Find the literal 0 within the span.
        var zeroLiteral = FindZeroLiteral(node);
        if (zeroLiteral is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace 0 with Environment.ProcessorCount",
                createChangedDocument: ct => ApplyFixAsync(context.Document, zeroLiteral, ct),
                equivalenceKey: nameof(ConcurrencyLimitCodeFixProvider)),
            diagnostic);
    }

    private static LiteralExpressionSyntax? FindZeroLiteral(SyntaxNode node)
    {
        // Direct literal node.
        if (node is LiteralExpressionSyntax directLiteral
            && directLiteral.Token.Value is int directVal
            && directVal == 0)
        {
            return directLiteral;
        }

        // Search descendants for the first literal 0 in an argument position.
        return node.DescendantNodes()
            .OfType<LiteralExpressionSyntax>()
            .FirstOrDefault(lit => lit.Token.Value is int val && val == 0);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode literalNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacement = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("Environment"),
                SyntaxFactory.IdentifierName("ProcessorCount"))
            .WithTriviaFrom(literalNode);

        var newRoot = root.ReplaceNode(literalNode, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
