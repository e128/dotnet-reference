using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     Reports non-sealed, non-abstract classes that have no derived types in the current compilation.
///     Encourages sealing classes by default to prevent unintended inheritance.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SealedByDefaultAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128005";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Seal classes that have no subclasses",
        "Class '{0}' has no derived types in this compilation — mark it sealed or add an explicit suppression if inheritance is intentional",
        "Design",
        DiagnosticSeverity.Warning,
        true,
        null,
        null,
        WellKnownDiagnosticTags.CompilationEnd);

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

        // Two-pass strategy: per-compilation state is captured inside RegisterCompilationStartAction
        // so each compilation gets its own fresh sets (correct for incremental builds).
        context.RegisterCompilationStartAction(startContext =>
        {
            var candidates = new ConcurrentBag<(INamedTypeSymbol Type, Location Location)>();
            var hasSubclassSet = new ConcurrentDictionary<ISymbol, bool>(SymbolEqualityComparer.Default);

            // Pass 1: Concurrent — collect candidates and base-type relationships.
            startContext.RegisterSymbolAction(
                ctx => CollectTypeInfo(ctx, candidates, hasSubclassSet),
                SymbolKind.NamedType);

            // Pass 2: Single-threaded — report leaf classes at compilation end.
            startContext.RegisterCompilationEndAction(ctx => ReportLeafClasses(ctx, candidates, hasSubclassSet));
        });
    }

    private static void CollectTypeInfo(
        SymbolAnalysisContext context,
        ConcurrentBag<(INamedTypeSymbol, Location)> candidates,
        ConcurrentDictionary<ISymbol, bool> hasSubclassSet)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        // Record this type's base as having at least one subclass.
        if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
        {
            hasSubclassSet.TryAdd(baseType.OriginalDefinition, true);
        }

        // Collect candidates that could trigger E128005.
        // Exclusions: abstract, static, sealed, record (IsRecord covers record class),
        // struct (TypeKind.Struct), direct Object subclass.
        if (type.TypeKind != TypeKind.Class
            || type.IsAbstract
            || type.IsStatic
            || type.IsSealed
            || type.IsRecord
            || type.BaseType is null
            || type.BaseType.SpecialType == SpecialType.System_Object)
        {
            return;
        }

        var locations = type.Locations;
        if (locations.Length > 0)
        {
            candidates.Add((type, locations[0]));
        }
    }

    private static void ReportLeafClasses(
        CompilationAnalysisContext context,
        ConcurrentBag<(INamedTypeSymbol Type, Location Location)> candidates,
        ConcurrentDictionary<ISymbol, bool> hasSubclassSet)
    {
        foreach (var (type, location) in candidates)
        {
            if (!hasSubclassSet.ContainsKey(type.OriginalDefinition))
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, location, type.Name));
            }
        }
    }
}
