using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace E128.Analyzers.Design;

/// <summary>
///     Reports non-sealed, non-abstract classes that have no derived types in the current compilation.
///     Encourages sealing classes by default to prevent unintended inheritance.
///     A class that a type parameter names as a constraint is never reported.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SealedByDefaultAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128005";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Seal classes that have no subclasses",
        "Class '{0}' has no derived types in this compilation — mark it sealed or add an explicit suppression if inheritance is intentional",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

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

        context.RegisterCompilationStartAction(static startContext =>
        {
            var candidates = new ConcurrentBag<(INamedTypeSymbol Type, Location Location)>();
            var hasSubclassSet = new ConcurrentDictionary<ISymbol, bool>(SymbolEqualityComparer.Default);
            var constraintTypes = new ConcurrentDictionary<ISymbol, bool>(SymbolEqualityComparer.Default);

            startContext.RegisterSymbolAction(
                ctx => CollectTypeInfo(ctx, candidates, hasSubclassSet, constraintTypes),
                SymbolKind.NamedType);

            startContext.RegisterSymbolAction(
                ctx => CollectConstraintTypes(((IMethodSymbol)ctx.Symbol).TypeParameters, constraintTypes),
                SymbolKind.Method);

            startContext.RegisterOperationAction(
                ctx => CollectConstraintTypes(
                    ((ILocalFunctionOperation)ctx.Operation).Symbol.TypeParameters,
                    constraintTypes),
                OperationKind.LocalFunction);

            startContext.RegisterCompilationEndAction(
                ctx => ReportLeafClasses(ctx, candidates, hasSubclassSet, constraintTypes));
        });
    }

    private static void CollectTypeInfo(
        SymbolAnalysisContext context,
        ConcurrentBag<(INamedTypeSymbol, Location)> candidates,
        ConcurrentDictionary<ISymbol, bool> hasSubclassSet,
        ConcurrentDictionary<ISymbol, bool> constraintTypes)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        CollectConstraintTypes(type.TypeParameters, constraintTypes);

        if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
        {
            _ = hasSubclassSet.TryAdd(baseType.OriginalDefinition, true);
        }

        if (type.TypeKind is not TypeKind.Class
            || type.IsAbstract
            || type.IsStatic
            || type.IsSealed
            || type.IsRecord
            || type.BaseType is null
            || type.BaseType.SpecialType is SpecialType.System_Object)
        {
            return;
        }

        var locations = type.Locations;
        if (locations.Length is 0)
        {
            return;
        }

        candidates.Add((type, locations[0]));
    }

    private static void CollectConstraintTypes(
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        ConcurrentDictionary<ISymbol, bool> constraintTypes)
    {
        foreach (var typeParameter in typeParameters)
        {
            foreach (var constraintType in typeParameter.ConstraintTypes)
            {
                if (constraintType.TypeKind is TypeKind.Class)
                {
                    _ = constraintTypes.TryAdd(constraintType, true);
                    _ = constraintTypes.TryAdd(constraintType.OriginalDefinition, true);
                }
            }
        }
    }

    private static void ReportLeafClasses(
        CompilationAnalysisContext context,
        ConcurrentBag<(INamedTypeSymbol Type, Location Location)> candidates,
        ConcurrentDictionary<ISymbol, bool> hasSubclassSet,
        ConcurrentDictionary<ISymbol, bool> constraintTypes)
    {
        foreach (var (type, location) in candidates)
        {
            if (!hasSubclassSet.ContainsKey(type.OriginalDefinition)
                && !constraintTypes.ContainsKey(type.OriginalDefinition))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, location, type.Name));
            }
        }
    }
}
