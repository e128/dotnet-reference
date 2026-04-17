using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128036: removes the <c>Task.Run</c> wrapper and keeps the inner async expression.
///     <c>Task.Run(async () => await DoWorkAsync())</c> becomes <c>DoWorkAsync()</c>.
///     <c>Task.Run(async () => { await A(); await B(); })</c> is not auto-fixable (block body).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskRunAsyncLambdaCodeFixProvider))]
[Shared]
public sealed class TaskRunAsyncLambdaCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TaskRunAsyncLambdaAnalyzer.DiagnosticId];

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

        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;
        if (!args.Any())
        {
            return;
        }

        var lambda = args[0].Expression;

        // Only fix expression-body lambdas — block bodies are too structural.
        var innerExpression = GetExpressionBodyFromLambda(lambda);
        if (innerExpression is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Remove Task.Run wrapper",
                ct => RemoveTaskRunAsync(context.Document, root, invocation, innerExpression, ct),
                nameof(TaskRunAsyncLambdaCodeFixProvider)),
            diagnostic);
    }

    private static ExpressionSyntax? GetExpressionBodyFromLambda(ExpressionSyntax lambda)
    {
        return lambda switch
        {
            ParenthesizedLambdaExpressionSyntax { ExpressionBody: { } body } => UnwrapAwait(body),
            SimpleLambdaExpressionSyntax { ExpressionBody: { } body } => UnwrapAwait(body),
            _ => null
        };
    }

    private static ExpressionSyntax UnwrapAwait(ExpressionSyntax expression)
    {
        return expression is AwaitExpressionSyntax awaitExpr
            ? awaitExpr.Expression
            : expression;
    }

    private static Task<Document> RemoveTaskRunAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax replacement,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var newExpression = replacement.WithTriviaFrom(invocation);
        var newRoot = root.ReplaceNode(invocation, newExpression);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
