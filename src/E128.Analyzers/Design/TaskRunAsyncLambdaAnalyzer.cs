using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskRunAsyncLambdaAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128036";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Task.Run wrapping async lambda — unnecessary thread pool hop for I/O-bound work",
        "Task.Run wrapping an async lambda queues I/O-bound work to the thread pool unnecessarily — await the async method directly unless the work is CPU-bound",
        "Design",
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
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsTaskRunSyntax(invocation))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;

#pragma warning disable RCS9004
        if (args.Count == 0)
#pragma warning restore RCS9004
        {
            return;
        }

        var firstArg = args[0].Expression;
        if (!IsAsyncLambdaOrDelegate(firstArg))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!IsSystemThreadingTasksTaskRun(method))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsTaskRunSyntax(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
               && string.Equals(memberAccess.Name.Identifier.ValueText, "Run", StringComparison.Ordinal);
    }

    private static bool IsAsyncLambdaOrDelegate(ExpressionSyntax expression)
    {
        return expression switch
        {
            ParenthesizedLambdaExpressionSyntax lambda => lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
            SimpleLambdaExpressionSyntax lambda => lambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
            AnonymousMethodExpressionSyntax anonymous => anonymous.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
            _ => false
        };
    }

    private static bool IsSystemThreadingTasksTaskRun(IMethodSymbol method)
    {
        if (!string.Equals(method.Name, "Run", StringComparison.Ordinal))
        {
            return false;
        }

        var containingType = method.ContainingType;
        return containingType is not null
               && string.Equals(containingType.Name, "Task", StringComparison.Ordinal)
               && containingType.ContainingNamespace is { Name: "Tasks" }
               && containingType.ContainingNamespace.ContainingNamespace is { Name: "Threading" }
               && containingType.ContainingNamespace.ContainingNamespace.ContainingNamespace is { Name: "System" };
    }
}
