using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpRequestInLoopAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128203";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "HTTP request inside a loop creates N+1 round trips",
        "'{0}' inside a loop sends one request per iteration — batch the ids into one request",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true);

    private static readonly ImmutableHashSet<string> RequestMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "SendAsync",
        "GetAsync",
        "PostAsync",
        "PutAsync",
        "DeleteAsync",
        "PatchAsync");

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
            var httpClient = startContext.Compilation.GetTypeByMetadataName("System.Net.Http.HttpClient");
            if (httpClient is null)
            {
                return;
            }

            startContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, httpClient),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol httpClient)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return;
        }

        var methodName = access.Name.Identifier.ValueText;
        if (!RequestMethods.Contains(methodName))
        {
            return;
        }

        var body = LoopContext.FindEnclosingLoopBody(invocation);
        if (body is null || LoopContext.HasConstantBound(invocation))
        {
            return;
        }

        var receiverType = context.SemanticModel.GetTypeInfo(access.Expression, context.CancellationToken).Type;
        if (!TypeSymbols.DerivesFrom(receiverType, httpClient))
        {
            return;
        }

        if (LoopContext.ContainsWaitCall(body, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }
}
