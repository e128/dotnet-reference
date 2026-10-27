using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

internal static class RefUsage
{
    /// <summary>
    ///     Returns true when the call result is used as a reference (assigned to, passed by ref,
    ///     bound to a ref local or returned by ref). A value-copy replacement such as
    ///     Unsafe.BitCast cannot stand in for such a use.
    /// </summary>
    internal static bool IsUsedAsReference(InvocationExpressionSyntax invocation)
    {
        SyntaxNode node = invocation;

        while (true)
        {
            var parent = node.Parent;
            if (parent is ParenthesizedExpressionSyntax
                || (parent is MemberAccessExpressionSyntax access && access.Expression == node))
            {
                node = parent;
                continue;
            }

            return parent switch
            {
                RefExpressionSyntax => true,
                ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None),
                AssignmentExpressionSyntax assignment => assignment.Left == node,
                PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression)
                    || prefix.IsKind(SyntaxKind.PreDecrementExpression),
                PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression)
                    || postfix.IsKind(SyntaxKind.PostDecrementExpression),
                _ => false,
            };
        }
    }
}
