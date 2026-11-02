using System;
using System.Collections.Generic;
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

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix for E128028: converts sync I/O calls to their async equivalents,
///     adds <c lang="csharp">async</c> modifier, inserts <c lang="csharp">await</c>, and removes <c lang="csharp">Task.FromResult</c> wrapper.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskFromResultSyncIoCodeFixProvider))]
[Shared]
public sealed class TaskFromResultSyncIoCodeFixProvider : E128CodeFixProviderBase
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TaskFromResultSyncIoAnalyzer.DiagnosticId];

    protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
    {
        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax invocation)
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                "Convert to async/await",
                ct => ConvertToAsyncAsync(context.Document, invocation, ct),
                nameof(TaskFromResultSyncIoCodeFixProvider)),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> ConvertToAsyncAsync(
        Document document,
        InvocationExpressionSyntax fromResultInvocation,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return document;
        }

        var method = fromResultInvocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return document;
        }

        var newMethod = method;

        // 1. Replace sync I/O calls with async equivalents wrapped in await. The rewrite reads the
        // same gated table as the analyzer, so a pair whose async member is absent is left alone.
        var availableSyncIoMethods =
            TaskFromResultSyncIoAnalyzer.FindAvailableSyncIoMethods(semanticModel.Compilation);
        var syncIoOwnerTypes =
            TaskFromResultSyncIoAnalyzer.ResolveSyncIoOwnerTypes(semanticModel.Compilation, availableSyncIoMethods);
        var syncIoCalls = FindAllSyncIoCalls(
            newMethod, semanticModel, availableSyncIoMethods, syncIoOwnerTypes, cancellationToken);
        if (syncIoCalls.Count > 0)
        {
            newMethod = ReplaceSyncCallsWithAsyncEquivalents(newMethod, syncIoCalls);
        }

        // 2. Unwrap Task.FromResult / ValueTask.FromResult -> just the inner expression
        newMethod = UnwrapFromResultCalls(newMethod);

        // 3. Add async modifier if not already present
        if (!newMethod.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            var asyncToken = SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
                .WithTrailingTrivia(SyntaxFactory.Space);
            newMethod = newMethod.WithModifiers(newMethod.Modifiers.Add(asyncToken));
        }

        var newRoot = root.ReplaceNode(method, newMethod);
        return document.WithSyntaxRoot(newRoot);
    }

    private static List<InvocationExpressionSyntax> FindAllSyncIoCalls(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        ImmutableDictionary<string, string> availableSyncIoMethods,
        ImmutableDictionary<string, INamedTypeSymbol?> syncIoOwnerTypes,
        CancellationToken cancellationToken)
    {
        var results = new List<InvocationExpressionSyntax>();

        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            if (TaskFromResultSyncIoAnalyzer.IsInNestedFunction(invocation, method))
            {
                continue;
            }

            var methodName = memberAccess.Name.Identifier.ValueText;
            if (!availableSyncIoMethods.TryGetValue(methodName, out var expectedType))
            {
                continue;
            }

            if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol calledMethod)
            {
                continue;
            }

            // Compare the bound type against the per-compilation symbol, so a user type that
            // shares the metadata name is not a match.
            if (syncIoOwnerTypes.TryGetValue(expectedType, out var expectedTypeSymbol)
                && expectedTypeSymbol is not null
                && SymbolEqualityComparer.Default.Equals(calledMethod.ContainingType, expectedTypeSymbol))
            {
                results.Add(invocation);
            }
        }

        return results;
    }

    private static MethodDeclarationSyntax ReplaceSyncCallsWithAsyncEquivalents(
        MethodDeclarationSyntax method,
        List<InvocationExpressionSyntax> syncCalls)
    {
        return method.ReplaceNodes(syncCalls, (original, rewritten) =>
        {
            if (rewritten.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                return rewritten;
            }

            var methodName = memberAccess.Name.Identifier.ValueText;
            if (!TaskFromResultSyncIoAnalyzer.SyncToAsyncMethodNames.TryGetValue(methodName, out var asyncName))
            {
                return rewritten;
            }

            var newMemberAccess = memberAccess.WithName(
                SyntaxFactory.IdentifierName(asyncName));

            var newInvocation = rewritten.WithExpression(newMemberAccess);

            return SyntaxFactory.AwaitExpression(newInvocation)
                .WithLeadingTrivia(rewritten.GetLeadingTrivia());
        });
    }

    private static MethodDeclarationSyntax UnwrapFromResultCalls(MethodDeclarationSyntax method)
    {
        // Unwrap only a call that is the returned value. Any other call may feed a Task-typed
        // variable or a continuation, and the bare value would not compile there.
        var fromResultCalls = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => IsFromResultInvocation(invocation)
                                 && IsReturnedValue(invocation)
                                 && !TaskFromResultSyncIoAnalyzer.IsInNestedFunction(invocation, method))
            .ToList();

        return method.ReplaceNodes(fromResultCalls, (original, rewritten) =>
        {
            return rewritten.ArgumentList.Arguments.Count != 1
                ? rewritten
                : (SyntaxNode)rewritten.ArgumentList.Arguments[0].Expression
                    .WithLeadingTrivia(rewritten.GetLeadingTrivia())
                    .WithTrailingTrivia(rewritten.GetTrailingTrivia());
        });
    }

    private static bool IsReturnedValue(InvocationExpressionSyntax invocation)
    {
        return invocation.Parent is ReturnStatementSyntax or ArrowExpressionClauseSyntax;
    }

    private static bool IsFromResultInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "FromResult", StringComparison.Ordinal))
        {
            return false;
        }

        // Check that the receiver is Task or ValueTask (syntactic check — good enough for code fix)
        return memberAccess.Expression is IdentifierNameSyntax identifier && (string.Equals(identifier.Identifier.ValueText, "Task", StringComparison.Ordinal)
                                                                              || string.Equals(identifier.Identifier.ValueText, "ValueTask", StringComparison.Ordinal));
    }
}
