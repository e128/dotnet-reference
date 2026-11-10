using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128134: A loop counts the set bits of a value one bit at a time.
///     <c lang="csharp">BitOperations.PopCount</c> does the same work in one call.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManualBitCountAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128134";

    private const int LoopBodyStatementCount = 2;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use BitOperations.PopCount instead of a bit-count loop",
        "The loop counts set bits one at a time. Use BitOperations.PopCount.",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "BitOperations.PopCount maps to one hardware instruction on a supported CPU. The loop " +
        "runs one iteration per bit and keeps the value live for the whole loop.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName("System.Numerics.BitOperations") is not { } bitOperations
                || !bitOperations.GetMembers("PopCount").Any())
            {
                return;
            }

            start.RegisterSyntaxNodeAction(AnalyzeWhile, SyntaxKind.WhileStatement);
        });
    }

    private static void AnalyzeWhile(SyntaxNodeAnalysisContext context)
    {
        var loop = (WhileStatementSyntax)context.Node;

        if (!TryMatchLoop(loop, out var valueName, out var countName)
            || loop.Parent is not BlockSyntax block
            || IsUsedAfter(block, loop, valueName))
        {
            return;
        }

        if (!IsSupportedValue(context.SemanticModel, loop, valueName, context.CancellationToken)
            || !IsSupportedCount(context.SemanticModel, loop, countName, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, loop.GetLocation()));
    }

    /// <summary>
    ///     Matches the two-statement loop that adds the low bit and shifts the value right.
    ///     The analyzer and the fix provider call this one helper.
    /// </summary>
    internal static bool TryMatchLoop(WhileStatementSyntax loop, out string valueName, out string countName)
    {
        valueName = string.Empty;
        countName = string.Empty;

        if (loop.Condition is not BinaryExpressionSyntax condition
            || !condition.IsKind(SyntaxKind.NotEqualsExpression)
            || loop.Statement is not BlockSyntax body
            || body.Statements.Count is not LoopBodyStatementCount
            || !TryReadNotZero(condition, out var conditionName)
            || !TryMatchAddStatement(body.Statements[0], conditionName, out var count)
            || !IsShiftRightByOne(body.Statements[1], conditionName))
        {
            return false;
        }

        valueName = conditionName;
        countName = count;
        return true;
    }

    private static bool TryMatchAddStatement(StatementSyntax statement, string valueName, out string countName)
    {
        countName = string.Empty;

        if (statement is not ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax add }
            || !add.IsKind(SyntaxKind.AddAssignmentExpression)
            || add.Left is not IdentifierNameSyntax countIdentifier
            || add.Right is not BinaryExpressionSyntax bitwiseAnd
            || !bitwiseAnd.IsKind(SyntaxKind.BitwiseAndExpression)
            || bitwiseAnd.Left is not IdentifierNameSyntax masked
            || !IsLiteral(bitwiseAnd.Right, 1)
            || !string.Equals(valueName, masked.Identifier.ValueText, StringComparison.Ordinal))
        {
            return false;
        }

        countName = countIdentifier.Identifier.ValueText;

        return true;
    }

    private static bool IsShiftRightByOne(StatementSyntax statement, string valueName)
    {
        return statement is ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax shift }
            && shift.IsKind(SyntaxKind.RightShiftAssignmentExpression)
            && shift.Left is IdentifierNameSyntax shifted
            && IsLiteral(shift.Right, 1)
            && string.Equals(valueName, shifted.Identifier.ValueText, StringComparison.Ordinal);
    }

    private static bool TryReadNotZero(BinaryExpressionSyntax condition, out string name)
    {
        name = string.Empty;

        if (condition.Left is IdentifierNameSyntax left && IsLiteral(condition.Right, 0))
        {
            name = left.Identifier.ValueText;
            return true;
        }

        if (condition.Right is not IdentifierNameSyntax right || !IsLiteral(condition.Left, 0))
        {
            return false;
        }

        name = right.Identifier.ValueText;

        return true;
    }

    private static bool IsLiteral(ExpressionSyntax expression, int expected)
    {
        return expression is LiteralExpressionSyntax literal
            && literal.Token.Value is int value
            && value == expected;
    }

    private static bool IsUsedAfter(BlockSyntax block, WhileStatementSyntax loop, string name)
    {
        var found = false;

        for (var index = 0; index < block.Statements.Count; index++)
        {
            if (block.Statements[index] != loop)
            {
                continue;
            }

            for (var later = index + 1; later < block.Statements.Count; later++)
            {
                foreach (var node in block.Statements[later].DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                {
                    found |= string.Equals(node.Identifier.ValueText, name, StringComparison.Ordinal);
                }
            }

            break;
        }

        return found;
    }

    private static bool IsSupportedValue(
        SemanticModel model,
        WhileStatementSyntax loop,
        string name,
        System.Threading.CancellationToken cancellationToken)
    {
        return FindType(model, loop, name, cancellationToken) is { } type
            && type.SpecialType is SpecialType.System_Int32
                or SpecialType.System_UInt32
                or SpecialType.System_Int64
                or SpecialType.System_UInt64;
    }

    private static bool IsSupportedCount(
        SemanticModel model,
        WhileStatementSyntax loop,
        string name,
        System.Threading.CancellationToken cancellationToken)
    {
        return FindType(model, loop, name, cancellationToken) is { } type
            && type.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64;
    }

    internal static ITypeSymbol? FindType(
        SemanticModel model,
        SyntaxNode scope,
        string name,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var node in scope.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (!string.Equals(node.Identifier.ValueText, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (model.GetSymbolInfo(node, cancellationToken).Symbol is ILocalSymbol or IParameterSymbol)
            {
                return model.GetTypeInfo(node, cancellationToken).Type;
            }
        }

        return null;
    }
}
