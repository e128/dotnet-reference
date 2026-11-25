using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128109: stackalloc must sit outside loops and have a bounded length of at most 1024 bytes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StackAllocSafetyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128109";

    private const int MaxStackBytes = 1024;
    private const int PointerSize = 8;
    private const int MaxFieldAlignment = 8;
    private const int MathMinArity = 2;

    private static readonly Dictionary<SpecialType, int> PrimitiveSizes = new()
    {
        [SpecialType.System_Boolean] = 1,
        [SpecialType.System_Byte] = 1,
        [SpecialType.System_SByte] = 1,
        [SpecialType.System_Int16] = 2,
        [SpecialType.System_UInt16] = 2,
        [SpecialType.System_Char] = 2,
        [SpecialType.System_Int32] = 4,
        [SpecialType.System_UInt32] = 4,
        [SpecialType.System_Single] = 4,
        [SpecialType.System_Int64] = 8,
        [SpecialType.System_UInt64] = 8,
        [SpecialType.System_Double] = 8,
        [SpecialType.System_Decimal] = 16,
        [SpecialType.System_IntPtr] = PointerSize,
        [SpecialType.System_UIntPtr] = PointerSize,
    };

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "stackalloc must be bounded and outside loops",
        "stackalloc is unsafe here: {0}",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Stack space is not reclaimed until the method returns. Keep stackalloc out of loops, cap it at " +
        "1024 bytes, and fall back to ArrayPool or a heap array above the cap.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.StackAllocArrayCreationExpression,
            SyntaxKind.ImplicitStackAllocArrayCreationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var reason = Classify(context.Node, context.SemanticModel, context.CancellationToken);
        if (reason is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), reason));
    }

    private static string? Classify(SyntaxNode node, SemanticModel model, System.Threading.CancellationToken cancellationToken)
    {
        if (IsInsideLoop(node))
        {
            return "it is inside a loop, and stack space is not reclaimed until the method returns";
        }

        if (model.GetTypeInfo(node, cancellationToken).ConvertedType is IPointerTypeSymbol)
        {
            return "it is assigned to a pointer, so assign it to Span<T> or ReadOnlySpan<T> for bounds checks";
        }

        if (node is not StackAllocArrayCreationExpressionSyntax { Type: ArrayTypeSyntax array }
            || !HasSizedFirstRank(array))
        {
            return null;
        }

        var elementType = model.GetTypeInfo(array.ElementType, cancellationToken).Type;
        if (elementType is null)
        {
            return null;
        }

        var size = array.RankSpecifiers[0].Sizes[0];
        var constant = model.GetConstantValue(size, cancellationToken);

        if (constant.HasValue)
        {
            return ClassifyConstantSize(constant.Value, elementType);
        }

        return IsBounded(node, size, elementType, model, cancellationToken)
            ? null
            : "its length is not bounded, so guard it and fall back to the heap above 1024 bytes";
    }

    private static bool HasSizedFirstRank(ArrayTypeSyntax array)
    {
        return array.RankSpecifiers.Count is not 0
               && array.RankSpecifiers[0].Sizes.Count is not 0
               && array.RankSpecifiers[0].Sizes[0] is not OmittedArraySizeExpressionSyntax;
    }

    private static string? ClassifyConstantSize(object? value, ITypeSymbol elementType)
    {
        if (value is not int count)
        {
            return null;
        }

        if (!TryGetSize(elementType, out var elementSize))
        {
            return "its element type size is not known, so the 1024 byte limit cannot be checked";
        }

        var bytes = (long)count * elementSize;

        return bytes > MaxStackBytes
            ? "its size is " + bytes.ToString(CultureInfo.InvariantCulture) + " bytes, above the 1024 byte limit"
            : null;
    }

    private static bool IsInsideLoop(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax)
            {
                return true;
            }

            if (IsScopeBoundary(ancestor))
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsBounded(
        SyntaxNode node,
        ExpressionSyntax size,
        ITypeSymbol elementType,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken)
    {
        if (!TryGetSize(elementType, out var elementSize))
        {
            return false;
        }

        if (size is InvocationExpressionSyntax invocation)
        {
            return TryGetCappedMin(invocation, model, cancellationToken, out var minCount)
                   && Fits(minCount, elementSize);
        }

        if (!MentionsAnyIdentifier(size))
        {
            return false;
        }

        return HasBoundingGuard(node, size, model, elementSize, cancellationToken);
    }

    private static bool MentionsAnyIdentifier(ExpressionSyntax size)
    {
        return size.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any();
    }

    private static bool Fits(int count, int elementSize)
    {
        return (long)count * elementSize <= MaxStackBytes;
    }

    private static bool HasBoundingGuard(
        SyntaxNode node,
        ExpressionSyntax size,
        SemanticModel model,
        int elementSize,
        System.Threading.CancellationToken cancellationToken)
    {
        var child = node;
        foreach (var ancestor in node.Ancestors())
        {
            if (GetCondition(ancestor) is { } condition
                && condition != child
                && TryGetCap(condition, size, model, cancellationToken, out var count)
                && Fits(count, elementSize))
            {
                return true;
            }

            if (IsScopeBoundary(ancestor))
            {
                return false;
            }

            child = ancestor;
        }

        return false;
    }

    private static bool TryGetCappedMin(
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken,
        out int count)
    {
        count = 0;

        if (invocation.ArgumentList.Arguments.Count is not MathMinArity
            || model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method
            || !string.Equals(method.Name, "Min", StringComparison.Ordinal)
            || !string.Equals(method.ContainingType?.ToDisplayString(), "System.Math", StringComparison.Ordinal))
        {
            return false;
        }

        return TryGetConstant(invocation.ArgumentList.Arguments[0].Expression, model, cancellationToken, out count)
               || TryGetConstant(invocation.ArgumentList.Arguments[1].Expression, model, cancellationToken, out count);
    }

    private static bool TryGetConstant(
        ExpressionSyntax expression,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken,
        out int value)
    {
        var constant = model.GetConstantValue(expression, cancellationToken);
        value = constant.HasValue && constant.Value is int number ? number : 0;
        return constant.HasValue && constant.Value is int;
    }

    private static ExpressionSyntax? GetCondition(SyntaxNode ancestor)
    {
        return ancestor switch
        {
            ConditionalExpressionSyntax conditional => conditional.Condition,
            IfStatementSyntax ifStatement => ifStatement.Condition,
            _ => null
        };
    }

    private static bool IsScopeBoundary(SyntaxNode node)
    {
        return node is MemberDeclarationSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax;
    }

    private static bool TryGetCap(
        ExpressionSyntax condition,
        ExpressionSyntax size,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken,
        out int count)
    {
        count = 0;

        if (Unwrap(condition) is not BinaryExpressionSyntax binary)
        {
            return false;
        }

        if (binary.IsKind(SyntaxKind.LogicalAndExpression))
        {
            return TryGetAndCap(binary, size, model, cancellationToken, out count);
        }

        return binary.IsKind(SyntaxKind.LogicalOrExpression)
            ? TryGetOrCap(binary, size, model, cancellationToken, out count)
            : TryGetRelationalCap(binary, size, model, cancellationToken, out count);
    }

    private static bool TryGetAndCap(
        BinaryExpressionSyntax binary,
        ExpressionSyntax size,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken,
        out int count)
    {
        count = 0;
        var leftCaps = TryGetCap(binary.Left, size, model, cancellationToken, out var left);
        var rightCaps = TryGetCap(binary.Right, size, model, cancellationToken, out var right);

        if (leftCaps && rightCaps)
        {
            count = Math.Min(left, right);
        }
        else if (leftCaps)
        {
            count = left;
        }
        else if (rightCaps)
        {
            count = right;
        }

        return leftCaps || rightCaps;
    }

    private static bool TryGetOrCap(
        BinaryExpressionSyntax binary,
        ExpressionSyntax size,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken,
        out int count)
    {
        count = 0;

        if (!TryGetCap(binary.Left, size, model, cancellationToken, out var left)
            || !TryGetCap(binary.Right, size, model, cancellationToken, out var right))
        {
            return false;
        }

        count = Math.Max(left, right);
        return true;
    }

    private static bool TryGetRelationalCap(
        BinaryExpressionSyntax binary,
        ExpressionSyntax size,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken,
        out int count)
    {
        count = 0;

        if (!TryReadBound(binary, model, cancellationToken, out var capped, out var limit, out var inclusive)
            || !string.Equals(Unwrap(capped).ToString(), Unwrap(size).ToString(), StringComparison.Ordinal))
        {
            return false;
        }

        count = inclusive ? limit : limit - 1;
        return count >= 0;
    }

    private static bool TryReadBound(
        BinaryExpressionSyntax binary,
        SemanticModel model,
        System.Threading.CancellationToken cancellationToken,
        out ExpressionSyntax capped,
        out int limit,
        out bool inclusive)
    {
        capped = binary;
        limit = 0;
        inclusive = false;

        var left = model.GetConstantValue(binary.Left, cancellationToken);
        var right = model.GetConstantValue(binary.Right, cancellationToken);

        if (!left.HasValue && right.HasValue && right.Value is int rightLimit)
        {
            return TryReadRightBound(binary.Kind(), binary.Left, rightLimit, out capped, out limit, out inclusive);
        }

        return left.HasValue && !right.HasValue && left.Value is int leftLimit
               && TryReadLeftBound(binary.Kind(), binary.Right, leftLimit, out capped, out limit, out inclusive);
    }

    private static bool TryReadRightBound(
        SyntaxKind kind,
        ExpressionSyntax expression,
        int limit,
        out ExpressionSyntax capped,
        out int count,
        out bool inclusive)
    {
        capped = expression;
        count = limit;
        inclusive = kind is SyntaxKind.LessThanOrEqualExpression;
        return kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression;
    }

    private static bool TryReadLeftBound(
        SyntaxKind kind,
        ExpressionSyntax expression,
        int limit,
        out ExpressionSyntax capped,
        out int count,
        out bool inclusive)
    {
        capped = expression;
        count = limit;
        inclusive = kind is SyntaxKind.GreaterThanOrEqualExpression;
        return kind is SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static bool TryGetSize(ITypeSymbol type, out int size)
    {
        return TryGetSize(type, new HashSet<ISymbol>(SymbolEqualityComparer.Default), out size);
    }

    private static bool TryGetSize(ITypeSymbol type, HashSet<ISymbol> visiting, out int size)
    {
        size = 0;

        if (type is IPointerTypeSymbol)
        {
            size = PointerSize;
            return true;
        }

        if (PrimitiveSizes.TryGetValue(type.SpecialType, out size))
        {
            return true;
        }

        return type.TypeKind is TypeKind.Enum
            ? TryGetEnumSize(type, visiting, out size)
            : TryGetStructSize(type, visiting, out size);
    }

    private static bool TryGetEnumSize(ITypeSymbol type, HashSet<ISymbol> visiting, out int size)
    {
        size = 0;
        return type is INamedTypeSymbol { EnumUnderlyingType: { } underlying }
               && TryGetSize(underlying, visiting, out size);
    }

    private static bool TryGetStructSize(ITypeSymbol type, HashSet<ISymbol> visiting, out int size)
    {
        size = 0;

        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Struct } named || !named.IsUnmanagedType)
        {
            return false;
        }

        if (!visiting.Add(type))
        {
            return false;
        }

        var measured = TryMeasure(named, visiting, out size);
        _ = visiting.Remove(type);
        return measured;
    }

    private static bool TryMeasure(INamedTypeSymbol type, HashSet<ISymbol> visiting, out int size)
    {
        size = 0;
        var offset = 0;
        var alignment = 1;
        var hasField = false;

        foreach (var field in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.IsStatic)
            {
                continue;
            }

            if (!TryGetSize(field.Type, visiting, out var fieldSize))
            {
                return false;
            }

            var fieldAlignment = Math.Min(fieldSize, MaxFieldAlignment);
            alignment = Math.Max(alignment, fieldAlignment);
            offset = Align(offset, fieldAlignment) + fieldSize;
            hasField = true;
        }

        size = hasField ? Align(offset, alignment) : 1;
        return true;
    }

    private static int Align(int offset, int alignment)
    {
        return alignment <= 1 ? offset : (offset + alignment - 1) & ~(alignment - 1);
    }
}
