using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ChainedStringReplaceAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128098";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Collapse chained or looped string Replace calls into a single pass",
        "{0}",
        "Performance",
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

        context.RegisterSyntaxNodeAction(AnalyzeChain, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeReplaceUntilStableLoop, SyntaxKind.WhileStatement);
    }

    private static void AnalyzeChain(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Replace" }
            memberAccess)
        {
            return;
        }

        if (!IsStringReplaceInvocation(context, invocation))
        {
            return;
        }

        if (memberAccess.Expression is not InvocationExpressionSyntax innerInvocation
            || !IsStringReplaceInvocation(context, innerInvocation))
        {
            return;
        }

        if (IsChainedFurtherOutward(context, invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            "Two or more chained .Replace calls on the same string can be collapsed into a single pass"));
    }

    private static bool IsStringReplaceInvocation(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol
        {
            Name: "Replace"
        } method && method.ContainingType?.SpecialType == SpecialType.System_String;
    }

    private static bool IsChainedFurtherOutward(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        return invocation.Parent is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Replace" } outerAccess
               && outerAccess.Parent is InvocationExpressionSyntax outerInvocation
               && IsStringReplaceInvocation(context, outerInvocation);
    }

    private static void AnalyzeReplaceUntilStableLoop(SyntaxNodeAnalysisContext context)
    {
        var whileStatement = (WhileStatementSyntax)context.Node;

        var conditionSymbol = TryGetStringContainsReceiverSymbol(context, whileStatement.Condition);
        if (conditionSymbol is null)
        {
            return;
        }

        if (!HasMatchingReplaceAssignment(context, whileStatement.Statement, conditionSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            whileStatement.GetLocation(),
            "This replace-until-stable loop can be collapsed into a single pass"));
    }

    private static ISymbol? TryGetStringContainsReceiverSymbol(SyntaxNodeAnalysisContext context, ExpressionSyntax condition)
    {
        return condition is InvocationExpressionSyntax containsInvocation
               && containsInvocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Contains" } containsAccess
               && context.SemanticModel.GetSymbolInfo(containsInvocation, context.CancellationToken).Symbol is IMethodSymbol { Name: "Contains" } containsMethod
               && containsMethod.ContainingType?.SpecialType == SpecialType.System_String
            ? context.SemanticModel.GetSymbolInfo(containsAccess.Expression, context.CancellationToken).Symbol
            : null;
    }

    private static bool HasMatchingReplaceAssignment(SyntaxNodeAnalysisContext context, StatementSyntax body, ISymbol conditionSymbol)
    {
        return EnumerateBodyStatements(body).Any(statement =>
            TryGetReplaceAssignmentSymbols(context, statement, out var targetSymbol, out var replaceReceiverSymbol)
            && SymbolEqualityComparer.Default.Equals(targetSymbol, conditionSymbol)
            && SymbolEqualityComparer.Default.Equals(replaceReceiverSymbol, conditionSymbol));
    }

    private static bool TryGetReplaceAssignmentSymbols(
        SyntaxNodeAnalysisContext context,
        StatementSyntax statement,
        out ISymbol? targetSymbol,
        out ISymbol? replaceReceiverSymbol)
    {
        targetSymbol = null;
        replaceReceiverSymbol = null;

        if (statement is not ExpressionStatementSyntax
            {
                Expression: AssignmentExpressionSyntax
                {
                    Left: IdentifierNameSyntax assignmentTarget,
                    Right: InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Replace" } replaceAccess
                    } replaceInvocation
                }
            } || !IsStringReplaceInvocation(context, replaceInvocation))
        {
            return false;
        }

        targetSymbol = context.SemanticModel.GetSymbolInfo(assignmentTarget, context.CancellationToken).Symbol;
        replaceReceiverSymbol = context.SemanticModel.GetSymbolInfo(replaceAccess.Expression, context.CancellationToken).Symbol;
        return true;
    }

    private static SyntaxList<StatementSyntax> EnumerateBodyStatements(StatementSyntax body)
    {
        return body is BlockSyntax block ? block.Statements : new SyntaxList<StatementSyntax>(body);
    }
}
