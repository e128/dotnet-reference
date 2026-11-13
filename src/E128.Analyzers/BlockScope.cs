using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Resolves the body that one analysis reads. A lambda body is the scope for a node inside a
///     callback. A method body or expression body is the scope otherwise.
/// </summary>
internal static class BlockScope
{
    internal static SyntaxNode? GetScope(SyntaxNode node)
    {
        if (node.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() is { } lambda)
        {
            return lambda.Body;
        }

        if (node.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>() is { } method)
        {
            return (SyntaxNode?)method.Body ?? method.ExpressionBody;
        }

        return node.FirstAncestorOrSelf<BlockSyntax>();
    }
}
