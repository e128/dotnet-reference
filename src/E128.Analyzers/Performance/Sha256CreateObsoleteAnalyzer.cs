using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Sha256CreateObsoleteAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128072";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Prefer SHA256.HashData() over SHA256.Create()",
        "SHA256.Create() allocates — use SHA256.HashData() for single-shot hashing or SHA256.HashData(Stream) for streaming",
        "Performance",
        DiagnosticSeverity.Info,
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

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Create", StringComparison.Ordinal))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!string.Equals(method.ContainingType?.Name, "SHA256", StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(method.ContainingType?.ContainingNamespace?.ToDisplayString(), "System.Security.Cryptography", StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation()));
    }
}
