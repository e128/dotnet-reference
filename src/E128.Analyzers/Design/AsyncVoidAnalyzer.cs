using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128007";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Avoid async void methods",
        messageFormat: "Method '{0}' is async void — use async Task instead",
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
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            return;
        }

        if (method.ReturnType is not PredefinedTypeSyntax predefined
            || !predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            return;
        }

        if (IsEventHandlerSignature(method, context.SemanticModel, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.ValueText));
    }

    private static bool IsEventHandlerSignature(
        MethodDeclarationSyntax method,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        if (method.ParameterList.Parameters.Count != 2)
        {
            return false;
        }

        var secondParam = method.ParameterList.Parameters[1];
        if (secondParam.Type is null)
        {
            return false;
        }

        var typeInfo = semanticModel.GetTypeInfo(secondParam.Type, cancellationToken);
        return typeInfo.Type is INamedTypeSymbol paramType && DerivesFromEventArgs(paramType);
    }

    private static bool DerivesFromEventArgs(INamedTypeSymbol type)
    {
        var current = type;
        while (current is not null)
        {
            if (string.Equals(current.Name, "EventArgs", StringComparison.Ordinal)
                && current.ContainingNamespace is { Name: "System" }
                && current.ContainingNamespace.ContainingNamespace?.IsGlobalNamespace == true)
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
