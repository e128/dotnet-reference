using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace E128.Analyzers;

/// <summary>
///     Matches the <c lang="csharp">for (var i = 0; i &lt; source.Length; i++)</c> shape. The
///     performance rules that rewrite such a loop read the shape through this helper.
/// </summary>
internal static class IndexedForLoop
{
    internal static bool TryMatch(
        ForStatementSyntax forStatement,
        out string variable,
        out ExpressionSyntax? boundOwner,
        out StatementSyntax body)
    {
        variable = string.Empty;
        boundOwner = null;
        body = forStatement.Statement;

        if (forStatement.Declaration is not { Variables.Count: 1 } declaration)
        {
            return false;
        }

        var declarator = declaration.Variables[0];

        if (declarator.Initializer?.Value is not LiteralExpressionSyntax initial
            || !initial.IsKind(SyntaxKind.NumericLiteralExpression)
            || !string.Equals(initial.Token.ValueText, "0", StringComparison.Ordinal)
            || forStatement.Condition is not BinaryExpressionSyntax condition
            || !condition.IsKind(SyntaxKind.LessThanExpression)
            || condition.Left is not IdentifierNameSyntax conditionVariable
            || condition.Right is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } length
            || forStatement.Incrementors.Count != 1
            || forStatement.Incrementors[0] is not PostfixUnaryExpressionSyntax increment
            || !increment.IsKind(SyntaxKind.PostIncrementExpression)
            || increment.Operand is not IdentifierNameSyntax incrementVariable
            || !string.Equals(declarator.Identifier.ValueText, conditionVariable.Identifier.ValueText, StringComparison.Ordinal)
            || !string.Equals(declarator.Identifier.ValueText, incrementVariable.Identifier.ValueText, StringComparison.Ordinal))
        {
            return false;
        }

        variable = declarator.Identifier.ValueText;
        boundOwner = length.Expression;
        return true;
    }
}
