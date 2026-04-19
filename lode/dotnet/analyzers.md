# .NET 10 Roslyn Analyzers
*Updated: 2026-04-19T21:17:06Z*

## Strategy: Deny by Default

All analyzer diagnostics default to `error` severity. Rules are explicitly relaxed only when justified. This prevents new violations from being introduced silently.

```
# .globalconfig
dotnet_analyzer_diagnostic.severity = error
```

## Configuration Split

Per [Microsoft guidance](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options):

| File                  | Purpose                                             | Scope              |
| --------------------- | --------------------------------------------------- | ------------------ |
| `.editorconfig`       | Code style, formatting, naming (canonical home)     | All projects       |
| `.globalconfig`       | Analyzer diagnostic severities and enabling only    | All projects       |
| `tests/.globalconfig` | Test-specific severity overrides                    | Test projects only |

### Rationale

**`.editorconfig` is the canonical style/formatting home.** It supports both `dotnet_style_*`/`csharp_style_*` preferences and inline severity hints (e.g., `dotnet_style_coalesce_expression = true:error`). IDEs (Visual Studio, Rider, VS Code) read it to apply settings immediately while editing. It also supports `dotnet_diagnostic.*` entries if needed, but this repo keeps diagnostic severities in `.globalconfig` for separation of concerns.

**`.globalconfig` is purely for analyzer configuration.** It handles rule severity (`dotnet_diagnostic.*`) and enabling (`roslynator_analyzers.enabled_by_default`). It does NOT support editor style settings like indentation, whitespace, or formatting preferences — those must be in `.editorconfig`.

**Inline severity vs `dotnet_diagnostic.*`.** Style rules in `.editorconfig` use the `:error`/`:suggestion`/`:silent` suffix directly on the option value (e.g., `csharp_style_var_for_built_in_types = true:error`). The `.globalconfig` then lists explicit `dotnet_diagnostic.IDE*` overrides only where the behavior deviates from the blanket `dotnet_analyzer_diagnostic.severity = error` — avoiding silent drift when rule IDs change.

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
- Never use the null-forgiving operator `!` — enforced as error via `MA0191` (production) and `E128043`; test projects relax both for guard-clause assertions
- Editorconfig severity downgrades require justification
- Test project relaxations go in `tests/.globalconfig`, not inline suppressions

## Key Meziantou Rules Enabled as Error

Aligned with Meziantou's [comparison table](https://github.com/meziantou/Meziantou.Analyzer/blob/main/docs/comparison-with-other-analyzers.md):

| Rule    | Guardrail                                                                   |
| ------- | --------------------------------------------------------------------------- |
| MA0036  | Make class static (distinct from CA1822 which only covers methods)          |
| MA0110  | Use `[GeneratedRegex]` source generator over `new Regex(...)`               |
| MA0186  | `Equals(object?)` override must use `[NotNullWhen(true)]` on the parameter  |
| MA0191  | Do not use the null-forgiving operator (aligns with custom `E128043`)       |

## Netstandard2.0 Nullable Polyfill (E128.Analyzers)

The analyzer project targets `netstandard2.0` (required for Roslyn analyzers) which lacks `System.Diagnostics.CodeAnalysis.NotNullWhenAttribute`. Rather than hand-rolling a polyfill (which trips MA0048 file-name rules and IDE0130 namespace-mismatch), the project references **PolySharp** as a source generator:

```xml
<PackageReference Include="PolySharp" PrivateAssets="all" />
<PropertyGroup>
  <PolySharpIncludeGeneratedTypes>
    System.Diagnostics.CodeAnalysis.NotNullWhenAttribute
  </PolySharpIncludeGeneratedTypes>
</PropertyGroup>
```

`PrivateAssets="all"` keeps the generator out of the NuGet package. `PolySharpIncludeGeneratedTypes` scopes generation to only the attribute the project actually uses, avoiding duplicate-type conflicts with consumer projects that target modern TFMs.

## Custom Analyzers: E128.Analyzers

`src/E128.Analyzers/` is a solution-local Roslyn analyzer project. It is wired via `Directory.Build.targets` as a `ProjectReference` with `OutputItemType="Analyzer"` — applied to all projects except the analyzer itself (excluded via `IsRoslynComponent` condition). Severity is governed by `.globalconfig` (blanket error by default).

