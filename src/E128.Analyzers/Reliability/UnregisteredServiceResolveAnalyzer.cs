using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128103: Reports a service type resolved through <c lang="csharp">GetRequiredService&lt;T&gt;()</c>,
///     <c lang="csharp">GetService&lt;T&gt;()</c>, or a constructor parameter of a DI-registered type when no
///     <c lang="csharp">Add*</c> or <c lang="csharp">TryAdd*</c> registration for that type exists in the same
///     compilation. The container throws <c lang="csharp">InvalidOperationException</c> at runtime for such a resolve.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnregisteredServiceResolveAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128103";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "DI resolve without a registration in the compilation",
        "'{0}' is resolved but has no DI registration in this compilation",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A service type resolved through GetRequiredService<T>(), GetService<T>(), or a constructor " +
        "parameter of a DI-registered type must have an Add* or TryAdd* registration in the same " +
        "compilation. Without one the DI container throws InvalidOperationException at runtime.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly ImmutableHashSet<string> ResolveMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "GetRequiredService",
        "GetService");

    private static readonly ImmutableHashSet<string> FrameworkProvidedServices = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "ILogger",
        "IOptions",
        "IOptionsSnapshot",
        "IOptionsMonitor",
        "IHttpClientFactory",
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
        var registeredServices = new ConcurrentBag<string>();
        var resolveCandidates = new ConcurrentBag<(Location Location, string TypeName)>();
        var parameterCandidates = new ConcurrentBag<(Location Location, string TypeName, string ContainingTypeName)>();

        context.RegisterSyntaxNodeAction(
            ctx => CollectRegistrations(ctx, registeredServices),
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
            resolveCandidates,
            parameterCandidates));
    }

    private static void CollectRegistrations(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<string> registeredServices)
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

        foreach (var typeArgument in method.TypeArguments)
        {
            if (typeArgument.Name.Length > 0)
            {
                registeredServices.Add(typeArgument.Name);
            }
        }

        // A registration overload taking Type arguments carries open generics, as in
        // AddSingleton(typeof(IRepo<>), typeof(Repo<>)). The unbound name is the service name.
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is TypeOfExpressionSyntax typeOf
                && context.SemanticModel.GetTypeInfo(typeOf.Type, context.CancellationToken).Type
                    is INamedTypeSymbol registeredType)
            {
                registeredServices.Add(registeredType.Name);
            }
        }
    }

    private static void CollectResolves(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<(Location Location, string TypeName)> resolveCandidates)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var methodName = GetInvokedMethodName(invocation);

        if (methodName is null || !ResolveMethodNames.Contains(methodName))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is IMethodSymbol { TypeArguments.Length: 1 } method
            && method.TypeArguments[0].Name.Length > 0)
        {
            resolveCandidates.Add((invocation.GetLocation(), method.TypeArguments[0].Name));
        }
    }

    private static void CollectConstructorParameters(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<(Location Location, string TypeName, string ContainingTypeName)> parameterCandidates)
    {
        var constructor = (ConstructorDeclarationSyntax)context.Node;

        if (constructor.ParameterList is null
            || constructor.Parent is not TypeDeclarationSyntax containingType)
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
                    parameterType.Name,
                    containingType.Identifier.Text));
            }
        }
    }

    private static void ReportUnregisteredResolves(
        CompilationAnalysisContext context,
        ConcurrentBag<string> registeredServices,
        ConcurrentBag<(Location Location, string TypeName)> resolveCandidates,
        ConcurrentBag<(Location Location, string TypeName, string ContainingTypeName)> parameterCandidates)
    {
        var registered = ImmutableHashSet.CreateRange(StringComparer.Ordinal, registeredServices);

        foreach (var (location, typeName) in resolveCandidates)
        {
            if (IsUnregistered(typeName, registered))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, typeName));
            }
        }

        foreach (var (location, typeName, containingTypeName) in parameterCandidates)
        {
            if (registered.Contains(containingTypeName) && IsUnregistered(typeName, registered))
            {
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, typeName));
            }
        }
    }

    private static bool IsUnregistered(string typeName, ImmutableHashSet<string> registered)
    {
        return !registered.Contains(typeName) && !FrameworkProvidedServices.Contains(typeName);
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
