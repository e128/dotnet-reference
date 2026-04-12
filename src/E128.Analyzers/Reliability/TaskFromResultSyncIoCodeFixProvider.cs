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
/// Code fix for E128028: converts sync I/O calls to their async equivalents,
/// adds <c>async</c> modifier, inserts <c>await</c>, and removes <c>Task.FromResult</c> wrapper.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskFromResultSyncIoCodeFixProvider))]
[Shared]
public sealed class TaskFromResultSyncIoCodeFixProvider : CodeFixProvider
{
    /// <summary>
    /// Maps sync I/O method names to their async equivalents.
    /// </summary>
    private static readonly ImmutableDictionary<string, string> SyncToAsyncMethodNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ReadAllText"] = "ReadAllTextAsync",
            ["ReadAllBytes"] = "ReadAllBytesAsync",
            ["ReadAllLines"] = "ReadAllLinesAsync",
            ["WriteAllText"] = "WriteAllTextAsync",
            ["WriteAllBytes"] = "WriteAllBytesAsync",
            ["WriteAllLines"] = "WriteAllLinesAsync",
            ["AppendAllText"] = "AppendAllTextAsync",
            ["AppendAllLines"] = "AppendAllLinesAsync",
            ["Read"] = "ReadAsync",
            ["Write"] = "WriteAsync",
            ["CopyTo"] = "CopyToAsync",
            ["Flush"] = "FlushAsync",
            ["Send"] = "SendAsync",
        }.ToImmutableDictionary(StringComparer.Ordinal);

    public override ImmutableArray<string> FixableDiagnosticIds =>
        [TaskFromResultSyncIoAnalyzer.DiagnosticId];

    public override FixAllProvider GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

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

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Convert to async/await",
                createChangedDocument: ct => ConvertToAsyncAsync(context.Document, invocation, ct),
                equivalenceKey: nameof(TaskFromResultSyncIoCodeFixProvider)),
            diagnostic);
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

        // 1. Replace sync I/O calls with async equivalents wrapped in await
        var syncIoCalls = FindAllSyncIoCalls(newMethod, semanticModel, cancellationToken);
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
        CancellationToken cancellationToken)
    {
        var results = new List<InvocationExpressionSyntax>();

        foreach (var invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                continue;
            }

            var methodName = memberAccess.Name.Identifier.ValueText;
            if (!SyncToAsyncMethodNames.ContainsKey(methodName))
            {
                continue;
            }

            var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
            if (symbolInfo.Symbol is IMethodSymbol calledMethod)
            {
                var containingType = calledMethod.ContainingType?.ToDisplayString();
                if (TaskFromResultSyncIoAnalyzer.SyncIoMethodsWithAsyncAlternatives.TryGetValue(methodName, out var expectedType)
                    && string.Equals(containingType, expectedType, StringComparison.Ordinal))
                {
                    results.Add(invocation);
                }
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
            if (!SyncToAsyncMethodNames.TryGetValue(methodName, out var asyncName))
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
        var fromResultCalls = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(IsFromResultInvocation)
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
