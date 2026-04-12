using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
/// E128044: Flags types that implement <c>IAsyncDisposable</c> but not <c>IDisposable</c>.
/// Consumers using synchronous <see langword="using"/> will silently skip disposal.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncDisposableWithoutDisposableAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128044";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Type implements IAsyncDisposable but not IDisposable",
        messageFormat: "Type '{0}' implements IAsyncDisposable but not IDisposable — consumers using sync 'using' will silently skip disposal",
        category: "Design",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        // Skip interfaces — they define contracts, not implementations.
        if (type.TypeKind == TypeKind.Interface)
        {
            return;
        }

        // Only check classes and structs.
        if (type.TypeKind is not TypeKind.Class and not TypeKind.Struct)
        {
            return;
        }

        var hasAsyncDisposable = false;
        var hasDisposable = false;

        foreach (var iface in type.AllInterfaces)
        {
            if (IsSystemInterface(iface, "IAsyncDisposable"))
            {
                hasAsyncDisposable = true;
            }

            if (IsSystemInterface(iface, "IDisposable"))
            {
                hasDisposable = true;
            }
        }

        if (hasAsyncDisposable && !hasDisposable)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, type.Locations[0], type.Name));
        }
    }

    private static bool IsSystemInterface(INamedTypeSymbol iface, string name)
    {
        if (!string.Equals(iface.Name, name, StringComparison.Ordinal))
        {
            return false;
        }

        var ns = iface.ContainingNamespace;
        return ns != null
            && string.Equals(ns.Name, "System", StringComparison.Ordinal)
            && ns.ContainingNamespace != null
            && ns.ContainingNamespace.IsGlobalNamespace;
    }
}
