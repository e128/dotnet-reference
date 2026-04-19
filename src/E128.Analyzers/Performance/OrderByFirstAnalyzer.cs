using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OrderByFirstAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128009";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use MinBy/MaxBy instead of OrderBy().First()",
        "Replace OrderBy/OrderByDescending + First/FirstOrDefault with MinBy/MaxBy for O(n) instead of O(n log n)",
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

        if (!IsFirstOrFirstOrDefault(invocation, out var outerAccess))
        {
            return;
        }

        if (!IsOrderByWithSingleArg(outerAccess, out var innerInvocation))
        {
            return;
        }

        var outerSymbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (outerSymbol is null || !IsLinqEnumerableMethod(outerSymbol))
        {
            return;
        }

        var innerSymbol = context.SemanticModel.GetSymbolInfo(innerInvocation, context.CancellationToken).Symbol;
        if (innerSymbol is null || !IsLinqEnumerableMethod(innerSymbol))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool IsFirstOrFirstOrDefault(
        InvocationExpressionSyntax invocation,
        [NotNullWhen(true)] out MemberAccessExpressionSyntax? outerAccess)
    {
        outerAccess = null;

        if (invocation.Expression is not MemberAccessExpressionSyntax access)
        {
            return false;
        }

        var name = access.Name.Identifier.ValueText;
        if (!string.Equals(name, "First", StringComparison.Ordinal)
            && !string.Equals(name, "FirstOrDefault", StringComparison.Ordinal))
        {
            return false;
        }

        if (invocation.ArgumentList.Arguments.Any())
        {
            return false;
        }

        outerAccess = access;
        return true;
    }

    private static bool IsOrderByWithSingleArg(
        MemberAccessExpressionSyntax outerAccess,
        [NotNullWhen(true)] out InvocationExpressionSyntax? innerInvocation)
    {
        innerInvocation = null;

        if (outerAccess.Expression is not InvocationExpressionSyntax inner)
        {
            return false;
        }

        if (inner.Expression is not MemberAccessExpressionSyntax innerAccess)
        {
            return false;
        }

        var innerName = innerAccess.Name.Identifier.ValueText;
        if (!string.Equals(innerName, "OrderBy", StringComparison.Ordinal)
            && !string.Equals(innerName, "OrderByDescending", StringComparison.Ordinal))
        {
            return false;
        }

        if (inner.ArgumentList.Arguments.Count != 1)
        {
            return false;
        }

        innerInvocation = inner;
        return true;
    }

    private static bool IsLinqEnumerableMethod(ISymbol symbol)
    {
        if (symbol is not IMethodSymbol method)
        {
            return false;
        }

        var containingType = method.ContainingType;
        if (containingType is null
            || !string.Equals(containingType.Name, "Enumerable", StringComparison.Ordinal))
        {
            return false;
        }

        var ns = containingType.ContainingNamespace;
        return ns is not null
               && string.Equals(ns.Name, "Linq", StringComparison.Ordinal)
               && ns.ContainingNamespace is not null
               && string.Equals(ns.ContainingNamespace.Name, "System", StringComparison.Ordinal)
               && ns.ContainingNamespace.ContainingNamespace is not null
               && ns.ContainingNamespace.ContainingNamespace.IsGlobalNamespace;
    }
}
