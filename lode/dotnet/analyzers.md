# .NET 10 Roslyn Analyzers
*Updated: 2026-11-01T20:55:29Z*

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

Rules span categories: Design, Reliability, Performance, Unsafe, Maintainability, Security, Style, Testing, and FileSystem. Most rules ship with a code fix. Several rules carry a diagnostic only, for example `E128045` Direct Console usage, `E128046` Excessive inheritance, E128125 to E128129, and E128136 to E128138, because the remediation is context-specific. See `src/E128.Analyzers/README.md` for the complete rule table, code-fix status, and usage examples.

Redundant E128 rules are disabled in `.globalconfig`, whether or not a code fix ships. See [redundant-rules.md](analyzers/redundant-rules.md).

Key rules by category (not exhaustive):

| Category    | Examples                                                                                           |
| ----------- | -------------------------------------------------------------------------------------------------- |
| Design      | Sealed-by-default, async void, sync-over-async, ConfigureAwait, TimeProvider, DI, ImmutableArray   |
| Reliability | GeneratedRegex safety, DateTime roundtrip, Task.WhenAll, JsonDocument lifetime, pool Rent() guard, stackalloc bounds, pool zeroing |
| Performance | MinBy/MaxBy, HttpCompletionOption, FrozenSet, O(n²) loops, `SearchValues<char>` (E128102), UTF-8 parsing, alternate lookups (E128131 to E128135), span bulk methods (E128140), u8 literals (E128141) |
| Unsafe      | `Unsafe.As` to `Unsafe.BitCast`, unaligned reads, unaligned non-blittable reads (E128139), bounds bypass, bool and int reinterpret, non-blittable `MemoryMarshal`, fixed buffers, `SkipInit`, unpinned pointers, string writes, null byref, delegate lifetime, bulk copy, pointer and length pairs, nonpublic reflection, raw IL (E128112 to E128129, E128136 to E128139) |
| Style       | string.Empty, Encoding.UTF8, XML doc comments, null-forgiving operator                             |
| Maintainability | See the README rule table                                                                      |
| Testing     | Temp directory cleanup, stale ReferenceAssemblies, low-value test conditions E128107/E128108       |

### E128061: Static readonly array → immutable type

Flags `private static readonly T[]` and `internal static readonly T[]` fields. Arrays are reference types. The `readonly` keyword prevents reassignment but callers can still mutate contents via the indexer. The code fix reads a constant `byte` sequence as a `ReadOnlySpan<byte>` property, which the compiler stores as read-only data. For every other element type it replaces `T[]` with `ImmutableArray<T>` and unwraps `new T[]` / `new[]` initializers to collection expressions.

### E128062: Stale ReferenceAssemblies in tests

Flags `ReferenceAssemblies.Net.Net80` / `Net90` in test code when the configured minimum framework version is higher (default: 100 for net10.0). Configurable via `e128_minimum_framework_version` in `.globalconfig`. Code fix replaces outdated version with the minimum.

### E128064: Disk write-then-read round-trip

Flags file-I/O sequences that write to a path and then immediately read the same path back into memory within the same method body. The recoverable in-memory source should be returned instead of re-reading from disk. This eliminates both the redundant syscall and the TOCTOU race window where another process can modify the file between write and read.

