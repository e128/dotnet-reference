using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UntimedProcessExitAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128099";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Wait for process exit with a provable timeout",
        "{0}",
        "E128-Reliability",
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
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "WaitForExit" or "WaitForExitAsync"
            })
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsProcessType(method.ContainingType))
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count is 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.GetLocation(),
                $"Process.{method.Name}() has no timeout and can hang forever — pass a timeout"));
            return;
        }

        if (!string.Equals(method.Name, "WaitForExitAsync", StringComparison.Ordinal) || arguments.Count is not 1)
        {
            return;
        }

        if (!IsTokenWithoutProvableTimeout(context, arguments[0].Expression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            invocation.GetLocation(),
            "WaitForExitAsync's token isn't provably timeout-backed — use a CancellationTokenSource created in this method, or add a timeout"));
    }

    private static bool IsTokenWithoutProvableTimeout(SyntaxNodeAnalysisContext context, ExpressionSyntax tokenArgument)
    {
        if (IsInMethodCancellationTokenSourceToken(context, tokenArgument))
        {
            return false;
        }

        return IsEnclosingMethodParameterToken(context, tokenArgument) || IsCancellationTokenNone(context, tokenArgument);
    }

    private static bool IsProcessType(ITypeSymbol? type)
    {
        return type is not null
               && string.Equals(type.Name, "Process", StringComparison.Ordinal)
               && string.Equals(type.ContainingNamespace?.ToDisplayString(), "System.Diagnostics", StringComparison.Ordinal);
    }

    private static bool IsInMethodCancellationTokenSourceToken(SyntaxNodeAnalysisContext context, ExpressionSyntax tokenArgument)
    {
        return tokenArgument is MemberAccessExpressionSyntax tokenAccess
               && string.Equals(tokenAccess.Name.Identifier.ValueText, "Token", StringComparison.Ordinal)
               && context.SemanticModel.GetSymbolInfo(tokenAccess.Expression, context.CancellationToken).Symbol is ILocalSymbol local
               && string.Equals(local.Type.Name, "CancellationTokenSource", StringComparison.Ordinal);
    }

    private static bool IsEnclosingMethodParameterToken(SyntaxNodeAnalysisContext context, ExpressionSyntax tokenArgument)
    {
        return context.SemanticModel.GetSymbolInfo(tokenArgument, context.CancellationToken).Symbol is IParameterSymbol parameter
               && string.Equals(parameter.Type.Name, "CancellationToken", StringComparison.Ordinal);
    }

    private static bool IsCancellationTokenNone(SyntaxNodeAnalysisContext context, ExpressionSyntax tokenArgument)
    {
        return context.SemanticModel.GetSymbolInfo(tokenArgument, context.CancellationToken).Symbol is IPropertySymbol property
               && string.Equals(property.Name, "None", StringComparison.Ordinal)
               && string.Equals(property.ContainingType.Name, "CancellationToken", StringComparison.Ordinal);
    }
}
