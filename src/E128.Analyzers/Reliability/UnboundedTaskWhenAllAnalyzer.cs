using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Reports <c>Task.WhenAll</c> calls whose argument is a LINQ <c>.Select(async ...)</c>
///     without a <c>SemaphoreSlim.WaitAsync()</c> throttle in the async lambda body.
///     Unbounded fan-out can exhaust resources (memory, connections, thread pool) when the
///     collection is large.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnboundedTaskWhenAllAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128037";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Unbounded Task.WhenAll over async Select",
        "Task.WhenAll fans out all items concurrently with no throttle — add SemaphoreSlim or use bounded parallelism",
        "Reliability",
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

        if (!IsTaskWhenAll(context, invocation))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;
        if (args.Count != 1)
        {
            return;
        }

        var argument = args[0].Expression;

        if (!TryGetSelectInvocation(argument, out var selectInvocation))
        {
            return;
        }

        if (!HasAsyncLambda(selectInvocation))
        {
            return;
        }

        if (ContainsSemaphoreThrottle(selectInvocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsTaskWhenAll(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        return symbolInfo.Symbol is IMethodSymbol method
               && string.Equals(method.Name, "WhenAll", StringComparison.Ordinal)
               && string.Equals(method.ContainingType?.ToDisplayString(), "System.Threading.Tasks.Task", StringComparison.Ordinal);
    }

    private static bool TryGetSelectInvocation(ExpressionSyntax expression, out InvocationExpressionSyntax selectInvocation)
    {
        selectInvocation = null!;

        if (expression is not InvocationExpressionSyntax candidate)
        {
            return false;
        }

        if (candidate.Expression is MemberAccessExpressionSyntax memberAccess
            && string.Equals(memberAccess.Name.Identifier.ValueText, "Select", StringComparison.Ordinal))
        {
            selectInvocation = candidate;
            return true;
        }

        return false;
    }

    private static bool HasAsyncLambda(InvocationExpressionSyntax selectInvocation)
    {
        var args = selectInvocation.ArgumentList.Arguments;
        if (!args.Any())
        {
            return false;
        }

        var lambdaArg = args.Last().Expression;
        return lambdaArg switch
        {
            ParenthesizedLambdaExpressionSyntax pLambda => pLambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
            SimpleLambdaExpressionSyntax sLambda => sLambda.AsyncKeyword.IsKind(SyntaxKind.AsyncKeyword),
            _ => false
        };
    }

    private static bool ContainsSemaphoreThrottle(InvocationExpressionSyntax selectInvocation)
    {
        var args = selectInvocation.ArgumentList.Arguments;
        if (!args.Any())
        {
            return false;
        }

        var lambdaArg = args.Last().Expression;

        return lambdaArg.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(inv => inv.Expression is MemberAccessExpressionSyntax ma
                        && string.Equals(ma.Name.Identifier.ValueText, "WaitAsync", StringComparison.Ordinal));
    }
}
