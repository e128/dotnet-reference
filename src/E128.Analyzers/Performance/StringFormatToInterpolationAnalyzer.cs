using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StringFormatToInterpolationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128015";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use string interpolation instead of string.Format",
        "Replace string.Format with string interpolation ($\"...\") for better performance and readability",
        "Performance",
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

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Format", StringComparison.Ordinal))
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return;
        }

        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!IsSystemStringFormat(method))
        {
            return;
        }

        var formatArgIndex = GetFormatStringArgumentIndex(method);
        if (formatArgIndex < 0 || formatArgIndex >= arguments.Count)
        {
            return;
        }

        var formatExpression = arguments[formatArgIndex].Expression;
        var constantValue = context.SemanticModel.GetConstantValue(formatExpression, context.CancellationToken);
        if (!constantValue.HasValue || constantValue.Value is not string)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsSystemStringFormat(IMethodSymbol method)
    {
        if (!string.Equals(method.Name, "Format", StringComparison.Ordinal))
        {
            return false;
        }

        var containingType = method.ContainingType;
        return containingType is not null && containingType.SpecialType == SpecialType.System_String;
    }

    internal static int GetFormatStringArgumentIndex(IMethodSymbol method)
    {
        var parameters = method.Parameters;
        return parameters.Length < 2 ? -1 : IsFormatProviderParameter(parameters[0].Type) ? 1 : 0;
    }

    private static bool IsFormatProviderParameter(ITypeSymbol type)
    {
        if (IsFormatProviderInterface(type))
        {
            return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (IsFormatProviderInterface(iface))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFormatProviderInterface(ITypeSymbol type)
    {
        if (!string.Equals(type.Name, "IFormatProvider", StringComparison.Ordinal))
        {
            return false;
        }

        var ns = type.ContainingNamespace;
        return ns != null
               && string.Equals(ns.Name, "System", StringComparison.Ordinal)
               && ns.ContainingNamespace != null
               && ns.ContainingNamespace.IsGlobalNamespace;
    }
}
