using System;
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

namespace E128.Analyzers.Design;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SyncOverAsyncCodeFixProvider))]
[Shared]
public sealed class SyncOverAsyncCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [SyncOverAsyncAnalyzer.DiagnosticId];

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

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        context.RegisterCodeFix(
            CodeAction.Create(
                "Replace with await",
                ct => ApplyFixAsync(context.Document, node, ct),
                nameof(SyncOverAsyncCodeFixProvider)),
            diagnostic);
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
        newRoot = PromoteContainingMethod(newRoot, insertedAwait);

        return document.WithSyntaxRoot(newRoot);
    }

    private static bool TryGetAwaitTarget(
        SyntaxNode diagnosticNode,
        out SyntaxNode nodeToReplace,
        out ExpressionSyntax taskExpression)
    {
        nodeToReplace = diagnosticNode;
        taskExpression = null!;

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
        if (diagnosticNode is IdentifierNameSyntax { Identifier.ValueText: "GetAwaiter" }
            && diagnosticNode.Parent is MemberAccessExpressionSyntax getAwaiterAccess
            && getAwaiterAccess.Parent is InvocationExpressionSyntax getAwaiterInvocation
            && getAwaiterInvocation.Parent is MemberAccessExpressionSyntax getResultAccess
            && getResultAccess.Parent is InvocationExpressionSyntax getResultInvocation)
        {
            taskExpression = getAwaiterAccess.Expression;
            nodeToReplace = getResultInvocation;
            return true;
        }

        return false;
    }

    private static SyntaxNode PromoteContainingMethod(
        SyntaxNode root,
        AwaitExpressionSyntax awaitExpression)
    {
        // Find the containing method declaration in the NEW tree.
        var method = awaitExpression.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return root;
        }

        // Already async — nothing to do.
        if (method.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            return root;
        }

        var newMethod = method;

        // Add async modifier.
        var asyncToken = SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
            .WithTrailingTrivia(SyntaxFactory.Space);
        newMethod = newMethod.WithModifiers(newMethod.Modifiers.Add(asyncToken));

        // Update return type.
        newMethod = UpdateReturnType(newMethod);

        return root.ReplaceNode(method, newMethod);
    }

    private static MethodDeclarationSyntax UpdateReturnType(
        MethodDeclarationSyntax method)
    {
        var returnType = method.ReturnType;
        var returnTypeText = returnType.ToString().Trim();

        // void -> Task
        if (string.Equals(returnTypeText, "void", StringComparison.Ordinal))
        {
            return method.WithReturnType(
                SyntaxFactory.IdentifierName("Task")
                    .WithTriviaFrom(returnType));
        }

        // T -> Task<T> (for non-void, non-Task return types)
        if (!returnTypeText.StartsWith("Task", StringComparison.Ordinal)
            && !returnTypeText.StartsWith("ValueTask", StringComparison.Ordinal))
        {
            var taskOfT = SyntaxFactory.GenericName(
                SyntaxFactory.Identifier("Task"),
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SingletonSeparatedList(returnType.WithoutTrivia())));

            return method.WithReturnType(taskOfT.WithTriviaFrom(returnType));
        }

        return method;
    }
}
