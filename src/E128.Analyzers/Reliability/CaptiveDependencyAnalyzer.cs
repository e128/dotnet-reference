using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

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
        "E128-Reliability",
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

        var lifetime = GetLifetime(memberAccess.Name.Identifier.ValueText);

        if (lifetime == Lifetime.None)
        {
            return;
        }

        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken)
            is not IInvocationOperation operation)
        {
            return;
        }

        var method = operation.TargetMethod;
        var typeOfRegistrations = ReadTypeOfArguments(operation, out var hasFactoryOrInstance);

        if (lifetime == Lifetime.Scoped)
        {
            CollectScopedServices(method, typeOfRegistrations, scopedServices);

            return;
        }

        // A factory or an instance argument means the container receives a built object, so the type
        // arguments alone do not name the object the container holds. A service key is not such an
        // argument, so a keyed registration stays in scope.
        if (hasFactoryOrInstance)
        {
            return;
        }

        CollectSingletonCandidate(invocation, method, typeOfRegistrations, singletonCandidates);
    }

    private static void CollectScopedServices(
        IMethodSymbol method,
        List<TypeOfRegistration> typeOfRegistrations,
        ConcurrentBag<INamedTypeSymbol> scopedServices)
    {
        AddTypeArguments(method, scopedServices);

        foreach (var registration in typeOfRegistrations)
        {
            if (registration.Type is INamedTypeSymbol namedType)
            {
                scopedServices.Add(namedType);
            }
        }
    }

    private static void CollectSingletonCandidate(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        List<TypeOfRegistration> typeOfRegistrations,
        ConcurrentBag<SingletonCandidate> singletonCandidates)
    {
        if (typeOfRegistrations.Count > 0)
        {
            var typeOfImplementation = GetTypeOfImplementation(typeOfRegistrations);

            if (typeOfImplementation is not null)
            {
                singletonCandidates.Add(new SingletonCandidate(
                    typeOfImplementation.Location,
                    (INamedTypeSymbol)typeOfImplementation.Type));
            }

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

    private static Lifetime GetLifetime(string methodName)
    {
        // The rule covers Add, TryAdd, AddKeyed, and TryAddKeyed. AddHostedService registers a singleton.
        if (string.Equals(methodName, "AddHostedService", StringComparison.Ordinal))
        {
            return Lifetime.Singleton;
        }

        var name = methodName.StartsWith("Try", StringComparison.Ordinal) ? methodName.Substring(3) : methodName;
        name = name.StartsWith("AddKeyed", StringComparison.Ordinal)
            ? name.Substring("AddKeyed".Length)
            : name.StartsWith("Add", StringComparison.Ordinal)
                ? name.Substring("Add".Length)
                : string.Empty;

        return name switch
        {
            "Scoped" => Lifetime.Scoped,
            "Singleton" => Lifetime.Singleton,
            _ => Lifetime.None
        };
    }

    private static List<TypeOfRegistration> ReadTypeOfArguments(IInvocationOperation operation, out bool hasFactoryOrInstance)
    {
        var registrations = new List<TypeOfRegistration>();
        hasFactoryOrInstance = false;

        foreach (var argument in operation.Arguments)
        {
            var parameter = argument.Parameter;

            if (parameter is null
                || argument.ArgumentKind == ArgumentKind.DefaultValue
                || (operation.TargetMethod.IsExtensionMethod && parameter.Ordinal == 0)
                || string.Equals(parameter.Name, "serviceKey", StringComparison.Ordinal))
            {
                continue;
            }

            var value = argument.Value;

            while (value is IConversionOperation conversion)
            {
                value = conversion.Operand;
            }

            if (value is ITypeOfOperation typeOf)
            {
                registrations.Add(new TypeOfRegistration(parameter.Name, typeOf.TypeOperand, typeOf.Syntax.GetLocation()));
            }
            else
            {
                hasFactoryOrInstance = true;
            }
        }

        return registrations;
    }

    private static TypeOfRegistration? GetTypeOfImplementation(List<TypeOfRegistration> registrations)
    {
        // The implementationType argument names the object. A lone serviceType argument names it only
        // when the type is a concrete class, because an interface has no constructor.
        TypeOfRegistration? service = null;

        foreach (var registration in registrations)
        {
            if (string.Equals(registration.ParameterName, "implementationType", StringComparison.Ordinal))
            {
                return registration.Type is INamedTypeSymbol ? registration : null;
            }

            service = registration;
        }

        return service is { Type: INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false } } ? service : null;
    }

    private enum Lifetime
    {
        None = 0,
        Scoped = 1,
        Singleton = 2
    }

    private sealed class TypeOfRegistration
    {
        internal TypeOfRegistration(string parameterName, ITypeSymbol type, Location location)
        {
            ParameterName = parameterName;
            Type = type;
            Location = location;
        }

        internal string ParameterName { get; }

        internal ITypeSymbol Type { get; }

        internal Location Location { get; }
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
