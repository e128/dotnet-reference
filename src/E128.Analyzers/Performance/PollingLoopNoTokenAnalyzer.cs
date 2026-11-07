using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PollingLoopNoTokenAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128211";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Task.Delay in a loop has no cancellation token",
        "'Task.Delay' in a loop does not observe cancellation — pass a CancellationToken",
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

        context.RegisterCompilationStartAction(startContext =>
        {
            var task = startContext.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
            var cancellationToken = startContext.Compilation.GetTypeByMetadataName(
                "System.Threading.CancellationToken");
            if (task is null || cancellationToken is null)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, task, cancellationToken),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol task,
        INamedTypeSymbol cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (!string.Equals(GetInvokedName(invocation), "Delay", StringComparison.Ordinal))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, task))
        {
            return;
        }

        if (method.Parameters.Any(
                parameter => SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationToken)))
        {
            return;
        }

        if (LoopContext.FindEnclosingLoopBody(invocation) is null)
        {
            return;
        }

        if (ObservesCancellationInLoopCondition(invocation, cancellationToken, context))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool ObservesCancellationInLoopCondition(
        InvocationExpressionSyntax invocation,
        INamedTypeSymbol cancellationToken,
        SyntaxNodeAnalysisContext context)
    {
        var loop = LoopContext.FindEnclosingLoop(invocation);
        var condition = loop switch
        {
            ForStatementSyntax forStatement => forStatement.Condition,
            WhileStatementSyntax whileStatement => whileStatement.Condition,
            DoStatementSyntax doStatement => doStatement.Condition,
            _ => null
        };

        if (condition is null)
        {
            return false;
        }

        foreach (var memberAccess in condition.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
        {
            if (string.Equals(
                    memberAccess.Name.Identifier.ValueText,
                    "IsCancellationRequested",
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (var identifier in condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            var type = context.SemanticModel.GetTypeInfo(identifier, context.CancellationToken).Type;
            if (SymbolEqualityComparer.Default.Equals(type, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetInvokedName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };
    }
}
