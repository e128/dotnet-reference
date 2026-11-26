using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

namespace E128.Analyzers.Reliability;

/// <summary>
///     Intentional no-op code fix for E128041. Restructuring a <see langword="using" /> scope around
///     <c lang="csharp">JsonDocument.Parse()</c> is a non-trivial refactoring that requires understanding
///     the full data flow of <c lang="csharp">RootElement</c> — inserting <c lang="csharp">.Clone()</c> in the right
///     place, restructuring control flow, and potentially changing method signatures. This
///     is beyond what a mechanical code fix can safely do. The analyzer flags the issue;
///     the developer must restructure manually.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(JsonDocumentLifetimeCodeFixProvider))]
[Shared]
public sealed class JsonDocumentLifetimeCodeFixProvider : E128NoOpCodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [JsonDocumentLifetimeAnalyzer.DiagnosticId];
}
