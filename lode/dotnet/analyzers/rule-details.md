# Rule Detail Notes
*Updated: 2026-11-05T14:14:46Z*

Behavior notes for individual E128 rules. The complete rule table, the
severity, and the code-fix status live in `src/E128.Analyzers/README.md`. This
file records the detection shape and the exclusion that the code does not state
on its own. [analyzers.md](../analyzers.md) holds the analyzer strategy and the
configuration split.

Rules from the performance categories live in
[rule-details-performance.md](rule-details-performance.md). Rules from the
reliability category live in
[rule-details-reliability.md](rule-details-reliability.md).

### E128061: Static readonly array → immutable type

Flags `private static readonly T[]` and `internal static readonly T[]` fields. Arrays are reference types. The `readonly` keyword prevents reassignment but callers can still mutate contents via the indexer. The code fix reads a constant `byte` sequence as a `ReadOnlySpan<byte>` property, which the compiler stores as read-only data. For every other element type it replaces `T[]` with `ImmutableArray<T>` and unwraps `new T[]` / `new[]` initializers to collection expressions.

### E128062: Stale ReferenceAssemblies in tests

Flags `ReferenceAssemblies.Net.Net80` / `Net90` in test code when the configured minimum framework version is higher (default: 100 for net10.0). Configurable via `e128_minimum_framework_version` in `.globalconfig`. Code fix replaces outdated version with the minimum.

### E128063: Mid-name underscore in private static member

Severity: **Error**. Flags private/internal static members whose name contains an underscore at index ≥ 2 (for example `Nots_supportedExtensions`, `Creates_enrichmentJsonOptions`, `Spectres_terminal`). These are artifacts of IDE1006 batch-rename operations that mangle identifiers by inserting underscores at word boundaries instead of adjusting capitalization. Excludes: leading underscore (`_foo`), Hungarian prefix (`s_foo`, `m_foo`, `t_foo`), const fields, `op_` operator methods, `__` double-underscore patterns, and compiler-generated property accessors. Code fix uses `SequentialRenameFixAllProvider` (not `BatchFixer`) and removes the mid-name underscore by PascalCasing each segment.

### E128064: Disk write-then-read round-trip

Flags file-I/O sequences that write to a path and then immediately read the same path back into memory within the same method body. The recoverable in-memory source should be returned instead of re-reading from disk. This eliminates both the redundant syscall and the TOCTOU race window where another process can modify the file between write and read.

**Coverage tiers**:
- **Tier A**: `File` static API: `WriteAllText`/`ReadAllText`, `WriteAllBytes`/`ReadAllBytes`, `WriteAllLines`/`ReadAllLines`, `AppendAllText`/`AppendAllLines`, plus every `*Async` variant
- **Tier B**: `File.CreateText` / `File.AppendText` / `File.Create` / `File.OpenWrite` followed by `File.ReadAllText`/`ReadAllBytes` (sync or async) on the same path
- **Tier C**: `StreamWriter`/`StreamReader` ctors, `FileStream(path, FileMode.Create|CreateNew|Truncate|Append, ...)` write intent vs. `FileStream(path, FileMode.Open, FileAccess.Read)` read intent, `BinaryWriter`/`BinaryReader` over a `FileStream`
- **Tier D**: `FileInfo` instance methods: `CreateText`/`AppendText`/`Create`/`OpenWrite` paired with `OpenRead`/`OpenText` or `File.ReadAll*(fi.FullName)`
- **Tier E**: Opaque write-to-disk. A local variable takes the result of an awaited call whose name matches `*ToDisk*`, `*ToFile*`, `*SaveTo*`, `*WriteTo*`, or `*DownloadTo*`, in any case. A `File.Read*` call follows, with a path taken from a property of that result, such as `result.HtmlFilePath.FullName` or `result.FilePath`. No code fix is offered, because no in-memory source expression exists. The diagnostic still fires.

