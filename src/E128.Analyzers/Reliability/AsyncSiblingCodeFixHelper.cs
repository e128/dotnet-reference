using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Shared rewrite for every "blocking-call-with-async-sibling" code fix (E128092, E128093,
///     ...): rename the member to its <c lang="csharp">Async</c> sibling, wrap it in <c lang="csharp">await</c>, and promote
///     the containing method to <c lang="csharp">async Task</c>/<c lang="csharp">async Task&lt;T&gt;</c> if needed.
/// </summary>
internal static class AsyncSiblingCodeFixHelper
{
    // PromoteContainingMethod (reused below) only understands MethodDeclarationSyntax.
    // Local functions/constructors/lambdas are left to the analyzer-only diagnostic --
    // promoting the wrong enclosing scope there would produce an await outside an async body.
    public static bool HasFixableEnclosingMethod(SyntaxNode node)
    {
        return node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault() is { } method
               && node.Ancestors()
                   .TakeWhile(a => a != method)
                   .All(a => a is not LocalFunctionStatementSyntax
                       and not ConstructorDeclarationSyntax
                       and not ParenthesizedLambdaExpressionSyntax
                       and not SimpleLambdaExpressionSyntax
                       and not AnonymousMethodExpressionSyntax);
    }

    public static async Task<Document> ApplyFixAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        var asyncName = memberAccess.Name.Identifier.ValueText + "Async";
        var asyncInvocation = invocation.WithExpression(
            memberAccess.WithName(SyntaxFactory.IdentifierName(asyncName).WithTriviaFrom(memberAccess.Name)));

        var annotation = new SyntaxAnnotation("AwaitInserted");
        var awaitExpression = SyntaxFactory.AwaitExpression(asyncInvocation)
            .WithTriviaFrom(invocation)
            .WithAdditionalAnnotations(annotation);

        var newRoot = root.ReplaceNode(invocation, awaitExpression);

        if (newRoot.GetAnnotatedNodes(annotation).FirstOrDefault() is not AwaitExpressionSyntax insertedAwait)
        {
            return document.WithSyntaxRoot(newRoot);
        }

        newRoot = SyncOverAsyncCodeFixProvider.PromoteContainingMethod(newRoot, insertedAwait);
        newRoot = AddTaskUsingIfMissing(newRoot);

        return document.WithSyntaxRoot(newRoot);
    }

    private static SyntaxNode AddTaskUsingIfMissing(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        if (compilationUnit.Usings.Any(u =>
                string.Equals(u.Name?.ToString(), "System.Threading.Tasks", StringComparison.Ordinal)))
        {
            return root;
        }

        var usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.QualifiedName(
                    SyntaxFactory.QualifiedName(
                        SyntaxFactory.IdentifierName("System"),
                        SyntaxFactory.IdentifierName("Threading")),
                    SyntaxFactory.IdentifierName("Tasks")))
            .NormalizeWhitespace()
            .WithTrailingTrivia(SyntaxFactory.LineFeed);

        return compilationUnit.AddUsings(usingDirective);
    }
}
