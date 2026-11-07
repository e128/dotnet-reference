using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Attribute-argument queries shared by the literal rules.
/// </summary>
internal static class AttributeArguments
{
    /// <summary>
    ///     True when the node or an ancestor is an attribute argument.
    /// </summary>
    internal static bool IsInside(SyntaxNode node)
    {
        return node.FirstAncestorOrSelf<AttributeArgumentSyntax>() is not null;
    }
}
