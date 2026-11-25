# .NET 11 Roslyn Analyzers
*Updated: 2026-11-25T22:13:23Z*

## Strategy: Deny by Default

All analyzer diagnostics default to `error` via `.globalconfig` (`dotnet_analyzer_diagnostic.severity = error`). A rule stays below `error` only for a recorded reason. See § Rules Not Enabled.

A bulk key reaches only a rule that is enabled by default. `.globalconfig` therefore names every default-off rule of every installed package with its own `error` line. The CA rules need no line because `AnalysisMode = All` enables them. CA1707 is the exception: it carries an explicit `error` line, because `AnalysisMode = All` gives it `warning` severity only.

## Configuration Split

Per [Microsoft guidance](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/code-style-rule-options):

| File                       | Purpose                                          | Scope                  |
| -------------------------- | ------------------------------------------------ | ---------------------- |
| `.editorconfig`            | Code style, formatting, naming (canonical home)  | All projects           |
| `.globalconfig`            | Analyzer diagnostic severities and enabling only | All projects           |
| `tests/.globalconfig`      | Test-specific severity overrides                 | Test projects only     |
| `benchmarks/.globalconfig` | Benchmark-specific severity overrides            | Benchmark project only |

### Rationale

**`.editorconfig` is the canonical style/formatting home.** It supports both `dotnet_style_*`/`csharp_style_*` preferences and inline severity hints (e.g., `dotnet_style_coalesce_expression = true:error`). IDEs (Visual Studio, Rider, VS Code) read it to apply settings immediately while editing. It also supports `dotnet_diagnostic.*` entries if needed, but this repo keeps diagnostic severities in `.globalconfig` for separation of concerns.

**`.globalconfig` is purely for analyzer configuration.** It handles rule severity (`dotnet_diagnostic.*`) and enabling (`roslynator_analyzers.enabled_by_default`). It does NOT support editor style settings like indentation, whitespace, or formatting preferences. Put those in `.editorconfig`.

**Inline severity vs `dotnet_diagnostic.*`.** Style rules in `.editorconfig` carry the `:error` suffix on the option value (e.g., `csharp_style_var_for_built_in_types = true:error`). An option suffix of `:silent` or `:suggestion` beats the bulk key. The `.editorconfig` uses `:silent` on the expression-bodied member options, on `csharp_style_prefer_primary_constructors`, and on `csharp_style_prefer_top_level_statements`. It uses `:suggestion` on `csharp_style_prefer_range_operator`. IDE0290 and IDE0210 are off. The `.globalconfig` lists explicit `dotnet_diagnostic.IDE*` entries only for guardrails and for rules that stay off. That avoids silent drift when rule IDs change. The seven `*_experimental` blank-line options (IDE2000 to IDE2006) are set to `true`, which allows the construct and silences the rule. The `tests/.globalconfig` and `benchmarks/.globalconfig` use `global_level = 101` to override the root `global_level = 100`.

## MSBuild Properties

In `Directory.Build.props`:

