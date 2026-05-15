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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(QuerySelectorAllMaterializationCodeFixProvider))]
[Shared]
public sealed class QuerySelectorAllMaterializationCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [QuerySelectorAllMaterializationAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
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

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add .ToList() to materialize QuerySelectorAll result",
                ct => ApplyFixAsync(context.Document, invocation, ct),
                nameof(QuerySelectorAllMaterializationCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        InvocationExpressionSyntax qsaInvocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var toListAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            qsaInvocation.WithoutTrivia(),
            SyntaxFactory.IdentifierName("ToList"));

        var toListInvocation = SyntaxFactory.InvocationExpression(
                toListAccess,
                SyntaxFactory.ArgumentList())
            .WithTriviaFrom(qsaInvocation);

        var newRoot = root.ReplaceNode(qsaInvocation, toListInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
