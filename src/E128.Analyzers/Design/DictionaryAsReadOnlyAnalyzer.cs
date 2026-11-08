using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace E128.Analyzers.Design;

/// <summary>
///     E128060: Detects methods and properties that return a <c lang="csharp">Dictionary&lt;K,V&gt;</c> directly
///     as <c lang="csharp">IReadOnlyDictionary&lt;K,V&gt;</c>, exposing the internal mutable dictionary through the
///     read-only interface. The caller can cast it back to <c lang="csharp">Dictionary&lt;K,V&gt;</c> and mutate it.
///     Use <c lang="csharp">.AsReadOnly()</c> (requires .NET 9+) or wrap in a <c lang="csharp">ReadOnlyDictionary&lt;K,V&gt;</c>
///     instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DictionaryAsReadOnlyAnalyzer : DiagnosticAnalyzer
{
    internal const string DiagnosticId = "E128060";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Return Dictionary<K,V> via .AsReadOnly() when exposing as IReadOnlyDictionary<K,V>",
        "Returning a mutable Dictionary<K,V> as IReadOnlyDictionary<K,V> leaks the internal dictionary — use .AsReadOnly() to prevent callers from casting back to Dictionary<K,V>",
        "E128-Design",
        DiagnosticSeverity.Warning,
        true,
        "Returning a mutable Dictionary<K,V> directly as IReadOnlyDictionary<K,V> allows callers to cast back to Dictionary<K,V> and mutate the internal collection. Return .AsReadOnly() instead.");

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
        if (ReadOnlyCollectionExposure.Find(MutableCollectionKind.Dictionary, context) is { } location)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, location));
        }
    }
}