```xml
<AnalysisLevel>latest-all</AnalysisLevel>
<AnalysisMode>All</AnalysisMode>
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

Key interactions:
- `AnalysisLevel=latest-all` and `AnalysisMode=All` both select mode `All` in every category. The AI SDLC Readier policy for the security, reliability, and performance categories needs no separate category line. Do not set `AnalysisMode` below `All`, because the CA rules that are off in `Recommended` would stop running.
- `EnforceCodeStyleInBuild` makes IDE rules (IDE*) fire during `dotnet build`
- `TreatWarningsAsErrors` promotes all enabled warnings to errors

## Third-Party Analyzer Packages

Declared in `Directory.Build.props` with `PrivateAssets="all"` (zero runtime impact). Versions pinned in `Directory.Packages.props`.

| Package                                    | Prefix  | Focus                               |
| ------------------------------------------ | ------- | ----------------------------------- |
| AsyncFixer                                 | AF*     | Async/await anti-patterns           |
| IDisposableAnalyzers                       | IDISP*  | Resource ownership and disposal     |
| Meziantou.Analyzer                         | MA*     | Code quality, performance, security |
| Microsoft.CodeAnalysis.CSharp.CodeStyle    | IDE*    | Code style rules in the build       |
| Microsoft.VisualStudio.Threading.Analyzers | VSTHRD* | Threading correctness               |
| Roslynator.Analyzers                       | RCS*    | Code style + quality                |
| Roslynator.CodeAnalysis.Analyzers          | RCS*    | Advanced code analysis              |
| Roslynator.Formatting.Analyzers            | RCS*    | Formatting consistency              |
| SharpSource                                | SS*     | Common pitfalls                     |
| SonarAnalyzer.CSharp                       | S*      | Security + reliability              |

IDisposableAnalyzers enables every rule by default. The blanket
`dotnet_analyzer_diagnostic.severity = error` promotes each one to an error,
including the `Info` default of IDISP014. Two E128 rules overlap an IDISP rule,
and both stay enabled. See [redundant-rules.md](analyzers/redundant-rules.md).
The package sits outside the maintenance window of
[dependency-policy.md](../dependency-policy.md). The project keeps it as an
explicit exception.

## Suppression Policy

- Never use `#pragma warning disable` or `[SuppressMessage]` without user approval
- Never use the null-forgiving operator `!`: `E128043` (every use) and `MA0191` (only `null!` and `default!`) enforce this as error. Test projects relax both for guard-clause assertions
- Editorconfig severity downgrades require justification
- Test project relaxations go in `tests/.globalconfig`, not inline suppressions. Each one resolves a conflict with xUnit or with the test naming convention. See § Common Test Overrides.

## Key Meziantou Rules Enabled as Error

The root `.globalconfig` sets every Meziantou rule to `error`, including each rule that is off by default. The `.globalconfig` carries no setup-owned Meziantou block. Put an override for a test or benchmark project in a `global_level = 101` file.

After a Meziantou version change, refresh the rule inventory from the restored package. Compare `configuration/all-errors.editorconfig` between the old and new versions. That file lists the rules the version ships. The bulk key does not reach a default-off rule, so give every new default-off rule an explicit `error` line. Then run the full build and test. See § Strategy: Deny by Default.

The table below is aligned with Meziantou's [comparison table](https://github.com/meziantou/Meziantou.Analyzer/blob/main/docs/comparison-with-other-analyzers.md):

| Rule    | Guardrail                                                                   |
| ------- | --------------------------------------------------------------------------- |
| MA0036  | Make class static (distinct from CA1822 which only covers methods)          |
| MA0054  | Embed the caught exception as the inner exception on rethrow                |
| MA0110  | Use `[GeneratedRegex]` source generator over `new Regex(...)`               |
| MA0186  | `Equals(object?)` override must use `[NotNullWhen(true)]` on the parameter  |
| MA0191  | Do not use `null!` or `default!` (E128043 covers every other use)            |

## Netstandard2.0 Nullable Polyfill (E128.Analyzers)

The analyzer project targets `netstandard2.0` (required for Roslyn analyzers) which lacks `System.Diagnostics.CodeAnalysis.NotNullWhenAttribute` and `MaybeNullWhenAttribute`. Rather than hand-rolling a polyfill (which trips MA0048 file-name rules and IDE0130 namespace-mismatch), the project references **PolySharp** as a source generator. `E128.Analyzers.CodeFixers` does the same and lists only `NotNullWhenAttribute`. `E128.Analyzers.Union` references no PolySharp. The example shows the `E128.Analyzers` entry:

```xml
<PackageReference Include="PolySharp" PrivateAssets="all" />
<PropertyGroup>
  <PolySharpIncludeGeneratedTypes>
    System.Diagnostics.CodeAnalysis.NotNullWhenAttribute
  </PolySharpIncludeGeneratedTypes>
</PropertyGroup>
```

`PrivateAssets="all"` keeps the generator out of the NuGet package. `PolySharpIncludeGeneratedTypes` scopes generation to only the attributes the project actually uses, avoiding duplicate-type conflicts with consumer projects that target modern TFMs.

## Public API Tracking (E128.Analyzers)

`Microsoft.CodeAnalysis.PublicApiAnalyzers` enforces API compatibility via `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`. RS0016 and RS0017 are `none` in the root `.globalconfig`, so no `PublicAPI` entries are required. The project carries no `NoWarn` for either. The `NoWarn` list holds RS2007, NU5128, and RSEXPERIMENTAL006. The project's `.editorconfig` sets `RCS9004` to `none` (`SeparatedSyntaxList<T>.Count` is O(1), and the rule conflicts with RCS1080).

