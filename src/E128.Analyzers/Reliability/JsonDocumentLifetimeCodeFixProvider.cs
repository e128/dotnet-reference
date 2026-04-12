using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace E128.Analyzers.Reliability;

/// <summary>
/// Intentional no-op code fix for E128041. Restructuring a <see langword="using"/> scope around
/// <c>JsonDocument.Parse()</c> is a non-trivial refactoring that requires understanding
/// the full data flow of <c>RootElement</c> — inserting <c>.Clone()</c> in the right
/// place, restructuring control flow, and potentially changing method signatures. This
/// is beyond what a mechanical code fix can safely do. The analyzer flags the issue;
/// the developer must restructure manually.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(JsonDocumentLifetimeCodeFixProvider))]
[Shared]
public sealed class JsonDocumentLifetimeCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [JsonDocumentLifetimeAnalyzer.DiagnosticId];

    public override FixAllProvider? GetFixAllProvider() => null;

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        // Intentional no-op: restructuring using scope around JsonDocument.Parse()
        // requires understanding the full data flow of RootElement — where it escapes,
        // whether .Clone() is appropriate, and how to restructure the method's control
        // flow. This is too context-dependent for a mechanical fix.
        return Task.CompletedTask;
    }
}
