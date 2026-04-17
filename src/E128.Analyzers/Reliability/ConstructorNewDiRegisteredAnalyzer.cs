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
///     E128034: Detects <c>new T(...)</c> inside a constructor when T is a type also
///     registered via <c>services.AddSingleton&lt;T&gt;()</c>, <c>AddScoped&lt;T&gt;()</c>,
///     <c>AddTransient&lt;T&gt;()</c>, or <c>AddHttpClient&lt;T&gt;()</c> in the same compilation.
///     Inline construction of DI-owned types defeats testability.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConstructorNewDiRegisteredAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128034";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Constructor news a DI-registered type — inject via DI instead",
        "'{0}' is registered in DI — inject it via the constructor parameter list instead of using 'new'",
        "Reliability",
        DiagnosticSeverity.Warning,
        true,
        "When a type is registered in the DI container via AddSingleton<T>, AddScoped<T>, " +
        "AddTransient<T>, or AddHttpClient<T>, constructing it with 'new' inside another constructor " +
        "bypasses the DI container, defeating testability and creating invisible inconsistency.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    private static readonly HashSet<string> DiRegistrationMethods =
        new(StringComparer.Ordinal)
        {
            "AddSingleton",
            "AddScoped",
            "AddTransient",
            "AddHttpClient"
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
        var registeredTypes = new ConcurrentBag<INamedTypeSymbol>();
        var candidateViolations = new ConcurrentBag<(Location Location, INamedTypeSymbol CreatedType)>();

        context.RegisterSyntaxNodeAction(
            ctx => CollectDiRegistration(ctx, registeredTypes),
            SyntaxKind.InvocationExpression);

        context.RegisterSyntaxNodeAction(
            ctx => CollectConstructorNew(ctx, candidateViolations),
            SyntaxKind.ObjectCreationExpression);

        context.RegisterCompilationEndAction(ctx =>
            ReportViolations(ctx, registeredTypes, candidateViolations));
    }

    private static void CollectDiRegistration(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<INamedTypeSymbol> registeredTypes)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (!IsDiRegistrationCall(invocation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
                is IMethodSymbol { IsGenericMethod: true, TypeArguments.Length: >= 1 } method
            && method.TypeArguments[0] is INamedTypeSymbol registeredType)
        {
            registeredTypes.Add(registeredType);
        }
    }

    private static void CollectConstructorNew(
        SyntaxNodeAnalysisContext context,
        ConcurrentBag<(Location, INamedTypeSymbol)> candidates)
    {
        var creation = (ObjectCreationExpressionSyntax)context.Node;

        if (!IsInsideConstructor(creation))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(creation.Type, context.CancellationToken).Symbol
            is INamedTypeSymbol createdType)
        {
            candidates.Add((creation.GetLocation(), createdType));
        }
    }

    private static void ReportViolations(
        CompilationAnalysisContext context,
        ConcurrentBag<INamedTypeSymbol> registeredTypes,
        ConcurrentBag<(Location Location, INamedTypeSymbol CreatedType)> candidates)
    {
        var registered = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in registeredTypes)
        {
            registered.Add(type);
        }

        if (registered.Count == 0)
        {
            return;
        }

        foreach (var (location, createdType) in candidates)
        {
            if (registered.Contains(createdType))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    location,
                    createdType.Name));
            }
        }
    }

    private static bool IsDiRegistrationCall(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null
        };

        return methodName is not null && DiRegistrationMethods.Contains(methodName);
    }

    private static bool IsInsideConstructor(SyntaxNode node)
    {
        var current = node.Parent;
        while (current is not null)
        {
            if (current is ConstructorDeclarationSyntax)
            {
                return true;
            }

            if (current is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }

            current = current.Parent;
        }

        return false;
    }
}
