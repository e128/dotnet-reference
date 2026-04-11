using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SyncOverAsyncAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128008";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Avoid sync-over-async (.Result / .GetAwaiter().GetResult())",
        messageFormat: "Avoid blocking on async code with {0} — use await instead",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeResultAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeGetAwaiterCall, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeResultAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Result", StringComparison.Ordinal))
        {
            return;
        }

        if (IsInExcludedMethod(memberAccess))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken);
        if (symbolInfo.Symbol is not IPropertySymbol property)
        {
            return;
        }

        if (!IsTaskOrValueTaskType(property.ContainingType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation(), ".Result"));
    }

    private static void AnalyzeGetAwaiterCall(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "GetAwaiter", StringComparison.Ordinal))
        {
            return;
        }

        if (IsInExcludedMethod(memberAccess))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken);
        if (symbolInfo.Symbol is not IMethodSymbol method)
        {
            return;
        }

        if (!IsTaskOrValueTaskType(method.ContainingType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.Name.GetLocation(), ".GetAwaiter().GetResult()"));
    }

    private static bool IsInExcludedMethod(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            if (ancestor is not MethodDeclarationSyntax method)
            {
                continue;
            }

            var name = method.Identifier.ValueText;
            return (string.Equals(name, "Main", StringComparison.Ordinal)
                    && method.Modifiers.Any(SyntaxKind.StaticKeyword))
                || string.Equals(name, "Dispose", StringComparison.Ordinal);
        }

        return false;
    }

    private static bool IsTaskOrValueTaskType(INamedTypeSymbol type)
    {
        var originalDefinition = type.OriginalDefinition;
        var name = originalDefinition.Name;

        if (!string.Equals(name, "Task", StringComparison.Ordinal)
            && !string.Equals(name, "ValueTask", StringComparison.Ordinal))
        {
            return false;
        }

        var ns = originalDefinition.ContainingNamespace;
        return ns is { Name: "Tasks" }
            && ns.ContainingNamespace is { Name: "Threading" }
            && ns.ContainingNamespace.ContainingNamespace is { Name: "System" }
            && ns.ContainingNamespace.ContainingNamespace.ContainingNamespace?.IsGlobalNamespace == true;
    }
}
