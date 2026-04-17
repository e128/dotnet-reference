using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Code fix provider for E128034. This diagnostic flags <see langword="new" /> T() inside constructors
///     where T is DI-registered, but the correct fix (adding a constructor parameter, removing
///     the <see langword="new" />, and updating all callers) is too complex and context-dependent for a
///     safe automatic code action. This provider is intentionally a no-op — it exists to satisfy
///     the convention that every analyzer has a paired code fix provider, but registers zero fixes.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ConstructorNewDiRegisteredCodeFixProvider))]
[Shared]
public sealed class ConstructorNewDiRegisteredCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [ConstructorNewDiRegisteredAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider()
    {
        return null;
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // Intentionally no-op: the fix requires adding a constructor parameter,
        // removing the 'new', and propagating to all call sites — too complex
        // for a safe automatic code action.
        return Task.CompletedTask;
    }
}
