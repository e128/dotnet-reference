using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SequenceEqualLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128121";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use SequenceEqual instead of an element-by-element loop",
        "This loop reimplements SequenceEqual. The span method is vectorized and bounds-checked.",
        "Performance",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeFor, SyntaxKind.ForStatement);
    }

    private static void AnalyzeFor(SyntaxNodeAnalysisContext context)
    {
        var forStatement = (ForStatementSyntax)context.Node;

        if (TryGetOperands(forStatement, context.SemanticModel, context.CancellationToken) is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, forStatement.GetLocation()));
    }

    internal static (ExpressionSyntax Left, ExpressionSyntax Right)? TryGetOperands(
        ForStatementSyntax forStatement,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (!TryGetLoopVariable(forStatement, out var loopVariable, out var boundOwner))
        {
            return null;
        }

        if (forStatement.Statement is not BlockSyntax loopBody
            || loopBody.Statements.Count != 1
            || loopBody.Statements[0] is not IfStatementSyntax ifStatement
            || ifStatement.Condition is not BinaryExpressionSyntax comparison
            || !comparison.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return null;
        }

        if (GetSingleStatement(ifStatement.Statement) is not ReturnStatementSyntax
            {
                Expression: LiteralExpressionSyntax falseLiteral,
            }
            || !falseLiteral.IsKind(SyntaxKind.FalseLiteralExpression))
        {
            return null;
        }

        if (GetElementAccess(comparison.Left, loopVariable) is not { } left
            || GetElementAccess(comparison.Right, loopVariable) is not { } right)
        {
            return null;
        }

        if (forStatement.Parent is not BlockSyntax block)
        {
            return null;
        }

        var index = block.Statements.IndexOf(forStatement);

        return IsFollowedByTrueReturn(block, index)
            && IsOperand(boundOwner, left, right)
            && HasLengthGuard(block, index, left, right)
            && HaveSafeElementType(left, right, semanticModel, cancellationToken)
            ? (left, right)
            : null;
    }

    private static bool IsOperand(ExpressionSyntax? owner, ExpressionSyntax left, ExpressionSyntax right)
    {
        return owner is not null
            && (SyntaxFactory.AreEquivalent(owner, left) || SyntaxFactory.AreEquivalent(owner, right));
    }

    // SequenceEqual compares lengths first. The loop does not, so the fix is only safe
    // when the code before the loop already returns false on a length mismatch.
    private static bool HasLengthGuard(BlockSyntax block, int index, ExpressionSyntax left, ExpressionSyntax right)
    {
        if (index < 1
            || block.Statements[index - 1] is not IfStatementSyntax guard
            || guard.Else is not null
            || guard.Condition is not BinaryExpressionSyntax comparison
            || !comparison.IsKind(SyntaxKind.NotEqualsExpression)
            || comparison.Left is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } leftLength
            || comparison.Right is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } rightLength)
        {
            return false;
        }

        var returnsFalse = GetEmbeddedStatement(guard.Statement) is ReturnStatementSyntax
        {
            Expression: LiteralExpressionSyntax literal,
        } && literal.IsKind(SyntaxKind.FalseLiteralExpression);

        return returnsFalse
            && ((SyntaxFactory.AreEquivalent(leftLength.Expression, left)
                    && SyntaxFactory.AreEquivalent(rightLength.Expression, right))
                || (SyntaxFactory.AreEquivalent(leftLength.Expression, right)
                    && SyntaxFactory.AreEquivalent(rightLength.Expression, left)));
    }

    private static StatementSyntax GetEmbeddedStatement(StatementSyntax statement)
    {
        return statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : statement;
    }

    // Floating-point elements are excluded: != treats NaN as unequal, SequenceEqual does not.
    private static bool HaveSafeElementType(
        ExpressionSyntax left,
        ExpressionSyntax right,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (semanticModel.GetTypeInfo(left, cancellationToken).Type is not IArrayTypeSymbol { Rank: 1 } leftArray
            || semanticModel.GetTypeInfo(right, cancellationToken).Type is not IArrayTypeSymbol { Rank: 1 } rightArray
            || !SymbolEqualityComparer.Default.Equals(leftArray.ElementType, rightArray.ElementType))
        {
            return false;
        }

        var element = leftArray.ElementType;
        return element.TypeKind == TypeKind.Enum
            || element.SpecialType is SpecialType.System_Boolean
                or SpecialType.System_Char
                or SpecialType.System_SByte
                or SpecialType.System_Byte
                or SpecialType.System_Int16
                or SpecialType.System_UInt16
                or SpecialType.System_Int32
                or SpecialType.System_UInt32
                or SpecialType.System_Int64
                or SpecialType.System_UInt64;
    }

    private static StatementSyntax? GetSingleStatement(StatementSyntax statement)
    {
        return statement is BlockSyntax block && block.Statements.Count == 1
            ? block.Statements[0]
            : null;
    }

    private static bool IsFollowedByTrueReturn(BlockSyntax block, int index)
    {
        return index >= 0
            && index + 1 < block.Statements.Count
            && block.Statements[index + 1] is ReturnStatementSyntax { Expression: LiteralExpressionSyntax trueLiteral }
            && trueLiteral.IsKind(SyntaxKind.TrueLiteralExpression);
    }

    private static bool TryGetLoopVariable(ForStatementSyntax forStatement, out string loopVariable, out ExpressionSyntax? boundOwner)
    {
        loopVariable = string.Empty;
        boundOwner = null;

        if (forStatement.Declaration is not { Variables.Count: 1 } declaration)
        {
            return false;
        }

        var variable = declaration.Variables[0];

        if (variable.Initializer?.Value is not LiteralExpressionSyntax initial
            || !initial.IsKind(SyntaxKind.NumericLiteralExpression)
            || !string.Equals(initial.Token.ValueText, "0", StringComparison.Ordinal)
            || forStatement.Condition is not BinaryExpressionSyntax condition
            || !condition.IsKind(SyntaxKind.LessThanExpression)
            || condition.Left is not IdentifierNameSyntax conditionVariable
            || condition.Right is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } lengthAccess
            || forStatement.Incrementors.Count != 1
            || forStatement.Incrementors[0] is not PostfixUnaryExpressionSyntax increment
            || !increment.IsKind(SyntaxKind.PostIncrementExpression)
            || increment.Operand is not IdentifierNameSyntax incrementVariable)
        {
            return false;
        }

        if (!string.Equals(variable.Identifier.ValueText, conditionVariable.Identifier.ValueText, StringComparison.Ordinal)
            || !string.Equals(variable.Identifier.ValueText, incrementVariable.Identifier.ValueText, StringComparison.Ordinal))
        {
            return false;
        }

        loopVariable = variable.Identifier.ValueText;
        boundOwner = lengthAccess.Expression;
        return true;
    }

    private static ExpressionSyntax? GetElementAccess(ExpressionSyntax expression, string loopVariable)
    {
        return expression is ElementAccessExpressionSyntax elementAccess
            && elementAccess.ArgumentList.Arguments.Count == 1
            && elementAccess.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax identifier
            && string.Equals(identifier.Identifier.ValueText, loopVariable, StringComparison.Ordinal)
            ? elementAccess.Expression
            : null;
    }
}
