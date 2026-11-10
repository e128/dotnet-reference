using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnboundedChannelAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128216";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Unbounded channel can grow without limit",
        "'Channel.CreateUnbounded' has no capacity bound — use 'Channel.CreateBounded' when producers can outrun consumers",
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

        context.RegisterCompilationStartAction(static startContext =>
            MetadataTypes.RegisterForType(
                startContext,
                "System.Threading.Channels.Channel",
                AnalyzeInvocation,
                SyntaxKind.InvocationExpression));
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, INamedTypeSymbol channel)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax access
            || !IsUnboundedFactory(access.Name.Identifier.ValueText))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, channel))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsUnboundedFactory(string methodName)
    {
        return string.Equals(methodName, "CreateUnbounded", StringComparison.Ordinal)
               || string.Equals(methodName, "CreateUnboundedPrioritized", StringComparison.Ordinal);
    }
}
