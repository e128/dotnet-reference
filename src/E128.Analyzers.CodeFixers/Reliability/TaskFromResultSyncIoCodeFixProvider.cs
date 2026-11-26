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
        RegisterNodeFix<InvocationExpressionSyntax>(
            context,
            root,
            "Convert to async/await",
            nameof(TaskFromResultSyncIoCodeFixProvider),
            (invocation, ct) => ConvertToAsyncAsync(context.Document, invocation, ct));

        return Task.CompletedTask;
    }

    private static async Task<Document> ConvertToAsyncAsync(
        Document document,
        InvocationExpressionSyntax fromResultInvocation,
        CancellationToken cancellationToken)
    {
        var rootAndModel = await FixSyntax.GetRootAndSemanticModelAsync(document, cancellationToken).ConfigureAwait(false);
        if (rootAndModel is null)
        {
            return document;
        }

        var root = rootAndModel.Value.Root;
        var semanticModel = rootAndModel.Value.SemanticModel;

        var method = fromResultInvocation.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return document;
        }

        var unwrapped = UnwrapFromResultCalls(method, semanticModel, cancellationToken);
        var intermediateDocument = document.WithSyntaxRoot(root.ReplaceNode(method, unwrapped));
        var intermediateModel = await intermediateDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (intermediateModel is null)
        {
            return document;
        }

        var intermediateRoot = await intermediateModel.SyntaxTree
            .GetRootAsync(cancellationToken)
            .ConfigureAwait(false);
        var newMethod = intermediateRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(candidate => candidate.SpanStart == method.SpanStart);

        var availableSyncIoMethods =
            TaskFromResultSyncIoAnalyzer.FindAvailableSyncIoMethods(intermediateModel.Compilation);
        var syncIoOwnerTypes =
            TaskFromResultSyncIoAnalyzer.ResolveSyncIoOwnerTypes(intermediateModel.Compilation, availableSyncIoMethods);
        var syncIoCalls = FindAllSyncIoCalls(
            newMethod, intermediateModel, availableSyncIoMethods, syncIoOwnerTypes, cancellationToken);
        if (syncIoCalls.Count > 0)
        {
            newMethod = ReplaceSyncCallsWithAsyncEquivalents(newMethod, syncIoCalls);
        }

        newMethod = AddAsyncModifier(newMethod);

        var newRoot = intermediateRoot.ReplaceNode(
            intermediateRoot.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .First(candidate => candidate.SpanStart == method.SpanStart),
            newMethod);

        return intermediateDocument.WithSyntaxRoot(newRoot);
    }

    private static MethodDeclarationSyntax AddAsyncModifier(MethodDeclarationSyntax method)
    {
        if (method.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            return method;
        }

        var leadingTrivia = method.GetLeadingTrivia();
        var asyncToken = SyntaxFactory.Token(SyntaxKind.AsyncKeyword)
            .WithTrailingTrivia(SyntaxFactory.Space);

        return method
            .WithLeadingTrivia(default(SyntaxTriviaList))
            .WithModifiers(method.Modifiers.Add(asyncToken))
            .WithLeadingTrivia(leadingTrivia);
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
        return method.ReplaceNodes(syncCalls, static (_, rewritten) =>
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

    private static MethodDeclarationSyntax UnwrapFromResultCalls(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var fromResultCalls = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => TaskFromResultSyncIoAnalyzer.IsFromResultInvocation(invocation, semanticModel, cancellationToken)
                                 && IsReturnedValue(invocation)
                                 && !TaskFromResultSyncIoAnalyzer.IsInNestedFunction(invocation, method))
            .ToList();

        return method.ReplaceNodes(fromResultCalls, static (_, rewritten) =>
        {
            return rewritten.ArgumentList.Arguments.Count is not 1
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
}
