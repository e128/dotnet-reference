using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Performance;

/// <summary>
///     E128230: A minimal API app serves static files through the request-time middleware with
///     <c lang="csharp">UseStaticFiles</c>. <c lang="csharp">MapStaticAssets</c> serves a
///     build-time manifest instead.
/// </summary>
/// <remarks>
///     The rule registers only when the compilation can bind <c lang="csharp">MapStaticAssets</c>,
///     so a target framework without the endpoint mapping API gets no report. The rewrite moves the
///     call into the endpoint mapping section, and a Razor app also changes how it references the
///     assets, so the rule carries no code fix.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RuntimeStaticFilesAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128230";

    private const string StaticFileExtensionsTypeName = "Microsoft.AspNetCore.Builder.StaticFileExtensions";

    private const string StaticAssetsExtensionsTypeName = "Microsoft.AspNetCore.Builder.StaticAssetsEndpointRouteBuilderExtensions";

    private const string UseStaticFilesName = "UseStaticFiles";

    private const string MapStaticAssetsName = "MapStaticAssets";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Static files served through the runtime pipeline",
        "UseStaticFiles runs a request-time middleware. MapStaticAssets serves the build-time asset manifest with precompressed variants and ETags.",
        "E128-Performance",
        DiagnosticSeverity.Warning,
        true,
        "MapStaticAssets serves the assets from a build-time manifest. " +
        "Move the call into the endpoint mapping section and let the manifest publish the assets.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
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
            var compilation = start.Compilation;
            var staticFiles = compilation.GetTypeByMetadataName(StaticFileExtensionsTypeName);
            var staticAssets = compilation.GetTypeByMetadataName(StaticAssetsExtensionsTypeName);

            if (staticFiles is null
                || staticAssets is null
                || !HasMapStaticAssets(staticAssets))
            {
                return;
            }

            start.RegisterSyntaxNodeAction(nodeContext => Analyze(nodeContext, staticFiles), SyntaxKind.InvocationExpression);
        });
    }

    private static bool HasMapStaticAssets(INamedTypeSymbol staticAssets)
    {
        return staticAssets.GetMembers(MapStaticAssetsName)
            .OfType<IMethodSymbol>()
            .Any(static method => method.IsExtensionMethod);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context, INamedTypeSymbol staticFiles)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.ArgumentList.Arguments.Count is not 0)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !string.Equals(method.Name, UseStaticFilesName, StringComparison.Ordinal)
            || !TypeSymbols.Matches(method.ContainingType, staticFiles))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }
}
