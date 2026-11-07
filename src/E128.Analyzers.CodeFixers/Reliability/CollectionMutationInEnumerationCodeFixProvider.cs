using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128155: materializes the enumerated collection with
///     <c lang="csharp">ToArray()</c>, so the loop walks a copy and the body can mutate the
///     original.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CollectionMutationInEnumerationCodeFixProvider))]
[Shared]
public sealed class CollectionMutationInEnumerationCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [CollectionMutationInEnumerationAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var forEach = node.FirstAncestorOrSelf<CommonForEachStatementSyntax>();

        if (forEach is null)
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Iterate a copy", nameof(CollectionMutationInEnumerationCodeFixProvider), ct => MaterializeAsync(context.Document, forEach, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> MaterializeAsync(
        Document document,
        CommonForEachStatementSyntax forEach,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        var collection = forEach.Expression;
        var collectionText = collection.ToString();
        var materialized = SyntaxFactory.ParseExpression($"{collectionText}.ToArray()")
            .WithTriviaFrom(collection);

        var newRoot = root.ReplaceNode(collection, materialized);

        // List<T> and the other mutable collections declare an instance ToArray(), so the using is
        // only needed for an enumerable that resolves through Enumerable.ToArray.
        if (!await HasInstanceToArrayAsync(document, collection, cancellationToken).ConfigureAwait(false))
        {
            newRoot = UsingDirectives.AddIfMissing(newRoot, "System.Linq");
        }

        return document.WithSyntaxRoot(newRoot);
    }

    private static async Task<bool> HasInstanceToArrayAsync(
        Document document,
        ExpressionSyntax collection,
        CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

        if (model is null)
        {
            return false;
        }

        var type = model.GetTypeInfo(collection, cancellationToken).Type;

        if (type is null)
        {
            return false;
        }

        foreach (var member in type.GetMembers("ToArray"))
        {
            if (!member.IsStatic && member is IMethodSymbol { Parameters.Length: 0 })
            {
                return true;
            }
        }

        return false;
    }
}
