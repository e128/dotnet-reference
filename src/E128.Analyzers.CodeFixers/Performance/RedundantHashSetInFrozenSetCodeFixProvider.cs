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
public sealed class RedundantHashSetInFrozenSetCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [RedundantHashSetInFrozenSetE128Analyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is not InvocationExpressionSyntax invocation
                || !IsRewritable(semanticModel, invocation, context.CancellationToken))
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

    // The rewrite passes the constructor argument straight to ToFrozenSet(). A capacity, a comparer,
    // or a collection initializer carries behavior that the rewrite would drop. A collection
    // expression has no type of its own, so the rewrite leaves it without a target type.
    private static bool IsRewritable(
        SemanticModel semanticModel,
        InvocationExpressionSyntax toFrozenSetInvocation,
        CancellationToken cancellationToken)
    {
        return toFrozenSetInvocation.Expression
                   is MemberAccessExpressionSyntax { Expression: ObjectCreationExpressionSyntax hashSetCreation }
               && hashSetCreation.Initializer is null
               && hashSetCreation.ArgumentList?.Arguments.FirstOrDefault()
                   is { Expression: not CollectionExpressionSyntax }
               && semanticModel.GetSymbolInfo(hashSetCreation, cancellationToken).Symbol is IMethodSymbol constructor
               && constructor.Parameters.Length == 1
               && constructor.Parameters[0].Type is INamedTypeSymbol parameterType
               && parameterType.OriginalDefinition.SpecialType
                   == SpecialType.System_Collections_Generic_IEnumerable_T;
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
