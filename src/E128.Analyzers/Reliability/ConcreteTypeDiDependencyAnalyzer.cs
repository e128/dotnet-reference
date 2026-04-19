using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace E128.Analyzers.Reliability;

/// <summary>
///     E128035: Detects constructor parameters of concrete types that are only registered in DI
///     via their interface (e.g., <c>AddSingleton&lt;IFoo, T&gt;()</c>) but lack a direct
///     registration (<c>AddSingleton&lt;T&gt;()</c>). This causes <c>InvalidOperationException</c>
///     at runtime when another service depends on the concrete type directly.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConcreteTypeDiDependencyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128035";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Concrete-type DI dependency without direct registration",
        "'{0}' is only registered via interface — add a direct DI registration (e.g., AddSingleton<{0}>()) or resolve via the interface instead",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "When a constructor parameter is a concrete type that is registered in DI only via " +
        "an interface mapping (AddSingleton<IFoo, T>()), the DI container cannot resolve the " +
        "concrete type directly. Add a direct registration or use the interface type instead.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly HashSet<string> DiRegistrationMethods =
        new(StringComparer.Ordinal)
        {
            "AddSingleton",
            "AddScoped",
            "AddTransient"
        };

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
        var interfaceRegistered = new ConcurrentBag<INamedTypeSymbol>();
        var directlyRegistered = new ConcurrentBag<INamedTypeSymbol>();
        var candidateParams = new ConcurrentBag<(Location Location, INamedTypeSymbol ParamType)>();

        context.RegisterSyntaxNodeAction(
            ctx => CollectDiRegistration(ctx, interfaceRegistered, directlyRegistered),
            SyntaxKind.InvocationExpression);

        context.RegisterSyntaxNodeAction(
            ctx => CollectConstructorParams(ctx, candidateParams),
            SyntaxKind.ConstructorDeclaration);

        context.RegisterCompilationEndAction(ctx =>
            ReportViolations(ctx, interfaceRegistered, directlyRegistered, candidateParams));
    }

    private static void CollectDiRegistration(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<INamedTypeSymbol> interfaceRegistered,
        ConcurrentBag<INamedTypeSymbol> directlyRegistered)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!TryGetDiMethodName(invocation, out _))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol { IsGenericMethod: true } method)
        {
            return;
        }

        ClassifyRegistration(method, interfaceRegistered, directlyRegistered);
    }

    private static void ClassifyRegistration(
        IMethodSymbol method,
        ConcurrentBag<INamedTypeSymbol> interfaceRegistered,
        ConcurrentBag<INamedTypeSymbol> directlyRegistered)
    {
        if (method.TypeArguments.Length == 2
            && method.TypeArguments[1] is INamedTypeSymbol implType)
        {
            interfaceRegistered.Add(implType);
        }
        else if (method.TypeArguments.Length == 1
                 && method.TypeArguments[0] is INamedTypeSymbol concreteType)
        {
            directlyRegistered.Add(concreteType);
        }
    }

    private static void CollectConstructorParams(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<(Location, INamedTypeSymbol)> candidateParams)
    {
        var ctor = (ConstructorDeclarationSyntax)context.Node;

        if (ctor.ParameterList is null)
        {
            return;
        }

        foreach (var param in ctor.ParameterList.Parameters)
        {
            if (param.Type is null)
            {
                continue;
            }

            if (context.SemanticModel.GetTypeInfo(param.Type, context.CancellationToken).Type
                    is INamedTypeSymbol paramType
                && IsConcreteNonPrimitive(paramType))
            {
                var span = TextSpan.FromBounds(
                    param.Type.SpanStart,
                    param.Identifier.Span.End);
                var location = Location.Create(param.SyntaxTree, span);
                candidateParams.Add((location, paramType));
            }
        }
    }

    private static void ReportViolations(
        CompilationAnalysisContext context,
        ConcurrentBag<INamedTypeSymbol> interfaceRegistered,
        ConcurrentBag<INamedTypeSymbol> directlyRegistered,
        ConcurrentBag<(Location Location, INamedTypeSymbol ParamType)> candidateParams)
    {
        var interfaceOnly = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in interfaceRegistered)
        {
            interfaceOnly.Add(type);
        }

        var direct = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in directlyRegistered)
        {
            direct.Add(type);
        }

        interfaceOnly.ExceptWith(direct);

        if (interfaceOnly.Count == 0)
        {
            return;
        }

        foreach (var (location, paramType) in candidateParams)
        {
            if (interfaceOnly.Contains(paramType))
            {
                var properties = ImmutableDictionary.CreateRange(
                    [new KeyValuePair<string, string?>("ConcreteType", paramType.Name)]);
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    location,
                    properties,
                    paramType.Name));
            }
        }
    }

    private static bool TryGetDiMethodName(InvocationExpressionSyntax invocation, [NotNullWhen(true)] out string? methodName)
    {
        methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null
        };

        return methodName is not null && DiRegistrationMethods.Contains(methodName);
    }

    private static bool IsConcreteNonPrimitive(INamedTypeSymbol type)
    {
        return !(type.TypeKind is TypeKind.Interface or TypeKind.Delegate) && !type.IsAbstract
                                                                           && type.SpecialType is SpecialType.None && !InheritsFromDelegate(type);
    }

    private static bool InheritsFromDelegate(ITypeSymbol type)
    {
        var current = type.BaseType;
        while (current is not null)
        {
            if (string.Equals(current.ToDisplayString(), "System.Delegate", StringComparison.Ordinal))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
