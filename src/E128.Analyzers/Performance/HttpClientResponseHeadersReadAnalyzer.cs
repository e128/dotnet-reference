using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpClientResponseHeadersReadAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128010";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "HttpClient call missing HttpCompletionOption.ResponseHeadersRead",
        messageFormat: "'{0}' called without HttpCompletionOption.ResponseHeadersRead buffers the entire response body into memory",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "HttpClient.GetAsync and SendAsync default to ResponseContentRead, which buffers " +
            "the entire response body before the Task completes. Pass HttpCompletionOption.ResponseHeadersRead " +
            "to avoid unnecessary buffering.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var httpClientType = compilationContext.Compilation.GetTypeByMetadataName("System.Net.Http.HttpClient");
            if (httpClientType is null)
            {
                return;
            }

            var completionOptionType = compilationContext.Compilation.GetTypeByMetadataName("System.Net.Http.HttpCompletionOption");
            if (completionOptionType is null)
            {
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeInvocation(nodeContext, httpClientType, completionOptionType),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol httpClientType,
        INamedTypeSymbol completionOptionType)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.Text;
        if (!IsTargetMethod(methodName))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, httpClientType))
        {
            return;
        }

        if (methodSymbol.Parameters.Any(p => SymbolEqualityComparer.Default.Equals(p.Type, completionOptionType)))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }

    private static bool IsTargetMethod(string name) => name switch
    {
        "GetAsync" or "SendAsync" => true,
        _ => false,
    };
}
