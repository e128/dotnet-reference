using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PresizeCollectionCodeFixProvider))]
[Shared]
public sealed class PresizeCollectionCodeFixProvider : E128CodeFixProviderBase
{
    private const string Title = "Pass the source count to the constructor";

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [PresizeCollectionAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.TryGetValue(PresizeCollectionAnalyzer.CapacityProperty, out var capacity)
            || capacity is null
            || capacity.Length == 0)
        {
            return Task.CompletedTask;
        }

        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            is not BaseObjectCreationExpressionSyntax creation)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                cancellationToken => ApplyFixAsync(context.Document, creation, capacity, cancellationToken),
                nameof(PresizeCollectionCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        BaseObjectCreationExpressionSyntax creation,
        string capacity,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var argumentList = SyntaxFactory.ArgumentList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Argument(SyntaxFactory.ParseExpression(capacity))));

        SyntaxNode replacement = creation switch
        {
            ObjectCreationExpressionSyntax explicitCreation => explicitCreation.WithArgumentList(argumentList),
            ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.WithArgumentList(argumentList),
            _ => creation
        };

        var newRoot = root.ReplaceNode(creation, replacement.WithTriviaFrom(creation));
        return document.WithSyntaxRoot(newRoot);
    }
}
