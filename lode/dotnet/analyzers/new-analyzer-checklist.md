# New Analyzer Checklist
*Updated: 2026-11-02T18:22:11Z*

Pre-flight checks and common pitfalls when writing new E128.Analyzers rules.

## Rule Registration

- `DiagnosticId` is an `internal const string` on the analyzer class. The value
  is unique and continues the last assigned number.
- Add the rule to `src/E128.Analyzers/AnalyzerReleases.Unshipped.md`. See
  [Release Tracking](../release-tracking.md).
- Add the rule row to `src/E128.Analyzers/README.md`. Confirm the row with
  `scripts/readme-table-diff.sh --analyzer`.
- Put the analyzer in `src/E128.Analyzers/<Category>/`. Put its tests in
  `tests/E128.Analyzers.Tests/`. Name the files `<Name>AnalyzerTests.cs` and
  `<Name>CodeFixTests.cs`.
- Add `[Trait("Category", "CI")]` to every new test method.

## Choose the Analysis API

- Analyze with `IOperation` or `ISymbol`. Fall back to `SyntaxNode` only when
  neither expresses the check.
- Resolve a type with `Compilation.GetTypeByMetadataName` inside
  `RegisterCompilationStartAction`. The pattern is in
  [Analyzer Performance](performance.md).
- Expect `null` from that call when two references declare the same type. Walk
  `GetTypesByMetadataName` in that case. `MetadataTypes` holds the shared
  lookup.

## Generated Code

- Pass `GeneratedCodeAnalysisFlags.None` to `ConfigureGeneratedCodeAnalysis`
  for a rule that must ignore generated code. That is most rules.
- Opt in only when the generated file is the subject of the rule. Add a comment
  that states why.

## Netstandard2.0 Constraints

The analyzer project targets `netstandard2.0`. This imposes constraints that do not surface until compile time:

- **String equality**: use `string.Equals(a, b, StringComparison.Ordinal)` instead of `a != b` or `a == b`. Raw `!=` and `==` on strings triggers MA0006 (`UseStringComparison`) in the analyzer project because netstandard2.0 lacks the `StringComparer`-aware operators. Test projects (net10.0) do not trigger this, so the error only appears when building the analyzer itself.
- **No `Span<T>` / `Memory<T>`**: unavailable without polyfills
- **No default interface methods**: unsupported by the runtime
- **No `IAsyncEnumerable<T>`**: unavailable without polyfills
- **PolySharp**: used to polyfill `NotNullWhenAttribute` (see `analyzers.md § Netstandard2.0 Nullable Polyfill`)

## Common Nullable Surprises in Analyzer Code

Roslyn syntax node properties that are semantically always-present can be nullable in the netstandard2.0 API surface. The compiler enforces this, producing CS8631 or CS8600-series warnings. Always null-check these:

- `SimpleLambdaExpressionSyntax.ExpressionBody`: nullable even though lambdas used in LINQ always have expression bodies
- `MemberAccessExpressionSyntax.Name`: nullable in the type system
- `InvocationExpressionSyntax.Expression`: can technically be null
- `VariableDeclaratorSyntax.Initializer`: nullable when no initializer present
- `ArgumentSyntax.Expression`: nullable in the API

**Rule of thumb**: treat every Roslyn `SyntaxNode` property as potentially nullable unless the type is a non-nullable value type. Use pattern matching (`is { ExpressionBody: { } body }`) or explicit null guards.

## Keep the Package README Current

`src/E128.Analyzers/README.md` is the NuGet package page (`PackageReadmeFile`). It is the primary documentation surface for the package and is copied to nuget.org. After any analyzer change (new rule, renamed rule, changed category/severity, added/removed code fix), update the README's rule tables, "What each rule catches" sections, and code examples to match. A stale rule table makes the package look unmaintained.

## Test Infrastructure Notes

- `CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>` and `CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>` from `Microsoft.CodeAnalysis.CSharp.Testing` are the standard test harness. Most test files build a `test.RunAsync()` call directly, not the static `CSharpAnalyzerVerifier` helper.
- Test `ReferenceAssemblies` default to the SDK version configured in `tests/.globalconfig` via `e128_minimum_framework_version`
- xUnit v3 MTP runner requires `scripts/test.sh`. Raw `dotnet test --filter` does not work

## Related

- [Analyzers](../analyzers.md): main analyzer documentation
- [Code Fix Patterns](code-fix-patterns.md): code fix implementation patterns
- [Release Tracking](../release-tracking.md): Shipped/Unshipped workflow
