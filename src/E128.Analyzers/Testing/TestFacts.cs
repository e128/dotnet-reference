using System;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Testing;

/// <summary>
///     The facts one candidate test method contributes to the low-value test conditions. The collector runs
///     once per method, so every detector reads the same data. An assertion is an xUnit Assert member, an
///     exception expectation, or a verification helper whose name starts with Verify.
/// </summary>
internal sealed class TestFacts
{
    private TestFacts(
        MethodDeclarationSyntax method,
        IMethodSymbol symbol,
        ImmutableArray<AssertionFact> assertions,
        int statements,
        ImmutableArray<ProductionCallFact> productionCalls,
        bool containsAwait,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        Method = method;
        Symbol = symbol;
        Assertions = assertions;
        Statements = statements;
        ProductionCalls = productionCalls;
        ContainsAwait = containsAwait;
        Model = model;
        CancellationToken = cancellationToken;
    }

    internal MethodDeclarationSyntax Method { get; }

    internal IMethodSymbol Symbol { get; }

    internal ImmutableArray<AssertionFact> Assertions { get; }

    internal int Statements { get; }

    internal ImmutableArray<ProductionCallFact> ProductionCalls { get; }

    internal bool ContainsAwait { get; }

    internal SemanticModel Model { get; }

    internal CancellationToken CancellationToken { get; }

    internal static TestFacts Create(
        MethodDeclarationSyntax method,
        IMethodSymbol symbol,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var assertions = ImmutableArray.CreateBuilder<AssertionFact>();
        var productionCalls = ImmutableArray.CreateBuilder<ProductionCallFact>();
        var containsAwait = false;

        foreach (var node in method.DescendantNodes())
        {
            if (node is AwaitExpressionSyntax)
            {
                containsAwait = true;
                continue;
            }

            if (node is not InvocationExpressionSyntax invocation
                || semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol called)
            {
                continue;
            }

            if (IsAssertion(called))
            {
                assertions.Add(new AssertionFact(invocation, called));
            }
            else
            {
                productionCalls.Add(new ProductionCallFact(invocation, called));
            }
        }

        return new TestFacts(
            method,
            symbol,
            assertions.ToImmutable(),
            method.Body?.Statements.Count ?? 1,
            productionCalls.ToImmutable(),
            containsAwait,
            semanticModel,
            cancellationToken);
    }

    private static bool IsAssertion(IMethodSymbol method)
    {
        return method.Name.StartsWith("Verify", StringComparison.Ordinal)
               || (method.ContainingType is { } containingType
                   && string.Equals(containingType.Name, "Assert", StringComparison.Ordinal)
                   && string.Equals(
                       containingType.ContainingNamespace.ToDisplayString(),
                       "Xunit",
                       StringComparison.Ordinal));
    }
}
