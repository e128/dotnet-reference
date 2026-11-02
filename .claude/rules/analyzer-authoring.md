# Analyzer Authoring

Rules for a new or changed rule in `src/E128.Analyzers`. The test harness lives
in [dotnet-testing.md](dotnet-testing.md).

## Rule Registration

- Declare `internal const string DiagnosticId = "E128NNN"` in the analyzer
  class. The value is unique and continues the last assigned number.
- Add the rule to `src/E128.Analyzers/AnalyzerReleases.Unshipped.md`.
- Add the row to the rule table in `src/E128.Analyzers/README.md`. Verify the
  table with `scripts/readme-table-diff.sh --analyzer`.
- Write the analyzer under `src/E128.Analyzers/<Category>/`. Write its tests
  under `tests/E128.Analyzers.Tests/`.
- Update the README text for a changed rule in the same change.

## Choose the Analysis API

- Analyze with `IOperation` or `ISymbol`. Fall back to `SyntaxNode` only when
  neither expresses the check.
- Resolve a type with `Compilation.GetTypeByMetadataName` inside
  `RegisterCompilationStartAction`.
- Expect `null` from that call when two references declare the same type. Walk
  `GetTypesByMetadataName` in that case.

## Generated Code

- Pass the minimum `GeneratedCodeAnalysisFlags` value to
  `ConfigureGeneratedCodeAnalysis`. Most rules pass `None`.
- Opt in for a rule whose subject is the generated file itself. Add a comment
  that states why the rule reads generated code.

## Code Fixes

Validate every precondition before you call `RegisterCodeFix`. A registered fix
that returns the document unchanged shows the user an action that does nothing.

```csharp
public override async Task RegisterCodeFixesAsync(CodeFixContext context)
{
    var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
    if (root?.FindNode(context.Span, getInnermostNodeForTie: true) is not InvocationExpressionSyntax invocation)
    {
        return;
    }

    var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
    if (model?.Compilation.GetTypeByMetadataName(SomeTypeName) is null)
    {
        return;
    }

    context.RegisterCodeFix(
        CodeAction.Create(Title, ct => FixAsync(context.Document, invocation, ct), Title),
        context.Diagnostics);
}
```

For trivia handling and fix-all behavior, see
[code-fix-patterns.md](../../lode/dotnet/analyzers/code-fix-patterns.md).

## Test Snippets

- Write the snippet as a raw string literal (`"""`).
- Cut the snippet to the code that reproduces the diagnostic.
- Mark the reported span with `{|E128NNN:code|}`. Do not set line or column.

## See Also

- [new-analyzer-checklist.md](../../lode/dotnet/analyzers/new-analyzer-checklist.md):
  netstandard2.0 constraints and nullable surprises
- [quality-gates.md](quality-gates.md): suppressions and the format gate
