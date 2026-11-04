using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128154: Reports a singleton registration whose implementation type takes a constructor
///     parameter of a type that an <c lang="csharp">AddScoped</c> call registers in the same
///     compilation. The container builds the singleton once, so it captures the first scoped
///     instance for the lifetime of the application.
/// </summary>
/// <remarks>
///     The rule reads direct dependencies only. A singleton that depends on another singleton that
///     depends on a scoped service is not reported, because the chain crosses a type the rule does
///     not model.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CaptiveDependencyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128154";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Singleton service depends on a scoped service",
        "'{0}' is registered as a singleton and depends on the scoped service '{1}'",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A singleton is built once and keeps every dependency for the lifetime of the application. " +
        "A scoped dependency captured this way is never disposed at the end of its scope and is shared " +
        "across requests. Use a scope factory, or register the dependent type as scoped.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly ImmutableHashSet<string> ScopeAccessorNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "IServiceProvider",
        "IServiceScopeFactory",
        "IServiceScope");

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
        var scopedServices = new ConcurrentBag<INamedTypeSymbol>();
        var singletonCandidates = new ConcurrentBag<SingletonCandidate>();

        context.RegisterSyntaxNodeAction(
            ctx => CollectRegistrations(ctx, scopedServices, singletonCandidates),
            SyntaxKind.InvocationExpression);

        context.RegisterCompilationEndAction(ctx =>
            ReportCaptiveDependencies(ctx, scopedServices, singletonCandidates));
    }

    private static void CollectRegistrations(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<INamedTypeSymbol> scopedServices,
        ConcurrentBag<SingletonCandidate> singletonCandidates)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        var isScoped = string.Equals(methodName, "AddScoped", StringComparison.Ordinal);
        var isSingleton = string.Equals(methodName, "AddSingleton", StringComparison.Ordinal);

        if (!isScoped && !isSingleton)
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol method)
        {
            return;
        }

        if (isScoped)
        {
            AddTypeArguments(method, scopedServices);
            return;
        }

        // Any argument makes the registration an instance or a factory call. The container then picks
        // the constructor at run time, or receives a built instance, so the type arguments alone do
        // not name the object the container holds.
        if (invocation.ArgumentList.Arguments.Count > 0)
        {
            return;
        }

        var implementation = GetSingletonImplementation(method);

        if (implementation is not null)
        {
            singletonCandidates.Add(new SingletonCandidate(
                GetRegistrationLocation(invocation, method),
                implementation));
        }
    }

    private static void AddTypeArguments(IMethodSymbol method, ConcurrentBag<INamedTypeSymbol> target)
    {
        // Both type arguments join the set. A caller can depend on the service interface or on the
        // implementation class, and either dependency captures the same scoped instance.
        foreach (var typeArgument in method.TypeArguments)
        {
            if (typeArgument is INamedTypeSymbol { Name.Length: > 0 } namedType)
            {
                target.Add(namedType);
            }
        }
    }

    private static INamedTypeSymbol? GetSingletonImplementation(IMethodSymbol method)
    {
        // AddSingleton<TService, TImplementation>() names both types. AddSingleton<T>() names the
        // implementation only when T is a concrete class, because an interface has no constructor.
        return method.TypeArguments.Length switch
        {
            2 => method.TypeArguments[1] as INamedTypeSymbol,
            1 when method.TypeArguments[0] is INamedTypeSymbol { TypeKind: TypeKind.Class } service
                   && !service.IsAbstract => service,
            _ => null
        };
    }

    private static Location GetRegistrationLocation(InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        // The report belongs on the registered type, not on the whole call. The type argument list is
        // absent for an expression-bodied generic call, so the invocation location is the fallback.
        var genericName = (invocation.Expression as MemberAccessExpressionSyntax)?.Name as GenericNameSyntax;

        // The implementation type is the last type argument. AddSingleton<T>() carries one, and
        // AddSingleton<TService, TImplementation>() carries two.
        var registeredType = genericName is { TypeArgumentList.Arguments.Count: > 0 }
            ? genericName.TypeArgumentList.Arguments.Last()
            : null;

        return registeredType?.GetLocation()
               ?? (method.Locations.Length > 0 ? method.Locations[0] : invocation.GetLocation());
    }

    private static void ReportCaptiveDependencies(
        CompilationAnalysisContext context,
        ConcurrentBag<INamedTypeSymbol> scopedServices,
        ConcurrentBag<SingletonCandidate> singletonCandidates)
    {
        var scoped = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in scopedServices)
        {
            scoped.Add(type);
        }

        if (scoped.Count == 0)
        {
            return;
        }

        foreach (var candidate in singletonCandidates)
        {
            var scopedDependency = FindScopedDependency(candidate.Implementation, scoped);

            if (scopedDependency is not null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    candidate.Location,
                    candidate.Implementation.Name,
                    scopedDependency));
            }
        }
    }

    private static string? FindScopedDependency(INamedTypeSymbol implementation, HashSet<INamedTypeSymbol> scoped)
    {
        foreach (var constructor in implementation.InstanceConstructors)
        {
            foreach (var parameter in constructor.Parameters)
            {
                if (parameter.Type is INamedTypeSymbol parameterType
                    && !ScopeAccessorNames.Contains(parameterType.Name)
                    && scoped.Contains(parameterType))
                {
                    return parameterType.Name;
                }
            }
        }

        return null;
    }

    private sealed class SingletonCandidate
    {
        internal SingletonCandidate(Location location, INamedTypeSymbol implementation)
        {
            Location = location;
            Implementation = implementation;
        }

        internal Location Location { get; }

        internal INamedTypeSymbol Implementation { get; }
    }
}
