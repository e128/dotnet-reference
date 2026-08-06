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
        if (arguments.Count == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.GetLocation(),
                $"Process.{method.Name}() has no timeout and can hang forever — pass a timeout"));
            return;
        }

        if (!string.Equals(method.Name, "WaitForExitAsync", StringComparison.Ordinal) || arguments.Count != 1)
        {
            return;
        }

        var tokenArgument = arguments[0].Expression;
        if (IsInMethodCancellationTokenSourceToken(context, tokenArgument))
        {
            return;
        }

        if (IsEnclosingMethodParameterToken(context, tokenArgument) || IsCancellationTokenNone(context, tokenArgument))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                invocation.GetLocation(),
                "WaitForExitAsync's token isn't provably timeout-backed — use a CancellationTokenSource created in this method, or add a timeout"));
        }
    }

    private static bool IsProcessType(ITypeSymbol? type)
    {
        return type is not null
               && string.Equals(type.Name, "Process", StringComparison.Ordinal)
               && string.Equals(type.ContainingNamespace?.ToDisplayString(), "System.Diagnostics", StringComparison.Ordinal);
    }

    private static bool IsInMethodCancellationTokenSourceToken(SyntaxNodeAnalysisContext context, ExpressionSyntax tokenArgument)
    {
        return tokenArgument is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Token" } tokenAccess
               && context.SemanticModel.GetSymbolInfo(tokenAccess.Expression, context.CancellationToken).Symbol is ILocalSymbol
               {
                   Type.Name: "CancellationTokenSource"
               };
    }

    private static bool IsEnclosingMethodParameterToken(SyntaxNodeAnalysisContext context, ExpressionSyntax tokenArgument)
    {
        return context.SemanticModel.GetSymbolInfo(tokenArgument, context.CancellationToken).Symbol is IParameterSymbol
        {
            Type.Name: "CancellationToken"
        };
    }

    private static bool IsCancellationTokenNone(SyntaxNodeAnalysisContext context, ExpressionSyntax tokenArgument)
    {
        return context.SemanticModel.GetSymbolInfo(tokenArgument, context.CancellationToken).Symbol is IPropertySymbol
        {
            Name: "None",
            ContainingType.Name: "CancellationToken"
        };
    }
}