## Custom Analyzers: E128.Analyzers

`src/E128.Analyzers/` is a solution-local Roslyn analyzer project. `Directory.Build.targets` adds it as a `ProjectReference` with `OutputItemType="Analyzer"`. A second `ProjectReference` wires in `E128.Analyzers.Union` the same way. Both apply to every project except the three analyzer projects (each sets `IsRoslynComponent`, which the condition tests). Severity is governed by `.globalconfig` (blanket error by default).

Rules span categories: Design, Reliability, Performance, Unsafe, Maintainability, Security, Style, Testing, and FileSystem. Most rules ship with a code fix. Several rules carry a diagnostic only, because the remediation is context-specific. Examples are `E128045` Direct Console usage, `E128046` Excessive inheritance, E128125 to E128129, and E128136 to E128138. Every rule from E128175 to E128217 is diagnostic only except E128205 and E128209, which ship a code fix. The rules E128154, E128156, and E128162 are diagnostic only. So are the FIPS rules E128142 to E128151. E128071 and E128075 ship a code fix. See `src/E128.Analyzers/README.md` for the complete rule table, code-fix status, and usage examples.

E128 rules that overlap a third-party rule stay enabled in `.globalconfig`. One construct can report two diagnostics. See [redundant-rules.md](analyzers/redundant-rules.md). Only E128107 and E128108 stay at `suggestion`, because their code fix deletes tests.

Key rules by category (not exhaustive):

| Category    | Examples                                                                                           |
| ----------- | -------------------------------------------------------------------------------------------------- |
| Design      | Sealed-by-default, async void, sync-over-async, ConfigureAwait, TimeProvider, DI, ImmutableArray, abstract base with members is closed (E128225) |
| Reliability | GeneratedRegex safety, DateTime roundtrip, Task.WhenAll, JsonDocument lifetime, pool Rent() guard, stackalloc bounds, pool zeroing, service lifetime (E128154, `TryAdd`, keyed, `typeof` and `AddHostedService` forms, and a singleton `DbContext`), `foreach` mutation (E128155, including array writes and `break` or `return` exits), `TaskCompletionSource` options and shared-source completers (E128159), hand-rolled process capture (E128223, redirect, read, then wait), `Path.Combine` segments (E128161), use after `Dispose` (E128162, direct block statement only), `ImmutableArray` default (E128163, no fix for struct instance fields), `await` in `finally` (E128186), exception filter text read (E128188), unreleased `GCHandle` (E128189), hidden `GetBuffer` (E128192), disposable field overwritten in a loop (E128193), `Lazy<T>` without thread safety (E128194), configuration key absent from the settings files (E128195), `JsonSerializerOptions` written after first use (E128196), fixed day count used as a month (E128197), `Substring` count underflow (E128198) |
| Performance | MinBy/MaxBy, HttpCompletionOption, FrozenSet, O(n²) loops, `SearchValues<char>` (E128102), UTF-8 parsing (E128131), culture-aware comparer (E128132), alternate lookups (E128133), bit count and span sum loops (E128134, E128135), span bulk methods (E128140), element-wise `Add` and `Multiply` span loops (E128255), u8 literals (E128141), complexity ceiling (E128152), `RemoveAt(0)` in loop (E128153), `SaveChanges` in loop (E128185), uncapped `Parallel.ForEachAsync` (E128187), `FileStream` without async I/O (E128190), HTTP and database round trips in a loop (E128203, E128204), collection presize (E128205), empty spin loop (E128206), reflection lookup in a loop (E128207), concurrent dictionary double lookup (E128208), `GetBytes` length in a loop (E128209), `new Regex` in a loop (E128210), `Task.Delay` in a loop without a token (E128211), `ToCharArray` copy (E128212), `Activator` in a loop (E128213), handler without decompression (E128214), `StringBuilder` capacity (E128215), unbounded channel (E128216), large struct by value (E128217), read-only `MemoryStream` (E128224), comparer set filled by a loop (E128226), middleware `Func<Task>` next delegate (E128229), static files through the runtime pipeline (E128230), split result enumerated without a stored array (E128231) |
| Security    | FIPS cryptography rules: unapproved hash names, `System.Random` in security code, managed and CNG type creation, RSA key size, cipher mode, PBKDF2 work factor, TLS versions, deprecated RNG, excluded cipher names, AES-GCM tag size, SHA-3 policy, HMAC key length (E128071, E128075, E128142 to E128151), certificate bypass (E128156, `SslOptions` and `SslStream` too), fixed-time compare (E128157), hardcoded URL (E128164, dot boundary), hardcoded secret (E128165, token, credential and bearer names, `??`, indexers, getter returns, `u8`, connection strings), sensitive log argument (E128166, `LoggerMessage`, wrapped args, whole-word `ssn` and `pwd`, skips `EventId` and `Exception`), PFX import without `EphemeralKeySet` (E128191), unvalidated JWT claims used as an identity (E128202), token validation flag pinned false (E128228) |
| Unsafe      | `Unsafe.As` to `Unsafe.BitCast`, unaligned reads, unaligned non-blittable reads (E128139), bounds bypass, bool and int reinterpret, non-blittable `MemoryMarshal`, fixed buffers, `SkipInit`, unpinned pointers, string writes, null byref, delegate lifetime, bulk copy, pointer and length pairs, nonpublic reflection, static readonly reflection write (E128254), raw IL, function pointer types (E128112 to E128115, E128118 to E128120, E128125 to E128129, E128136 to E128139, E128200, E128254) |
| Style       | string.Empty, Encoding.UTF8, XML doc comments, null-forgiving operator                             |
| Maintainability | Direct recursion (E128199), conditional directive ceiling (E128201)                           |
| Testing     | Temp directory cleanup, stale ReferenceAssemblies, low-value test conditions E128107/E128108       |

