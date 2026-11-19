using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Enum constant normalization shared by the enum value rules. One copy owns the signed and
///     unsigned width handling and the <c lang="csharp">[Flags]</c> test.
/// </summary>
internal static class EnumConstants
{
    /// <summary>
    ///     Converts a boxed integral constant to one bit pattern in the enum's underlying width.
    ///     A signed value keeps its two's-complement pattern, so an alias such as <c lang="csharp">-1</c>
    ///     matches a member with the same value.
    /// </summary>
    internal static bool TryNormalize(object? constant, INamedTypeSymbol enumType, out ulong value)
    {
        value = 0;
        if (constant is null)
        {
            return false;
        }

        try
        {
            value = IsUnsigned(enumType.EnumUnderlyingType)
                ? Convert.ToUInt64(constant, CultureInfo.InvariantCulture)
                : unchecked((ulong)Convert.ToInt64(constant, CultureInfo.InvariantCulture));
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    internal static bool IsPowerOfTwo(ulong value)
    {
        return value is not 0 && (value & (value - 1)) is 0;
    }

    /// <summary>
    ///     True for a signed constant below zero. A negative flags value is often an
    ///     all-bits member, so the flags rule leaves it out of the combination check.
    /// </summary>
    internal static bool IsNegative(object? constant, INamedTypeSymbol enumType)
    {
        if (constant is null || IsUnsigned(enumType.EnumUnderlyingType))
        {
            return false;
        }

        try
        {
            return Convert.ToInt64(constant, CultureInfo.InvariantCulture) < 0;
        }
        catch (OverflowException)
        {
            return false;
        }
        catch (InvalidCastException)
        {
            return false;
        }
    }

    internal static bool IsFlags(INamedTypeSymbol enumType, INamedTypeSymbol? flagsAttribute)
    {
        return flagsAttribute is not null
            && enumType.GetAttributes().Any(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, flagsAttribute));
    }

    private static bool IsUnsigned(INamedTypeSymbol? underlyingType)
    {
        return underlyingType?.SpecialType
            is SpecialType.System_Byte
            or SpecialType.System_UInt16
            or SpecialType.System_UInt32
            or SpecialType.System_UInt64;
    }
}
