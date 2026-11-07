using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

internal static class ExpressionUnwrapper
{
    /// <summary>
    ///     Strips any enclosing parentheses from <paramref name="expression" />.
    /// </summary>
    internal static ExpressionSyntax Parentheses(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    /// <summary>
    ///     Returns the receiver of a member-access invocation, or null for another shape.
    /// </summary>
    internal static ExpressionSyntax? Receiver(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax access ? access.Expression : null;
    }

    /// <summary>
    ///     Strips any enclosing parentheses and explicit casts from <paramref name="expression" />.
    /// </summary>
    internal static ExpressionSyntax ParenthesesAndCasts(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    break;
                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    break;
                default:
                    return expression;
            }
        }
    }
}