The FIPS rule set reports an error and fails the build by default. It has its own page. See [fips-rules.md](analyzers/fips-rules.md) for the policy values and the shared helper.

Individual rule behavior, detection shapes, and exclusions live in [rule-details.md](analyzers/rule-details.md), [rule-details-performance.md](analyzers/rule-details-performance.md), [rule-details-reliability.md](analyzers/rule-details-reliability.md), [rule-details-reliability-concurrency.md](analyzers/rule-details-reliability-concurrency.md), and [rule-details-reliability-resources.md](analyzers/rule-details-reliability-resources.md). Those files record the shapes that `src/E128.Analyzers/README.md` does not state.

## Common Test Overrides

Rules relaxed in test projects (via `tests/.globalconfig` at `global_level=101`). Each row resolves a conflict. Every other rule runs at `error` in tests.

| Rule     | Reason                                                              |
| -------- | ------------------------------------------------------------------- |
| CA2007, MA0004, VSTHRD111, xUnit1030 | `ConfigureAwait` rules stay off everywhere. The root `.globalconfig` owns the policy. |
| CA1707   | Test names use the `Method_Condition_Result` form                   |
| S100     | Same naming form                                                    |
| RCS1046  | Same naming form: test names carry no `Async` suffix               |
| S4018    | Test runner helpers take explicit type arguments                    |
| E128043  | Guard-clause tests pass `null!` on purpose                          |
| MA0191   | Same guard-clause tests                                             |
| E128097  | The test suite uses explanatory comments by convention              |
| E128227  | Tests read a fixed response from an in-process app or a loopback container |

The same file sets `e128_minimum_framework_version = 100`. The `E128.Analyzers.Tests` project also keeps `RS1036`, `RS1038`, `RS1041`, `RS2007`, `RS2008` and `S2699` in `NoWarn`, with a reason beside each in the csproj. `tests/E128.Analyzers.Tests/.globalconfig` sets `none` for E128107 and E128108 at `global_level = 102`.

CA1707 stays at `error` in every project outside these test directories. E128063 (mid-name underscore) needs no override, because it skips a test compilation through `XunitTestHelper.IsTestCompilation`. The shipped package carries no analyzer configuration file, so the repository severity policy never reaches a consumer.

## ConfigureAwait Rules

Every `ConfigureAwait` rule stays off by maintainer policy. No rule requires or forbids `ConfigureAwait`. The bulk `dotnet_analyzer_diagnostic.severity = error` key reaches every rule that is enabled by default. Each `ConfigureAwait` rule therefore needs its own `none` entry in the root `.globalconfig`:

