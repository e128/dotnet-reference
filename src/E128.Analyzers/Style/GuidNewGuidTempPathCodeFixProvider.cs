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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(GuidNewGuidTempPathCodeFixProvider))]
[Shared]
public sealed class GuidNewGuidTempPathCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [GuidNewGuidTempPathE128Analyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

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
            if (node is not InvocationExpressionSyntax)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Replace with Path.GetRandomFileName()",
                    ct => ReplaceWithGetRandomFileNameAsync(context.Document, root, node, ct),
                    "ReplaceWithGetRandomFileName"),
                diagnostic);
        }
    }

    private static Task<Document> ReplaceWithGetRandomFileNameAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var replacement = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("Path"),
                    SyntaxFactory.IdentifierName("GetRandomFileName")))
            .WithTriviaFrom(node);

        var newRoot = root.ReplaceNode(node, replacement);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
