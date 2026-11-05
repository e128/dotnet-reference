using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class EnclosingBody
{
    /// <summary>
    ///     Returns the block body of the nearest enclosing method, constructor, local function,
    ///     accessor, or anonymous function. Returns null for an expression body or no enclosing member.
    /// </summary>
    internal static BlockSyntax? Find(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is MethodDeclarationSyntax method)
            {
                return method.Body;
            }

            if (ancestor is ConstructorDeclarationSyntax constructor)
            {
                return constructor.Body;
            }

            if (ancestor is LocalFunctionStatementSyntax localFunction)
            {
                return localFunction.Body;
            }

            if (ancestor is AccessorDeclarationSyntax accessor)
            {
                return accessor.Body;
            }

            if (ancestor is AnonymousFunctionExpressionSyntax anonymous)
            {
                return anonymous.Block;
            }
        }

        return null;
    }
}
