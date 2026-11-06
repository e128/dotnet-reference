using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
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
///     instance for the lifetime of the application. The rule also reports an Entity Framework Core
///     <c lang="csharp">DbContext</c> registered as a singleton, because a context is scoped by
///     contract and a singleton context tracks every entity it materializes.
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

    private const string DbContextMetadataName = "Microsoft.EntityFrameworkCore.DbContext";

    private const string ServiceLifetimeMetadataName = "Microsoft.Extensions.DependencyInjection.ServiceLifetime";

    private const string ServiceCollectionMetadataName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    private const string EntityFrameworkExtensionsMetadataName = "Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions";

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

    private static readonly DiagnosticDescriptor DbContextRule = new(
        DiagnosticId,
        "DbContext registered as a singleton",
        "'{0}' is an Entity Framework Core DbContext registered as a singleton",
        "E128-Reliability",
        DiagnosticSeverity.Warning,
        true,
        "A DbContext is scoped by contract and tracks every entity it materializes. A singleton context " +
        "keeps those entities for the lifetime of the application, grows without bound, and is shared " +
        "across requests. Register the context as scoped, or resolve it from a created scope.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly ImmutableHashSet<string> ScopeAccessorNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "IServiceProvider",
        "IServiceScopeFactory",
        "IServiceScope");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule, DbContextRule];

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
        var efCore = EfCoreTypes.Resolve(context.Compilation);
        var scopedServices = new ConcurrentBag<INamedTypeSymbol>();
        var singletonCandidates = new ConcurrentBag<SingletonCandidate>();
        var dbContextCandidates = new ConcurrentBag<DbContextCandidate>();

        context.RegisterSyntaxNodeAction(
            ctx => CollectRegistrations(ctx, scopedServices, singletonCandidates, dbContextCandidates, efCore),
            SyntaxKind.InvocationExpression);

        context.RegisterCompilationEndAction(ctx =>
        {
            ReportCaptiveDependencies(ctx, scopedServices, singletonCandidates);
            ReportDbContextSingletons(ctx, dbContextCandidates);
        });
    }

    private static void CollectRegistrations(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<INamedTypeSymbol> scopedServices,
        ConcurrentBag<SingletonCandidate> singletonCandidates,
        ConcurrentBag<DbContextCandidate> dbContextCandidates,
        EfCoreTypes efCore)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.ValueText;
        var lifetime = GetLifetime(methodName);
        var isDbContextRegistration = efCore.IsDbContextRegistration(methodName);

        if (lifetime == Lifetime.None && !isDbContextRegistration)
        {
            return;
        }

        if (context.SemanticModel.GetOperation(invocation, context.CancellationToken)
            is not IInvocationOperation operation)
        {
            return;
        }

        if (isDbContextRegistration)
        {
            CollectDbContextRegistration(
                invocation,
                operation,
                dbContextCandidates,
                efCore,
                context.SemanticModel,
                context.CancellationToken);

            return;
        }

        var method = operation.TargetMethod;
        var typeOfRegistrations = ReadTypeOfArguments(operation, out var hasFactoryOrInstance);

        if (lifetime == Lifetime.Scoped)
        {
            CollectScopedServices(method, typeOfRegistrations, scopedServices);

            return;
        }

        CollectDbContextSingleton(invocation, method, typeOfRegistrations, dbContextCandidates, efCore);

        // A factory or an instance argument means the container receives a built object, so the type
        // arguments alone do not name the object the container holds. A service key is not such an
        // argument, so a keyed registration stays in scope.
        if (hasFactoryOrInstance)
        {
            return;
        }

        CollectSingletonCandidate(invocation, method, typeOfRegistrations, singletonCandidates);
    }

    private static void CollectDbContextSingleton(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        List<TypeOfRegistration> typeOfRegistrations,
        ConcurrentBag<DbContextCandidate> dbContextCandidates,
        EfCoreTypes efCore)
    {
        // The container builds one context for the application. The type arguments name that context
        // even when a factory or an instance supplies it, because the container stores the result of
        // the factory in the singleton slot.
        if (!efCore.IsAvailable)
        {
            return;
        }

        foreach (var typeArgument in method.TypeArguments)
        {
            if (typeArgument is INamedTypeSymbol namedType && efCore.DerivesFromDbContext(namedType))
            {
                dbContextCandidates.Add(new DbContextCandidate(
                    GetRegistrationLocation(invocation),
                    namedType));

                return;
            }
        }

        foreach (var registration in typeOfRegistrations)
        {
            if (registration.Type is INamedTypeSymbol namedType && efCore.DerivesFromDbContext(namedType))
            {
                dbContextCandidates.Add(new DbContextCandidate(registration.Location, namedType));

                return;
            }
        }
    }

    private static void CollectDbContextRegistration(
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        ConcurrentBag<DbContextCandidate> dbContextCandidates,
        EfCoreTypes efCore,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        // The method name alone does not prove the registration. A user helper can carry the same
        // name and a contextLifetime parameter.
        if (!efCore.IsEntityFrameworkRegistration(invocation, operation, model, cancellationToken))
        {
            return;
        }

        // AddDbContext registers a context with the scoped lifetime by default. Only an explicit
        // Singleton argument makes the registration a singleton.
        var contextType = GetDbContextType(operation, efCore);

        if (contextType is null || !HasSingletonLifetime(operation, efCore))
        {
            return;
        }

        dbContextCandidates.Add(new DbContextCandidate(
            GetDbContextRegistrationLocation(invocation, operation),
            contextType));
    }

    private static INamedTypeSymbol? GetDbContextType(IInvocationOperation operation, EfCoreTypes efCore)
    {
        foreach (var typeArgument in operation.TargetMethod.TypeArguments)
        {
            if (typeArgument is INamedTypeSymbol namedType && efCore.DerivesFromDbContext(namedType))
            {
                return namedType;
            }
        }

        foreach (var argument in operation.Arguments)
        {
            if (argument.Parameter is not null
                && UnwrapConversions(argument.Value) is ITypeOfOperation typeOf
                && typeOf.TypeOperand is INamedTypeSymbol namedType
                && efCore.DerivesFromDbContext(namedType))
            {
                return namedType;
            }
        }

        return null;
    }

    private static bool HasSingletonLifetime(IInvocationOperation operation, EfCoreTypes efCore)
    {
        // contextLifetime controls the context itself. optionsLifetime controls the options object and
        // stays scoped in a normal application. Read only the contextLifetime parameter, so an
        // explicit singleton options lifetime does not report a scoped context.
        IArgumentOperation? contextLifetime = null;

        foreach (var argument in operation.Arguments)
        {
            if (argument.Parameter is not { } parameter
                || argument.ArgumentKind == ArgumentKind.DefaultValue
                || !SymbolEqualityComparer.Default.Equals(parameter.Type, efCore.ServiceLifetime))
            {
                continue;
            }

            if (string.Equals(parameter.Name, "contextLifetime", StringComparison.Ordinal))
            {
                contextLifetime = argument;
            }
        }

        return contextLifetime is not null
               && UnwrapConversions(contextLifetime.Value).ConstantValue is { HasValue: true } constant
               && Equals(constant.Value, efCore.SingletonValue);
    }

    private static Location GetDbContextRegistrationLocation(InvocationExpressionSyntax invocation, IInvocationOperation operation)
    {
        var genericName = (invocation.Expression as MemberAccessExpressionSyntax)?.Name as GenericNameSyntax;

        if (genericName is { TypeArgumentList.Arguments.Count: > 0 })
        {
            return genericName.TypeArgumentList.Arguments[0].GetLocation();
        }

        foreach (var argument in operation.Arguments)
        {
            if (argument.Parameter is not null && UnwrapConversions(argument.Value) is ITypeOfOperation typeOf)
            {
                return typeOf.Syntax.GetLocation();
            }
        }

        return invocation.GetLocation();
    }

    private static IOperation UnwrapConversions(IOperation operation)
    {
        var current = operation;

        while (current is IConversionOperation conversion)
        {
            current = conversion.Operand;
        }

        return current;
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
                GetRegistrationLocation(invocation),
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

            var value = UnwrapConversions(argument.Value);

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

    private static Location GetRegistrationLocation(InvocationExpressionSyntax invocation)
    {
        // The report belongs on the registered type, not on the whole call. The type argument list is
        // absent for an expression-bodied generic call, so the invocation location is the fallback.
        var genericName = (invocation.Expression as MemberAccessExpressionSyntax)?.Name as GenericNameSyntax;

        // The implementation type is the last type argument. AddSingleton<T>() carries one, and
        // AddSingleton<TService, TImplementation>() carries two.
        var registeredType = genericName is { TypeArgumentList.Arguments.Count: > 0 }
            ? genericName.TypeArgumentList.Arguments.Last()
            : null;

        return registeredType?.GetLocation() ?? invocation.GetLocation();
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

    private static void ReportDbContextSingletons(
        CompilationAnalysisContext context,
        ConcurrentBag<DbContextCandidate> dbContextCandidates)
    {
        foreach (var candidate in dbContextCandidates)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DbContextRule,
                candidate.Location,
                candidate.ContextType.Name));
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

    private sealed class DbContextCandidate
    {
        internal DbContextCandidate(Location location, INamedTypeSymbol contextType)
        {
            Location = location;
            ContextType = contextType;
        }

        internal Location Location { get; }

        internal INamedTypeSymbol ContextType { get; }
    }

    private sealed class EfCoreTypes
    {
        private readonly INamedTypeSymbol? dbContext;
        private readonly INamedTypeSymbol? serviceCollection;
        private readonly INamedTypeSymbol? entityFrameworkExtensions;

        private EfCoreTypes(
            INamedTypeSymbol? dbContext,
            INamedTypeSymbol? serviceLifetime,
            object? singletonValue,
            INamedTypeSymbol? serviceCollection,
            INamedTypeSymbol? entityFrameworkExtensions)
        {
            this.dbContext = dbContext;
            this.serviceCollection = serviceCollection;
            this.entityFrameworkExtensions = entityFrameworkExtensions;
            ServiceLifetime = serviceLifetime;
            SingletonValue = singletonValue;
        }

        internal INamedTypeSymbol? ServiceLifetime { get; }

        internal object? SingletonValue { get; }

        internal bool IsAvailable => dbContext is not null && ServiceLifetime is not null && SingletonValue is not null;

        internal static EfCoreTypes Resolve(Compilation compilation)
        {
            // EF Core ships DbContext, and its dependency graph ships ServiceLifetime. Resolve both
            // once, and turn the rule off when either type is absent.
            var contextType = compilation.GetTypeByMetadataName(DbContextMetadataName);
            var serviceLifetime = compilation.GetTypeByMetadataName(ServiceLifetimeMetadataName);

            return new EfCoreTypes(
                contextType,
                serviceLifetime,
                ReadSingletonValue(serviceLifetime),
                compilation.GetTypeByMetadataName(ServiceCollectionMetadataName),
                compilation.GetTypeByMetadataName(EntityFrameworkExtensionsMetadataName));
        }

        internal bool IsDbContextRegistration(string methodName)
        {
            return IsAvailable
                   && (string.Equals(methodName, "AddDbContext", StringComparison.Ordinal)
                       || string.Equals(methodName, "AddDbContextPool", StringComparison.Ordinal));
        }

        /// <summary>
        ///     True when the call is the framework registration. The helper lives on one extension
        ///     class, and its receiver is an <c lang="csharp">IServiceCollection</c>.
        /// </summary>
        internal bool IsEntityFrameworkRegistration(
            InvocationExpressionSyntax invocation,
            IInvocationOperation operation,
            SemanticModel model,
            CancellationToken cancellationToken)
        {
            return (entityFrameworkExtensions is not null
                    && SymbolEqualityComparer.Default.Equals(
                        operation.TargetMethod.ContainingType,
                        entityFrameworkExtensions))
                   || (serviceCollection is not null
                       && invocation.Expression is MemberAccessExpressionSyntax access
                       && SymbolEqualityComparer.Default.Equals(
                           model.GetTypeInfo(access.Expression, cancellationToken).Type,
                           serviceCollection));
        }

        internal bool DerivesFromDbContext(INamedTypeSymbol type)
        {
            for (var current = (INamedTypeSymbol?)type; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, dbContext))
                {
                    return true;
                }
            }

            return false;
        }

        private static object? ReadSingletonValue(INamedTypeSymbol? serviceLifetime)
        {
            if (serviceLifetime is null)
            {
                return null;
            }

            foreach (var member in serviceLifetime.GetMembers("Singleton"))
            {
                if (member is IFieldSymbol { HasConstantValue: true } field)
                {
                    return field.ConstantValue;
                }
            }

            return null;
        }
    }
}