Rules span categories: Design, Reliability, Performance, Style, Testing, and FileSystem. Most rules ship with a code fix; a few (e.g., `E128045` Direct Console usage, `E128046` Excessive inheritance, `E128051` Broad HttpClient catch) have no fix because the remediation is context-specific. See `src/E128.Analyzers/README.md` for the complete rule table, code-fix status, and usage examples.

Key rules by category (not exhaustive):

| Category    | Examples                                                                                           |
| ----------- | -------------------------------------------------------------------------------------------------- |
| Design      | Sealed-by-default, async void, sync-over-async, ConfigureAwait, TimeProvider, DI, ImmutableArray   |
| Reliability | GeneratedRegex safety, DateTime roundtrip, Task.WhenAll, JsonDocument lifetime                     |
| Performance | MinBy/MaxBy, HttpCompletionOption, FrozenSet, string interpolation                                 |
| Style       | string.Empty, Encoding.UTF8, XML doc comments, null-forgiving operator                             |
| Testing     | Temp directory cleanup, stale ReferenceAssemblies                                                  |

### E128061 — Static readonly array → ImmutableArray

Flags `private static readonly T[]` and `internal static readonly T[]` fields. Arrays are reference types — `readonly` prevents reassignment but callers can still mutate contents via the indexer. Code fix replaces `T[]` with `ImmutableArray<T>` and unwraps `new T[]` / `new[]` initializers to collection expressions.

### E128062 — Stale ReferenceAssemblies in tests

Flags `ReferenceAssemblies.Net.Net80` / `Net90` in test code when the configured minimum framework version is higher (default: 100 for net10.0). Configurable via `e128_minimum_framework_version` in `.globalconfig`. Code fix replaces outdated version with the minimum.

### E128063 — Mid-name underscore in private static member

Severity: **Error**. Flags private/internal static members whose name contains an underscore at index ≥ 2 (e.g., `Nots_supportedExtensions`, `Creates_enrichmentJsonOptions`, `Spectres_terminal`). These are artifacts of IDE1006 batch-rename operations that mangle identifiers by inserting underscores at word boundaries instead of adjusting capitalization. Excludes: leading underscore (`_foo`), Hungarian prefix (`s_foo`, `m_foo`, `t_foo`), const fields, `op_` operator methods, `__` double-underscore patterns, and compiler-generated property accessors. Code fix uses `SequentialRenameFixAllProvider` (not `BatchFixer`) and removes the mid-name underscore by PascalCasing each segment.

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

## SequentialRenameFixAllProvider (Shared Fix-All Logic)

`SequentialRenameFixAllProvider` in `E128.Analyzers.Style` is a shared `FixAllProvider` used by any code fix that renames symbols via `Renamer.RenameSymbolAsync`. It replaces `WellKnownFixAllProviders.BatchFixer`, which computes all renames from the original solution snapshot and then merges — this merge fails when multiple renames touch the same document, causing `dotnet format` to log "doesn't support Fix All in Solution".

The provider accepts a `Func<Diagnostic, string?, string?>` delegate for computing the new name, allowing reuse across different fixers:

- `NamingStyleCodeFixProvider` (IDE1006): delegates to `ComputeCompliantName(diagnostic, symbolName)`
- `MidNameUnderscoreCodeFixProvider` (E128063): delegates to `ComputeFixedName(symbolName)`

Sequential application:
1. Collects all diagnostics across the fix-all scope (Document / Project / Solution)
2. Sorts renames back-to-front within each document to prevent span drift
3. Applies `Renamer.RenameSymbolAsync` one at a time to the evolving solution
4. Skips a rename if the symbol has already been renamed (name mismatch guard)

`dotnet format` resolves the IDE1006 suggested name from the `SuggestedName` property embedded in Roslyn's diagnostic. Tests use `FakeNamingViolationAnalyzer` which embeds `SymbolName` + style properties instead.

## Related

- [Project Structure](project-structure.md)
- [Testing](testing.md)
- [Practices](../practices.md)