**Coverage tiers**:
- **Tier A**: `File` static API: `WriteAllText`/`ReadAllText`, `WriteAllBytes`/`ReadAllBytes`, `WriteAllLines`/`ReadAllLines`, `AppendAllText`/`AppendAllLines`, plus every `*Async` variant
- **Tier B**: `File.CreateText` / `File.AppendText` / `File.Create` / `File.OpenWrite` followed by `File.ReadAllText`/`ReadAllBytes` (sync or async) on the same path
- **Tier C**: `StreamWriter`/`StreamReader` ctors, `FileStream(path, FileMode.Create|CreateNew|Truncate|Append, ...)` write intent vs. `FileStream(path, FileMode.Open, FileAccess.Read)` read intent, `BinaryWriter`/`BinaryReader` over a `FileStream`
- **Tier D**: `FileInfo` instance methods: `CreateText`/`AppendText`/`Create`/`OpenWrite` paired with `OpenRead`/`OpenText` or `File.ReadAll*(fi.FullName)`
- **Tier E**: Opaque write-to-disk: a local variable assigned from an awaited call whose method name matches `*ToDisk*`, `*ToFile*`, `*SaveTo*`, `*WriteTo*`, or `*DownloadTo*` (case-insensitive), followed by `File.Read*` with a path sourced from a property of that result variable (e.g., `result.HtmlFilePath.FullName` or `result.FilePath`). No code fix is offered (no in-memory source expression); the diagnostic still fires.

**Correlation rules**:
- Path-key normalization (`Ident:`, `Member:`, `OpaqueWriteRoot:`, `Expr:`, `FileInfo:`) so `p`, `this.p`, `fi.FullName`, `result.FilePath`, `fetchResult.HtmlFilePath.FullName`, and `Path.Combine(...)` all match the corresponding read against the same source
- Block-scope linear domination: the write must precede the read on the same execution path; writes inside conditional branches do not match reads outside them
- Path reassignment between write and read (`p = other;`) disqualifies the match
- Stream-writer variables track ctor → `.Write*(...)` calls through to disposal; multi-write disposals (`BinaryWriter`, `FileStream` with multiple `.Write` calls, `AppendAllText`) still flag but attach no `SourceExpression`, so the code fix is skipped rather than producing a wrong rewrite

**Code fix**: replaces the read expression (or whole `await ...` for async reads) with the captured in-memory source expression. Same-kind matches use the source directly; text↔bytes cross-kind matches wrap in `Encoding.UTF8.GetBytes(...)` / `Encoding.UTF8.GetString(...)` and auto-add `using System.Text;`. `BatchFixer` is sufficient because each fix is a local expression replacement.

**Test-project exemption**: `tests/.globalconfig` sets `dotnet_diagnostic.E128064.severity = none`. Fixtures for file-I/O analyzers (E128028, E128056, E128057) legitimately round-trip through disk to exercise the analyzer under test.

### E128063: Mid-name underscore in private static member

Severity: **Error**. Flags private/internal static members whose name contains an underscore at index ≥ 2 (e.g., `Nots_supportedExtensions`, `Creates_enrichmentJsonOptions`, `Spectres_terminal`). These are artifacts of IDE1006 batch-rename operations that mangle identifiers by inserting underscores at word boundaries instead of adjusting capitalization. Excludes: leading underscore (`_foo`), Hungarian prefix (`s_foo`, `m_foo`, `t_foo`), const fields, `op_` operator methods, `__` double-underscore patterns, and compiler-generated property accessors. Code fix uses `SequentialRenameFixAllProvider` (not `BatchFixer`) and removes the mid-name underscore by PascalCasing each segment.

### E128066: Linear lookup in loop (O(n²))

Flags linear-time methods (`.Contains`, `.Any`, `.IndexOf`, `.Remove`, `.Exists`, `.Find`, `.FindAll`, `.FindIndex`) on collection types inside loops (`for`, `foreach`, `while`, `do`) and LINQ lambda callbacks (`.Where`, `.Select`, `.SelectMany`, `.Any`, `.All`, `.First`, `.Count`, `.ForEach`, etc.).

**Exclusions**:
- `System.String` receivers: `string.Contains(substring)` and `string.IndexOf(substring)` are O(n) substring operations, not collection lookups. `System.String` implements `IEnumerable<char>`, which would otherwise cause false positives
- O(1) lookup types: `ISet<T>`, `IReadOnlySet<T>`, `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>`, `FrozenSet<T>`, `FrozenDictionary<K,V>`, `ImmutableHashSet<T>`, `ImmutableDictionary<K,V>`, checked via `SymbolEqualityComparer` against types resolved at `CompilationStart`
- Constant-bound `for` loops (e.g., `for (int i = 0; i < 10; i++)`): literal bound means total work is bounded
- Also detects `.Where(...).Count()` chains (linear scan + count = O(n) per call)

