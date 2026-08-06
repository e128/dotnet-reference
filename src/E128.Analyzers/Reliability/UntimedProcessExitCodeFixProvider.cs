using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace E128.Analyzers.Reliability;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UntimedProcessExitCodeFixProvider))]
[Shared]
public sealed class UntimedProcessExitCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UntimedProcessExitAnalyzer.DiagnosticId];

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
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (string.Equals(memberAccess.Name.Identifier.ValueText, "WaitForExit", StringComparison.Ordinal))
        {
            RegisterSyncFix(context, diagnostic, memberAccess, invocation);
        }
        else
        {
            RegisterAsyncFix(context, diagnostic, memberAccess, invocation);
        }
    }

    // Only the no-argument form is fixable here: it is always an ExpressionStatement (WaitForExit()
    // returns void), so there is no call-site ambiguity about how the result is consumed.
    private static void RegisterSyncFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        MemberAccessExpressionSyntax memberAccess,
        InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count != 0 || invocation.Parent is not ExpressionStatementSyntax statement)
        {
            return;
        }

        var receiverText = memberAccess.Expression.ToString();
        context.RegisterCodeFix(
            CodeAction.Create(
                "Add a timeout and Kill fallback",
                ct => ReplaceStatementAsync(context.Document, statement, BuildSyncReplacement(receiverText), ct),
                nameof(UntimedProcessExitCodeFixProvider)),
            diagnostic);
    }

    // Only no-arg WaitForExitAsync() and WaitForExitAsync(CancellationToken.None) are fixable:
    // neither carries a real caller-supplied token, so adding a fresh timeout token doesn't drop
    // any caller cancellation semantics. A parameter-sourced token is left diagnostic-only —
    // silently replacing the caller's token would be a behavior regression, not a safe fix.
    private static void RegisterAsyncFix(
        CodeFixContext context,
        Diagnostic diagnostic,
        MemberAccessExpressionSyntax memberAccess,
        InvocationExpressionSyntax invocation)
    {
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count > 1
            || (arguments.Count == 1 && !IsCancellationTokenNoneArgument(arguments[0].Expression))
            || invocation.Parent is not AwaitExpressionSyntax awaitExpression
            || awaitExpression.Parent is not ExpressionStatementSyntax statement)
        {
            return;
        }

        var receiverText = memberAccess.Expression.ToString();
        context.RegisterCodeFix(
            CodeAction.Create(
                "Add a timeout CancellationTokenSource with a Kill fallback",
                ct => ReplaceStatementAsync(context.Document, statement, BuildAsyncReplacement(receiverText), ct),
                nameof(UntimedProcessExitCodeFixProvider)),
            diagnostic);
    }

    private static bool IsCancellationTokenNoneArgument(ExpressionSyntax expression)
    {
        return expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "None" };
    }

    private static StatementSyntax BuildSyncReplacement(string receiverText)
    {
        return SyntaxFactory.ParseStatement($$"""
            if (!{{receiverText}}.WaitForExit(TimeSpan.FromSeconds(30)))
            {
                {{receiverText}}.Kill();
            }
            """);
    }

    private static StatementSyntax BuildAsyncReplacement(string receiverText)
    {
        return SyntaxFactory.ParseStatement($$"""
            {
                using var __timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    await {{receiverText}}.WaitForExitAsync(__timeoutCts.Token);
                }
                catch (OperationCanceledException) when (__timeoutCts.IsCancellationRequested)
                {
                    {{receiverText}}.Kill(true);
                }
            }
            """);
    }

    private static async Task<Document> ReplaceStatementAsync(
        Document document,
        StatementSyntax oldStatement,
        StatementSyntax newStatement,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var replacement = newStatement
            .WithLeadingTrivia(oldStatement.GetLeadingTrivia())
            .WithTrailingTrivia(oldStatement.GetTrailingTrivia())
            .WithAdditionalAnnotations(Formatter.Annotation);

        var newRoot = root.ReplaceNode(oldStatement, replacement);
        return document.WithSyntaxRoot(newRoot);
    }
}