**Correlation rules**:
- Path-key normalization (`Ident:`, `Member:`, `OpaqueWriteRoot:`, `Expr:`, `FileInfo:`) so `p`, `this.p`, `fi.FullName`, `result.FilePath`, `fetchResult.HtmlFilePath.FullName`, and `Path.Combine(...)` all match the corresponding read against the same source
- Block-scope linear domination: the write must precede the read on the same execution path. A write inside a conditional branch does not match a read outside it
- Path reassignment between write and read (`p = other;`) disqualifies the match
- Stream-writer variables track the constructor call and the `.Write*(...)` calls through to disposal. A multi-write disposal still reports but attaches no `SourceExpression`, so the code fix is skipped rather than writing a wrong rewrite. Examples are `BinaryWriter`, a `FileStream` with several `.Write` calls, and `AppendAllText`.

**Code fix**: replaces the read expression, or the whole `await ...` for an async read, with the captured in-memory source expression. A same-kind match uses the source directly. A text to bytes match wraps in `Encoding.UTF8.GetBytes(...)`. A bytes to text match wraps in `Encoding.UTF8.GetString(...)`. The fix adds `using System.Text;`. `BatchFixer` is sufficient, because each fix is a local expression replacement.

**Test-project exemption**: `tests/.globalconfig` sets `dotnet_diagnostic.E128064.severity = none`. Fixtures for file-I/O analyzers (E128028, E128056, E128057) legitimately round-trip through disk to exercise the analyzer under test.

### E128070: Pool Rent() capacity guard

Flags `Rent(arg)` calls on pool types (`ArrayPool<T>`, `StringBuilderPool`, or any type with "Pool" in the name) where the capacity argument is not bounded. Passing `int.MaxValue` or an unbounded variable causes an OOM crash. Guard with `Math.Min(value, cap)`.

**Detection**: Registers on `InvocationExpression`, checks for method named `Rent` with a single `int` parameter on a type containing "Pool" in the name. Flags when the argument is `int.MaxValue` (literal or constant), a non-constant variable not assigned from `Math.Min`, or any non-safe-literal expression.

**Exclusions**: `Math.Min(value, cap)` as the direct argument or via a local variable. Small constant literals (for example `Rent(1024)`).

`PoolRentCapacityGuardCodeFixProvider` wraps the argument in `Math.Min`. The cap comes from the `e128_pool_rent_max_capacity` option, which defaults to 1,048,576.

Rules E128109, E128110, and E128111 cover `stackalloc` limits, pooled buffer zeroing, and use after Return. See [pooled-buffer-rules.md](pooled-buffer-rules.md). Rules E128112 to E128129 come from two published guidance documents. See [analyzer-candidates.md](../analyzer-candidates.md) for the ID map and the excluded rows.

### E128152: Complexity ceiling

Reports a loop whose varying nesting depth, or a recursion whose argument shrinks by a constant step, passes the `e128_max_complexity` tier. Tiers run `linear`, `linearithmic`, `quadratic`, `cubic`, `exponential`. Constant-trip loops do not raise the depth, so `for (var i = 0; i < 4; i++)` inside a loop stays linear. Recursion that halves its argument stays silent. A cache counts only when the body reads and writes the same field or dictionary local.

Two code actions serve this rule. `ComplexityCeilingSuppressionCodeFixProvider` wraps the reported loop, or the reported recursive method, in a `#pragma warning disable E128152` pair carrying a `// Justification:` comment that E128047 accepts. `ComplexityCeilingMembershipScanCodeFixProvider` rewrites a nested equality scan as a set lookup. It offers the rewrite only when the outer loop is top level and sits in a statement list. The inner body must be one `==` guard that returns on a match without reading the inner variable. The elements must compare by value without a floating point type. The method must never mention the inner collection outside that loop.

### E128179: Options type bound from configuration with no validation

Severity: **Warning**. Flags an options registration that reads configuration and
carries no validation call in the same chain.

**Exclusions**:

