using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Style;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(EmptyStringLiteralCodeFixProvider))]
[Shared]
public sealed class EmptyStringLiteralCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [EmptyStringLiteralAnalyzer.DiagnosticId];

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

        if (node is not LiteralExpressionSyntax)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace \"\" with string.Empty",
                createChangedDocument: ct => ApplyFixAsync(context.Document, node, ct),
                equivalenceKey: nameof(EmptyStringLiteralCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode emptyLiteralNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Build: string.Empty — preserving the original literal's trivia.
        var stringEmpty = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                SyntaxFactory.IdentifierName("Empty"))
            .WithTriviaFrom(emptyLiteralNode);

        var newRoot = root.ReplaceNode(emptyLiteralNode, stringEmpty);
        return document.WithSyntaxRoot(newRoot);
    }
}
