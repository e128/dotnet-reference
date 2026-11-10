using System;
using System.Collections.Immutable;
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

    private const int SubstringArgumentCount = 2;

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
            || invocation.ArgumentList.Arguments.Count is not SubstringArgumentCount)
        {
            return;
        }

        var model = context.SemanticModel;
        var token = context.CancellationToken;

        if (model.GetSymbolInfo(invocation, token).Symbol is not IMethodSymbol method
            || method.ContainingType.SpecialType is not SpecialType.System_String)
        {
            return;
        }

        if (!TryGetUnderflow(context, invocation.ArgumentList.Arguments[1].Expression, out var receiver, out var constant))
        {
            return;
        }

        if (HasLengthGuard(invocation, receiver, model, token))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), constant));
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

        if (ExpressionUnwrapper.Parentheses(count) is not BinaryExpressionSyntax subtract
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

        while (ExpressionUnwrapper.Parentheses(root) is BinaryExpressionSyntax inner
               && inner.IsKind(SyntaxKind.SubtractExpression))
        {
            root = inner.Left;
        }

        if (ExpressionUnwrapper.Parentheses(root) is not MemberAccessExpressionSyntax length
            || !string.Equals(length.Name.Identifier.ValueText, "Length", StringComparison.Ordinal))
        {
            return false;
        }

        receiver = context.SemanticModel.GetSymbolInfo(length.Expression, context.CancellationToken).Symbol;
        constant = value;

        return true;
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

            if (ancestor is IfStatementSyntax andIf
                && GuardsBranch(andIf.Condition, andIf.Statement, andIf.Else, invocation, receiver, model, token))
            {
                return true;
            }

            if (ancestor is ConditionalExpressionSyntax conditional
                && GuardsBranch(conditional.Condition, conditional.WhenTrue, conditional.WhenFalse, invocation, receiver, model, token))
            {
                return true;
            }

            if (ancestor is BlockSyntax block
                && HasEarlyExitGuard(block, invocation, receiver, model, token))
            {
                return true;
            }
        }

        return false;
    }

    // A condition that reads the length guards the branch that holds the long string. A plain
    // comparison of the length says which branch that is. Any other shape guards both branches.
    private static bool GuardsBranch(
        ExpressionSyntax condition,
        SyntaxNode thenBranch,
        SyntaxNode? elseBranch,
        SyntaxNode use,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken token)
    {
        if (!ReadsLength(condition, receiver, model, token))
        {
            return false;
        }

        var inThen = thenBranch.Span.Contains(use.Span);
        var inElse = elseBranch is not null && elseBranch.Span.Contains(use.Span);

        return LongStringBranch(condition, receiver, model, token) switch
        {
            Branch.Then => inThen,
            Branch.Else => inElse,
            Branch.Both => true,
            _ => throw new InvalidOperationException("Unknown branch value.")
        };
    }

    private static Branch LongStringBranch(
        ExpressionSyntax condition,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken token)
    {
        if (ExpressionUnwrapper.Parentheses(condition) is not BinaryExpressionSyntax comparison
            || !comparison.IsKind(SyntaxKind.GreaterThanExpression)
            && !comparison.IsKind(SyntaxKind.GreaterThanOrEqualExpression)
            && !comparison.IsKind(SyntaxKind.LessThanExpression)
            && !comparison.IsKind(SyntaxKind.LessThanOrEqualExpression))
        {
            return Branch.Both;
        }

        var lengthOnLeft = ReadsLength(comparison.Left, receiver, model, token);
        var lengthOnRight = ReadsLength(comparison.Right, receiver, model, token);

        if (lengthOnLeft == lengthOnRight)
        {
            return Branch.Both;
        }

        var isGreater = comparison.IsKind(SyntaxKind.GreaterThanExpression)
                        || comparison.IsKind(SyntaxKind.GreaterThanOrEqualExpression);

        // length > k and k < length keep the long string in the then branch.
        return lengthOnLeft == isGreater ? Branch.Then : Branch.Else;
    }

    private enum Branch
    {
        Both = 0,
        Then = 1,
        Else = 2
    }

    // A preceding if with no else that returns or throws when the length is short guards every
    // statement after it in the block.
    private static bool HasEarlyExitGuard(
        BlockSyntax block,
        InvocationExpressionSyntax invocation,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken token)
    {
        foreach (var statement in block.Statements)
        {
            if (statement.SpanStart >= invocation.SpanStart || statement.Span.Contains(invocation.Span))
            {
                break;
            }

            if (statement is IfStatementSyntax { Else: null } guard
                && StatementExits.EndsInExit(guard.Statement)
                && ReadsLength(guard.Condition, receiver, model, token))
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
        foreach (var node in condition.DescendantNodesAndSelf())
        {
            if (node is MemberAccessExpressionSyntax access
                && string.Equals(access.Name.Identifier.ValueText, "Length", StringComparison.Ordinal)
                && IsSameReceiver(access.Expression, receiver, model, token))
            {
                return true;
            }

            if (node is InvocationExpressionSyntax invocation
                && IsNonEmptyGuard(invocation, receiver, model, token))
            {
                return true;
            }

            if (node is BinaryExpressionSyntax comparison
                && IsNonNegativeIndexOf(comparison, receiver, model, token))
            {
                return true;
            }
        }

        return false;
    }

    // A guard that proves the receiver holds at least one character.
    private static bool IsNonEmptyGuard(
        InvocationExpressionSyntax invocation,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken token)
    {
        return invocation.Expression is MemberAccessExpressionSyntax access
               && invocation.ArgumentList.Arguments.Count > 0
               && model.GetSymbolInfo(invocation, token).Symbol is IMethodSymbol method
               && method.ContainingType.SpecialType is SpecialType.System_String
               && (IsNullOrEmptyGuard(invocation, method, receiver, model, token)
                   || IsTextProbeGuard(invocation, access, method, receiver, model, token));
    }

    private static bool IsNullOrEmptyGuard(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken token)
    {
        return (string.Equals(method.Name, "IsNullOrEmpty", StringComparison.Ordinal)
                || string.Equals(method.Name, "IsNullOrWhiteSpace", StringComparison.Ordinal))
               && invocation.ArgumentList.Arguments.Count is 1
               && IsSameReceiver(invocation.ArgumentList.Arguments[0].Expression, receiver, model, token)
               && IsNegated(invocation);
    }

    private static bool IsTextProbeGuard(
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax access,
        IMethodSymbol method,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken token)
    {
        return (string.Equals(method.Name, "EndsWith", StringComparison.Ordinal)
                || string.Equals(method.Name, "StartsWith", StringComparison.Ordinal)
                || string.Equals(method.Name, "Contains", StringComparison.Ordinal))
               && !IsNegated(invocation)
               && IsSameReceiver(access.Expression, receiver, model, token);
    }

    // A guard of the form x.IndexOf(...) >= 0, which proves the receiver is non-empty.
    private static bool IsNonNegativeIndexOf(
        BinaryExpressionSyntax comparison,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken token)
    {
        return comparison.IsKind(SyntaxKind.GreaterThanOrEqualExpression)
               && model.GetConstantValue(comparison.Right, token) is { HasValue: true, Value: int zero }
               && zero is 0
               && ExpressionUnwrapper.Parentheses(comparison.Left) is InvocationExpressionSyntax invocation
               && invocation.Expression is MemberAccessExpressionSyntax access
               && string.Equals(access.Name.Identifier.ValueText, "IndexOf", StringComparison.Ordinal)
               && model.GetSymbolInfo(invocation, token).Symbol is IMethodSymbol method
               && method.ContainingType.SpecialType is SpecialType.System_String
               && IsSameReceiver(access.Expression, receiver, model, token);
    }

    private static bool IsNegated(ExpressionSyntax expression)
    {
        var current = expression;

        while (current.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            current = parenthesized;
        }

        return current.Parent is PrefixUnaryExpressionSyntax prefix
               && prefix.IsKind(SyntaxKind.LogicalNotExpression);
    }

    private static bool IsSameReceiver(
        ExpressionSyntax expression,
        ISymbol receiver,
        SemanticModel model,
        CancellationToken token)
    {
        return SymbolEqualityComparer.Default.Equals(
            model.GetSymbolInfo(expression, token).Symbol,
            receiver);
    }
}
