using System;
using Microsoft.CodeAnalysis;

namespace E128.Analyzers;

/// <summary>
///     Interface-implementation queries shared by the design and performance rules.
/// </summary>
internal static class InterfaceMembers
{
    /// <summary>
    ///     True when the method implements an interface member, including an explicit
    ///     implementation. A method that belongs to an interface or a delegate returns false.
    /// </summary>
    internal static bool Implements(IMethodSymbol method)
    {
        if (!method.ExplicitInterfaceImplementations.IsEmpty)
        {
            return true;
        }

        if (method.ContainingType is not INamedTypeSymbol containingType)
        {
            return false;
        }

        if (containingType.TypeKind is TypeKind.Interface || InheritsFromDelegate(containingType))
        {
            return false;
        }

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var ifaceMember in iface.GetMembers(method.Name).OfType<IMethodSymbol>())
            {
                var impl = containingType.FindImplementationForInterfaceMember(ifaceMember);
                if (impl is not null
                    && SymbolEqualityComparer.Default.Equals(impl.OriginalDefinition, method.OriginalDefinition))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     True when the containing type maps some interface member to this method.
    /// </summary>
    internal static bool ImplementsMember(IMethodSymbol method)
    {
        var containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        foreach (var iface in containingType.AllInterfaces)
        {
            foreach (var member in iface.GetMembers())
            {
                if (member is IMethodSymbol
                    && SymbolEqualityComparer.Default.Equals(
                        containingType.FindImplementationForInterfaceMember(member),
                        method))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool InheritsFromDelegate(INamedTypeSymbol type)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (string.Equals(current.Name, "MulticastDelegate", StringComparison.Ordinal)
                || string.Equals(current.Name, "Delegate", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
