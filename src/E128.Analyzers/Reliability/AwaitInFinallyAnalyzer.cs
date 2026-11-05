using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128186: Detects an <c lang="csharp">await</c> expression in the body of a
///     <see langword="finally" /> clause. A fault in the awaited call replaces the exception
///     that the <see langword="finally" /> clause was cleaning up after.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AwaitInFinallyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128186";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Await inside a finally block",
        "'await' inside 'finally' replaces the original exception when the awaited call faults",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "An awaited call in a finally block runs while the original exception propagates. " +
        "A fault in the awaited call replaces that exception, so the original failure is lost.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeFinally, SyntaxKind.FinallyClause);
    }

    private static void AnalyzeFinally(SyntaxNodeAnalysisContext context)
    {
        var finallyClause = (FinallyClauseSyntax)context.Node;

        foreach (var awaitExpression in finallyClause.Block.DescendantNodes().OfType<AwaitExpressionSyntax>())
        {
            if (!BelongsToFinally(awaitExpression, finallyClause))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(Rule, awaitExpression.GetLocation()));
        }
    }

    /// <summary>
    ///     Reports only the await expressions that run as part of this finally clause. An await
    ///     inside a nested finally belongs to that inner clause, an await inside a nested try or
    ///     catch runs under that construct, and an await inside a local function or a lambda runs
    ///     only when code calls that member.
    /// </summary>
    private static bool BelongsToFinally(AwaitExpressionSyntax awaitExpression, FinallyClauseSyntax finallyClause)
    {
        for (var current = awaitExpression.Parent; current is not null; current = current.Parent)
        {
            if (current == finallyClause)
            {
                return true;
            }

            if (current is FinallyClauseSyntax
                or TryStatementSyntax
                or LocalFunctionStatementSyntax
                or AnonymousFunctionExpressionSyntax)
            {
                return false;
            }
        }

        return false;
    }
}
