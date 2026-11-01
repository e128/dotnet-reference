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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ListGetRangeCodeFixProvider))]
[Shared]
public sealed class ListGetRangeCodeFixProvider : CodeFixProvider
{
    private const string InteropNamespace = "System.Runtime.InteropServices";

    public override ImmutableArray<string> FixableDiagnosticIds => [ListGetRangeAnalyzer.DiagnosticId];

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

        if (invocation is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Use CollectionsMarshal.AsSpan with Slice",
                ct => ApplyFixAsync(context.Document, invocation, ct),
                ListGetRangeAnalyzer.DiagnosticId),
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

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var alreadyImported = model is not null
            && model.LookupNamespacesAndTypes(invocation.SpanStart, name: "CollectionsMarshal").Length > 0;

        var receiver = memberAccess.Expression;
        var startArg = invocation.ArgumentList.Arguments[0].Expression;
        var countArg = invocation.ArgumentList.Arguments[1].Expression;

        // Build: CollectionsMarshal.AsSpan(list).Slice(start, count).ToArray()
        var asSpanCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("CollectionsMarshal"),
                SyntaxFactory.IdentifierName("AsSpan")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(receiver))));

        var sliceCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                asSpanCall,
                SyntaxFactory.IdentifierName("Slice")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SeparatedList(new[]
                {
                    SyntaxFactory.Argument(startArg),
                    SyntaxFactory.Argument(countArg)
                })));

        var toArrayCall = SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                sliceCall,
                SyntaxFactory.IdentifierName("ToArray")));

        var replaced = root.ReplaceNode(invocation, toArrayCall.WithTriviaFrom(invocation));
        var newRoot = alreadyImported ? replaced : UsingDirectives.AddIfMissing(replaced, InteropNamespace);
        return document.WithSyntaxRoot(newRoot);
    }
}
