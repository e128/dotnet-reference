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

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ImmutableArrayCreateToArrayCodeFixProvider))]
[Shared]
public sealed class ImmutableArrayCreateToArrayCodeFixProvider : CodeFixProvider
{
    private const string InteropNamespace = "System.Runtime.InteropServices";

    public override ImmutableArray<string> FixableDiagnosticIds => [ImmutableArrayCreateToArrayAnalyzer.DiagnosticId];

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

        var invocation = root.FindNode(context.Diagnostics[0].Location.SourceSpan)
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();

        // An explicit type argument can widen the result, for example Create<Base>(derived). The
        // rewrite infers the element type from the array, so it would change the type. Leave the
        // diagnostic without a fix.
        if (invocation is null
            || invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax })
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use ImmutableCollectionsMarshal.AsImmutableArray",
                ct => ApplyFixAsync(context.Document, invocation, ct),
                ImmutableArrayCreateToArrayAnalyzer.DiagnosticId),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var alreadyImported = model is not null
            && model.LookupNamespacesAndTypes(invocation.SpanStart, name: "ImmutableCollectionsMarshal").Length > 0;

        var argument = invocation.ArgumentList.Arguments[0].Expression;

        var replacement = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("ImmutableCollectionsMarshal"),
                SyntaxFactory.IdentifierName("AsImmutableArray")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(argument))));

        var replaced = root.ReplaceNode(invocation, replacement.WithTriviaFrom(invocation));
        var newRoot = alreadyImported ? replaced : UsingDirectives.AddIfMissing(replaced, InteropNamespace);
        return document.WithSyntaxRoot(newRoot);
    }
}
