using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128197: Reports a fixed 28 to 31 day span added to or subtracted from a
///     <c lang="csharp">DateTime</c> or a <c lang="csharp">DateTimeOffset</c>. The span stands in
///     for a calendar month, and a calendar month has no fixed day count.
/// </summary>
/// <remarks>
///     The rule reads an inline <c lang="csharp">TimeSpan.FromDays</c> call only. A span held in a
///     variable or a constant is skipped, because the rule cannot tell a month from a 30 day window.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MonthTimeSpanAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128197";

    private const int ShortestMonth = 28;

    private const int LongestMonth = 31;

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Fixed day count used as a calendar month",
        "Use AddMonths instead of adding or subtracting TimeSpan.FromDays({0})",
        "E128-Reliability",
        DiagnosticSeverity.Info,
        true,
        "A calendar month holds 28 to 31 days. A fixed span drifts from the calendar, so a monthly " +
        "boundary moves a day or more over time. AddMonths keeps the day of the month.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var timeSpanType = context.Compilation.GetTypeByMetadataName("System.TimeSpan");

        if (timeSpanType is null)
        {
            return;
        }

        INamedTypeSymbol?[] targets =
        [
            context.Compilation.GetTypeByMetadataName("System.DateTime"),
            context.Compilation.GetTypeByMetadataName("System.DateTimeOffset")
        ];

        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeCall(ctx, timeSpanType, targets),
            SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeOperator(ctx, timeSpanType, targets),
            SyntaxKind.AddExpression,
            SyntaxKind.SubtractExpression);
        context.RegisterSyntaxNodeAction(
            ctx => AnalyzeCompoundAssignment(ctx, timeSpanType, targets),
            SyntaxKind.AddAssignmentExpression,
            SyntaxKind.SubtractAssignmentExpression);
    }

    private static void AnalyzeCall(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol timeSpanType,
        INamedTypeSymbol?[] targets)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax call
            || !IsAddOrSubtract(call, targets, context)
            || invocation.ArgumentList.Arguments.Count is not 1)
        {
            return;
        }

        ReportIfMonth(context, invocation.ArgumentList.Arguments[0].Expression, timeSpanType);
    }

    private static void AnalyzeOperator(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol timeSpanType,
        INamedTypeSymbol?[] targets)
    {
        var binary = (BinaryExpressionSyntax)context.Node;

        var model = context.SemanticModel;
        var token = context.CancellationToken;

        if (IsTarget(model.GetTypeInfo(binary.Left, token).Type, targets))
        {
            ReportIfMonth(context, binary.Right, timeSpanType);
            return;
        }

        if (!binary.IsKind(SyntaxKind.AddExpression)
            || !IsTarget(model.GetTypeInfo(binary.Right, token).Type, targets))
        {
            return;
        }

        ReportIfMonth(context, binary.Left, timeSpanType);
    }

    private static void AnalyzeCompoundAssignment(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol timeSpanType,
        INamedTypeSymbol?[] targets)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (!IsTarget(context.SemanticModel.GetTypeInfo(assignment.Left, context.CancellationToken).Type, targets))
        {
            return;
        }

        ReportIfMonth(context, assignment.Right, timeSpanType);
    }

    private static bool IsAddOrSubtract(
        MemberAccessExpressionSyntax call,
        INamedTypeSymbol?[] targets,
        SyntaxNodeAnalysisContext context)
    {
        var name = call.Name.Identifier.ValueText;

        return (string.Equals(name, "Add", StringComparison.Ordinal)
                || string.Equals(name, "Subtract", StringComparison.Ordinal))
               && context.SemanticModel.GetSymbolInfo(call, context.CancellationToken).Symbol
               is IMethodSymbol method
               && IsTarget(method.ContainingType, targets);
    }

    private static bool IsTarget(ITypeSymbol? type, INamedTypeSymbol?[] targets)
    {
        foreach (var target in targets)
        {
            if (target is not null && SymbolEqualityComparer.Default.Equals(target, type))
            {
                return true;
            }
        }

        return false;
    }

    private static void ReportIfMonth(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax candidate,
        INamedTypeSymbol timeSpanType)
    {
        if (candidate is not InvocationExpressionSyntax invocation
            || !IsMonthSpan(invocation, timeSpanType, context, out var days))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), days));
    }

    /// <summary>
    ///     True for an inline <c lang="csharp">TimeSpan.FromDays</c> call with a constant day count
    ///     in the range of a calendar month.
    /// </summary>
    private static bool IsMonthSpan(
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol timeSpanType,
        SyntaxNodeAnalysisContext context,
        out int days)
    {
        days = 0;

        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !string.Equals(access.Name.Identifier.ValueText, "FromDays", StringComparison.Ordinal)
            || invocation.ArgumentList.Arguments.Count is not 1)
        {
            return false;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, timeSpanType))
        {
            return false;
        }

        var constant = context.SemanticModel.GetConstantValue(
            invocation.ArgumentList.Arguments[0].Expression,
            context.CancellationToken);
        return constant.HasValue && TryGetWholeDays(constant.Value, out days);
    }

    // Accept a whole number in the month range from an int, long, double, or float constant.
    private static bool TryGetWholeDays(object? value, out int days)
    {
        days = 0;

        switch (value)
        {
            case int intValue when intValue is >= ShortestMonth and <= LongestMonth:
                days = intValue;
                return true;
            case long longValue when longValue is >= ShortestMonth and <= LongestMonth:
                days = (int)longValue;
                return true;
            case double doubleValue when IsWholeDay(doubleValue):
                days = (int)doubleValue;
                return true;
            case float floatValue when IsWholeDay(floatValue):
                days = (int)floatValue;
                return true;
            default:
                return false;
        }
    }

    private static bool IsWholeDay(double value)
    {
        return value >= ShortestMonth
               && value <= LongestMonth
               && Math.Abs(value - Math.Floor(value)) < double.Epsilon;
    }
}
