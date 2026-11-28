using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Shared detection for the blocking-call-with-async-sibling rules. A call reports when it is
///     not already inside an async context, the bound method has an <c lang="csharp">Async</c>
///     sibling on the same receiver, and the receiver resolves to the rule's target type.
/// </summary>
internal static class AsyncSiblingRules
{
    internal static void Analyze(
        SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor rule,
        Func<IMethodSymbol, ITypeSymbol?> resolveReceiver,
        Func<ITypeSymbol, string, bool> hasAsyncSibling)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (AsyncContextSyntaxHelper.IsInsideAsyncContext(invocation)
            || AsyncContextSyntaxHelper.HasAsyncSuffixSyntaxName(invocation)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.Name.EndsWith("Async", StringComparison.Ordinal)
            || resolveReceiver(method) is not { } receiver)
        {
            return;
        }

        var asyncSiblingName = method.Name + "Async";
        if (!hasAsyncSibling(receiver, asyncSiblingName))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(rule, invocation.GetLocation(), method.Name, asyncSiblingName));
    }
}
