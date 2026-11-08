using System.Linq;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers.Unsafe;

/// <summary>
///     True when a type can be copied as a raw bit pattern. A struct that holds a bool or decimal
///     is not blittable.
/// </summary>
internal static class Blittability
{
    internal static bool IsBlittable(ITypeSymbol type)
    {
        if (type.SpecialType is SpecialType.System_Boolean or SpecialType.System_Decimal)
        {
            return false;
        }

        // IsUnmanagedType accepts a struct that holds a bool or decimal, so check the fields too.
        return type.IsUnmanagedType
            && (type.TypeKind != TypeKind.Struct
                || type.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic).All(field => IsBlittable(field.Type)));
    }
}
