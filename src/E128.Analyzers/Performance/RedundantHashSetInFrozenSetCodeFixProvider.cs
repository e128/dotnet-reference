using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RedundantHashSetInFrozenSetCodeFixProvider))]
[Shared]
public sealed class RedundantHashSetInFrozenSetCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RedundantHashSetInFrozenSetE128Analyzer.DiagnosticId];

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
            if (node is not InvocationExpressionSyntax invocation)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove intermediate HashSet",
                    ct => RemoveHashSetAsync(context.Document, root, invocation, ct),
                    "RemoveRedundantHashSet"),
                diagnostic);
        }
    }

    private static Task<Document> RemoveHashSetAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax toFrozenSetInvocation,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        if (toFrozenSetInvocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return Task.FromResult(document);
        }

        if (memberAccess.Expression is not ObjectCreationExpressionSyntax hashSetCreation)
        {
            return Task.FromResult(document);
        }

        var hashSetArgs = hashSetCreation.ArgumentList;
        var frozenSetArgs = toFrozenSetInvocation.ArgumentList;

        var collectionArg = hashSetArgs?.Arguments.FirstOrDefault();
        if (collectionArg is null)
        {
            return Task.FromResult(document);
        }

        var newInvocation = toFrozenSetInvocation
            .WithExpression(memberAccess.WithExpression(collectionArg.Expression))
            .WithArgumentList(frozenSetArgs);

        var newRoot = root.ReplaceNode(toFrozenSetInvocation, newInvocation.WithTriviaFrom(toFrozenSetInvocation));
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
