using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Performance;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ListGetRangeCodeFixProvider))]
[Shared]
public sealed class ListGetRangeCodeFixProvider : E128CodeFixProviderBase
{
    private const string InteropNamespace = "System.Runtime.InteropServices";

    public override ImmutableArray<string> FixableDiagnosticIds => [ListGetRangeAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var node = root.FindNode(context.Diagnostics[0].Location.SourceSpan, getInnermostNodeForTie: true);

        if (node is ForStatementSyntax loop)
        {
            await RegisterLoopFixAsync(context, loop).ConfigureAwait(false);
            return;
        }

        var invocation = node
            .AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault();

        if (invocation is null)
        {
            return;
        }

        CodeFixRegistration.Register(context, context.Diagnostics[0], "Use CollectionsMarshal.AsSpan with Slice", ListGetRangeAnalyzer.DiagnosticId, ct => ApplyFixAsync(context.Document, invocation, ct));
    }

    private static async Task RegisterLoopFixAsync(CodeFixContext context, ForStatementSyntax loop)
    {
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        var listType = model?.Compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");

        if (model is null
            || listType is null
            || !ListGetRangeAnalyzer.TryMatchIndexerLoop(
                loop,
                model,
                listType,
                context.CancellationToken,
                out var list,
                out _)
            || list is null)
        {
            return;
        }

        CodeFixRegistration.Register(
            context,
            context.Diagnostics[0],
            "Enumerate CollectionsMarshal.AsSpan",
            ListGetRangeAnalyzer.DiagnosticId,
            ct => ApplyLoopFixAsync(context.Document, context.Diagnostics[0].Location.SourceSpan, ct));
    }

    private static async Task<Document> ApplyLoopFixAsync(
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var rootAndModel = await FixSyntax.GetRootAndSemanticModelAsync(document, cancellationToken).ConfigureAwait(false);
        if (rootAndModel is null)
        {
            return document;
        }

        var (root, model) = rootAndModel.Value;
        var listType = model.Compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");

        if (listType is null
            || root.FindNode(span, getInnermostNodeForTie: true) is not ForStatementSyntax loop
            || !ListGetRangeAnalyzer.TryMatchIndexerLoop(
                loop,
                model,
                listType,
                cancellationToken,
                out var list,
                out var reads)
            || list is null
            || loop.Declaration is not { Variables.Count: 1 } declaration)
        {
            return document;
        }

        var variableName = declaration.Variables[0].Identifier.ValueText;
        var asSpanCall = SyntaxFactory.ParseExpression(
            "CollectionsMarshal.AsSpan(" + list.WithoutTrivia().ToFullString() + ")");

        var body = loop.Statement.ReplaceNodes(
            reads,
            (original, _) => SyntaxFactory.IdentifierName(variableName).WithTriviaFrom(original));

        var newLoop = SyntaxFactory.ForEachStatement(
            SyntaxFactory.IdentifierName("var"),
            declaration.Variables[0].Identifier,
            asSpanCall,
            body);

        var replaced = root.ReplaceNode(loop, newLoop.WithTriviaFrom(loop));

        var alreadyImported = model.LookupNamespacesAndTypes(loop.SpanStart, name: "CollectionsMarshal").Length > 0;

        var newRoot = alreadyImported ? replaced : UsingDirectives.AddIfMissing(replaced, InteropNamespace);

        return document.WithSyntaxRoot(newRoot);
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
