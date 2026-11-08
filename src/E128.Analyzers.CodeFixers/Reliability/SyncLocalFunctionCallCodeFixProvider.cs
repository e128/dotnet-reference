using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SyncLocalFunctionCallCodeFixProvider))]
[Shared]
public sealed class SyncLocalFunctionCallCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SyncLocalFunctionCallAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation
            || !AsyncSiblingCodeFixHelper.HasFixableEnclosingMethod(invocation))
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Replace synchronous call with await", nameof(SyncLocalFunctionCallCodeFixProvider), ct => ApplyFixAsync(context.Document, invocation, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || invocation.Parent is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        SyntaxNode nodeToReplace = string.Equals(memberAccess.Name.Identifier.Text, "Wait", StringComparison.Ordinal)
                                   && memberAccess.Parent is InvocationExpressionSyntax waitInvocation
            ? waitInvocation
            : memberAccess;

        var annotation = new SyntaxAnnotation("AwaitInserted");
        var awaitExpression = SyntaxFactory.AwaitExpression(invocation)
            .WithTriviaFrom(nodeToReplace)
            .WithAdditionalAnnotations(annotation);

        var newRoot = root.ReplaceNode(nodeToReplace, awaitExpression);

        if (newRoot.GetAnnotatedNodes(annotation).FirstOrDefault() is not AwaitExpressionSyntax insertedAwait)
        {
            return document.WithSyntaxRoot(newRoot);
        }

        newRoot = FixSyntax.PromoteContainingMethod(newRoot, insertedAwait);
        newRoot = UsingDirectives.AddIfMissing(newRoot, "System.Threading.Tasks");

        return document.WithSyntaxRoot(newRoot);
    }
}
