# .NET 10 Roslyn Analyzers
*Updated: 2026-04-14T19:54:19Z*

## Strategy: Deny by Default

All analyzer diagnostics default to `error` severity. Rules are explicitly relaxed only when justified. This prevents new violations from being introduced silently.

```
# .globalconfig
dotnet_analyzer_diagnostic.severity = error
```

## Configuration Split

| File               | Purpose                            | Scope             |
| ------------------ | ---------------------------------- | ----------------- |
| `.globalconfig`    | Analyzer diagnostic severities     | All projects      |
| `.editorconfig`    | Code style, formatting, naming     | All projects      |
| `tests/.globalconfig` | Test-specific overrides         | Test projects only |

The `tests/.globalconfig` uses `global_level = 101` to override the root `global_level = 100`.

## MSBuild Properties

In `Directory.Build.props`:

```xml
<AnalysisLevel>latest-all</AnalysisLevel>
<AnalysisMode>Recommended</AnalysisMode>
<AnalysisModeSecurity>All</AnalysisModeSecurity>
<AnalysisModeReliability>All</AnalysisModeReliability>
<AnalysisModePerformance>All</AnalysisModePerformance>
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

Key interactions:
- `AnalysisLevel=latest-all` enables rules; per-category `AnalysisMode` overrides specifics
- `EnforceCodeStyleInBuild` makes IDE rules (IDE*) fire during `dotnet build`
- `TreatWarningsAsErrors` promotes all enabled warnings to errors

## Third-Party Analyzer Packages

Declared in `Directory.Build.props` with `PrivateAssets="all"` (zero runtime impact). Versions pinned in `Directory.Packages.props`.

| Package                                    | Prefix    | Focus                    |
| ------------------------------------------ | --------- | ------------------------ |
| AsyncFixer                                 | AF*       | Async/await anti-patterns |
| Meziantou.Analyzer                         | MA*       | Code quality, performance, security |
| Microsoft.VisualStudio.Threading.Analyzers | VSTHRD*   | Threading correctness    |
| Roslynator.Analyzers                       | RCS*      | Code style + quality     |
| Roslynator.CodeAnalysis.Analyzers          | RCS*      | Advanced code analysis   |
| Roslynator.Formatting.Analyzers            | RCS*      | Formatting consistency   |
| SharpSource                                | SS*       | Common pitfalls          |
| SonarAnalyzer.CSharp                       | S*        | Security + reliability   |

## Suppression Policy

- Never use `#pragma warning disable` or `[SuppressMessage]` without user approval
- Never use the null-forgiving operator `!` to silence CS8600-CS8604
- Editorconfig severity downgrades require justification
- Test project relaxations go in `tests/.globalconfig`, not inline suppressions

## Custom Analyzers: E128.Analyzers

`src/E128.Analyzers/` is a solution-local Roslyn analyzer project. It is wired via `Directory.Build.targets` as a `ProjectReference` with `OutputItemType="Analyzer"` — applied to all projects except the analyzer itself (excluded via `IsRoslynComponent` condition). Severity is governed by `.globalconfig` (blanket error by default).

54 rules across 6 categories: Design, Reliability, Performance, Style, Testing, and FileSystem. All rules have code fixes. See `src/E128.Analyzers/README.md` for the complete rule table and usage examples.

Key rules by category (not exhaustive):

| Category    | Examples                                                                            |
| ----------- | ----------------------------------------------------------------------------------- |
| Design      | Sealed-by-default, async void, sync-over-async, ConfigureAwait, TimeProvider, DI    |
| Reliability  | GeneratedRegex safety, DateTime roundtrip, Task.WhenAll, JsonDocument lifetime       |
| Performance | MinBy/MaxBy, HttpCompletionOption, FrozenSet, string interpolation                  |
| Style       | string.Empty, Encoding.UTF8, XML doc comments, null-forgiving operator              |
| Testing     | Temp directory cleanup interface                                                     |

## Common Test Overrides

Rules suppressed in test projects (via `tests/.globalconfig` at `global_level=101`):

| Rule      | Reason                                                       |
| --------- | ------------------------------------------------------------ |
| CA1515    | xUnit requires public types for discovery                    |
| CA1707    | Underscores in test method names                             |
| CA1062    | xUnit fixture injection is always non-null                   |
| CA2007    | ConfigureAwait not needed in test methods                    |
| CA2234    | String URLs acceptable in test data                          |
| MA0040    | Ambient CancellationToken too noisy in tests                 |
| SS003     | Integer division intentional in test assertions              |
| SS037     | Tests use fake HttpMessageHandlers                           |
| SS059     | Sync using for MemoryStream acceptable in tests              |
| VSTHRD200 | Test methods don't need Async suffix                         |
| xunit1051 | xUnit v3 ambient CancellationToken too noisy                 |
| MA0006    | Relaxed to suggestion — test LINQ predicates use string `==` |

## CA2007 (ConfigureAwait) Scoping

Library code must use `ConfigureAwait(false)`. App code (ASP.NET, CLI) must not. Scope CA2007 to DLL projects only:

```
dotnet_diagnostic.CA2007.severity = error
dotnet_code_quality.CA2007.output_kind = DynamicallyLinkedLibrary
```

## Source Generators

`[GeneratedRegex]` must use partial **properties** (not partial methods) per MA0190:

```csharp
[GeneratedRegex(@"...", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
private static partial Regex MyRegex { get; }
```

## NamingStyleCodeFixProvider (IDE1006 Fix All)

`NamingStyleCodeFixProvider` in `E128.Analyzers.Style` provides a code fix for IDE1006 naming violations. It uses a custom `SequentialFixAllProvider` (not `WellKnownFixAllProviders.BatchFixer`) because `BatchFixer` computes all renames against the original solution snapshot and then tries to merge them — this fails when multiple renames touch the same document, causing `dotnet format` to log "doesn't support Fix All in Solution".

The sequential provider:
1. Collects all IDE1006 diagnostics across the fix-all scope (Document / Project / Solution)
2. Sorts renames back-to-front within each document to prevent span drift
3. Applies `Renamer.RenameSymbolAsync` one at a time to the evolving solution
4. Skips a rename if the symbol has already been renamed (name mismatch guard)

`dotnet format` resolves the suggested name from the `SuggestedName` property embedded in Roslyn's IDE1006 diagnostic. Tests use `FakeNamingViolationAnalyzer` which embeds `SymbolName` + style properties instead.

## Related

- [Project Structure](project-structure.md)
- [Testing](testing.md)
- [Practices](../practices.md)
