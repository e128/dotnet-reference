# Project Conventions (Optional)

This file is read by the overhauler if present. It contains project-specific coding standards,
analyzer configuration, and test relaxations that customize the overhaul behavior.

**If this file does not exist, the overhauler runs with sensible .NET defaults.**

Delete or modify this file to match your project's conventions.

## Coding Standards

- Use `string.Empty` instead of `""` for empty string literals
- Never use `!` (null-forgiving operator) to silence CS8600-CS8604 — these require human judgment
- Never suppress analyzer rules without explicit approval (`#pragma warning disable`, `[SuppressMessage]`)

## Immutability

Favor immutable code by default. Convert mutable types when the mutation is not required:

| Mutable pattern                        | Immutable replacement                                   |
| -------------------------------------- | ------------------------------------------------------- |
| `class` with only data properties      | `record` or `readonly record struct`                    |
| `{ get; set; }`                        | `{ get; init; }` or `{ get; }`                          |
| `struct`                               | `readonly struct`                                       |
| `List<T>` field/property (public)      | `IReadOnlyList<T>` or `ImmutableArray<T>`               |
| `Dictionary<TK,TV>` (lookup-only)      | `FrozenDictionary<TK,TV>` or `IReadOnlyDictionary<TK,TV>` |
| `HashSet<T>` (lookup-only)             | `FrozenSet<T>` or `IReadOnlySet<T>`                     |
| `T[]` return from public method        | `ReadOnlySpan<T>`, `IReadOnlyList<T>`, or `ImmutableArray<T>` |
| Method parameter `List<T>`             | `IReadOnlyList<T>` or `IEnumerable<T>`                  |

**Packages to add when needed:**
- `System.Collections.Immutable` — `ImmutableArray<T>`, `ImmutableList<T>`, `ImmutableDictionary<TK,TV>`
- `FrozenSet<T>` / `FrozenDictionary<TK,TV>` — in-box for .NET 8+ (no additional package)

**Do not convert:**
- Private mutable state behind an immutable public API
- Builder patterns, object pools, or caches that require mutation
- Entity Framework models (EF requires mutable properties)
- ViewModels/DTOs that frameworks mutate via reflection

## Analyzer Packages

If your project uses analyzers, list them here so the overhauler can interpret their output:

| Package | Rules | Focus |
|---------|-------|-------|
| AsyncFixer | AF* | async/await anti-patterns |
| Meziantou.Analyzer | MA* | code quality, performance |
| Microsoft.VisualStudio.Threading.Analyzers | VSTHRD* | threading |
| Roslynator.Analyzers | RCS* | general analyzers |
| SharpSource | SS* | runtime correctness |
| SonarAnalyzer.CSharp | S* | code smells, bugs |

## Severity Triage

Override the default severity triage by adding custom mappings:

| Severity | Criteria | Examples |
|----------|----------|---------|
| HIGH | Build errors, null-ref warnings, security | CS0246, CS8602, CS8604, S* |
| MEDIUM | Analyzer violations, format, unused code | IDE0005, IDE0011, CA1822, MA* |
| LOW | Style, naming, documentation | IDE0062, CS1591 |

## Auto-Approved Fixes

These fixes align with common auto-approval policies — apply without prompting:

| Code | Fix |
|------|-----|
| IDE0005 | Remove unused `using` statements |
| IDE0161 | Convert to file-scoped namespace |
| Format | Run `dotnet format --include <files>` |
| Using sort | Sort `using` directives |

## Never Auto-Fix

| Code | Reason |
|------|--------|
| CS8600-CS8604 | Null-ref warnings require human judgment — list in findings, don't add `!` |
| Any `#pragma` | Suppressions require explicit approval |

## Global Suppressions (Always `severity = none`)

These rules must be set to `severity = none` in the root `.globalconfig` (or `.editorconfig`)
whenever the overhaul creates or audits analyzer configuration. They apply repo-wide, not just
to tests.

| Rule      | Reason                                                                                |
|-----------|---------------------------------------------------------------------------------------|
| S4055     | Literals should not be passed as localized parameters — not applicable (no L10N)      |
| VSTHRD111 | Add ConfigureAwait — VS extension model (JoinableTaskFactory), not relevant here      |

## Test Project Relaxations

Rules that are NOT violations in test code (files under `tests/` or `*Tests*` directories):

| Rule | Reason |
|------|--------|
| CA1707 | Underscores in test method names |
| CA2007, MA0004, VSTHRD111 | No ConfigureAwait required in tests |
| VSTHRD200 | Test methods don't need Async suffix |
| CA1515 | Test classes can be public |
| MA0040, xUnit1051 | Ambient CancellationToken not required |
