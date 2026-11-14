using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128220: Reports a switch over a union or a closed hierarchy that ends in a discard or
///     <see langword="var" /> arm instead of a <see langword="null" /> arm. The
///     <see langword="null" /> arm names the empty case, so the compiler keeps warning when a
///     case type or a leaf is added later. A discard or <see langword="var" /> arm accepts any
///     case type the union grows, and the missing-case warning disappears. A
///     <see langword="when" /> guard does not close a discard or <see langword="var" /> arm
///     either, so the rule reports a guarded arm the same way.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnionSwitchDiscardArmAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128220";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Union or closed-hierarchy switch ends in a discard arm",
        "Switch over '{0}' ends in a '{1}' arm. End the switch with a null arm so the compiler keeps warning about a missing case.",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true);

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

        context.RegisterSyntaxNodeAction(AnalyzeSwitchExpression, SyntaxKind.SwitchExpression);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchStatement, SyntaxKind.SwitchStatement);
    }

    private static void AnalyzeSwitchExpression(SyntaxNodeAnalysisContext context)
    {
        var switchExpression = (SwitchExpressionSyntax)context.Node;
        var governingType = GetUnionType(context.SemanticModel, switchExpression.GoverningExpression, context.CancellationToken);
        if (governingType is null)
        {
            return;
        }

        var expressionCatchAll = TryFindCatchAll(
            switchExpression.Arms.Select(static arm => (arm.Pattern, arm.WhenClause is not null)));
        if (expressionCatchAll is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            expressionCatchAll.GetLocation(),
            governingType.Name,
            expressionCatchAll is DiscardPatternSyntax ? "_" : "var"));
    }

    private static void AnalyzeSwitchStatement(SyntaxNodeAnalysisContext context)
    {
        var switchStatement = (SwitchStatementSyntax)context.Node;
        if (switchStatement.Expression is null)
        {
            return;
        }

        var governingType = GetUnionType(context.SemanticModel, switchStatement.Expression, context.CancellationToken);
        if (governingType is null)
        {
            return;
        }

        var labels = switchStatement.Sections
            .SelectMany(static section => section.Labels)
            .OfType<CasePatternSwitchLabelSyntax>()
            .Select(static label => (label.Pattern, label.WhenClause is not null));
        var statementCatchAll = TryFindCatchAll(labels);
        if (statementCatchAll is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            statementCatchAll.GetLocation(),
            governingType.Name,
            statementCatchAll is DiscardPatternSyntax ? "_" : "var"));
    }

    private static INamedTypeSymbol? GetUnionType(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken)
    {
        var type = model.GetTypeInfo(expression, cancellationToken).Type;
        if (type is null)
        {
            return null;
        }

        var unwrapped = TypeSymbols.UnwrapNullable(type);
        if (unwrapped is not INamedTypeSymbol named)
        {
            return null;
        }

        // A closed class hierarchy carries the compiler closed attribute, so the same catch-all
        // rule applies: a named or null arm keeps CS8509 alive for a missing leaf.
        return UnionTypes.IsUnion(unwrapped) || ClosedHierarchy.IsClosed(named) ? named : null;
    }

    private static PatternSyntax? TryFindCatchAll(IEnumerable<(PatternSyntax Pattern, bool IsGuarded)> patterns)
    {
        PatternSyntax? lastCatchAll = null;
        PatternSyntax? lastGuardedCatchAll = null;
        var hasNullArm = false;

        foreach (var (pattern, isGuarded) in patterns)
        {
            if (IsNullPattern(pattern))
            {
                hasNullArm = true;
            }
            else if (IsCatchAllPattern(pattern))
            {
                if (isGuarded)
                {
                    lastGuardedCatchAll = pattern;
                }
                else
                {
                    lastCatchAll = pattern;
                }
            }
        }

        // A when-guarded discard or var arm never closes the switch, so it stays a report
        // target instead of a closing catch-all. Only an unguarded arm closes the switch.
        return hasNullArm ? null : lastCatchAll ?? lastGuardedCatchAll;
    }

    private static bool IsNullPattern(PatternSyntax pattern)
    {
        return pattern is ConstantPatternSyntax
        {
            Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression }
        };
    }

    private static bool IsCatchAllPattern(PatternSyntax pattern)
    {
        return pattern is DiscardPatternSyntax or VarPatternSyntax;
    }
}
