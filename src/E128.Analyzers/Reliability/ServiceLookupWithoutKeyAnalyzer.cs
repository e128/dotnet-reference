using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128258: A service-collection lookup throws <see cref="InvalidOperationException"/> from
///     <c lang="csharp">Enumerable.First</c> or <c lang="csharp">Single</c> and names no key.
///     Use <c lang="csharp">FirstOrDefault</c> and throw an exception that names the extension.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceLookupWithoutKeyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128258";

    private const string EnumerableMetadataName = "System.Linq.Enumerable";

    private const string ServiceProviderExtensionsMetadataName =
        "Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions";

    private const string GetServicesName = "GetServices";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Service lookup throws a generic exception with no key",
        "'{0}' throws when no service matches and names no key: use '{1}' and throw an exception that names the key",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "Enumerable.First and Enumerable.Single throw an InvalidOperationException that names no key. Use FirstOrDefault or SingleOrDefault, then throw an exception that names the missing service or extension.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static start =>
        {
            var enumerable = start.Compilation.GetTypeByMetadataName(EnumerableMetadataName);
            var serviceProviderExtensions = start.Compilation.GetTypeByMetadataName(ServiceProviderExtensionsMetadataName);

            if (enumerable is null || serviceProviderExtensions is null)
            {
                return;
            }

            start.RegisterSyntaxNodeAction(
                node => Analyze(node, enumerable, serviceProviderExtensions),
                SyntaxKind.InvocationExpression);
        });
    }

    private static void Analyze(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol enumerable,
        INamedTypeSymbol serviceProviderExtensions)
    {
        if (context.Node is not InvocationExpressionSyntax syntax
            || context.SemanticModel.GetSymbolInfo(syntax, context.CancellationToken).Symbol is not IMethodSymbol method
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, enumerable)
            || !TryGetReplacement(method.Name, out var replacement)
            || syntax.Expression is not MemberAccessExpressionSyntax memberAccess
            || !IsGetServicesCall(memberAccess.Expression, context, serviceProviderExtensions))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            syntax.GetLocation(),
            method.Name,
            replacement));
    }

    private static bool TryGetReplacement(string name, out string replacement)
    {
        switch (name)
        {
            case "First":
                replacement = "FirstOrDefault";
                return true;
            case "Single":
                replacement = "SingleOrDefault";
                return true;
            default:
                replacement = string.Empty;
                return false;
        }
    }

    private static bool IsGetServicesCall(
        ExpressionSyntax expression,
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol serviceProviderExtensions)
    {
        return expression is InvocationExpressionSyntax invocation
               && context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
               && string.Equals(method.Name, GetServicesName, StringComparison.Ordinal)
               && SymbolEqualityComparer.Default.Equals(method.ContainingType, serviceProviderExtensions);
    }

}
