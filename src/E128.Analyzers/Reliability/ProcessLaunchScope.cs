using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Syntax helpers shared by the process rules. A process, its start info, and its standard
///     streams resolve through a member chain to one root symbol.
/// </summary>
internal static class ProcessLaunchScope
{
    /// <summary>
    ///     Resolves the leftmost receiver of a member chain, so <c lang="csharp">process</c>,
    ///     <c lang="csharp">process.StartInfo</c>, and <c lang="csharp">process.StandardOutput</c>
    ///     all resolve to the same process symbol.
    /// </summary>
    internal static ISymbol? GetRootSymbol(
        ExpressionSyntax expression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var current = expression;

        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            current = memberAccess.Expression;
        }

        return model.GetSymbolInfo(current, cancellationToken).Symbol;
    }

    /// <summary>
    ///     Returns the body that holds both the configuration and the wait. A lambda body is the
    ///     scope for a wait inside a callback, because the enclosing method holds no relation to
    ///     the process.
    /// </summary>
    internal static SyntaxNode? GetScope(InvocationExpressionSyntax invocation)
    {
        return BlockScope.GetScope(invocation);
    }

    internal static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        var current = expression;

        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    current = parenthesized.Expression;
                    break;

                case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    current = postfix.Operand;
                    break;

                default:
                    return current;
            }
        }
    }
}
