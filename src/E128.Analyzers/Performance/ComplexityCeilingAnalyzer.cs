using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128152: Reports code whose inferred algorithmic complexity exceeds a configurable ceiling.
///     Flags nested loops of depth 2 or more (quadratic and worse) and direct non-memoized recursion
///     within a type (exponential). The ceiling is a named tier set by the
///     `e128_max_complexity` option.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ComplexityCeilingAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128152";

    private const string CeilingOptionKey = "e128_max_complexity";
    private const string DefaultCeiling = "linear";

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

        context.RegisterOperationAction(AnalyzeOperation, OperationKind.Loop);
        context.RegisterOperationBlockAction(AnalyzeMethodRecursion);
    }

    private static void AnalyzeOperation(OperationAnalysisContext context)
    {
        if (context.Operation is not ILoopOperation loop)
        {
            return;
        }

        var tier = GetNestingTier(context.Operation);
        var ceiling = GetCeiling(context.Operation.Syntax.SyntaxTree, context.Options.AnalyzerConfigOptionsProvider);

        if (TierRank(tier) <= TierRank(ceiling))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(NestedLoopRule, loop.Syntax.GetLocation(), tier, ceiling));
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

            current = current.Parent;
        }

        return enclosing;
    }

    private static string GetNestingTier(IOperation operation)
    {
        return CountEnclosingLoops(operation) == 0
            ? "linear"
            : "quadratic";
    }

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
            else if (operation is IAssignmentOperation assignment && WritesToContainingTypeField(assignment.Target, containingType))
            {
                memoizes = true;
            }
            else if (operation is IIncrementOrDecrementOperation increment && WritesToContainingTypeField(increment.Target, containingType))
            {
                memoizes = true;
            }
        }

        if (recursiveCalls < 2 || memoizes)
        {
            return;
        }

        var ceiling = GetCeiling(body.Syntax.SyntaxTree, context.Options.AnalyzerConfigOptionsProvider);

        if (TierRank("exponential") <= TierRank(ceiling))
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

    private static int TierRank(string tier) => tier switch
    {
        { } t when string.Equals(t, "linear", StringComparison.Ordinal) => 0,
        { } t when string.Equals(t, "linearithmic", StringComparison.Ordinal) => 1,
        { } t when string.Equals(t, "quadratic", StringComparison.Ordinal) => 2,
        _ => 3
    };

    private static string GetCeiling(SyntaxTree syntaxTree, AnalyzerConfigOptionsProvider provider)
    {
        var options = provider.GetOptions(syntaxTree);

        return options.TryGetValue(CeilingOptionKey, out var rawValue)
               && IsNamedTier(rawValue)
            ? rawValue
            : DefaultCeiling;
    }

    private static bool IsNamedTier(string value)
    {
        return string.Equals(value, "linear", StringComparison.Ordinal)
               || string.Equals(value, "linearithmic", StringComparison.Ordinal)
               || string.Equals(value, "quadratic", StringComparison.Ordinal)
               || string.Equals(value, "exponential", StringComparison.Ordinal);
    }
}
