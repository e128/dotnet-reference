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

        if (!IsZeroInitialized(declarator)
            || !TryMatchCondition(forStatement, out var conditionName, out var owner)
            || !TryGetIncrementedName(forStatement, out var incrementName)
            || !string.Equals(declarator.Identifier.ValueText, conditionName, StringComparison.Ordinal)
            || !string.Equals(declarator.Identifier.ValueText, incrementName, StringComparison.Ordinal))
        {
            return false;
        }

        variable = declarator.Identifier.ValueText;
        boundOwner = owner;
        return true;
    }

    private static bool IsZeroInitialized(VariableDeclaratorSyntax declarator)
    {
        return declarator.Initializer?.Value is LiteralExpressionSyntax initial
               && initial.IsKind(SyntaxKind.NumericLiteralExpression)
               && string.Equals(initial.Token.ValueText, "0", StringComparison.Ordinal);
    }

    private static bool TryMatchCondition(
        ForStatementSyntax forStatement,
        out string conditionName,
        out ExpressionSyntax? owner)
    {
        conditionName = string.Empty;
        owner = null;
        if (forStatement.Condition is not BinaryExpressionSyntax condition
            || !condition.IsKind(SyntaxKind.LessThanExpression)
            || condition.Left is not IdentifierNameSyntax conditionVariable
            || condition.Right is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } length)
        {
            return false;
        }

        conditionName = conditionVariable.Identifier.ValueText;
        owner = length.Expression;
        return true;
    }

    private static bool TryGetIncrementedName(ForStatementSyntax forStatement, out string name)
    {
        name = string.Empty;
        if (forStatement.Incrementors.Count is not 1
            || forStatement.Incrementors[0] is not PostfixUnaryExpressionSyntax increment
            || !increment.IsKind(SyntaxKind.PostIncrementExpression)
            || increment.Operand is not IdentifierNameSyntax incrementVariable)
        {
            return false;
        }

        name = incrementVariable.Identifier.ValueText;
        return true;
    }
}
