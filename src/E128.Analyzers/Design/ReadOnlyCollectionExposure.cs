using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Design;

/// <summary>
///     Finds a field or property of a mutable collection that a member returns as a read-only
///     interface. The List rules and the Dictionary rules share this walk.
/// </summary>
internal static class ReadOnlyCollectionExposure
{
    /// <summary>
    ///     Returns the location of the exposed reference, or null when the return does not expose a
    ///     mutable collection.
    /// </summary>
    internal static Location? Find(
        MutableCollectionKind kind,
        IOperation operation,
        ISymbol containingSymbol)
    {
        if (operation is not IReturnOperation { ReturnedValue: { } returnedValue })
        {
            return null;
        }

        // An implicit List<T> to IReadOnlyList<T> conversion wraps the field reference, so the
        // returned value type alone does not identify the underlying collection.
        while (returnedValue is IConversionOperation { IsImplicit: true } conversion)
        {
            returnedValue = conversion.Operand;
        }

        if (returnedValue.Type is not { } returnedType
            || !MutableCollectionCatalog.IsMutable(kind, returnedType))
        {
            return null;
        }

        // A collection the method just built has no other reference, so there is nothing to protect.
        if (returnedValue is not (IFieldReferenceOperation or IPropertyReferenceOperation))
        {
            return null;
        }

        var expectedReturn = UnwrapTask(ReturnTypes.Get(containingSymbol));
        return expectedReturn is not null && MutableCollectionCatalog.IsReadOnlyInterface(kind, expectedReturn)
            ? returnedValue.Syntax.GetLocation()
            : null;
    }

    private static ITypeSymbol? UnwrapTask(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol { TypeArguments.Length: 1 } named && TaskTypes.IsTaskOrValueTask(named)
            ? named.TypeArguments[0]
            : type;
    }
}
