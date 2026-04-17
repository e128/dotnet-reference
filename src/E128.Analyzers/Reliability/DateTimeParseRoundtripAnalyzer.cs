using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DateTimeParseRoundtripAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128016";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "DateTime.Parse/ParseExact missing DateTimeStyles parameter",
        "{0}.{1} is missing a DateTimeStyles parameter — add DateTimeStyles.RoundtripKind to preserve UTC kind from ISO 8601 strings",
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

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        if (!IsTargetMethod(methodName))
        {
            return;
        }

        var expectedArgCount = GetExpectedArgCountWithoutStyles(methodName);
        if (invocation.ArgumentList.Arguments.Count != expectedArgCount)
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        var containingType = method.ContainingType?.ToDisplayString();
        if (!IsTargetContainingType(containingType))
        {
            return;
        }

        if (HasDateTimeStylesLastParam(method))
        {
            return;
        }

        var typeName = GetShortTypeName(containingType);
        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), typeName, methodName));
    }

    private static bool IsTargetMethod(string name)
    {
        return name is "Parse" or "ParseExact";
    }

    private static int GetExpectedArgCountWithoutStyles(string methodName)
    {
        return methodName.Equals("Parse", StringComparison.Ordinal) ? 2 :
            methodName.Equals("ParseExact", StringComparison.Ordinal) ? 3 : -1;
    }

    private static bool IsTargetContainingType(string? containingType)
    {
        return string.Equals(containingType, "System.DateTime", StringComparison.Ordinal) ||
               string.Equals(containingType, "System.DateTimeOffset", StringComparison.Ordinal);
    }

    private static bool HasDateTimeStylesLastParam(IMethodSymbol method)
    {
        if (method.Parameters.Length < 1)
        {
            return false;
        }

        var lastParamType = method.Parameters[method.Parameters.Length - 1].Type?.ToDisplayString();
        return string.Equals(lastParamType, "System.Globalization.DateTimeStyles", StringComparison.Ordinal);
    }

    private static string GetShortTypeName(string? containingType)
    {
        return string.Equals(containingType, "System.DateTime", StringComparison.Ordinal) ? "DateTime" : "DateTimeOffset";
    }
}
