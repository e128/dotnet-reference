using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128092: Detects a synchronous <c>System.IO.File</c> call (<c>ReadAllText</c>,
///     <c>WriteAllBytes</c>, etc.) with an <c>Async</c> sibling, fired whenever the containing
///     method/local function/lambda is NOT already <c>async</c>. Complements CA1849/VSTHRD103,
///     which only fire inside an already-async method -- a wholly synchronous method gets no
///     signal from those rules at all.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncFileIoAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128092";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use the async File.*Async overload",
        "'{0}' has an async sibling '{1}' -- use it and make the containing method async instead of blocking synchronously",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "System.IO.File synchronous methods block the calling thread. CA1849/VSTHRD103 only flag this inside an already-async method; this rule also flags it in a fully synchronous method, where those analyzers give no signal at all.");

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

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (method.ContainingType is not { } containingType
            || !string.Equals(containingType.Name, "File", StringComparison.Ordinal)
            || !string.Equals(containingType.ContainingNamespace?.ToDisplayString(), "System.IO", StringComparison.Ordinal))
        {
            return;
        }

        if (method.Name.EndsWith("Async", StringComparison.Ordinal))
        {
            return;
        }

        var asyncSiblingName = method.Name + "Async";

        // Existence check only -- no parameter-shape comparison. BCL Async siblings always
        // accept the sync overload's parameters plus an optional trailing CancellationToken,
        // so a name match is sufficient without walking parameter lists.
        if (!containingType.GetMembers(asyncSiblingName).OfType<IMethodSymbol>().Any())
        {
            return;
        }

        if (IsInsideAsyncContext(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), method.Name, asyncSiblingName));
    }

    private static bool IsInsideAsyncContext(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            bool? isAsync = ancestor switch
            {
                MethodDeclarationSyntax m => m.Modifiers.Any(SyntaxKind.AsyncKeyword),
                LocalFunctionStatementSyntax lf => lf.Modifiers.Any(SyntaxKind.AsyncKeyword),
                ConstructorDeclarationSyntax => false,
                ParenthesizedLambdaExpressionSyntax lambda => lambda.AsyncKeyword != default,
                SimpleLambdaExpressionSyntax lambda => lambda.AsyncKeyword != default,
                AnonymousMethodExpressionSyntax anon => anon.AsyncKeyword != default,
                _ => null
            };

            if (isAsync is { } found)
            {
                return found;
            }
        }

        return false;
    }
}
