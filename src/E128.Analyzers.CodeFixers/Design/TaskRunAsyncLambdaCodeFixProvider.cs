using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace E128.Analyzers.Design;

/// <summary>
///     Code fix for E128036: removes the <c lang="csharp">Task.Run</c> wrapper and keeps the inner async expression.
///     <c lang="csharp">Task.Run(async () => await DoWorkAsync())</c> becomes <c lang="csharp">DoWorkAsync()</c>.
///     <c lang="csharp">Task.Run(async () => { await A(); await B(); })</c> is not auto-fixable (block body).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskRunAsyncLambdaCodeFixProvider))]
[Shared]
public sealed class TaskRunAsyncLambdaCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TaskRunAsyncLambdaAnalyzer.DiagnosticId];

    protected override async Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;

        if (args.Count is not 1)
        {
            return;
        }

        var lambda = args[0].Expression;

        var innerExpression = GetExpressionBodyFromLambda(lambda);
        if (innerExpression is not null)
        {
            CodeFixRegistration.Register(context, diagnostic, "Remove Task.Run wrapper", nameof(TaskRunAsyncLambdaCodeFixProvider), ct => RemoveTaskRunAsync(context.Document, root, invocation, innerExpression, ct));
            return;
        }

        if (invocation.Parent is not AwaitExpressionSyntax { Parent: ExpressionStatementSyntax statement }
            || lambda is not AnonymousFunctionExpressionSyntax { Body: BlockSyntax block })
        {
            return;
        }

        var semanticModel = await context.Document
            .GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);
        if (semanticModel is null || !CanInlineBlock(block, statement, semanticModel))
        {
            return;
        }

        CodeFixRegistration.Register(
            context,
            diagnostic,
            "Inline the awaited Task.Run body",
            nameof(TaskRunAsyncLambdaCodeFixProvider),
            ct => InlineBlockAsync(context.Document, statement, block, ct));
    }

    private static bool CanInlineBlock(
        BlockSyntax block,
        ExpressionStatementSyntax statement,
        SemanticModel semanticModel)
    {
        if (block.DescendantNodes(StopAtNestedFunction)
            .Any(static node => node is ReturnStatementSyntax or YieldStatementSyntax))
        {
            return false;
        }

        foreach (var name in DeclaredNames(block))
        {
            if (semanticModel.LookupSymbols(statement.SpanStart, name: name).Length > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> DeclaredNames(BlockSyntax block)
    {
        foreach (var node in block.DescendantNodes(StopAtNestedFunction))
        {
            switch (node)
            {
                case VariableDeclaratorSyntax variable:
                    yield return variable.Identifier.ValueText;
                    break;
                case ForEachStatementSyntax forEach:
                    yield return forEach.Identifier.ValueText;
                    break;
                case SingleVariableDesignationSyntax designation:
                    yield return designation.Identifier.ValueText;
                    break;
                case LocalFunctionStatementSyntax localFunction:
                    yield return localFunction.Identifier.ValueText;
                    break;
                default:
                    continue;
            }
        }
    }

    private static bool StopAtNestedFunction(SyntaxNode node)
    {
        return node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax;
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

    private static async Task<Document> InlineBlockAsync(
        Document document,
        ExpressionStatementSyntax statement,
        BlockSyntax block,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var inlined = SyntaxFactory.Block(block.Statements)
            .WithLeadingTrivia(statement.GetLeadingTrivia())
            .WithTrailingTrivia(statement.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.ReplaceNode(statement, inlined);
        return document.WithSyntaxRoot(newRoot);
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
