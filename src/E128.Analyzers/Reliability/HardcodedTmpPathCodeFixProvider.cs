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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(HardcodedTmpPathCodeFixProvider))]
[Shared]
public sealed class HardcodedTmpPathCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [HardcodedTmpPathE128Analyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not LiteralExpressionSyntax)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Replace with Path.GetTempPath()",
                    createChangedDocument: ct => ReplaceWithGetTempPathAsync(context.Document, root, node, ct),
                    equivalenceKey: "ReplaceWithGetTempPath"),
                diagnostic);
        }
    }

    private static Task<Document> ReplaceWithGetTempPathAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var getTempPath = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("System"),
                        SyntaxFactory.IdentifierName("IO")),
                    SyntaxFactory.IdentifierName("Path")),
                SyntaxFactory.IdentifierName("GetTempPath")));

        var replacement = getTempPath.WithTriviaFrom(node);
        var newRoot = root.ReplaceNode(node, replacement);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
