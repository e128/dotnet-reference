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

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use BitOperations.PopCount instead of a bit-count loop",
        "The loop counts set bits one at a time. Use BitOperations.PopCount.",
        "Performance",
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

        context.RegisterCompilationStartAction(start =>
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
            || body.Statements.Count != 2)
        {
            return false;
        }

        if (!TryReadNotZero(condition, out var conditionName)
            || body.Statements[0] is not ExpressionStatementSyntax first
            || first.Expression is not AssignmentExpressionSyntax add
            || !add.IsKind(SyntaxKind.AddAssignmentExpression)
            || add.Left is not IdentifierNameSyntax countIdentifier
            || add.Right is not BinaryExpressionSyntax bitwiseAnd
            || !bitwiseAnd.IsKind(SyntaxKind.BitwiseAndExpression)
            || bitwiseAnd.Left is not IdentifierNameSyntax masked
            || !IsLiteral(bitwiseAnd.Right, 1)
            || body.Statements[1] is not ExpressionStatementSyntax second
            || second.Expression is not AssignmentExpressionSyntax shift
            || !shift.IsKind(SyntaxKind.RightShiftAssignmentExpression)
            || shift.Left is not IdentifierNameSyntax shifted
            || !IsLiteral(shift.Right, 1))
        {
            return false;
        }

        if (!string.Equals(conditionName, masked.Identifier.ValueText, StringComparison.Ordinal)
            || !string.Equals(conditionName, shifted.Identifier.ValueText, StringComparison.Ordinal))
        {
            return false;
        }

        valueName = conditionName;
        countName = countIdentifier.Identifier.ValueText;
        return true;
    }

    private static bool TryReadNotZero(BinaryExpressionSyntax condition, out string name)
    {
        name = string.Empty;

        var left = condition.Left as IdentifierNameSyntax;
        var right = condition.Right as IdentifierNameSyntax;

        if (left is not null && IsLiteral(condition.Right, 0))
        {
            name = left.Identifier.ValueText;
            return true;
        }

        if (right is not null && IsLiteral(condition.Left, 0))
        {
            name = right.Identifier.ValueText;
            return true;
        }

        return false;
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
