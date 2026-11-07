using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Security;

internal static partial class FipsSymbolHelpers
{
    // Cap how much source-derived text the helpers decode or scan. A hostile literal must not
    // inflate analyzer memory and time. An over-cap value falls through to the fail-closed report.
    private const int MaxEncodedConstantLength = 1 << 20;

    /// <summary>
    ///     Reads the byte length of a buffer when it is a fixed-size array (<c lang="csharp">new byte[N]</c>,
    ///     <c lang="csharp">new byte[] { ... }</c>), a collection expression, a <c lang="csharp">stackalloc</c>, or an encoded
    ///     string with a constant argument, either written inline or held in a <c lang="csharp">readonly</c> field or an
    ///     unreassigned local.
    /// </summary>
    public static bool TryGetConstantByteLength(IOperation? operation, out int length, int depth = 0)
    {
        length = 0;
        operation = Unwrap(operation);

        if (operation is IInvocationOperation invocation && TryGetKnownByteLength(invocation, out length, depth))
        {
            return true;
        }

        switch (operation)
        {
            case null:
                return false;

            case IArrayCreationOperation array:
                if (array.DimensionSizes.Length == 1 && TryGetConstant<int>(array.DimensionSizes[0], out var size))
                {
                    length = size;
                    return true;
                }

                // 'new byte[] { ... }' carries no dimension size, so the element count is the length.
                if (array.Initializer is not null)
                {
                    length = array.Initializer.ElementValues.Length;
                    return true;
                }

                return false;

            case ICollectionExpressionOperation collection:
                foreach (var element in collection.Elements)
                {
                    if (element is ISpreadOperation)
                    {
                        return false;
                    }
                }

                length = collection.Elements.Length;
                return true;

            case IImplicitIndexerReferenceOperation { Argument: { } rangeArgument } indexer
                when Unwrap(rangeArgument) is IRangeOperation range:
                return TryGetRangeLength(indexer.Instance, range.LeftOperand, range.RightOperand, depth, out length);

            case IArrayElementReferenceOperation { Indices.Length: 1 } element
                when Unwrap(element.Indices[0]) is IRangeOperation arrayRange:
                return TryGetRangeLength(element.ArrayReference, arrayRange.LeftOperand, arrayRange.RightOperand, depth, out length);

            case IFieldReferenceOperation or ILocalReferenceOperation:
                return depth < MaxResolveDepth &&
                    ResolveInitializer(operation) is { } resolved &&
                    TryGetConstantByteLength(resolved, out length, depth + 1);

            default:
                // Roslyn does not expose a public operation interface for stackalloc, so read the syntax.
                return TryGetStackAllocLength(operation, out length);
        }
    }

    private static bool TryGetKnownByteLength(IInvocationOperation invocation, out int length, int depth)
    {
        length = 0;
        if (depth >= MaxResolveDepth)
        {
            return false;
        }

        if (TryGetSliceLength(invocation, out length, depth))
        {
            return true;
        }

        if (string.Equals(invocation.TargetMethod.Name, "GetBytes", StringComparison.Ordinal) &&
            NamespaceEquals(invocation.TargetMethod.ContainingNamespace, "System.Text") &&
            Unwrap(ResolveInitializer(invocation.Instance) ?? invocation.Instance) is IPropertyReferenceOperation encodingProperty &&
            encodingProperty.Property.IsStatic &&
            IsType(encodingProperty.Property.ContainingType, "Encoding", "System.Text") &&
            TryGetResolvedConstant<string>(ArgumentAt(invocation.Arguments, 0)?.Value, out var text, depth + 1) &&
            GetEncoding(encodingProperty.Property.Name) is { } encoding)
        {
            if (text.Length > MaxEncodedConstantLength)
            {
                return false;
            }

            length = encoding.GetByteCount(text);
            return true;
        }

        return invocation.TargetMethod.IsStatic &&
            IsType(invocation.TargetMethod.ContainingType, "Convert", "System") &&
            invocation.Arguments.Length == 1 &&
            TryGetResolvedConstant<string>(invocation.Arguments[0].Value, out var encoded, depth + 1) &&
            invocation.TargetMethod.Name switch
            {
                "FromBase64String" => TryGetBase64ByteLength(encoded, out length),
                "FromHexString" => TryGetHexByteLength(encoded, out length),
                _ => false,
            };
    }

