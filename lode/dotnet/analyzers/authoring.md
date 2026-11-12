# Analyzer Authoring
*Updated: 2026-11-12T00:54:46Z*

Rules for a new or changed rule in `src/E128.Analyzers`. Read
[new-analyzer-checklist.md](new-analyzer-checklist.md) before you write the
first line of a rule. It owns the netstandard2.0 limits, the nullable surprises,
the API gates, the symbol-matching pitfalls, and the generated-code policy. The
test harness lives in [testing.md](testing.md).

## Rule Registration

- Pass a category string of the form `"E128-<Area>"` to the `DiagnosticDescriptor`,
  such as `"E128-Security"`. The prefix lets a consumer select one area with
  `category-E128-Security`. List the same value in
  `AnalyzerReleases.Unshipped.md`.
- Declare `internal const string DiagnosticId = "E128NNN"` in the analyzer
  class. The value is unique and continues the last assigned number.
- Add the rule to `src/E128.Analyzers/AnalyzerReleases.Unshipped.md`. See
  [Release Tracking](../release-tracking.md).
- Add the row to the rule table in `src/E128.Analyzers/README.md`. Verify the
  table with `scripts/readme-table-diff.sh --analyzer`.
- Write the analyzer under `src/E128.Analyzers/<Category>/`. Write its code fix
  under `src/E128.Analyzers.CodeFixers/<Category>/`. Write its tests under
  `tests/E128.Analyzers.Tests/`.
- Add `[Trait("Category", "CI")]` to every new test method.

## Package README

`src/E128.Analyzers/README.md` ships as the NuGet package page
(`PackageReadmeFile`) and appears on nuget.org. Update its rule tables, its
"What each rule catches" sections, and its code examples in the same change as
any rule edit. Cover a new rule, a rename, a category change, a severity change,
and a new or removed code fix. A stale rule table makes the package look
unmaintained.

## Choosing the Analysis API

- Analyze with `IOperation` or `ISymbol`. Fall back to `SyntaxNode` only when
  neither expresses the check.
- Resolve a type with `Compilation.GetTypeByMetadataName` inside
  `RegisterCompilationStartAction`.
- Expect `null` from that call when two references declare the same type.
  `MetadataTypes` wraps the `GetTypesByMetadataName` lookup for that case.

## Generated Code

Most rules pass `GeneratedCodeAnalysisFlags.None`. The policy and the opt-in
cases live in the
[checklist](new-analyzer-checklist.md#generated-code).

## Code Fixes

Validate every precondition before you call `RegisterCodeFix`. A registered fix
that returns the document unchanged shows you a code action that does nothing.

Inherit `E128CodeFixProviderBase`. The base owns the fix-all provider and the
syntax root fetch. Override `RegisterFixesAsync(context, root)` for a fix that
reads the root.

```csharp
protected override Task RegisterFixesAsync(CodeFixContext context, SyntaxNode root)
{
    var span = context.Diagnostics[0].Location.SourceSpan;
    if (root.FindNode(span, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocation)
    {
        return Task.CompletedTask;
    }

    context.RegisterCodeFix(
        CodeAction.Create(Title, ct => FixAsync(context.Document, invocation, ct), Title),
        context.Diagnostics);

    return Task.CompletedTask;
}
```

For trivia handling and fix-all behavior, see
[code-fix-patterns.md](code-fix-patterns.md).

## Tests

- Route code fix tests through `CodeFixTestRunner` in
  `tests/E128.Analyzers.Tests/CodeFixTestRunner.cs`.
- Write the snippet as a raw string literal (`"""`).
- Cut the snippet to the code that reproduces the diagnostic.
- Mark the reported span with `{|E128NNN:code|}`. Do not set line or column.

## Related

- [new-analyzer-checklist.md](new-analyzer-checklist.md): pre-flight checks,
  netstandard2.0 constraints, nullable surprises, and generated-code policy
- [testing.md](testing.md): harness types, markup forms, and option injection
- [shared-helpers.md](shared-helpers.md): helpers to call before you write a copy
- [C# Conventions](../csharp-conventions.md): idioms and constructs to avoid
- [Analyzers](../analyzers.md): main analyzer documentation
