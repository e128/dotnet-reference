using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128198: Reports a two-argument <c lang="csharp">string.Substring</c> call whose count is a
///     <c lang="csharp">Length</c> value minus a positive constant. A short string makes the count
///     negative, and the call throws <c lang="csharp">ArgumentOutOfRangeException</c>.
/// </summary>
/// <remarks>
///     The rule reads one call site and its enclosing conditions. A length test on the same string in
///     an enclosing <c lang="csharp">if</c> or conditional expression clears the report.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SubstringLengthUnderflowAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128198";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Substring count can be negative",
        "Check the length before Substring(start, length - {0}) runs",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A count built from a Length value minus a positive constant goes negative on a short " +
        "string. Substring then throws ArgumentOutOfRangeException.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax call
            || !string.Equals(call.Name.Identifier.ValueText, "Substring", StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Count != 2)
        {
            return;
        }

        var model = context.SemanticModel;
        var token = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method
            || method.ContainingType.SpecialType != SpecialType.System_String)
        {
            return;
        }

        if (!TryGetUnderflow(context, invocation.ArgumentList.Arguments[1].Expression, out var receiver, out var days))
        {
            return;
        }

        if (HasLengthGuard(invocation, receiver, model, token))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), days));
    }

    /// <summary>
    ///     True when the count is a <c lang="csharp">Length</c> value minus a positive constant.
    ///     Returns the receiver of the <c lang="csharp">Length</c> read and the constant.
    /// </summary>
    private static bool TryGetUnderflow(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax count,
        out ISymbol? receiver,
        out int constant)
    {
        receiver = null;
        constant = 0;

        if (Unwrap(count) is not BinaryExpressionSyntax subtract
            || !subtract.IsKind(SyntaxKind.SubtractExpression))
        {
            return false;
        }

        if (context.SemanticModel.GetConstantValue(subtract.Right, context.CancellationToken)
            is not { HasValue: true, Value: int value }
            || value <= 0)
        {
            return false;
        }

        var root = subtract.Left;

        while (Unwrap(root) is BinaryExpressionSyntax inner
               && inner.IsKind(SyntaxKind.SubtractExpression))
        {
            root = inner.Left;
        }

        if (Unwrap(root) is not MemberAccessExpressionSyntax length
            || !string.Equals(length.Name.Identifier.ValueText, "Length", StringComparison.Ordinal))
        {
            return false;
        }

        receiver = context.SemanticModel.GetSymbolInfo(length.Expression, context.CancellationToken).Symbol;
        constant = value;

        return true;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        return expression is ParenthesizedExpressionSyntax parenthesized
            ? Unwrap(parenthesized.Expression)
            : expression;
    }

    /// <summary>
    ///     True when an enclosing condition reads the length of the same string.
    /// </summary>
    private static bool HasLengthGuard(
        InvocationExpressionSyntax invocation,
        ISymbol? receiver,
        SemanticModel model,
        CancellationToken token)
    {
        if (receiver is null)
        {
            return false;
        }

        foreach (var ancestor in invocation.Ancestors())
        {
            if (ancestor is MemberDeclarationSyntax)
            {
                return false;
            }

            if (ancestor is IfStatementSyntax andIf && ReadsLength(andIf.Condition, receiver, model, token))
            {
                return true;
            }

            if (ancestor is ConditionalExpressionSyntax conditional
                && ReadsLength(conditional.Condition, receiver, model, token))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReadsLength(
        ExpressionSyntax condition,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken token)
    {
        foreach (var access in condition.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            if (!string.Equals(access.Name.Identifier.ValueText, "Length", StringComparison.Ordinal))
            {
                continue;
            }

            if (SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(access.Expression, token).Symbol,
                    receiver))
            {
                return true;
            }
        }

        return false;
    }
}
