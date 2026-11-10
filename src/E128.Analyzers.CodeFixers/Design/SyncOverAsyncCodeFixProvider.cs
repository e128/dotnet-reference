using System;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SyncOverAsyncCodeFixProvider))]
[Shared]
public sealed class SyncOverAsyncCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SyncOverAsyncAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        // The await must land in an async member. A lambda, local function, or property accessor
        // cannot take the async modifier from this fix, and an await there does not compile.
        if (!IsInOrdinaryMethod(node))
        {
            return Task.CompletedTask;
        }

        CodeFixRegistration.Register(context, diagnostic, "Replace with await", nameof(SyncOverAsyncCodeFixProvider), ct => ApplyFixAsync(context.Document, node, ct));
        return Task.CompletedTask;
    }

    private static async Task<Document> ApplyFixAsync(
        Document document,
        SyntaxNode diagnosticNode,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        // Determine what expression to await and what node to replace.
        if (!TryGetAwaitTarget(diagnosticNode, out var nodeToReplace, out var taskExpression))
        {
            return document;
        }

        // Annotate the await expression so we can find it in the new tree.
        var annotation = new SyntaxAnnotation("AwaitInserted");
        var awaitExpression = SyntaxFactory.AwaitExpression(taskExpression)
            .WithTriviaFrom(nodeToReplace)
            .WithAdditionalAnnotations(annotation);

        var newRoot = root.ReplaceNode(nodeToReplace, awaitExpression);

        // Find the annotated node in the new tree.
        if (newRoot.GetAnnotatedNodes(annotation).FirstOrDefault() is not AwaitExpressionSyntax insertedAwait)
        {
            return document.WithSyntaxRoot(newRoot);
        }

        // Promote the containing method to async.
        newRoot = FixSyntax.PromoteContainingMethod(newRoot, insertedAwait);
        newRoot = UsingDirectives.AddIfMissing(newRoot, "System.Threading.Tasks");

        return document.WithSyntaxRoot(newRoot);
    }

    private static bool IsInOrdinaryMethod(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is AnonymousFunctionExpressionSyntax
                or LocalFunctionStatementSyntax
                or AccessorDeclarationSyntax
                or PropertyDeclarationSyntax
                or IndexerDeclarationSyntax)
            {
                return false;
            }

            if (ancestor is MethodDeclarationSyntax)
            {
                return true;
            }

            if (ancestor is BaseTypeDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryGetAwaitTarget(
        SyntaxNode diagnosticNode,
        out SyntaxNode nodeToReplace,
        [NotNullWhen(true)] out ExpressionSyntax? taskExpression)
    {
        nodeToReplace = diagnosticNode;
        taskExpression = null;

        // Case 1: .Result — diagnostic is on the MemberAccessExpression "task.Result"
        if (diagnosticNode is MemberAccessExpressionSyntax memberAccess
            && string.Equals(memberAccess.Name.Identifier.ValueText, "Result", StringComparison.Ordinal))
        {
            taskExpression = memberAccess.Expression;
            nodeToReplace = memberAccess;
            return true;
        }

        // Case 2: .GetAwaiter() — diagnostic is on the "GetAwaiter" identifier.
        // The full pattern is: task.GetAwaiter().GetResult()
        // We need to find the outermost invocation and extract the task expression.
        // Case 2: .GetAwaiter() — diagnostic is on the "GetAwaiter" identifier.
        // Walk up: GetAwaiter name -> MemberAccess (task.GetAwaiter) -> Invocation (task.GetAwaiter())
        //       -> MemberAccess (.GetResult) -> Invocation (.GetResult())
        if (diagnosticNode is not IdentifierNameSyntax getAwaiterName
            || !string.Equals(getAwaiterName.Identifier.ValueText, "GetAwaiter", StringComparison.Ordinal)
            || diagnosticNode.Parent is not MemberAccessExpressionSyntax getAwaiterAccess
            || getAwaiterAccess.Parent is not InvocationExpressionSyntax getAwaiterInvocation
            || getAwaiterInvocation.Parent is not MemberAccessExpressionSyntax getResultAccess
            || getResultAccess.Parent is not InvocationExpressionSyntax getResultInvocation)
        {
            return false;
        }

        taskExpression = getAwaiterAccess.Expression;
        nodeToReplace = getResultInvocation;
        return true;
    }

}
