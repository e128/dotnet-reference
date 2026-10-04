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
        bool hasHelperCall,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        Method = method;
        Symbol = symbol;
        Assertions = assertions;
        Statements = statements;
        ProductionCalls = productionCalls;
        ContainsAwait = containsAwait;
        HasHelperCall = hasHelperCall;
        Model = model;
        CancellationToken = cancellationToken;
    }

    internal MethodDeclarationSyntax Method { get; }

    internal IMethodSymbol Symbol { get; }

    internal ImmutableArray<AssertionFact> Assertions { get; }

    internal int Statements { get; }

    internal ImmutableArray<ProductionCallFact> ProductionCalls { get; }

    internal bool ContainsAwait { get; }

    /// <summary>
    ///     Whether the body calls a method declared in the test class itself. Such a call is test
    ///     scaffolding: it may build a fixture, and it may assert, but its body is not the code under test.
    /// </summary>
    internal bool HasHelperCall { get; }

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
        var hasHelperCall = false;
        var testClass = symbol.ContainingType;

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
            else if (testClass is not null
                     && SymbolEqualityComparer.Default.Equals(called.ContainingType, testClass))
            {
                // A method declared in the test class is a fixture or a local assertion helper. Its body
                // is scaffolding, so it seeds no duplicate key and it echoes no production literal. The
                // call still counts as an observation, because a helper such as AssertRanksPresentVsAbsent
                // carries the assertions the test method itself would otherwise repeat inline.
                hasHelperCall = true;
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
            hasHelperCall,
            semanticModel,
            cancellationToken);
    }

    internal static bool IsAssertion(ISymbol symbol)
    {
        return symbol.Name.StartsWith("Verify", StringComparison.Ordinal)
               || (symbol.ContainingType is { } containingType
                   && string.Equals(containingType.Name, "Assert", StringComparison.Ordinal)
                   && string.Equals(
                       containingType.ContainingNamespace.ToDisplayString(),
                       "Xunit",
                       StringComparison.Ordinal));
    }
}
