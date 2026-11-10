using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128103: Reports a service type resolved through <c lang="csharp">GetRequiredService&lt;T&gt;()</c>
///     or a constructor parameter of a DI-registered type when no <c lang="csharp">Add*</c> or
///     <c lang="csharp">TryAdd*</c> registration for that type exists in the same compilation. The container
///     throws <c lang="csharp">InvalidOperationException</c> at runtime for such a resolve. Also reports a
///     named <c lang="csharp">HttpClient</c> resolved through <c lang="csharp">CreateClient("name")</c> when no
///     <c lang="csharp">AddHttpClient("name", ...)</c> call registers that name.
/// </summary>
/// <remarks>
///     The option <c lang="csharp">e128_registered_services</c> holds comma-separated simple type names. A name in
///     the option counts as registered, so a service whose <c lang="csharp">Add*</c> registration lives in another
///     assembly does not report. Framework types that only a non-generic extension registers, such as
///     <c lang="csharp">HttpClient</c> and <c lang="csharp">HybridCache</c>, need no option entry. A named client
///     registration is matched on the <c lang="csharp">AddHttpClient</c> extension type, so an unrelated method
///     with the same name never counts. The empty name and the unnamed overload count as the default client.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnregisteredServiceResolveAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128103";

    private const string RegisteredServicesOptionKey = "e128_registered_services";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "DI resolve without a registration in the compilation",
        "'{0}' is resolved but has no DI registration in this compilation",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A service type resolved through GetRequiredService<T>() or a constructor parameter of a " +
        "DI-registered type must have an Add* or TryAdd* registration in the same compilation. Without " +
        "one the DI container throws InvalidOperationException at runtime.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly DiagnosticDescriptor ClientNameRule = new(
        DiagnosticId,
        "Named HttpClient resolve without a registration in the compilation",
        "'{0}' is resolved as a named HttpClient but has no AddHttpClient registration in this compilation",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A named HttpClient resolved through IHttpClientFactory.CreateClient(name) must have a matching " +
        "AddHttpClient(name, ...) registration in the same compilation. Without one the client carries no " +
        "registered configuration and the name is likely a typo.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private const string HttpClientFactoryTypeName = "System.Net.Http.IHttpClientFactory";

    private const string HttpClientExtensionsTypeName =
        "Microsoft.Extensions.DependencyInjection.HttpClientFactoryServiceCollectionExtensions";

    // GetService<T>() is deliberately absent. It returns null on a miss rather than throwing, and a
    // null probe is a legitimate way to ask whether a service exists, so the rule would be wrong on
    // both the failure mode and the call site.
    private static readonly ImmutableHashSet<string> ResolveMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "GetRequiredService");

    // HttpClient, HybridCache, IServer, and TracerProvider arrive through non-generic extensions such as
    // AddHttpClient("name") and AddHybridCache(). Those extensions carry no type argument, so the registration
    // collector cannot observe them and the names must be listed here.
    private static readonly ImmutableHashSet<string> FrameworkProvidedServices = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ILogger",
        "ILoggerFactory",
        "IOptions",
        "IOptionsSnapshot",
        "IOptionsMonitor",
        "IHttpClientFactory",
        "HttpClient",
        "HybridCache",
        "IServer",
        "TracerProvider",
        "IChatCompletionService",
        "ITextEmbeddingService",
        "IImageEmbeddingService",
        "IServiceProvider",
        "IConfiguration",
        "IHostEnvironment",
        "IEnumerable");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule, ClientNameRule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        // The collectors live in this closure, so each compilation gets its own state and the
        // analyzer host can never leak registrations from one compilation into the next.
        var state = new ResolveState(ReadConfiguredServices(context.Options.AnalyzerConfigOptionsProvider));

        // Both types ship in Microsoft.Extensions.Http. Without either one the named-client check cannot
        // tell a registration from a resolve, so it stays silent rather than guess.
        var httpClientFactoryType = context.Compilation.GetTypeByMetadataName(HttpClientFactoryTypeName);
        var httpClientExtensionsType = context.Compilation.GetTypeByMetadataName(HttpClientExtensionsTypeName);

        context.RegisterSyntaxNodeAction(
            ctx => CollectRegistrations(ctx, state.RegisteredServices, state.RegisteredOpenGenerics),
            SyntaxKind.InvocationExpression);

        context.RegisterSyntaxNodeAction(
            ctx => CollectResolves(ctx, state.ResolveCandidates),
            SyntaxKind.InvocationExpression);

        context.RegisterSyntaxNodeAction(
            ctx => CollectConstructorParameters(ctx, state.ParameterCandidates),
            SyntaxKind.ConstructorDeclaration);

        if (httpClientFactoryType is not null && httpClientExtensionsType is not null)
        {
            context.RegisterSyntaxNodeAction(
                ctx => CollectClientRegistrations(ctx, httpClientExtensionsType, state.RegisteredClientNames),
                SyntaxKind.InvocationExpression);

            context.RegisterSyntaxNodeAction(
                ctx => CollectClientResolves(ctx, httpClientFactoryType, state.ClientResolveCandidates),
                SyntaxKind.InvocationExpression);
        }

        context.RegisterCompilationEndAction(ctx => ReportUnregisteredResolves(ctx, state));
    }

    private static void CollectRegistrations(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<INamedTypeSymbol> registeredServices,
        ConcurrentBag<string> registeredOpenGenerics)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var methodName = GetInvokedMethodName(invocation);

        if (methodName is null || !IsRegistrationMethod(methodName))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method)
        {
            return;
        }

        // Two types that share a short name across namespaces are distinct services, so the
        // registration set is keyed on the symbol rather than on the name.
        foreach (var typeArgument in method.TypeArguments)
        {
            if (typeArgument is INamedTypeSymbol { Name.Length: > 0 } registeredType)
            {
                registeredServices.Add(registeredType);
            }
        }

        // A registration overload taking Type arguments carries open generics, as in
        // AddSingleton(typeof(IRepo<>), typeof(Repo<>)). An unbound generic cannot equal a
        // constructed resolve such as IRepo<Thing>, so this path stays name-keyed.
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is TypeOfExpressionSyntax typeOf
                && context.SemanticModel.GetTypeInfo(typeOf.Type, context.CancellationToken).Type
                    is INamedTypeSymbol registeredOpenGeneric)
            {
                registeredOpenGenerics.Add(registeredOpenGeneric.Name);
            }
        }
    }

    private static void CollectResolves(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<(Location Location, INamedTypeSymbol ServiceType)> resolveCandidates)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var methodName = GetInvokedMethodName(invocation);

        if (methodName is null || !ResolveMethodNames.Contains(methodName))
        {
            return;
        }

        if (IsInsideRegistrationFactory(invocation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { TypeArguments.Length: 1 } method
            || method.TypeArguments[0] is not INamedTypeSymbol { Name.Length: > 0 } serviceType)
        {
            return;
        }

        resolveCandidates.Add((invocation.GetLocation(), serviceType));
    }

    private static void CollectConstructorParameters(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<(Location Location, INamedTypeSymbol ParameterType, INamedTypeSymbol ContainingType)> parameterCandidates)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;

        if (constructor.ParameterList is null
            || constructor.Parent is not TypeDeclarationSyntax containingType)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(containingType, context.CancellationToken)
            is not INamedTypeSymbol containingTypeSymbol)
        {
            return;
        }

        foreach (var parameter in constructor.ParameterList.Parameters)
        {
            if (parameter.Type is null)
            {
                continue;
            }

            if (context.SemanticModel.GetTypeInfo(parameter.Type, context.CancellationToken).Type
                    is INamedTypeSymbol { SpecialType: SpecialType.None } parameterType
                && !parameterType.IsValueType)
            {
                parameterCandidates.Add((
                    parameter.GetLocation(),
                    parameterType,
                    containingTypeSymbol));
            }
        }
    }

    private static void CollectClientRegistrations(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol extensionsType,
        ConcurrentBag<string> registeredClientNames)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (GetInvokedMethodName(invocation) is not "AddHttpClient")
        {
            return;
        }

        // The name AddHttpClient belongs to several unrelated types. Matching the declaring type keeps a
        // local helper with the same name from counting as a registration.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol method
            || !IsDeclaredOn(method, extensionsType))
        {
            return;
        }

        if (!TryGetStringArgument(invocation, method, context.SemanticModel, context.CancellationToken, out var name)
            || name.Length is 0)
        {
            return;
        }

        registeredClientNames.Add(name);
    }

    private static void CollectClientResolves(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol factoryType,
        ConcurrentBag<(Location Location, string Name)> clientResolveCandidates)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (GetInvokedMethodName(invocation) is not "CreateClient")
        {
            return;
        }

        // The parameterless CreateClient is an extension method on its own type, so it never matches the
        // interface member and stays silent. It resolves the default client, which needs no registration.
        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is not IMethodSymbol method
            || !IsDeclaredOn(method, factoryType))
        {
            return;
        }

        // The empty name is the default client, which AddHttpClient() always supplies.
        if (!TryGetStringArgument(invocation, method, context.SemanticModel, context.CancellationToken, out var name)
            || name.Length is 0)
        {
            return;
        }

        clientResolveCandidates.Add((invocation.GetLocation(), name));
    }

    private static bool IsDeclaredOn(IMethodSymbol method, INamedTypeSymbol type)
    {
        return SymbolEqualityComparer.Default.Equals(method.ContainingType, type)
               || (method.ReducedFrom is { } reduced
                   && SymbolEqualityComparer.Default.Equals(reduced.ContainingType, type));
    }

    private static bool TryGetStringArgument(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string name)
    {
        name = string.Empty;

        // The named-client APIs carry exactly one string parameter, the client name. Reading the constant of
        // the matching argument keeps the check independent of parameter order and reduced receiver removal.
        if (!method.Parameters.Any(static parameter => parameter.Type.SpecialType is SpecialType.System_String))
        {
            return false;
        }

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var constant = semanticModel.GetConstantValue(argument.Expression, cancellationToken);

            if (constant.HasValue && constant.Value is string value)
            {
                name = value;
                return true;
            }
        }

        return false;
    }

    private static void ReportUnregisteredResolves(CompilationAnalysisContext context, ResolveState state)
    {
        var registeredServices = state.RegisteredServices;
        var registeredOpenGenerics = state.RegisteredOpenGenerics;
        var resolveCandidates = state.ResolveCandidates;
        var parameterCandidates = state.ParameterCandidates;
        var configuredServices = state.ConfiguredServices;
        var registeredClientNames = state.RegisteredClientNames;
        var clientResolveCandidates = state.ClientResolveCandidates;
        var registered = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in registeredServices)
        {
            registered.Add(type);
        }

        var openGenerics = ImmutableHashSet.CreateRange(StringComparer.Ordinal, registeredOpenGenerics);

        foreach (var (location, serviceType) in resolveCandidates)
        {
            if (IsUnregistered(serviceType, registered, openGenerics, configuredServices))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, serviceType.Name));
            }
        }

        foreach (var (location, parameterType, containingType) in parameterCandidates)
        {
            if (registered.Contains(containingType)
                && IsUnregistered(parameterType, registered, openGenerics, configuredServices))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, parameterType.Name));
            }
        }

        var registeredClientNameSet = ImmutableHashSet.CreateRange(StringComparer.Ordinal, registeredClientNames);

        // One diagnostic per name, reported at the first use. A bag enumerates in no fixed order, so the
        // location is picked by file and span to keep repeated runs identical.
        var unregisteredClientNames = clientResolveCandidates
            .Where(candidate => !registeredClientNameSet.Contains(candidate.Name))
            .GroupBy(static candidate => candidate.Name, StringComparer.Ordinal)
            .Select(static group => group
                .OrderBy(static candidate => candidate.Location.GetLineSpan().Path, StringComparer.Ordinal)
                .ThenBy(static candidate => candidate.Location.SourceSpan.Start)
                .First())
            .OrderBy(static candidate => candidate.Location.GetLineSpan().Path, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Location.SourceSpan.Start);

        foreach (var (location, name) in unregisteredClientNames)
        {
            context.ReportDiagnostic(Diagnostic.Create(ClientNameRule, location, name));
        }
    }

    private static bool IsUnregistered(
        INamedTypeSymbol serviceType,
        HashSet<INamedTypeSymbol> registered,
        ImmutableHashSet<string> registeredOpenGenerics,
        ImmutableHashSet<string> configuredServices)
    {
        // The registration set is symbol-keyed. The open-generic, configured, and framework checks
        // stay name-based: an unbound generic has no constructed symbol, the option arrives as text,
        // and the allowlist holds plain names.
        return !registered.Contains(serviceType)
               && !registeredOpenGenerics.Contains(serviceType.Name)
               && !configuredServices.Contains(serviceType.Name)
               && !FrameworkProvidedServices.Contains(serviceType.Name);
    }

    private static ImmutableHashSet<string> ReadConfiguredServices(AnalyzerConfigOptionsProvider provider)
    {
        var options = provider.GlobalOptions;

        if (!options.TryGetValue(RegisteredServicesOptionKey, out var rawValue)
            || string.IsNullOrWhiteSpace(rawValue))
        {
            return [];
        }

        var entries = rawValue
            .Split(',')
            .Select(static entry => entry.Trim())
            .Where(static entry => entry.Length > 0);

        return ImmutableHashSet.CreateRange(StringComparer.Ordinal, entries);
    }

    private static bool IsInsideRegistrationFactory(SyntaxNode node)
    {
        var enclosing = node.Ancestors().OfType<AnonymousFunctionExpressionSyntax>().FirstOrDefault();

        return enclosing is not null
               && enclosing.Parent is ArgumentSyntax { Parent.Parent: InvocationExpressionSyntax invocation }
               && GetInvokedMethodName(invocation) is { } methodName
               && IsRegistrationMethod(methodName);
    }

    private static string? GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };
    }

    private static bool IsRegistrationMethod(string methodName)
    {
        return methodName.StartsWith("Add", StringComparison.Ordinal)
               || methodName.StartsWith("TryAdd", StringComparison.Ordinal);
    }

    private sealed class ResolveState
    {
        internal ResolveState(ImmutableHashSet<string> configuredServices)
        {
            ConfiguredServices = configuredServices;
        }

        internal ConcurrentBag<INamedTypeSymbol> RegisteredServices { get; } = [];

        internal ConcurrentBag<string> RegisteredOpenGenerics { get; } = [];

        internal ConcurrentBag<(Location Location, INamedTypeSymbol ServiceType)> ResolveCandidates { get; } = [];

        internal ConcurrentBag<(Location Location, INamedTypeSymbol ParameterType, INamedTypeSymbol ContainingType)> ParameterCandidates { get; } = [];

        internal ConcurrentBag<string> RegisteredClientNames { get; } = [];

        internal ConcurrentBag<(Location Location, string Name)> ClientResolveCandidates { get; } = [];

        internal ImmutableHashSet<string> ConfiguredServices { get; }
    }
}
