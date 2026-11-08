using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128058: Detects methods that return a <c lang="csharp">List&lt;T&gt;</c> field directly as
///     <c lang="csharp">IReadOnlyList&lt;T&gt;</c> or <c lang="csharp">IReadOnlyCollection&lt;T&gt;</c>, exposing the
///     internal mutable list through the read-only interface. The caller can cast it back to
///     <c lang="csharp">List&lt;T&gt;</c> and mutate it. Use <c lang="csharp">.AsReadOnly()</c> or wrap in a
///     <c lang="csharp">ReadOnlyCollection&lt;T&gt;</c> instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ListAsReadOnlyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128058";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Return List<T> via .AsReadOnly() when exposing as IReadOnlyList<T>",
        "Returning a mutable List<T> as IReadOnlyList<T> leaks the internal list — use .AsReadOnly() to prevent callers from casting back to List<T>",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true,
        "Returning a mutable List<T> directly as IReadOnlyList<T> allows callers to cast back to List<T> and mutate the internal collection. Return .AsReadOnly() instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeReturn, OperationKind.Return);
    }

    private static void AnalyzeReturn(OperationAnalysisContext context)
    {
        if (ReadOnlyCollectionExposure.Find(MutableCollectionKind.List, context.Operation, context.ContainingSymbol) is { } location)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, location));
        }
    }
}
