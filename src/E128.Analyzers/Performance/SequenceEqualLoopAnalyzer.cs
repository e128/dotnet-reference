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
        "E128-Performance",
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
        if (!IndexedForLoop.TryMatch(forStatement, out var loopVariable, out var boundOwner, out _))
        {
            return null;
        }

        if (GetReturnFalseComparison(forStatement) is not { } comparison
            || GetElementAccess(comparison.Left, loopVariable) is not { } left
            || GetElementAccess(comparison.Right, loopVariable) is not { } right
            || forStatement.Parent is not BlockSyntax block)
        {
            return null;
        }

        var index = block.Statements.IndexOf(forStatement);

        return IsConvertibleLoop(block, index, boundOwner, (left, right), semanticModel, cancellationToken)
            ? (left, right)
            : null;
    }

    // Matches `for (...) { if (a[i] != b[i]) { return false; } }` and returns the comparison.
    private static BinaryExpressionSyntax? GetReturnFalseComparison(ForStatementSyntax forStatement)
    {
        if (forStatement.Statement is not BlockSyntax loopBody
            || loopBody.Statements.Count is not 1
            || loopBody.Statements[0] is not IfStatementSyntax ifStatement
            || ifStatement.Condition is not BinaryExpressionSyntax comparison
            || !comparison.IsKind(SyntaxKind.NotEqualsExpression))
        {
            return null;
        }

        return GetSingleStatement(ifStatement.Statement) is ReturnStatementSyntax { Expression: LiteralExpressionSyntax falseLiteral }
            && falseLiteral.IsKind(SyntaxKind.FalseLiteralExpression)
            ? comparison
            : null;
    }

    private static bool IsConvertibleLoop(
        BlockSyntax block,
        int index,
        ExpressionSyntax? boundOwner,
        (ExpressionSyntax Left, ExpressionSyntax Right) operands,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        return IsFollowedByTrueReturn(block, index)
            && IsOperand(boundOwner, operands.Left, operands.Right)
            && HasLengthGuard(block, index, operands.Left, operands.Right)
            && HaveSafeElementType(operands.Left, operands.Right, semanticModel, cancellationToken);
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
        if (GetGuardComparison(block, index) is not { } found
            || found.Comparison.Left is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } leftLength
            || found.Comparison.Right is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Length" } rightLength
            || !ReturnsFalse(found.Guard.Statement))
        {
            return false;
        }

        return (SyntaxFactory.AreEquivalent(leftLength.Expression, left)
                && SyntaxFactory.AreEquivalent(rightLength.Expression, right))
            || (SyntaxFactory.AreEquivalent(leftLength.Expression, right)
                && SyntaxFactory.AreEquivalent(rightLength.Expression, left));
    }

    private static (IfStatementSyntax Guard, BinaryExpressionSyntax Comparison)? GetGuardComparison(
        BlockSyntax block,
        int index)
    {
        return index >= 1
            && block.Statements[index - 1] is IfStatementSyntax { Else: null, Condition: BinaryExpressionSyntax comparison } guard
            && comparison.IsKind(SyntaxKind.NotEqualsExpression)
            ? (guard, comparison)
            : null;
    }

    private static bool ReturnsFalse(StatementSyntax statement)
    {
        return GetEmbeddedStatement(statement) is ReturnStatementSyntax { Expression: LiteralExpressionSyntax literal }
            && literal.IsKind(SyntaxKind.FalseLiteralExpression);
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
        return element.TypeKind is TypeKind.Enum
            || IsNarrowIntegral(element.SpecialType)
            || IsWideIntegral(element.SpecialType);
    }

    private static bool IsNarrowIntegral(SpecialType specialType)
    {
        return specialType is SpecialType.System_Boolean
            or SpecialType.System_Char
            or SpecialType.System_SByte
            or SpecialType.System_Byte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16;
    }

    private static bool IsWideIntegral(SpecialType specialType)
    {
        return specialType is SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64;
    }

    private static StatementSyntax? GetSingleStatement(StatementSyntax statement)
    {
        return statement is BlockSyntax block && block.Statements.Count is 1
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

    private static ExpressionSyntax? GetElementAccess(ExpressionSyntax expression, string loopVariable)
    {
        return expression is ElementAccessExpressionSyntax elementAccess
            && elementAccess.ArgumentList.Arguments.Count is 1
            && elementAccess.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax identifier
            && string.Equals(identifier.Identifier.ValueText, loopVariable, StringComparison.Ordinal)
            ? elementAccess.Expression
            : null;
    }
}
