using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EmptySpinLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128206";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Empty loop body spins the CPU",
        "Empty loop body spins the CPU — wait on a signal instead",
        "E128-Performance",
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

        context.RegisterSyntaxNodeAction(AnalyzeLoop, SyntaxKind.WhileStatement);
        context.RegisterSyntaxNodeAction(AnalyzeLoop, SyntaxKind.DoStatement);
    }

    private static void AnalyzeLoop(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is WhileStatementSyntax whileStatement)
        {
            Analyze(context, whileStatement.Statement, whileStatement.WhileKeyword, whileStatement.Condition);
            return;
        }

        if (context.Node is not DoStatementSyntax doStatement)
        {
            return;
        }

        Analyze(context, doStatement.Statement, doStatement.DoKeyword, doStatement.Condition);
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        StatementSyntax body,
        SyntaxToken keyword,
        ExpressionSyntax condition)
    {
        if (!IsEmptyBody(body))
        {
            return;
        }

        if (condition.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any())
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, keyword.GetLocation()));
    }

    private static bool IsEmptyBody(StatementSyntax body)
    {
        return body switch
        {
            BlockSyntax block => block.Statements.Count is 0,
            EmptyStatementSyntax => true,
            _ => false
        };
    }
}
