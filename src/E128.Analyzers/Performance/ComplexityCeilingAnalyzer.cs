using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128152: Reports code whose inferred algorithmic complexity exceeds a configurable ceiling.
///     Loop nesting counts only loops whose trip count varies, so a loop bounded by a constant does
///     not raise the tier. A coordinate step, whose trip count depends on the element the enclosing
///     loop produced, adds no depth either. Two varying loops infer `quadratic`, three infer `cubic`.
///     Recursion that branches on a shrinking argument infers `exponential` unless the method
///     memoizes the result. The ceiling is a named tier set by the `e128_max_complexity` option.
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
    private const int CubicTierRank = 3;
    private const int ExponentialTierRank = 4;
    private const int QuadraticDegree = 2;
    private const int CubicDegree = 3;
    private const int MinimumDivisor = 2;

    private static readonly DiagnosticDescriptor NestedLoopRule = new(
        DiagnosticId,
        "Nested loops exceed the configured complexity ceiling",
        "This loop nesting is at tier `{0}`, past the configured ceiling `{1}`. Hoist the outer work or track the inner iteration on a set-based collection.",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

    private static readonly DiagnosticDescriptor RecursionRule = new(
        DiagnosticId,
        "Recursion exceeds the configured complexity ceiling",
        "This recursion splits into several calls on a smaller argument, so its tier is `exponential`, past the configured ceiling `{0}`. Add memoization or restructure into iteration.",
        "E128-Performance",
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
        if (context.Operation is not ILoopOperation loop)
        {
            return;
        }

        var (tier, tierRank) = GetLoopTier(CountVaryingEnclosingLoops(loop));
        var ceiling = GetCeiling(context.Operation.Syntax.SyntaxTree, context.Options.AnalyzerConfigOptionsProvider);

        _ = TryGetTierRank(ceiling, out var ceilingRank);

        if (tierRank <= ceilingRank)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(NestedLoopRule, context.Operation.Syntax.GetLocation(), tier, ceiling));
    }

    private static int CountVaryingEnclosingLoops(ILoopOperation loop)
    {
        var degree = HasConstantTripCount(loop) ? 0 : 1;

        for (var current = loop.Parent; current is not null; current = current.Parent)
        {
            if (current.Kind is OperationKind.AnonymousFunction or OperationKind.LocalFunction)
            {
                return 0;
            }

            if (current is ILoopOperation enclosing
                && !HasConstantTripCount(enclosing)
                && !IsCoordinateStep(loop, enclosing))
            {
                degree++;
            }
        }

        return degree;
    }

    /// <summary>
    ///     A coordinate step walks a second dimension of the data the enclosing loop produced, such as
    ///     a cell inside a row. Its trip count depends on that element, so the enclosing loop adds no
    ///     depth: the pair costs one visit per element, not one visit per element pair.
    /// </summary>
    private static bool IsCoordinateStep(ILoopOperation loop, ILoopOperation enclosing)
    {
        var source = GetTripCountSource(loop);

        if (source is null)
        {
            return false;
        }

        var bodySpan = GetBodySpan(enclosing, out var bodySyntaxTree);

        foreach (var operation in source.DescendantsAndSelf())
        {
            var symbol = GetReferencedSymbol(operation);

            if (symbol is null || loop.Locals.Contains(symbol, SymbolEqualityComparer.Default))
            {
                continue;
            }

            if (IsEnclosingElement(enclosing, symbol, source)
                || IsDeclaredInBody(symbol, bodySpan, bodySyntaxTree))
            {
                return true;
            }
        }

        return false;
    }

    private static ISymbol? GetReferencedSymbol(IOperation operation)
    {
        return operation switch
        {
            ILocalReferenceOperation local => local.Local,
            IParameterReferenceOperation parameter => parameter.Parameter,
            _ => null,
        };
    }

    // A for-loop counter is not an element unless it indexes the collection the inner loop
    // walks. A bound such as `j < i` still makes the nesting grow with the input.
    private static bool IsEnclosingElement(ILoopOperation enclosing, ISymbol symbol, IOperation source)
    {
        if (!enclosing.Locals.Contains(symbol, SymbolEqualityComparer.Default))
        {
            return false;
        }

        return enclosing is IForEachLoopOperation
            || (enclosing is IForLoopOperation && UsesLocalAsElementIndex(symbol, source));
    }

    private static bool IsDeclaredInBody(ISymbol symbol, TextSpan bodySpan, SyntaxTree? bodySyntaxTree)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree == bodySyntaxTree && bodySpan.Contains(reference.Span))
            {
                return true;
            }
        }

        return false;
    }

    // True when the local is the index of an array element or an indexer argument in the trip source.
    private static bool UsesLocalAsElementIndex(ISymbol symbol, IOperation source)
    {
        foreach (var operation in source.DescendantsAndSelf())
        {
            if (operation is ILocalReferenceOperation local
                && SymbolEqualityComparer.Default.Equals(local.Local, symbol)
                && operation.Parent is IArrayElementReferenceOperation or IArgumentOperation { Parent: IPropertyReferenceOperation })
            {
                return true;
            }
        }

        return false;
    }

    private static IOperation? GetTripCountSource(ILoopOperation loop)
    {
        return loop switch
        {
            IForEachLoopOperation forEachLoop => forEachLoop.Collection,
            IForLoopOperation forLoop => forLoop.Condition,
            IWhileLoopOperation whileLoop => whileLoop.Condition,
            _ => null,
        };
    }

    private static TextSpan GetBodySpan(ILoopOperation loop, out SyntaxTree? syntaxTree)
    {
        var body = loop switch
        {
            IForEachLoopOperation forEachLoop => forEachLoop.Body,
            IForLoopOperation forLoop => forLoop.Body,
            IWhileLoopOperation whileLoop => whileLoop.Body,
            _ => null,
        };

        if (body is null)
        {
            syntaxTree = null;
            return default;
        }

        syntaxTree = body.Syntax.SyntaxTree;
        return body.Syntax.Span;
    }

    private static bool HasConstantTripCount(ILoopOperation loop)
    {
        return loop switch
        {
            IForLoopOperation forLoop => HasConstantBound(forLoop.Condition),
            IForEachLoopOperation forEachLoop => HasConstantSize(forEachLoop.Collection),
            _ => false,
        };
    }

    private static bool HasConstantBound(IOperation? condition)
    {
        return condition is IBinaryOperation binary
               && binary.OperatorKind is BinaryOperatorKind.LessThan or BinaryOperatorKind.LessThanOrEqual
                   or BinaryOperatorKind.GreaterThan or BinaryOperatorKind.GreaterThanOrEqual
               && (binary.RightOperand.ConstantValue.HasValue || binary.LeftOperand.ConstantValue.HasValue);
    }

    private static bool HasConstantSize(IOperation collection)
    {
        var source = Unwrap(collection);

        return source is IArrayCreationOperation array
            ? array.Initializer is not null
              || (array.DimensionSizes.Length is 1 && array.DimensionSizes[0].ConstantValue.HasValue)
            : source is ICollectionExpressionOperation expression
              && expression.Elements.Length > 0
              && expression.Elements.All(static element => Unwrap(element).ConstantValue.HasValue);
    }

    private static (string Tier, int Rank) GetLoopTier(int degree)
    {
        return degree switch
        {
            QuadraticDegree => ("quadratic", QuadraticTierRank),
            CubicDegree => ("cubic", CubicTierRank),
            > CubicDegree => ("cubic or worse", CubicTierRank),
            _ => ("linear", LinearTierRank),
        };
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

        var calls = CollectRecursiveCalls(method, body);

        if (!IsUnmemoizedExponentialRecursion(calls, body, containingType))
        {
            return;
        }

        var ceiling = GetCeiling(body.Syntax.SyntaxTree, context.Options.AnalyzerConfigOptionsProvider);

        _ = TryGetTierRank(ceiling, out var ceilingRank);

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

    private static bool IsUnmemoizedExponentialRecursion(
        ImmutableArray<Contraction> calls,
        IBlockOperation body,
        INamedTypeSymbol containingType)
    {
        return calls.Length >= 2
            && calls.Any(static contraction => contraction is Contraction.Subtractive)
            && !HasMemoization(body, containingType);
    }

    private static ImmutableArray<Contraction> CollectRecursiveCalls(IMethodSymbol method, IBlockOperation body)
    {
        var builder = ImmutableArray.CreateBuilder<Contraction>();

        foreach (var operation in body.DescendantsAndSelf())
        {
            if (operation is IInvocationOperation invocation
                && SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.OriginalDefinition, method.OriginalDefinition))
            {
                builder.Add(ClassifyArguments(invocation, method));
            }
        }

        return builder.ToImmutable();
    }

    private static Contraction ClassifyArguments(IInvocationOperation invocation, IMethodSymbol method)
    {
        var result = Contraction.Same;

        for (var index = 0; index < invocation.Arguments.Length && index < method.Parameters.Length; index++)
        {
            var contraction = ClassifyArgument(Unwrap(invocation.Arguments[index].Value), method.Parameters[index]);

            if (contraction is Contraction.Subtractive)
            {
                return Contraction.Subtractive;
            }

            if (contraction is Contraction.Unknown)
            {
                result = Contraction.Unknown;
            }
            else if (contraction is Contraction.Divisive && result is Contraction.Same)
            {
                result = Contraction.Divisive;
            }
        }

        return result;
    }

    private static Contraction ClassifyArgument(IOperation argument, IParameterSymbol parameter)
    {
        if (ReferencesParameter(argument, parameter))
        {
            return Contraction.Same;
        }

        if (argument is not IBinaryOperation binary)
        {
            return Contraction.Unknown;
        }

        if (!ReferencesParameter(binary.LeftOperand, parameter))
        {
            return Contraction.Unknown;
        }

        var subtractive = (binary.OperatorKind is BinaryOperatorKind.Subtract && binary.RightOperand.ConstantValue.HasValue)
                          || (binary.OperatorKind is BinaryOperatorKind.Add && IsNegativeConstant(binary.RightOperand));
        var divisive = binary.OperatorKind is BinaryOperatorKind.Divide && IsDivisorAtLeastTwo(binary.RightOperand);

        if (subtractive)
        {
            return Contraction.Subtractive;
        }

        return divisive ? Contraction.Divisive : Contraction.Unknown;
    }

    private static bool IsDivisorAtLeastTwo(IOperation divisor)
    {
        var value = divisor.ConstantValue.Value;

        return value switch
        {
            int intValue => intValue >= MinimumDivisor,
            long longValue => longValue >= MinimumDivisor,
            double doubleValue => doubleValue >= MinimumDivisor,
            _ => false,
        };
    }

    private static bool IsNegativeConstant(IOperation operand)
    {
        var value = operand.ConstantValue.Value;

        return value switch
        {
            int intValue => intValue < 0,
            long longValue => longValue < 0,
            double doubleValue => doubleValue < 0,
            _ => false,
        };
    }

    private static bool ReferencesParameter(IOperation operation, IParameterSymbol parameter)
    {
        return operation is IParameterReferenceOperation reference
               && SymbolEqualityComparer.Default.Equals(reference.Parameter, parameter);
    }

    private static bool HasMemoization(IOperation body, INamedTypeSymbol containingType)
    {
        var writeTargets = new HashSet<IOperation>();
        var writes = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var reads = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var operation in body.DescendantsAndSelf())
        {
            if (operation is IAssignmentOperation assignment)
            {
                writeTargets.Add(assignment.Target);
            }
            else if (operation is IIncrementOrDecrementOperation increment)
            {
                writeTargets.Add(increment.Target);
            }
        }

        foreach (var operation in body.DescendantsAndSelf())
        {
            if (operation is IFieldReferenceOperation field)
            {
                ClassifyReference(field.Field, field, writeTargets, containingType, writes, reads);
            }
            else if (operation is ILocalReferenceOperation local)
            {
                ClassifyReference(local.Local, local, writeTargets, containingType, writes, reads);
            }
        }

        return writes.Overlaps(reads);
    }

    private static void ClassifyReference(
        ISymbol symbol,
        IOperation reference,
        HashSet<IOperation> writeTargets,
        INamedTypeSymbol containingType,
        HashSet<ISymbol> writes,
        HashSet<ISymbol> reads)
    {
        if (!IsCacheSymbol(symbol, containingType))
        {
            return;
        }

        if (IsWithinWriteTarget(reference, writeTargets))
        {
            writes.Add(symbol);
            return;
        }

        reads.Add(symbol);
    }

    private static bool IsWithinWriteTarget(IOperation reference, HashSet<IOperation> writeTargets)
    {
        for (var current = reference; current is not null; current = current.Parent)
        {
            if (writeTargets.Contains(current))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCacheSymbol(ISymbol symbol, INamedTypeSymbol containingType)
    {
        if (symbol is IFieldSymbol field
            && !SymbolEqualityComparer.Default.Equals(field.ContainingType?.OriginalDefinition, containingType.OriginalDefinition))
        {
            return false;
        }

        var type = symbol switch
        {
            IFieldSymbol fieldSymbol => fieldSymbol.Type,
            ILocalSymbol localSymbol => localSymbol.Type,
            _ => null,
        };

        return type is not null && IsCacheType(type);
    }

    private static bool IsCacheType(ITypeSymbol type)
    {
        if (type.TypeKind is TypeKind.Array)
        {
            return true;
        }

        foreach (var interfaceType in type.AllInterfaces)
        {
            var metadataName = interfaceType.OriginalDefinition.MetadataName;

            if (string.Equals(metadataName, "IDictionary`2", StringComparison.Ordinal)
                || string.Equals(metadataName, "IReadOnlyDictionary`2", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static bool TryGetTierRank(string value, out int rank)
    {
        switch (value)
        {
            case "linear":
                rank = LinearTierRank;
                return true;

            case "linearithmic":
                rank = LinearithmicTierRank;
                return true;

            case "quadratic":
                rank = QuadraticTierRank;
                return true;

            case "cubic":
                rank = CubicTierRank;
                return true;

            case "exponential":
                rank = ExponentialTierRank;
                return true;

            default:
                rank = ExponentialTierRank;
                return false;
        }
    }

    private static string GetCeiling(SyntaxTree syntaxTree, AnalyzerConfigOptionsProvider provider)
    {
        var options = provider.GetOptions(syntaxTree);

        return options.TryGetValue(CeilingOptionKey, out var rawValue) && TryGetTierRank(rawValue, out _)
            ? rawValue
            : DefaultCeiling;
    }

    private enum Contraction
    {
        Same = 0,
        Subtractive = 1,
        Divisive = 2,
        Unknown = 3,
    }
}
