using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128096: Detects a synchronous call to an <c lang="csharp">async</c> local function
///     via <c lang="csharp">.Result</c> or <c lang="csharp">.Wait()</c>. Complements VSTHRD002, which
///     targets method declarations but not local functions.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SyncLocalFunctionCallAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128096";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Call async local function with await instead of blocking",
        "Synchronous call to async local function '{0}' via '{1}' blocks the calling thread",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "VSTHRD002 targets method declarations but not local functions. A local function declared with 'async' and returning Task/Task<T> invoked via .Result or .Wait() silently blocks the calling thread.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        var blockingKind = GetBlockingKind(invocation);
        if (blockingKind is null)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { MethodKind: MethodKind.LocalFunction, IsAsync: true } localFunction)
        {
            return;
        }

        // Skip when the call site is already inside an async method/local function.
        if (context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart, context.CancellationToken)
            is IMethodSymbol { IsAsync: true })
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), localFunction.Name, blockingKind));
    }

    private static string? GetBlockingKind(InvocationExpressionSyntax invocation)
    {
        return invocation.Parent is MemberAccessExpressionSyntax memberAccess
            ? memberAccess.Name.Identifier.Text switch
            {
                "Result" => ".Result",
                "Wait" when memberAccess.Parent is InvocationExpressionSyntax => ".Wait()",
                _ => null
            }
            : null;
    }
}
