using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128244: A divisor is compared with zero after an earlier division already used it.
/// </summary>
/// <remarks>
///     The division runs first, so an integer or decimal divide by zero throws before the guard
///     can run. The rule reports the late comparison, not the division. A guard before the
///     division stays silent. A floating-point division stays silent, because it yields infinity
///     instead of throwing. The rule reads only a local, a parameter, or a field as the divisor,
///     so a property read twice does not create a false order claim.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DivisionGuardOrderAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128244";

    private static readonly ImmutableHashSet<SpecialType> IntegralTypes = ImmutableHashSet.Create(
        SpecialType.System_Byte,
        SpecialType.System_SByte,
        SpecialType.System_Int16,
        SpecialType.System_UInt16,
        SpecialType.System_Int32,
        SpecialType.System_UInt32,
        SpecialType.System_Int64,
        SpecialType.System_UInt64,
        SpecialType.System_IntPtr,
        SpecialType.System_UIntPtr,
        SpecialType.System_Decimal);

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Zero guard after the division",
        "The zero check on '{0}' runs after a division that uses it",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Move the zero check before the division. An integer or decimal divide by zero throws, so the later guard never runs.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var comparison = (BinaryExpressionSyntax)context.Node;
        if (!TryGetGuardedSymbol(comparison, context, out var guarded)
            || guarded is null
            || comparison.Ancestors().OfType<StatementSyntax>().FirstOrDefault() is not { } statement
            || statement.Parent is not BlockSyntax block)
        {
            return;
        }

        var index = block.Statements.IndexOf(statement);
        if (index <= 0)
        {
            return;
        }

        for (var earlier = 0; earlier < index; earlier++)
        {
            if (!ContainsDivisionBy(block.Statements[earlier], guarded, context))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, comparison.GetLocation(), guarded.Name));
            return;
        }
    }

    /// <summary>
    ///     Returns the symbol one side of the comparison tests against zero. The other side is a
    ///     constant zero of an integral or decimal type.
    /// </summary>
    private static bool TryGetGuardedSymbol(
        BinaryExpressionSyntax comparison,
        SyntaxNodeAnalysisContext context,
        out ISymbol? guarded)
    {
        guarded = null;
        var rightIsZero = IsZeroConstant(comparison.Right, context);
        var leftIsZero = IsZeroConstant(comparison.Left, context);
        if (rightIsZero == leftIsZero)
        {
            return false;
        }

        return TryGetStorageSymbol(rightIsZero ? comparison.Left : comparison.Right, context, out guarded);
    }

    private static bool IsZeroConstant(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
    {
        var constant = context.SemanticModel.GetConstantValue(expression, context.CancellationToken);
        return constant.HasValue && IsNumericZero(constant.Value);
    }

    /// <summary>
    ///     True only for a numeric zero. A string, char, or bool constant is not a divisor check,
    ///     and <c lang="csharp">Convert.ToDecimal</c> throws
    ///     <c lang="csharp">FormatException</c> on a string constant, so the gate runs before the
    ///     conversion. An enum member arrives as its underlying integral value.
    /// </summary>
    private static bool IsNumericZero(object? value)
    {
        return value is IConvertible and not (string or char or bool)
            && Convert.ToDecimal(value, CultureInfo.InvariantCulture) is 0m;
    }

    private static bool TryGetStorageSymbol(
        ExpressionSyntax expression,
        SyntaxNodeAnalysisContext context,
        out ISymbol? symbol)
    {
        symbol = null;
        var candidate = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol;
        if (candidate is not (ILocalSymbol or IParameterSymbol or IFieldSymbol))
        {
            return false;
        }

        symbol = candidate;
        return true;
    }

    private static bool ContainsDivisionBy(
        StatementSyntax statement,
        ISymbol guarded,
        SyntaxNodeAnalysisContext context)
    {
        foreach (var node in statement.DescendantNodesAndSelf(Descend))
        {
            if (node is BinaryExpressionSyntax binary
                && IsDivision(binary)
                && IsIntegral(binary, context)
                && IsGuardedOperand(binary.Right, guarded, context))
            {
                return true;
            }

            if (node is AssignmentExpressionSyntax assignment
                && IsCompoundDivision(assignment)
                && IsIntegral(assignment, context)
                && IsGuardedOperand(assignment.Right, guarded, context))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDivision(BinaryExpressionSyntax binary)
    {
        return binary.IsKind(SyntaxKind.DivideExpression) || binary.IsKind(SyntaxKind.ModuloExpression);
    }

    private static bool IsCompoundDivision(AssignmentExpressionSyntax assignment)
    {
        return assignment.IsKind(SyntaxKind.DivideAssignmentExpression)
            || assignment.IsKind(SyntaxKind.ModuloAssignmentExpression);
    }

    private static bool IsGuardedOperand(
        ExpressionSyntax expression,
        ISymbol guarded,
        SyntaxNodeAnalysisContext context)
    {
        return SymbolEqualityComparer.Default.Equals(
            context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol,
            guarded);
    }

    private static bool IsIntegral(ExpressionSyntax expression, SyntaxNodeAnalysisContext context)
    {
        return context.SemanticModel.GetTypeInfo(expression, context.CancellationToken).Type is { } type
            && IntegralTypes.Contains(type.SpecialType);
    }

    private static bool Descend(SyntaxNode node)
    {
        return node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
    }
}
