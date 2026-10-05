using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128103: Reports a service type resolved through <c lang="csharp">GetRequiredService&lt;T&gt;()</c>
///     or a constructor parameter of a DI-registered type when no <c lang="csharp">Add*</c> or
///     <c lang="csharp">TryAdd*</c> registration for that type exists in the same compilation. The container
///     throws <c lang="csharp">InvalidOperationException</c> at runtime for such a resolve.
/// </summary>
/// <remarks>
///     The option <c lang="csharp">e128_registered_services</c> holds comma-separated simple type names. A name in
///     the option counts as registered, so a service whose <c lang="csharp">Add*</c> registration lives in another
///     assembly does not report. Framework types that only a non-generic extension registers, such as
///     <c lang="csharp">HttpClient</c> and <c lang="csharp">HybridCache</c>, need no option entry.
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
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A service type resolved through GetRequiredService<T>() or a constructor parameter of a " +
        "DI-registered type must have an Add* or TryAdd* registration in the same compilation. Without " +
        "one the DI container throws InvalidOperationException at runtime.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

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
        var registeredServices = new ConcurrentBag<INamedTypeSymbol>();
        var registeredOpenGenerics = new ConcurrentBag<string>();
        var resolveCandidates = new ConcurrentBag<(Location Location, INamedTypeSymbol ServiceType)>();
        var parameterCandidates = new ConcurrentBag<(Location Location, INamedTypeSymbol ParameterType, INamedTypeSymbol ContainingType)>();
        var configuredServices = ReadConfiguredServices(context.Options.AnalyzerConfigOptionsProvider);

        context.RegisterSyntaxNodeAction(
            ctx => CollectRegistrations(ctx, registeredServices, registeredOpenGenerics),
            SyntaxKind.InvocationExpression);

        context.RegisterSyntaxNodeAction(
            ctx => CollectResolves(ctx, resolveCandidates),
            SyntaxKind.InvocationExpression);

        context.RegisterSyntaxNodeAction(
            ctx => CollectConstructorParameters(ctx, parameterCandidates),
            SyntaxKind.ConstructorDeclaration);

        context.RegisterCompilationEndAction(ctx => ReportUnregisteredResolves(
            ctx,
            registeredServices,
            registeredOpenGenerics,
            resolveCandidates,
            parameterCandidates,
            configuredServices));
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
            is IMethodSymbol { TypeArguments.Length: 1 } method
            && method.TypeArguments[0] is INamedTypeSymbol { Name.Length: > 0 } serviceType)
        {
            resolveCandidates.Add((invocation.GetLocation(), serviceType));
        }
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

    private static void ReportUnregisteredResolves(
        CompilationAnalysisContext context,
        ConcurrentBag<INamedTypeSymbol> registeredServices,
        ConcurrentBag<string> registeredOpenGenerics,
        ConcurrentBag<(Location Location, INamedTypeSymbol ServiceType)> resolveCandidates,
        ConcurrentBag<(Location Location, INamedTypeSymbol ParameterType, INamedTypeSymbol ContainingType)> parameterCandidates,
        ImmutableHashSet<string> configuredServices)
    {
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
            .Select(entry => entry.Trim())
            .Where(entry => entry.Length > 0);

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
}
