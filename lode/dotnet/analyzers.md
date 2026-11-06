# .NET 10 Roslyn Analyzers
*Updated: 2026-11-06T13:02:22Z*

## Strategy: Deny by Default

All analyzer diagnostics default to `error` via `.globalconfig` (`dotnet_analyzer_diagnostic.severity = error`). Rules are relaxed only when justified.

## Configuration Split

Per [Microsoft guidance](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options):

| File                  | Purpose                                             | Scope              |
| --------------------- | --------------------------------------------------- | ------------------ |
| `.editorconfig`       | Code style, formatting, naming (canonical home)     | All projects       |
| `.globalconfig`       | Analyzer diagnostic severities and enabling only    | All projects       |
| `tests/.globalconfig` | Test-specific severity overrides                    | Test projects only |

### Rationale

**`.editorconfig` is the canonical style/formatting home.** It supports both `dotnet_style_*`/`csharp_style_*` preferences and inline severity hints (e.g., `dotnet_style_coalesce_expression = true:error`). IDEs (Visual Studio, Rider, VS Code) read it to apply settings immediately while editing. It also supports `dotnet_diagnostic.*` entries if needed, but this repo keeps diagnostic severities in `.globalconfig` for separation of concerns.

**`.globalconfig` is purely for analyzer configuration.** It handles rule severity (`dotnet_diagnostic.*`) and enabling (`roslynator_analyzers.enabled_by_default`). It does NOT support editor style settings like indentation, whitespace, or formatting preferences. Put those in `.editorconfig`.

**Inline severity vs `dotnet_diagnostic.*`.** Style rules in `.editorconfig` carry the `:error`/`:suggestion`/`:silent` suffix on the option value (e.g., `csharp_style_var_for_built_in_types = true:error`). The `.globalconfig` lists explicit `dotnet_diagnostic.IDE*` overrides only where the behavior deviates from the blanket `dotnet_analyzer_diagnostic.severity = error`. That avoids silent drift when rule IDs change. The `tests/.globalconfig` uses `global_level = 101` to override the root `global_level = 100`.

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
- `AnalysisLevel=latest-all` sets mode `All` in every category. A compound `AnalysisLevel` wins over `AnalysisMode` and over `AnalysisMode<Category>`, so the three category lines above change nothing. Use `<AnalysisLevel>latest</AnalysisLevel>` or a version such as `10.0` to let the category overrides apply.
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
- Never use the null-forgiving operator `!`: `E128043` (every use) and `MA0191` (only `null!` and `default!`) enforce this as error. Test projects relax both for guard-clause assertions
- Editorconfig severity downgrades require justification
- Test project relaxations go in `tests/.globalconfig`, not inline suppressions

## Key Meziantou Rules Enabled as Error

Aligned with Meziantou's [comparison table](https://github.com/meziantou/Meziantou.Analyzer/blob/main/docs/comparison-with-other-analyzers.md):

| Rule    | Guardrail                                                                   |
| ------- | --------------------------------------------------------------------------- |
| MA0036  | Make class static (distinct from CA1822 which only covers methods)          |
| MA0054  | Embed the caught exception as the inner exception on rethrow                |
| MA0110  | Use `[GeneratedRegex]` source generator over `new Regex(...)`               |
| MA0186  | `Equals(object?)` override must use `[NotNullWhen(true)]` on the parameter  |
| MA0191  | Do not use `null!` or `default!` (E128043 covers every other use)            |

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

## Public API Tracking (E128.Analyzers)

`Microsoft.CodeAnalysis.PublicApiAnalyzers` enforces API compatibility via `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`. Inherited override members are excluded via `<NoWarn>RS0016;RS0017</NoWarn>`. The project's `.editorconfig` suppresses `RCS9004` project-wide (`SeparatedSyntaxList<T>.Count` is O(1)).

## Custom Analyzers: E128.Analyzers

`src/E128.Analyzers/` is a solution-local Roslyn analyzer project. It is wired via `Directory.Build.targets` as a `ProjectReference` with `OutputItemType="Analyzer"`. It applies to all projects except the analyzer itself (excluded via `IsRoslynComponent` condition). Severity is governed by `.globalconfig` (blanket error by default).

Rules span categories: Design, Reliability, Performance, Unsafe, Maintainability, Security, Style, Testing, and FileSystem. Most rules ship with a code fix. Several rules carry a diagnostic only, because the remediation is context-specific. Examples are `E128045` Direct Console usage, `E128046` Excessive inheritance, E128125 to E128129, and E128136 to E128138. Every rule from E128175 to E128201 is diagnostic only. So are E128154, E128156, E128160, E128162, and the FIPS rules E128142 to E128151. E128071 and E128075 ship a code fix. See `src/E128.Analyzers/README.md` for the complete rule table, code-fix status, and usage examples.

