using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Object-initializer queries shared by the collection-capacity rules.
/// </summary>
internal static class InitializerAssignments
{
    /// <summary>
    ///     True when the object initializer assigns <paramref name="propertyName" />.
    /// </summary>
    internal static bool Assigns(BaseObjectCreationExpressionSyntax creation, string propertyName)
    {
        if (creation.Initializer is null)
        {
            return false;
        }

        foreach (var expression in creation.Initializer.Expressions)
        {
            if (expression is AssignmentExpressionSyntax assignment
                && string.Equals(GetAssignedName(assignment.Left), propertyName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns the simple name on the left of an initializer assignment, or null.
    /// </summary>
    internal static string? GetAssignedName(ExpressionSyntax left)
    {
        return left switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            _ => null
        };
    }
}