**Code fix**: For `.Contains` and `.Any`, inserts `var xSet = x.ToHashSet();` before the enclosing loop and rewrites the receiver. Other methods lack automatic fixes (remediation is context-specific). Ensures `using System.Linq;` is present.

Uses `RegisterCompilationStartAction` to pre-resolve O(1) type symbols, then `RegisterSyntaxNodeAction` on `InvocationExpression`. Falls back to `CandidateSymbols[0]` when `GetSymbolInfo().Symbol` is null (overloaded methods).

### E128067: String concatenation in loop

Disabled in `.globalconfig` (see [redundant-rules.md](analyzers/redundant-rules.md)). Flags `string +=` inside loops. Each iteration allocates a new string, creating O(n²) total allocations. Registers on `SyntaxKind.AddAssignmentExpression` and checks `SpecialType.System_String`. Code fix is a placeholder (string-to-StringBuilder refactoring is too invasive for automation).

### E128068: Sort in loop (O(n² log n))

Flags `.Sort()` (on `List<T>` or `System.Array`), `.OrderBy()`, `.OrderByDescending()`, `.ThenBy()`, `.ThenByDescending()` (on `System.Linq.Enumerable`) inside loops. Sorting on every iteration is almost always a bug. Sort once before the loop or use a sorted collection. No code fix (remediation is context-specific).

### E128069: List.Insert(0, ...) in loop (O(n²))

Flags `list.Insert(0, ...)` inside loops. Each insert at index 0 shifts all existing elements, making the overall loop O(n²). Verifies the first argument is literal `0` and the containing type is `List<T>`. No code fix. Use `LinkedList<T>.AddFirst` or collect-and-reverse instead.

### E128070: Pool Rent() capacity guard

Flags `Rent(arg)` calls on pool types (`ArrayPool<T>`, `StringBuilderPool`, or any type with "Pool" in the name) where the capacity argument is not bounded. Passing `int.MaxValue` or an unbounded variable causes an OOM crash. Guard with `Math.Min(value, cap)`.

**Detection**: Registers on `InvocationExpression`, checks for method named `Rent` with a single `int` parameter on a type containing "Pool" in the name. Flags when the argument is `int.MaxValue` (literal or constant), a non-constant variable not assigned from `Math.Min`, or any non-safe-literal expression.

**Exclusions**: `Math.Min(value, cap)` as the direct argument or via a local variable. Small constant literals (e.g., `Rent(1024)`).

`PoolRentCapacityGuardCodeFixProvider` wraps the argument in `Math.Min`. The cap comes from the `e128_pool_rent_max_capacity` option, which defaults to 1,048,576.

Rules E128109, E128110, and E128111 cover `stackalloc` limits, pooled buffer zeroing, and use after Return. See [pooled-buffer-rules.md](analyzers/pooled-buffer-rules.md). Rules E128112 to E128129 come from two published guidance documents. See [analyzer-candidates.md](analyzer-candidates.md) for the ID map and the excluded rows.

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

## ConfigureAwait Scoping (CA2007 + E128022)

Library code must use `ConfigureAwait(false)`. Application code must not. Two complementary rules enforce this:

- **CA2007** (DLL projects only): requires `ConfigureAwait(false)` in library code
- **E128022** (Exe/WindowsApplication projects): flags `ConfigureAwait(false)` as unnecessary in app code (console, ASP.NET Core, WPF, WinForms, Worker Services). Excludes Blazor WASM.

```
dotnet_diagnostic.CA2007.severity = error
dotnet_code_quality.CA2007.output_kind = DynamicallyLinkedLibrary
# E128022 inherits error from blanket dotnet_analyzer_diagnostic.severity = error
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