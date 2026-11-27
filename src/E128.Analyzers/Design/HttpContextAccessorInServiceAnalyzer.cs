using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128262: A service injects <c lang="csharp">IHttpContextAccessor</c>. The accessor is for
///     middleware, filters, Blazor components, SignalR hubs, and hosted services. A plain service
///     hides a dependency on the ambient request, so pass the value instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HttpContextAccessorInServiceAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128262";

    private const string AccessorMetadataName = "Microsoft.AspNetCore.Http.IHttpContextAccessor";

    private const string HttpContextMetadataName = "Microsoft.AspNetCore.Http.HttpContext";

    private const string ComponentBaseMetadataName = "Microsoft.AspNetCore.Components.ComponentBase";

    private const string HubMetadataName = "Microsoft.AspNetCore.SignalR.Hub";

    private const string AuthorizationHandlerMetadataName =
        "Microsoft.AspNetCore.Authorization.AuthorizationHandler`1";

    private const string DelegatingHandlerMetadataName = "System.Net.Http.DelegatingHandler";

    private const string InvokeName = "Invoke";

    private const string InvokeAsyncName = "InvokeAsync";

    private static readonly string[] AllowedInterfaceNames =
    [
        "Microsoft.AspNetCore.Http.IMiddleware",
        "Microsoft.AspNetCore.Http.IEndpointFilter",
        "Microsoft.Extensions.Hosting.IHostedService",
        "Microsoft.AspNetCore.Mvc.Filters.IActionFilter",
        "Microsoft.AspNetCore.Mvc.Filters.IAsyncActionFilter",
        "Microsoft.AspNetCore.Mvc.Filters.IResultFilter",
        "Microsoft.AspNetCore.Mvc.Filters.IAsyncResultFilter",
        "Microsoft.AspNetCore.Mvc.Filters.IExceptionFilter",
        "Microsoft.AspNetCore.Mvc.Filters.IAsyncExceptionFilter",
        "Microsoft.AspNetCore.Mvc.Filters.IAuthorizationFilter",
        "Microsoft.AspNetCore.Mvc.Filters.IAsyncAuthorizationFilter",
        "Microsoft.AspNetCore.Mvc.Filters.IResourceFilter",
        "Microsoft.AspNetCore.Mvc.Filters.IAsyncResourceFilter",
    ];

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "IHttpContextAccessor injected into a service",
        "'{0}' injects IHttpContextAccessor: pass the request value, or move the access into middleware or a filter",
        "E128-Design",
        DiagnosticSeverity.Info,
        true,
        "IHttpContextAccessor hides a dependency on the ambient request and returns null outside a request. Inject it into middleware, a filter, a Blazor component, a SignalR hub, or a hosted service. A plain service should receive the value it needs.");

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
            var accessor = start.Compilation.GetTypeByMetadataName(AccessorMetadataName);

            if (accessor is null)
            {
                return;
            }

            var types = new RuleTypes(
                accessor,
                start.Compilation.GetTypeByMetadataName(HttpContextMetadataName),
                start.Compilation.GetTypeByMetadataName(ComponentBaseMetadataName),
                start.Compilation.GetTypeByMetadataName(HubMetadataName),
                start.Compilation.GetTypeByMetadataName(AuthorizationHandlerMetadataName),
                start.Compilation.GetTypeByMetadataName(DelegatingHandlerMetadataName),
                ResolveAllowed(start.Compilation));

            start.RegisterSyntaxNodeAction(
                node => AnalyzeConstructor(node, types),
                SyntaxKind.ConstructorDeclaration);

            start.RegisterSyntaxNodeAction(
                node => AnalyzeType(node, types),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration);
        });
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context, RuleTypes types)
    {
        if (context.Node is not ConstructorDeclarationSyntax constructor
            || context.SemanticModel.GetDeclaredSymbol(constructor, context.CancellationToken) is not IMethodSymbol method
            || method.ContainingType is not { } containingType
            || IsAllowed(containingType, types))
        {
            return;
        }

        foreach (var parameter in constructor.ParameterList.Parameters)
        {
            AnalyzeParameter(context, parameter, containingType, types.Accessor);
        }
    }

    private static void AnalyzeType(SyntaxNodeAnalysisContext context, RuleTypes types)
    {
        if (context.Node is not TypeDeclarationSyntax declaration
            || declaration.ParameterList is not { } parameterList
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not INamedTypeSymbol containingType
            || IsAllowed(containingType, types))
        {
            return;
        }

        foreach (var parameter in parameterList.Parameters)
        {
            AnalyzeParameter(context, parameter, containingType, types.Accessor);
        }
    }

    private static void AnalyzeParameter(
        SyntaxNodeAnalysisContext context,
        ParameterSyntax parameter,
        INamedTypeSymbol containingType,
        INamedTypeSymbol accessor)
    {
        if (parameter.Type is null
            || !SymbolEqualityComparer.Default.Equals(
                context.SemanticModel.GetTypeInfo(parameter.Type, context.CancellationToken).Type,
                accessor))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            parameter.Identifier.GetLocation(),
            containingType.Name));
    }

    private static bool IsAllowed(INamedTypeSymbol type, RuleTypes types)
    {
        return IsKnownBase(type, types.ComponentBase)
               || IsKnownBase(type, types.Hub)
               || IsKnownBase(type, types.DelegatingHandler)
               || IsKnownDefinition(type, types.AuthorizationHandler)
               || ImplementsAllowed(type, types.Allowed)
               || HasInvoke(type, types.HttpContext);
    }

    private static bool IsKnownBase(INamedTypeSymbol type, INamedTypeSymbol? baseType)
    {
        return baseType is not null && TypeSymbols.DerivesFrom(type, baseType);
    }

    private static bool IsKnownDefinition(INamedTypeSymbol type, INamedTypeSymbol? definition)
    {
        if (definition is null)
        {
            return false;
        }

        if (SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, definition))
        {
            return true;
        }

        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, definition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ImplementsAllowed(
        INamedTypeSymbol type,
        ImmutableArray<INamedTypeSymbol> allowed)
    {
        foreach (var @interface in type.AllInterfaces)
        {
            foreach (var candidate in allowed)
            {
                if (SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasInvoke(INamedTypeSymbol type, INamedTypeSymbol? httpContext)
    {
        return HasHttpContextParameter(type, InvokeName, httpContext)
               || HasHttpContextParameter(type, InvokeAsyncName, httpContext);
    }

    private static bool HasHttpContextParameter(
        INamedTypeSymbol type,
        string name,
        INamedTypeSymbol? httpContext)
    {
        if (httpContext is null)
        {
            return false;
        }

        foreach (var member in type.GetMembers(name))
        {
            if (member is not IMethodSymbol method)
            {
                continue;
            }

            foreach (var parameter in method.Parameters)
            {
                if (SymbolEqualityComparer.Default.Equals(parameter.Type, httpContext))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ImmutableArray<INamedTypeSymbol> ResolveAllowed(Compilation compilation)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var name in AllowedInterfaceNames)
        {
            if (compilation.GetTypeByMetadataName(name) is { } type)
            {
                builder.Add(type);
            }
        }

        return builder.ToImmutable();
    }

    private sealed class RuleTypes
    {
        public RuleTypes(
            INamedTypeSymbol accessor,
            INamedTypeSymbol? httpContext,
            INamedTypeSymbol? componentBase,
            INamedTypeSymbol? hub,
            INamedTypeSymbol? authorizationHandler,
            INamedTypeSymbol? delegatingHandler,
            ImmutableArray<INamedTypeSymbol> allowed)
        {
            Accessor = accessor;
            HttpContext = httpContext;
            ComponentBase = componentBase;
            Hub = hub;
            AuthorizationHandler = authorizationHandler;
            DelegatingHandler = delegatingHandler;
            Allowed = allowed;
        }

        public INamedTypeSymbol Accessor { get; }

        public INamedTypeSymbol? HttpContext { get; }

        public INamedTypeSymbol? ComponentBase { get; }

        public INamedTypeSymbol? Hub { get; }

        public INamedTypeSymbol? AuthorizationHandler { get; }

        public INamedTypeSymbol? DelegatingHandler { get; }

        public ImmutableArray<INamedTypeSymbol> Allowed { get; }
    }
}
