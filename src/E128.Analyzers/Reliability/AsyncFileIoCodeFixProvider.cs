using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E128.Analyzers.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncFileIoCodeFixProvider))]
[Shared]
public sealed class AsyncFileIoCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [AsyncFileIoAnalyzer.DiagnosticId];

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
        if (root.FindNode(diagnostic.Location.SourceSpan) is not InvocationExpressionSyntax invocation
            || invocation.Expression is not MemberAccessExpressionSyntax
            || !HasFixableEnclosingMethod(invocation))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Await the async overload",
                ct => ApplyFixAsync(context.Document, invocation, ct),
                nameof(AsyncFileIoCodeFixProvider)),
            diagnostic);
    }

    // PromoteContainingMethod (reused below) only understands MethodDeclarationSyntax.
    // Local functions/constructors/lambdas are left to the analyzer-only diagnostic --
    // promoting the wrong enclosing scope there would produce an await outside an async body.
    private static bool HasFixableEnclosingMethod(SyntaxNode node)
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

    private static async Task<Document> ApplyFixAsync(
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