Redundant E128 rules are disabled in `.globalconfig`, whether or not a code fix ships. See [redundant-rules.md](analyzers/redundant-rules.md).

Key rules by category (not exhaustive):

| Category    | Examples                                                                                           |
| ----------- | -------------------------------------------------------------------------------------------------- |
| Design      | Sealed-by-default, async void, sync-over-async, ConfigureAwait, TimeProvider, DI, ImmutableArray   |
| Reliability | GeneratedRegex safety, DateTime roundtrip, Task.WhenAll, JsonDocument lifetime, pool Rent() guard, stackalloc bounds, pool zeroing, service lifetime (E128154, `TryAdd`, keyed, `typeof` and `AddHostedService` forms, and a singleton `DbContext`), `foreach` mutation (E128155, including array writes and `break` or `return` exits), `TaskCompletionSource` options and shared-source completers (E128159), process drain (E128160, binds start info and helper drains), `Path.Combine` segments (E128161), use after `Dispose` (E128162, direct block statement only), `ImmutableArray` default (E128163, no fix for struct instance fields), `await` in `finally` (E128186), exception filter text read (E128188), unreleased `GCHandle` (E128189), hidden `GetBuffer` (E128192), disposable field overwritten in a loop (E128193), `Lazy<T>` without thread safety (E128194), configuration key absent from the settings files (E128195), `JsonSerializerOptions` written after first use (E128196), fixed day count used as a month (E128197), `Substring` count underflow (E128198) |
| Performance | MinBy/MaxBy, HttpCompletionOption, FrozenSet, O(n²) loops, `SearchValues<char>` (E128102), UTF-8 parsing, alternate lookups (E128131 to E128135), span bulk methods (E128140), u8 literals (E128141), complexity ceiling (E128152), `RemoveAt(0)` in loop (E128153), `SaveChanges` in loop (E128185), uncapped `Parallel.ForEachAsync` (E128187), `FileStream` without async I/O (E128190) |
| Security    | FIPS cryptography rules: unapproved hash names, `System.Random` in security code, managed and CNG type creation, RSA key size, cipher mode, PBKDF2 work factor, TLS versions, deprecated RNG, excluded cipher names, AES-GCM tag size, SHA-3 policy, HMAC key length (E128071, E128075, E128142 to E128151), certificate bypass (E128156, `SslOptions` and `SslStream` too), fixed-time compare (E128157), hardcoded URL (E128164, dot boundary), hardcoded secret (E128165, token, credential and bearer names, `??`, indexers, getter returns, `u8`, connection strings), sensitive log argument (E128166, `LoggerMessage`, wrapped args, whole-word `ssn` and `pwd`, skips `EventId` and `Exception`), PFX import without `EphemeralKeySet` (E128191) |
| Unsafe      | `Unsafe.As` to `Unsafe.BitCast`, unaligned reads, unaligned non-blittable reads (E128139), bounds bypass, bool and int reinterpret, non-blittable `MemoryMarshal`, fixed buffers, `SkipInit`, unpinned pointers, string writes, null byref, delegate lifetime, bulk copy, pointer and length pairs, nonpublic reflection, raw IL, function pointer types (E128112 to E128129, E128136 to E128139, E128200) |
| Style       | string.Empty, Encoding.UTF8, XML doc comments, null-forgiving operator                             |
| Maintainability | Direct recursion (E128199), conditional directive ceiling (E128201)                             |
| Testing     | Temp directory cleanup, stale ReferenceAssemblies, low-value test conditions E128107/E128108       |

The FIPS rule set reports an error and fails the build by default. It has its own page. See [fips-rules.md](analyzers/fips-rules.md) for the policy values and the shared helper.

Individual rule behavior, detection shapes, and exclusions live in [rule-details.md](analyzers/rule-details.md), [rule-details-performance.md](analyzers/rule-details-performance.md), [rule-details-reliability.md](analyzers/rule-details-reliability.md), [rule-details-reliability-concurrency.md](analyzers/rule-details-reliability-concurrency.md), and [rule-details-reliability-resources.md](analyzers/rule-details-reliability-resources.md). Those files record the shapes that `src/E128.Analyzers/README.md` does not state.

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
| VSTHRD200 | Test methods need no Async suffix                         |
| xunit1051 | xUnit v3 ambient CancellationToken too noisy                 |
| MA0006    | Relaxed to suggestion: test LINQ predicates use string `==` |

## ConfigureAwait Scoping (CA2007)

Library code must use `ConfigureAwait(false)`. CA2007 enforces this in DLL projects only.

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

## Package Assembly Layout

The package ships two assemblies. `E128.Analyzers` holds the analyzers and
`E128.Analyzers.CodeFixers` holds the fix providers. See
[Package Layout](analyzers/package-layout.md) for both projects, the
per-Roslyn-version layout, and the build metadata.

## Related

- [Project Structure](project-structure.md)
- [Testing](testing.md)
- [Practices](../practices.md)
