# New Analyzer Checklist
*Updated: 2026-05-17T00:00:00Z*

Pre-flight checks and common pitfalls when writing new E128.Analyzers rules.

## Netstandard2.0 Constraints

The analyzer project targets `netstandard2.0`. This imposes constraints that don't surface until compile time:

- **String equality**: use `string.Equals(a, b, StringComparison.Ordinal)` instead of `a != b` or `a == b`. Raw `!=` / `==` on strings triggers MA0006 (`UseStringComparison`) in the analyzer project because netstandard2.0 lacks the `StringComparer`-aware operators. Test projects (net10.0) don't trigger this, so the error only appears when building the analyzer itself.
- **No `Span<T>` / `Memory<T>`**: not available without polyfills
- **No default interface methods**: not supported by the runtime
- **No `IAsyncEnumerable<T>`**: not available without polyfills
- **PolySharp**: used to polyfill `NotNullWhenAttribute` (see `analyzers.md § Netstandard2.0 Nullable Polyfill`)

## Common Nullable Surprises in Analyzer Code

Roslyn syntax node properties that are semantically always-present can be nullable in the netstandard2.0 API surface. The compiler enforces this, producing CS8631 or CS8600-series warnings. Always null-check these:

- `SimpleLambdaExpressionSyntax.ExpressionBody` — nullable even though lambdas used in LINQ always have expression bodies
- `MemberAccessExpressionSyntax.Name` — nullable in the type system
- `InvocationExpressionSyntax.Expression` — can technically be null
- `VariableDeclaratorSyntax.Initializer` — nullable when no initializer present
- `ArgumentSyntax.Expression` — nullable in the API

**Rule of thumb**: treat every Roslyn `SyntaxNode` property as potentially nullable unless the type is a non-nullable value type. Use pattern matching (`is { ExpressionBody: { } body }`) or explicit null guards.

## Test Infrastructure Notes

- `CSharpAnalyzerVerifier` / `CSharpCodeFixVerifier` from `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` are the standard test harness
- Test `ReferenceAssemblies` default to the SDK version configured in `tests/.globalconfig` via `e128_minimum_framework_version`
- xUnit v3 MTP runner requires `scripts/test.sh` — raw `dotnet test --filter` does not work

## Related

- [Analyzers](../analyzers.md) — main analyzer documentation
- [Code Fix Patterns](code-fix-patterns.md) — code fix implementation patterns
- [Release Tracking](../release-tracking.md) — Shipped/Unshipped workflow
