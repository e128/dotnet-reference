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
        var current = expression;
        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    current = parenthesized.Expression;
                    break;

                case CastExpressionSyntax cast:
                    current = cast.Expression;
                    break;

                default:
                    return current;
            }
        }
    }
}
