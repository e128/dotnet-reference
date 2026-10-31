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
            if (node is not LiteralExpressionSyntax)
            {
                continue;
            }

            diagnostic.Properties.TryGetValue(
                HardcodedTmpPathE128Analyzer.PropRelativeSuffix, out var suffix);

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Replace with Path.GetTempPath()",
                    ct => ReplaceWithGetTempPathAsync(context.Document, root, node, suffix, ct),
                    "ReplaceWithGetTempPath"),
                diagnostic);
        }
    }

    private static Task<Document> ReplaceWithGetTempPathAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode node,
        string? suffix,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        // A literal that names a subdirectory keeps that part, so the rewrite does not send every
        // caller to the bare temp root.
        var replacement = suffix is { Length: > 0 }
            ? BuildCombineCall(suffix).WithTriviaFrom(node)
            : SyntaxFactory.InvocationExpression(BuildPathMember("GetTempPath")).WithTriviaFrom(node);

        var newRoot = root.ReplaceNode(node, replacement);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }

    private static InvocationExpressionSyntax BuildCombineCall(string suffix)
    {
        return SyntaxFactory.InvocationExpression(
            BuildPathMember("Combine"),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(
                [
                    SyntaxFactory.Argument(
                        SyntaxFactory.InvocationExpression(BuildPathMember("GetTempPath"))),
                    SyntaxFactory.Argument(
                        SyntaxFactory.LiteralExpression(
                            SyntaxKind.StringLiteralExpression,
                            SyntaxFactory.Literal(suffix)))
                ])));
    }

    private static MemberAccessExpressionSyntax BuildPathMember(string memberName)
    {
        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("System"),
                    SyntaxFactory.IdentifierName("IO")),
                SyntaxFactory.IdentifierName("Path")),
            SyntaxFactory.IdentifierName(memberName));
    }
}