```
dotnet_diagnostic.CA2007.severity = none
dotnet_diagnostic.MA0004.severity = none
dotnet_diagnostic.RCS1090.severity = none
dotnet_diagnostic.S3216.severity = none
dotnet_diagnostic.VSTHRD111.severity = none
dotnet_diagnostic.xUnit1030.severity = none
```

Never enable one of these rules. Never change source to satisfy one. Add a `none` entry for any equivalent rule that a new analyzer package brings.

## Roslynator Required Options

Some Roslynator rules need a style option. A rule without its option reports ROS0003 at error. `.editorconfig` sets one `roslynator_*` key for each such rule, under "Roslynator Options". Add the key in the same change that enables a new Roslynator rule.

## Rules Not Enabled

Every rule runs at `error` except the rules below. Each one conflicts with another enforced rule, needs a file or option the repo lacks, has a destructive fix, or is a maintainer decision. `.globalconfig` carries a reason beside each line.

| Rule                  | State        | Reason                                                       |
| --------------------- | ------------ | ------------------------------------------------------------ |
| CA2007, MA0004, RCS1090, S3216, VSTHRD111, xUnit1030 | `none` | Maintainer policy. See ConfigureAwait Rules. |
| MA0174                | `none`       | Opposite of MA0175                                           |
| MA0177                | `none`       | Opposite of MA0211                                           |
| MA0214                | `none`       | Opposite of MA0215 and RCS1174                               |
| RCS1002, RCS1004      | `none`       | Opposite of the braces rules                                 |
| RCS1231               | `none`       | E128019 covers it                                            |
| RCS1016               | `none`       | One body style for every member kind. IDE0021 to IDE0027 set it per kind. |
| IDE0048, RCS1123      | `none`       | Oscillate with IDE0046                                       |
| IDE0290               | `none`       | Primary constructors conflict with the immutability order    |
| IDE0210               | `none`       | Top-level statements fix is destructive in format            |
| S1451                 | `none`       | Needs a `headerFormat` option the repo lacks                 |
| RS0051 to RS0058      | off          | Need `InternalAPI` files                                     |
| RS1020                | off          | Needs `AnalyzerCategories.txt`                               |
| CS1591, RCS0044, RS0017, MA0007, RS1007, RCS0036, S3990, S3992, RCS0027, RCS1223, RS0016, RCS0055, MA0181, S3242, MA0003, RS1015, RCS0062, RS1028, RCS1141, RCS1181, RCS0012, S1067, S3267, RCS0053, RCS0061, S126, RCS0056, MA0045, RCS1146, S3358, S1227, S1200, S4055, S4017, S103, S1192, RCS0054, SS039, S1301, RCS0063, IDE0066, RCS0028, RCS0013, RCS0008, RCS1056, IDE0046, RCS1250, IDE0045, S3240, RCS1211, RCS1111, RCS1031, S4040, RCS1014, RCS1050, MA0136, RCS1238, MA0071 | `none` | Maintainer decision. Some also duplicate or oppose another rule, and RCS1056 throws AD0001. |
| RCS9004               | `none`       | Conflicts with RCS1080. Set in the two analyzer-project `.editorconfig` files only. |
| E128107, E128108      | `suggestion` | The code fix deletes tests. `tests/E128.Analyzers.Tests/.globalconfig` sets `none`. |

The benchmarks project also sets CA1515 to `none`, because BenchmarkDotNet needs public types. It sets E128227 to `none` too, because a benchmark reads a fixed response from its own in-process app.

## Source Generators

`[GeneratedRegex]` must use partial **properties** (not partial methods) per MA0190:

```csharp
[GeneratedRegex(@"...", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
private static partial Regex MyRegex { get; }
```

## Package Assembly Layout

The package ships three assemblies. `E128.Analyzers` holds the base analyzers,
`E128.Analyzers.CodeFixers` holds the fix providers, and
`E128.Analyzers.Union` holds the two union rules at Roslyn 5.12. See
[Package Layout](analyzers/package-layout.md) for the projects, the
per-Roslyn-version folders, and the build metadata.

## Related

- [Project Structure](project-structure.md)
- [Testing](testing.md)
- [Practices](../practices.md)
