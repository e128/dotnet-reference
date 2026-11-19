using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128248: A <c lang="csharp">for</c> loop uses a <c lang="csharp">float</c> or
///     <c lang="csharp">double</c> counter.
/// </summary>
/// <remarks>
///     A floating-point step accumulates representation error, so a terminal comparison can miss
///     the expected value or run one extra iteration. A declared counter and a predeclared counter
///     assigned in the initializer both report. A <c lang="csharp">decimal</c> counter is exact
///     and stays silent. The rule carries no code fix, because an integer counter or a different
///     termination condition depends on the loop.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FloatingPointLoopCounterAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128248";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Floating-point loop counter",
        "Loop counter '{0}' is a floating-point value, so the step accumulates error",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Use an integer counter, or derive the value from one. A floating-point step accumulates representation error.");

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
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ForStatement);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var loop = (ForStatementSyntax)context.Node;
        var counter = FindDeclaredCounter(loop, context)
            ?? FindAssignedCounter(loop, context)
            ?? FindIncrementorCounter(loop, context);

        if (counter is not { } token)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, token.GetLocation(), token.ValueText));
    }

    private static SyntaxToken? FindDeclaredCounter(ForStatementSyntax loop, SyntaxNodeAnalysisContext context)
    {
        if (loop.Declaration is null)
        {
            return null;
        }

        foreach (var variable in loop.Declaration.Variables)
        {
            if (context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is ILocalSymbol local
                && IsFloatingPoint(local.Type))
            {
                return variable.Identifier;
            }
        }

        return null;
    }

    private static SyntaxToken? FindAssignedCounter(ForStatementSyntax loop, SyntaxNodeAnalysisContext context)
    {
        foreach (var initializer in loop.Initializers)
        {
            if (initializer is not AssignmentExpressionSyntax assignment)
            {
                continue;
            }

            if (context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol
                is ILocalSymbol local
                && IsFloatingPoint(local.Type))
            {
                return assignment.Left.GetFirstToken();
            }
        }

        return null;
    }

    /// <summary>
    ///     Returns the counter of a loop that declares no counter and assigns none in the
    ///     initializer, for example <c lang="csharp">for (; i &lt; 1; i += 0.1)</c>.
    /// </summary>
    private static SyntaxToken? FindIncrementorCounter(ForStatementSyntax loop, SyntaxNodeAnalysisContext context)
    {
        foreach (var incrementor in loop.Incrementors)
        {
            var target = incrementor switch
            {
                PostfixUnaryExpressionSyntax postfix => postfix.Operand,
                PrefixUnaryExpressionSyntax prefix => prefix.Operand,
                AssignmentExpressionSyntax assignment => assignment.Left,
                _ => null
            };

            if (target is not null
                && context.SemanticModel.GetSymbolInfo(target, context.CancellationToken).Symbol
                is ILocalSymbol local
                && IsFloatingPoint(local.Type))
            {
                return target.GetFirstToken();
            }
        }

        return null;
    }

    private static bool IsFloatingPoint(ITypeSymbol type)
    {
        return type.SpecialType is SpecialType.System_Single or SpecialType.System_Double;
    }
}
