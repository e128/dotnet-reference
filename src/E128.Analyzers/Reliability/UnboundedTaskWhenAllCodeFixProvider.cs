using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Intentional no-op code fix for E128037. Adding <c>SemaphoreSlim</c> throttling is too
///     structural to automate — it requires introducing a field, wrapping the lambda body in
///     <c>try/finally</c>, and choosing an appropriate concurrency limit. This provider exists
///     solely to register the diagnostic ID so the "Suppress or Configure" lightbulb still
///     appears in the IDE.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UnboundedTaskWhenAllCodeFixProvider))]
[Shared]
public sealed class UnboundedTaskWhenAllCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UnboundedTaskWhenAllAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // No automated fix — adding SemaphoreSlim requires structural changes beyond a code fix.
        return Task.CompletedTask;
    }
}
