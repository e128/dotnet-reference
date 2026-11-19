using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Security;

/// <summary>
///     E128242: A controller action carries a state-changing HTTP verb attribute with no
///     antiforgery validation in scope.
/// </summary>
/// <remarks>
///     The rule reports <c lang="csharp">[HttpPost]</c>, <c lang="csharp">[HttpPut]</c>,
///     <c lang="csharp">[HttpPatch]</c>, and <c lang="csharp">[HttpDelete]</c> actions.
///     <c lang="csharp">[ValidateAntiForgeryToken]</c> or
///     <c lang="csharp">[AutoValidateAntiforgeryToken]</c> on the method or its type clears
///     the report. <c lang="csharp">[IgnoreAntiforgeryToken]</c> is an explicit opt-out and
///     stays silent. A type marked <c lang="csharp">[ApiController]</c> also stays silent,
///     because a token-authenticated API is not a cookie CSRF surface.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AntiforgeryValidationAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128242";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Antiforgery missing on a state-changing action",
        "State-changing action '{0}' has no antiforgery validation",
        "E128-Security",
        DiagnosticSeverity.Warning,
        true,
        "Add [ValidateAntiForgeryToken] to the action or [AutoValidateAntiforgeryToken] to the controller. A cookie-authenticated action without a token accepts a cross-site request.");

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
            var types = MvcTypes.Create(start.Compilation);
            if (!types.IsUsable)
            {
                return;
            }

            start.RegisterSymbolAction(nodeContext => Analyze(nodeContext, types), SymbolKind.Method);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, MvcTypes types)
    {
        if (context.Symbol is not IMethodSymbol { MethodKind: MethodKind.Ordinary } method
            || !types.IsStateChanging(method))
        {
            return;
        }

        if (types.HasAntiforgery(method)
            || types.HasExplicitOptOut(method)
            || types.HasAntiforgery(method.ContainingType)
            || types.HasExplicitOptOut(method.ContainingType)
            || types.IsApiController(method.ContainingType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, method.Locations[0], method.Name));
    }

    private sealed class MvcTypes
    {
        private MvcTypes(Compilation compilation)
        {
            HttpPost = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.HttpPostAttribute");
            HttpPut = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.HttpPutAttribute");
            HttpPatch = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.HttpPatchAttribute");
            HttpDelete = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.HttpDeleteAttribute");
            Validate = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ValidateAntiForgeryTokenAttribute");
            AutoValidate = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute");
            Ignore = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryTokenAttribute");
            ApiController = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ApiControllerAttribute");
        }

        private INamedTypeSymbol? HttpPost { get; }

        private INamedTypeSymbol? HttpPut { get; }

        private INamedTypeSymbol? HttpPatch { get; }

        private INamedTypeSymbol? HttpDelete { get; }

        private INamedTypeSymbol? Validate { get; }

        private INamedTypeSymbol? AutoValidate { get; }

        private INamedTypeSymbol? Ignore { get; }

        private INamedTypeSymbol? ApiController { get; }

        internal bool IsUsable => Validate is not null
            && (HttpPost is not null || HttpPut is not null || HttpPatch is not null || HttpDelete is not null);

        internal static MvcTypes Create(Compilation compilation)
        {
            return new MvcTypes(compilation);
        }

        internal bool IsStateChanging(IMethodSymbol method)
        {
            return HasAttribute(method, HttpPost)
                || HasAttribute(method, HttpPut)
                || HasAttribute(method, HttpPatch)
                || HasAttribute(method, HttpDelete);
        }

        internal bool HasAntiforgery(ISymbol symbol)
        {
            return HasAttribute(symbol, Validate) || HasAttribute(symbol, AutoValidate);
        }

        internal bool HasExplicitOptOut(ISymbol symbol)
        {
            return HasAttribute(symbol, Ignore);
        }

        internal bool IsApiController(INamedTypeSymbol type)
        {
            return HasAttribute(type, ApiController);
        }

        private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeType)
        {
            if (attributeType is null)
            {
                return false;
            }

            foreach (var attribute in symbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