- A bare `services.AddOptions<T>()` with no bind call. The registration reads no
  configuration source.
- The `services.Configure<T>(Action<T>)` overload. Only an overload whose last
  parameter is `Microsoft.Extensions.Configuration.IConfiguration` counts.
- `PostConfigure`, which is not a registration.
- A chain that carries `Validate`, `ValidateDataAnnotations`, `ValidateOnStart`,
  `ValidateEagerly`, or `ValidateAllProperties`.

The rule matches the registration on `OptionsServiceCollectionExtensions`, the
bind on `OptionsBuilderConfigurationExtensions`, and the validation on
`OptionsBuilder<T>` or its extension types. A user type with the same method
name never triggers it.

The analysis reads the outermost call of a fluent chain. A trailing validation
call is therefore visible from the registration the rule reports.

No code fix. The validation choice depends on which properties are required.

### E128184: Recursive delete outside a proven safe root

Severity: **Info**. Flags a recursive `Directory.Delete` or
`DirectoryInfo.Delete` whose path the rule cannot prove safe. A false positive on
a build gate blocks work for no gain, so the severity stays low and the reported
set stays narrow.

**Reported**: a parameter, a field read, a property read, and a `Path.Combine`
whose first argument is one of those.

**Silent**: a path under `Path.GetTempPath()`, a string literal, a const field, a
`Path.GetFullPath` over a safe value, an unknown shape, a non-recursive call, and
a delete guarded by a `StartsWith` test on the same path in the same block.

No code fix. The safe root belongs to the caller.

### E128191: PFX import without `EphemeralKeySet`

Severity: **Warning**. Flags a certificate import whose flags omit
`X509KeyStorageFlags.EphemeralKeySet`. Without the flag, Windows writes the
private key into the machine key store.

The rule reports two shapes: an `X509Certificate2` constructor with a `fileName`
parameter, and `X509CertificateLoader.LoadPkcs12` or `LoadPkcs12FromFile`. It
reads the flags parameter from the constructor or method symbol, so positional
and named arguments both bind. It reads the `EphemeralKeySet` constant from the
enum symbol.

**Exclusions**:

- A constant that includes `EphemeralKeySet`.
- A non-constant flags value.
- A byte array or `ReadOnlySpan<byte>` constructor.
- A constructor with no flags parameter.
- A user type that shadows a BCL name.

No code fix. The flag set is a caller policy.

### E128199: Method calls itself directly

Severity: **Info**. Flags a method whose operation block holds an invocation of
the same method. The rule compares `OriginalDefinition`, so a constructed
generic call still matches.

The rule reports a local function or a lambda that calls the outer method,
because that call re-enters the method. It reports one diagnostic per method.

**Silent**: a call to another method, an overload on another type, and two
methods that never call themselves. Mutual recursion stays out of scope,
because it needs a compilation-wide call graph.

No code fix. The iterative replacement depends on the method contract.

### E128200: Do not declare a function pointer type

Severity: **Warning**. Flags every `SyntaxKind.FunctionPointerType` node. The
managed and the `unmanaged` forms both report. The node appears in a field, a
parameter, a return type, a local, and a cast.

S6640 reports the enclosing unsafe block only. This rule names the pointer type.

No code fix. The delegate signature depends on the call site.

### E128201: Conditional directives past the ceiling

Severity: **Info**. Counts `#if` directives per source tree and reports the
first directive past the ceiling. The option `e128_max_conditional_directives`
sets the ceiling and defaults to 4. A missing, non-numeric, or negative value
falls back to the default. `#elif` does not add to the count.

No code fix. Splitting a build variant is a project decision.

## Related

- [analyzers.md](../analyzers.md): strategy, configuration split, suppression policy
- [rule-details-performance.md](rule-details-performance.md): performance rule details
- [rule-details-reliability.md](rule-details-reliability.md): reliability rule details
- [redundant-rules.md](redundant-rules.md): rules disabled because another analyzer covers the shape
