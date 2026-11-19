using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128251: A <c lang="csharp">while (true)</c>, <c lang="csharp">for (;;)</c>, or
///     <c lang="csharp">do ... while (true)</c> loop has no local exit.
/// </summary>
/// <remarks>
///     A <c lang="csharp">break</c> that targets the loop, a <c lang="csharp">return</c>, a
///     <c lang="csharp">throw</c>, a <c lang="csharp">goto</c>, and a
///     <c lang="csharp">yield break</c> each count as an exit. A
///     <c lang="csharp">break</c> in a nested loop or switch does not count, because it targets
///     the inner construct. <c lang="csharp">Environment.Exit</c>,
///     <c lang="csharp">Environment.FailFast</c>, and
///     <c lang="csharp">ThrowIfCancellationRequested</c> also count, so a worker loop stays
///     silent. A lambda and a local function body stay out, because their return does not exit
///     the loop.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InfiniteLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128251";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Loop with no exit path",
        "The loop has no exit path, so the enclosing member cannot return",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Add a break, a return, or a throw, or use a condition that can become false. An endless loop that never yields hangs its caller.");

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

        context.RegisterCompilationStartAction(static start =>
        {
            var types = ExitTypes.Create(start.Compilation);
            start.RegisterSyntaxNodeAction(
                nodeContext => Analyze(nodeContext, types),
                SyntaxKind.WhileStatement,
                SyntaxKind.DoStatement,
                SyntaxKind.ForStatement);
        });
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, ExitTypes types)
    {
        var loop = (StatementSyntax)context.Node;
        if (!IsInfinite(loop, context) || HasExit(GetBody(loop), loop, types, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, GetKeyword(loop).GetLocation()));
    }

    private static bool IsInfinite(StatementSyntax loop, SyntaxNodeAnalysisContext context)
    {
        return loop switch
        {
            WhileStatementSyntax whileStatement => IsTrue(whileStatement.Condition, context),
            DoStatementSyntax doStatement => IsTrue(doStatement.Condition, context),
            ForStatementSyntax forStatement => forStatement.Condition is null,
            _ => false
        };
    }

    private static bool IsTrue(ExpressionSyntax? condition, SyntaxNodeAnalysisContext context)
    {
        if (condition is null)
        {
            return false;
        }

        var constant = context.SemanticModel.GetConstantValue(condition, context.CancellationToken);
        return constant.HasValue && constant.Value is true;
    }

    private static StatementSyntax GetBody(StatementSyntax loop)
    {
        return loop switch
        {
            WhileStatementSyntax whileStatement => whileStatement.Statement,
            DoStatementSyntax doStatement => doStatement.Statement,
            ForStatementSyntax forStatement => forStatement.Statement,
            _ => loop
        };
    }

    private static SyntaxToken GetKeyword(StatementSyntax loop)
    {
        return loop switch
        {
            WhileStatementSyntax whileStatement => whileStatement.WhileKeyword,
            DoStatementSyntax doStatement => doStatement.DoKeyword,
            ForStatementSyntax forStatement => forStatement.ForKeyword,
            _ => loop.GetFirstToken()
        };
    }

    private static bool HasExit(
        StatementSyntax body,
        StatementSyntax loop,
        ExitTypes types,
        SyntaxNodeAnalysisContext context)
    {
        foreach (var node in body.DescendantNodesAndSelf(Descend))
        {
            if (IsControlFlowExit(node)
                || IsLoopBreak(node, loop)
                || (node is InvocationExpressionSyntax invocation && IsExitCall(invocation, types, context)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsControlFlowExit(SyntaxNode node)
    {
        if (node is ReturnStatementSyntax or ThrowStatementSyntax or ThrowExpressionSyntax or GotoStatementSyntax)
        {
            return true;
        }

        return node is YieldStatementSyntax yieldStatement
            && yieldStatement.IsKind(SyntaxKind.YieldBreakStatement);
    }

    private static bool IsLoopBreak(SyntaxNode node, StatementSyntax loop)
    {
        return node is BreakStatementSyntax breakStatement && TargetsLoop(breakStatement, loop);
    }

    /// <summary>
    ///     True when the <c lang="csharp">break</c> targets this loop. A
    ///     <c lang="csharp">break</c> in a nested loop or switch stops at that construct first.
    /// </summary>
    private static bool TargetsLoop(BreakStatementSyntax @break, StatementSyntax loop)
    {
        for (var ancestor = @break.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor == loop)
            {
                return true;
            }

            if (ancestor is SwitchStatementSyntax
                or ForStatementSyntax
                or WhileStatementSyntax
                or DoStatementSyntax
                or CommonForEachStatementSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsExitCall(
        InvocationExpressionSyntax invocation,
        ExitTypes types,
        SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method)
        {
            return false;
        }

        if (types.Environment is { } environment
            && method.IsStatic
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, environment)
            && (string.Equals(method.Name, "Exit", StringComparison.Ordinal)
                || string.Equals(method.Name, "FailFast", StringComparison.Ordinal)))
        {
            return true;
        }

        return types.CancellationToken is { } token
            && string.Equals(method.Name, "ThrowIfCancellationRequested", StringComparison.Ordinal)
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, token);
    }

    private static bool Descend(SyntaxNode node)
    {
        return node is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
    }

    private sealed class ExitTypes
    {
        private ExitTypes(INamedTypeSymbol? environment, INamedTypeSymbol? cancellationToken)
        {
            Environment = environment;
            CancellationToken = cancellationToken;
        }

        internal INamedTypeSymbol? Environment { get; }

        internal INamedTypeSymbol? CancellationToken { get; }

        internal static ExitTypes Create(Compilation compilation)
        {
            return new ExitTypes(
                compilation.GetTypeByMetadataName("System.Environment"),
                compilation.GetTypeByMetadataName("System.Threading.CancellationToken"));
        }
    }
}
