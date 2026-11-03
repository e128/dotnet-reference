using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128152: Reports code whose inferred algorithmic complexity exceeds a configurable ceiling.
///     A loop inside another loop infers tier `quadratic`; a loop three or more levels deep infers
///     tier `cubic or worse`. Direct non-memoized recursion within a type infers tier `exponential`.
///     The ceiling is a named tier set by the `e128_max_complexity` option.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ComplexityCeilingAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128152";

    private const string CeilingOptionKey = "e128_max_complexity";
    private const string DefaultCeiling = "linear";
    private const int LinearTierRank = 0;
    private const int LinearithmicTierRank = 1;
    private const int QuadraticTierRank = 2;
    private const int ExponentialTierRank = 3;

    private static readonly DiagnosticDescriptor NestedLoopRule = new(
        DiagnosticId,
        "Nested loops exceed the configured complexity ceiling",
        "This loop nesting is at tier `{0}`, past the configured ceiling `{1}`. Hoist the outer work or track the inner iteration on a set-based collection.",
        "Performance",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor RecursionRule = new(
        DiagnosticId,
        "Recursion exceeds the configured complexity ceiling",
        "This recursion is at tier `exponential`, past the configured ceiling `{0}`. Add memoization or restructure into iteration.",
        "Performance",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [NestedLoopRule, RecursionRule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterOperationAction(AnalyzeLoop, OperationKind.Loop);
        context.RegisterOperationBlockAction(AnalyzeMethodRecursion);
    }

    private static void AnalyzeLoop(OperationAnalysisContext context)
    {
        var (tier, tierRank) = GetLoopTier(CountEnclosingLoops(context.Operation) + 1);
        var ceiling = GetCeiling(context.Operation.Syntax.SyntaxTree, context.Options.AnalyzerConfigOptionsProvider);

        TryGetTierRank(ceiling, out var ceilingRank);

        if (tierRank <= ceilingRank)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(NestedLoopRule, context.Operation.Syntax.GetLocation(), tier, ceiling));
    }

    private static int CountEnclosingLoops(IOperation operation)
    {
        var enclosing = 0;
        var current = operation.Parent;

        while (current != null)
        {
            if (current.Kind == OperationKind.Loop)
            {
                enclosing++;
            }

            if (current.Kind is OperationKind.AnonymousFunction or OperationKind.LocalFunction)
            {
                return 0;
            }

            current = current.Parent;
        }

        return enclosing;
    }

    private static (string Tier, int Rank) GetLoopTier(int depth) => depth switch
    {
        1 => ("linear", LinearTierRank),
        2 => ("quadratic", QuadraticTierRank),
        _ => ("cubic or worse", ExponentialTierRank),
    };

    private static void AnalyzeMethodRecursion(OperationBlockAnalysisContext context)
    {
        if (context.OwningSymbol is not IMethodSymbol method
            || method.ContainingType is not { } containingType
            || context.OperationBlocks.IsEmpty
            || context.OperationBlocks[0] is not IBlockOperation body)
        {
            return;
        }

        var recursiveCalls = 0;
        var memoizes = false;

        foreach (var operation in body.DescendantsAndSelf())
        {
            if (operation is IInvocationOperation invocation
                && SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, method.OriginalDefinition))
            {
                recursiveCalls++;
            }
            else if (operation is IAssignmentOperation assignment
                     && Memoizes(assignment.Target, containingType, context.Compilation))
            {
                memoizes = true;
            }
            else if (operation is IIncrementOrDecrementOperation increment
                     && Memoizes(increment.Target, containingType, context.Compilation))
            {
                memoizes = true;
            }
        }

        if (recursiveCalls < 2 || memoizes)
        {
            return;
        }

        var ceiling = GetCeiling(body.Syntax.SyntaxTree, context.Options.AnalyzerConfigOptionsProvider);

        TryGetTierRank(ceiling, out var ceilingRank);

        if (ExponentialTierRank <= ceilingRank)
        {
            return;
        }

        var declaration = method.DeclaringSyntaxReferences.Length > 0
            ? method.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken)
            : null;

        if (declaration is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(RecursionRule, declaration.GetLocation(), ceiling));
    }

    private static bool Memoizes(IOperation target, INamedTypeSymbol containingType, Compilation compilation)
    {
        return WritesToContainingTypeField(target, containingType)
               || WritesToDictionaryLocal(target, compilation);
    }

    private static bool WritesToContainingTypeField(IOperation target, INamedTypeSymbol containingType)
    {
        foreach (var part in target.DescendantsAndSelf())
        {
            if (part is IFieldReferenceOperation fieldReference
                && fieldReference.Field.ContainingType is { } owner
                && SymbolEqualityComparer.Default.Equals(owner.OriginalDefinition, containingType.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool WritesToDictionaryLocal(IOperation target, Compilation compilation)
    {
        var dictionaryInterface = compilation.GetTypeByMetadataName("System.Collections.Generic.IDictionary`2");

        if (dictionaryInterface is null)
        {
            return false;
        }

        foreach (var part in target.DescendantsAndSelf())
        {
            if (part is ILocalReferenceOperation localReference
                && localReference.Local.Type is { } localType
                && localType.AllInterfaces.Any(interfaceType =>
                    SymbolEqualityComparer.Default.Equals(interfaceType.OriginalDefinition, dictionaryInterface)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetTierRank(string value, out int rank)
    {
        if (string.Equals(value, "linear", StringComparison.Ordinal))
        {
            rank = LinearTierRank;
            return true;
        }

        if (string.Equals(value, "linearithmic", StringComparison.Ordinal))
        {
            rank = LinearithmicTierRank;
            return true;
        }

        if (string.Equals(value, "quadratic", StringComparison.Ordinal))
        {
            rank = QuadraticTierRank;
            return true;
        }

        if (string.Equals(value, "exponential", StringComparison.Ordinal))
        {
            rank = ExponentialTierRank;
            return true;
        }

        rank = ExponentialTierRank;
        return false;
    }

    private static string GetCeiling(SyntaxTree syntaxTree, AnalyzerConfigOptionsProvider provider)
    {
        var options = provider.GetOptions(syntaxTree);

        return options.TryGetValue(CeilingOptionKey, out var rawValue) && TryGetTierRank(rawValue, out _)
            ? rawValue
            : DefaultCeiling;
    }
}
