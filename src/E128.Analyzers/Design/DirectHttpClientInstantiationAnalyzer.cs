using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DirectHttpClientInstantiationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128004";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use IHttpClientFactory instead of new HttpClient()",
        "Use IHttpClientFactory.CreateClient() instead of new HttpClient() — register via DI",
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
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ObjectCreationExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        // Only interested in zero-argument constructors.
        if (creation.ArgumentList is { Arguments.Count: > 0 })
        {
            return;
        }

        // Quick name filter before semantic work.
        var typeName = creation.Type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            QualifiedNameSyntax q => q.Right.Identifier.ValueText,
            _ => null
        };

        if (!string.Equals(typeName, "HttpClient", StringComparison.Ordinal))
        {
            return;
        }

        // Semantic verification: confirm the type resolves to System.Net.Http.HttpClient.
        var typeInfo = context.SemanticModel.GetTypeInfo(creation, context.CancellationToken);
        if (typeInfo.Type is not INamedTypeSymbol namedType)
        {
            return;
        }

        if (!IsSystemNetHttpHttpClient(namedType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.GetLocation()));
    }

    private static bool IsSystemNetHttpHttpClient(INamedTypeSymbol type)
    {
        if (!string.Equals(type.Name, "HttpClient", StringComparison.Ordinal))
        {
            return false;
        }

        var ns = type.ContainingNamespace;
        // Expecting: System.Net.Http
        return ns is { Name: "Http" }
               && ns.ContainingNamespace is { Name: "Net" }
               && ns.ContainingNamespace.ContainingNamespace is { Name: "System" }
               && ns.ContainingNamespace.ContainingNamespace.ContainingNamespace?.IsGlobalNamespace == true;
    }
}
