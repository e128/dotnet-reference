using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Maintainability;

/// <summary>
///     E128088: Detects a <see langword="switch"/> statement or <see langword="switch"/>
///     expression over an enum type that does not case every member and carries no
///     <see langword="default"/> arm or discard pattern. An uncased member falls through
///     silently instead of failing loudly when a new enum member is added later.
///     <para>
///         A nullable enum governing type is skipped entirely — the null arm belongs to a
///         separate concern, not this rule.
///     </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SwitchEnumExhaustivenessAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128088";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Switch over an enum does not case every member",
        "Switch over '{0}' does not case member '{1}' and has no default arm — a new member falls through silently",
        "Maintainability",
        DiagnosticSeverity.Warning,
        true,
        "A switch over an enum type should case every member, or add a default arm or discard pattern " +
        "to make the fallthrough explicit. An uncased member otherwise falls through silently.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSwitchStatement, SyntaxKind.SwitchStatement);
        context.RegisterSyntaxNodeAction(AnalyzeSwitchExpression, SyntaxKind.SwitchExpression);
    }

    private static void AnalyzeSwitchStatement(SyntaxNodeAnalysisContext context)
    {
        var switchStatement = (SwitchStatementSyntax)context.Node;
        var enumType = GetEnumType(context.SemanticModel, switchStatement.Expression, context.CancellationToken);
        if (enumType is null)
        {
            return;
        }

        var casedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var section in switchStatement.Sections)
        {
            foreach (var label in section.Labels)
            {
                if (label is DefaultSwitchLabelSyntax or CasePatternSwitchLabelSyntax { Pattern: DiscardPatternSyntax })
                {
                    return;
                }

                if (label is CaseSwitchLabelSyntax caseLabel)
                {
                    AddCasedMemberName(context.SemanticModel, caseLabel.Value, casedNames, context.CancellationToken);
                    continue;
                }

                if (label is CasePatternSwitchLabelSyntax { Pattern: ConstantPatternSyntax constantPattern })
                {
                    AddCasedMemberName(context.SemanticModel, constantPattern.Expression, casedNames, context.CancellationToken);
                }
            }
        }

        ReportIfIncomplete(context, switchStatement.SwitchKeyword, enumType, casedNames);
    }

    private static void AnalyzeSwitchExpression(SyntaxNodeAnalysisContext context)
    {
        var switchExpression = (SwitchExpressionSyntax)context.Node;
        var enumType = GetEnumType(context.SemanticModel, switchExpression.GoverningExpression, context.CancellationToken);
        if (enumType is null)
        {
            return;
        }

        var casedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var arm in switchExpression.Arms)
        {
            if (arm.Pattern is DiscardPatternSyntax or VarPatternSyntax)
            {
                return;
            }

            if (arm.Pattern is ConstantPatternSyntax constantPattern)
            {
                AddCasedMemberName(context.SemanticModel, constantPattern.Expression, casedNames, context.CancellationToken);
            }
        }

        ReportIfIncomplete(context, switchExpression.SwitchKeyword, enumType, casedNames);
    }

    private static INamedTypeSymbol? GetEnumType(SemanticModel model, ExpressionSyntax expression, CancellationToken cancellationToken)
    {
        return model.GetTypeInfo(expression, cancellationToken).Type is INamedTypeSymbol namedType
               && namedType.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T
               && namedType.TypeKind == TypeKind.Enum
            ? namedType
            : null;
    }

    private static void AddCasedMemberName(
        SemanticModel model,
        ExpressionSyntax expression,
        HashSet<string> casedNames,
        CancellationToken cancellationToken)
    {
        if (model.GetSymbolInfo(expression, cancellationToken).Symbol is IFieldSymbol field)
        {
            casedNames.Add(field.Name);
        }
    }

    private static void ReportIfIncomplete(
        SyntaxNodeAnalysisContext context,
        SyntaxToken switchKeyword,
        INamedTypeSymbol enumType,
        HashSet<string> casedNames)
    {
        foreach (var member in enumType.GetMembers().OfType<IFieldSymbol>())
        {
            if (!member.IsConst || !member.HasConstantValue)
            {
                continue;
            }

            if (casedNames.Contains(member.Name))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, switchKeyword.GetLocation(), enumType.Name, member.Name));
            return;
        }
    }
}
