using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Shared answers to "does this expression get written?". A write target is the left side of
///     an assignment, the operand of an increment or decrement, or a <c lang="csharp">ref</c> or
///     <c lang="csharp">out</c> argument.
/// </summary>
internal static class SyntaxWriteChecks
{
    /// <summary>
    ///     Returns true when <paramref name="expression" /> is assigned, incremented, decremented,
    ///     or passed by <c lang="csharp">ref</c> or <c lang="csharp">out</c>. Parentheses are ignored.
    /// </summary>
    internal static bool IsWriteTarget(ExpressionSyntax expression)
    {
        var target = Unwrap(expression);

        return IsAssignmentTarget(target)
               || IsIncrementOrDecrementTarget(target)
               || IsRefOrOutArgument(target);
    }

    /// <summary>
    ///     Returns true when <paramref name="expression" /> is the left side of an assignment.
    ///     Parentheses are ignored.
    /// </summary>
    internal static bool IsAssignmentTarget(ExpressionSyntax expression)
    {
        var target = Unwrap(expression);

        return target.Parent is AssignmentExpressionSyntax assignment && assignment.Left == target;
    }

    private static bool IsIncrementOrDecrementTarget(ExpressionSyntax target)
    {
        return target.Parent switch
        {
            PrefixUnaryExpressionSyntax prefix => prefix.Operand == target
                                                   && (prefix.IsKind(SyntaxKind.PreIncrementExpression)
                                                       || prefix.IsKind(SyntaxKind.PreDecrementExpression)),
            PostfixUnaryExpressionSyntax postfix => postfix.Operand == target
                                                    && (postfix.IsKind(SyntaxKind.PostIncrementExpression)
                                                        || postfix.IsKind(SyntaxKind.PostDecrementExpression)),
            _ => false
        };
    }

    private static bool IsRefOrOutArgument(ExpressionSyntax target)
    {
        return target.Parent is ArgumentSyntax argument
               && (argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                   || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword));
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;

        while (current.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized;
        }

        if (current.Parent is MemberAccessExpressionSyntax { Expression: var receiver } access
            && receiver == current)
        {
            current = access;
        }

        return current;
    }
}
