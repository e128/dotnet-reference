using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Intentional no-op code fix for E128037. Adding <c lang="csharp">SemaphoreSlim</c> throttling is too
///     structural to automate — it requires introducing a field, wrapping the lambda body in
///     <c lang="csharp">try/finally</c>, and choosing an appropriate concurrency limit. This provider exists
///     solely to register the diagnostic ID so the "Suppress or Configure" lightbulb still
///     appears in the IDE.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnboundedTaskWhenAllCodeFixProvider))]
[Shared]
public sealed class UnboundedTaskWhenAllCodeFixProvider : E128NoOpCodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnboundedTaskWhenAllAnalyzer.DiagnosticId];
}