    /// <summary>
    ///     Reads the length of a slice: <c lang="csharp">AsSpan</c> or <c lang="csharp">AsMemory</c> with a start, a
    ///     length or a range, an instance <c lang="csharp">Slice</c>, or
    ///     <c lang="csharp">RuntimeHelpers.GetSubArray</c>. A constant length argument decides the result even when
    ///     the source length is unknown.
    /// </summary>
    private static bool TryGetSliceLength(IInvocationOperation invocation, out int length, int depth)
    {
        length = 0;
        var method = invocation.TargetMethod;
        var isSubArray = string.Equals(method.Name, "GetSubArray", StringComparison.Ordinal) &&
            IsType(method.ContainingType, "RuntimeHelpers", "System.Runtime.CompilerServices");

        if (!isSubArray &&
            ((!string.Equals(method.Name, "Slice", StringComparison.Ordinal) &&
              !string.Equals(method.Name, "AsSpan", StringComparison.Ordinal) &&
              !string.Equals(method.Name, "AsMemory", StringComparison.Ordinal)) ||
             !NamespaceEquals(method.ContainingNamespace, "System")))
        {
            return false;
        }

        // An extension method receives the source as its first argument, so the extra arguments follow it.
        var source = invocation.Instance ?? ArgumentAt(invocation.Arguments, 0)?.Value;
        var extraArguments = invocation.Instance is null ? invocation.Arguments.Length - 1 : invocation.Arguments.Length;

        if (ArgumentNamed(invocation.Arguments, "range") is { } rangeArgument)
        {
            return Unwrap(rangeArgument.Value) is IRangeOperation range &&
                TryGetRangeLength(source, range.LeftOperand, range.RightOperand, depth, out length);
        }

        if (ArgumentNamed(invocation.Arguments, nameof(length)) is { } lengthArgument)
        {
            if (TryGetResolvedConstant<int>(lengthArgument.Value, out var count, depth + 1) && count >= 0)
            {
                length = count;

                return true;
            }

            return false;
        }

        if (extraArguments == 0)
        {
            return !isSubArray && TryGetConstantByteLength(source, out length, depth + 1);
        }

        var startArgument = ArgumentNamed(invocation.Arguments, "start") ?? ArgumentNamed(invocation.Arguments, "startIndex");
        if (startArgument is null ||
            !TryGetIndexValue(startArgument.Value, source, depth, out var start) ||
            !TryGetConstantByteLength(source, out var total, depth + 1) ||
            total < start)
        {
            return false;
        }

        length = total - start;

        return true;
    }

    /// <summary>
    ///     Reads the length of <c lang="csharp">source[left..right]</c>. An omitted left bound is 0. An omitted right
    ///     bound, or an index from the end (<c lang="csharp">^4</c>), needs a source of known length.
    /// </summary>
    private static bool TryGetRangeLength(IOperation? source, IOperation? left, IOperation? right, int depth, out int length)
    {
        length = 0;
        if (depth >= MaxResolveDepth)
        {
            return false;
        }

        var start = 0;
        if (left is not null && !TryGetIndexValue(left, source, depth, out start))
        {
            return false;
        }

        int end;
        if (right is null)
        {
            if (!TryGetConstantByteLength(source, out end, depth + 1))
            {
                return false;
            }
        }
        else if (!TryGetIndexValue(right, source, depth, out end))
        {
            return false;
        }

        if (end < start)
        {
            return false;
        }

        length = end - start;

        return true;
    }

    /// <summary>
    ///     Reads a constant index, or an index from the end (<c lang="csharp">^4</c>) when the length of
    ///     <paramref name="source" /> is known.
    /// </summary>
    private static bool TryGetIndexValue(IOperation index, IOperation? source, int depth, out int value)
    {
        value = 0;
        var operation = Unwrap(index);
        if (operation is IUnaryOperation { OperatorKind: UnaryOperatorKind.Hat } hat)
        {
            if (!TryGetResolvedConstant<int>(hat.Operand, out var fromEnd, depth + 1) ||
                !TryGetConstantByteLength(source, out var total, depth + 1) ||
                total < fromEnd)
            {
                return false;
            }

            value = total - fromEnd;

            return true;
        }

        return TryGetResolvedConstant(operation, out value, depth + 1) && value >= 0;
    }

    private static bool TryGetBase64ByteLength(string value, out int length)
    {
        length = 0;
        if (value.Length > MaxEncodedConstantLength)
        {
            return false;
        }

        try
        {
            length = Convert.FromBase64String(value).Length;
            return true;
        }
        catch (FormatException)
        {
            length = 0;
            return false;
        }
    }

    private static bool TryGetHexByteLength(string value, out int length)
    {
        length = 0;
        if (value.Length > MaxEncodedConstantLength || value.Length % 2 != 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        length = value.Length >> 1;
        return true;
    }

    private static Encoding? GetEncoding(string propertyName) => propertyName switch
    {
        "UTF8" => Encoding.UTF8,
        "ASCII" => Encoding.ASCII,
        "Unicode" => Encoding.Unicode,
        "Latin1" => Encoding.GetEncoding("iso-8859-1"),
        "UTF32" => Encoding.UTF32,
        _ => null,
    };

    private static bool TryGetStackAllocLength(IOperation operation, out int length)
    {
        length = 0;
        if (operation.Syntax is ImplicitStackAllocArrayCreationExpressionSyntax implicitStackAlloc)
        {
            length = implicitStackAlloc.Initializer.Expressions.Count;
            return true;
        }

        if (operation.Syntax is not StackAllocArrayCreationExpressionSyntax stackAlloc ||
            stackAlloc.Type is not ArrayTypeSyntax { RankSpecifiers.Count: 1 } arrayType ||
            arrayType.RankSpecifiers[0].Sizes.Count != 1)
        {
            return false;
        }

        var sizeExpression = arrayType.RankSpecifiers[0].Sizes[0];
        if (sizeExpression is OmittedArraySizeExpressionSyntax)
        {
            if (stackAlloc.Initializer is null)
            {
                return false;
            }

            length = stackAlloc.Initializer.Expressions.Count;
            return true;
        }

        var model = operation.SemanticModel;
        if (model is null)
        {
            return false;
        }

        var constant = model.GetConstantValue(sizeExpression);
        if (constant.HasValue && constant.Value is int count)
        {
            length = count;
            return true;
        }

        return false;
    }
}
