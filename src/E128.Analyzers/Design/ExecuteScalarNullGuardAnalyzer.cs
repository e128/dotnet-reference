using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128042: Detects Convert.ToInt32/ToInt64 wrapping ExecuteScalar/ExecuteScalarAsync
///     without a null guard. ExecuteScalar returns null for empty result sets, and
///     Convert.ToInt32(null) silently returns 0.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExecuteScalarNullGuardAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128042";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Convert.ToInt32/ToInt64 wrapping ExecuteScalar without null guard",
        "Convert.{0} wraps {1} without a null check — ExecuteScalar returns null for empty result sets, and Convert.{0}(null) silently returns 0",
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

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!IsConvertMethod(methodName))
        {
            return;
        }

        var args = invocation.ArgumentList.Arguments;
        if (!args.Any())
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!string.Equals(method.ContainingType?.ToDisplayString(), "System.Convert", StringComparison.Ordinal))
        {
            return;
        }

        var firstArg = args[0].Expression;
        if (!TryGetExecuteScalarMethodName(firstArg, out var scalarMethodName))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName, scalarMethodName));
    }

    private static bool TryGetExecuteScalarMethodName(ExpressionSyntax expression, out string scalarMethodName)
    {
        // Handle: Convert.ToInt32(cmd.ExecuteScalar())
        if (expression is InvocationExpressionSyntax directCall &&
            directCall.Expression is MemberAccessExpressionSyntax directAccess)
        {
            var name = directAccess.Name.Identifier.ValueText;
            if (IsExecuteScalarMethod(name))
            {
                scalarMethodName = name + "()";
                return true;
            }
        }

        // Handle: Convert.ToInt32(await cmd.ExecuteScalarAsync())
        if (expression is AwaitExpressionSyntax awaitExpr &&
            awaitExpr.Expression is InvocationExpressionSyntax awaitedCall &&
            awaitedCall.Expression is MemberAccessExpressionSyntax awaitedAccess)
        {
            var name = awaitedAccess.Name.Identifier.ValueText;
            if (IsExecuteScalarMethod(name))
            {
                scalarMethodName = name + "()";
                return true;
            }
        }

        scalarMethodName = string.Empty;
        return false;
    }

    private static bool IsConvertMethod(string name)
    {
        return string.Equals(name, "ToInt32", StringComparison.Ordinal) ||
               string.Equals(name, "ToInt64", StringComparison.Ordinal);
    }

    private static bool IsExecuteScalarMethod(string name)
    {
        return string.Equals(name, "ExecuteScalar", StringComparison.Ordinal) ||
               string.Equals(name, "ExecuteScalarAsync", StringComparison.Ordinal);
    }
}
