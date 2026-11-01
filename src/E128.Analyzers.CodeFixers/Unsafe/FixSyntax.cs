using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Unsafe;

internal static class FixSyntax
{
    /// <summary>
    ///     Wraps an expression in parentheses unless it is already a primary expression,
    ///     so appending an indexer or member access keeps the original meaning.
    /// </summary>
    internal static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression)
    {
        var bare = expression.WithoutTrivia();

        return bare is IdentifierNameSyntax
            or MemberAccessExpressionSyntax
            or InvocationExpressionSyntax
            or ElementAccessExpressionSyntax
            or ThisExpressionSyntax
            or ParenthesizedExpressionSyntax
            ? bare
            : SyntaxFactory.ParenthesizedExpression(bare);
    }

    internal static ElementAccessExpressionSyntax IndexInto(ExpressionSyntax target, ExpressionSyntax index)
    {
        return SyntaxFactory.ElementAccessExpression(
            ParenthesizeIfNeeded(target),
            SyntaxFactory.BracketedArgumentList(
                SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(index.WithoutTrivia()))));
    }
}
