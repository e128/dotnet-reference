# Rule Detail Notes
*Updated: 2026-11-12T01:47:01Z*

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

Rules E128109, E128110, and E128111 cover `stackalloc` limits, pooled buffer zeroing, and use after Return. See [pooled-buffer-rules.md](pooled-buffer-rules.md). Rules E128112 to E128129 come from two published guidance documents. See [catalog.md](../analyzer-candidates/catalog.md) for the ID map and the excluded rows.

### E128152: Complexity ceiling

Reports a loop whose varying nesting depth, or a recursion whose argument shrinks by a constant step, passes the `e128_max_complexity` tier. Tiers run `linear`, `linearithmic`, `quadratic`, `cubic`, `exponential`. Constant-trip loops do not raise the depth, so `for (var i = 0; i < 4; i++)` inside a loop stays linear. A coordinate step, whose trip count depends on the element the enclosing loop produced, adds no depth either. An enclosing `for` loop counts its counter as a coordinate only when the counter indexes the collection the inner loop walks. A direct bound such as `j < i` still adds depth. Recursion that halves its argument stays silent. A cache counts only when the body reads and writes the same field or dictionary local.

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

**Silent**: a path under `Path.GetTempPath()`, a relative string literal, a const
field, a `Path.GetFullPath` over a safe value, a local that holds one of those, an
unknown shape, a non-recursive call, and a delete guarded by a proving
`StartsWith` test on the same path in the same block.

**Reported literals**: a rooted literal, such as `"/etc/nginx"` or `"C:\Data"`.
The rule cannot prove such a directory is disposable. A readonly field, a
get-only property, and a mutable field report, because the value is computed at
run time.

A guard proves the test passed in two shapes, and the comparison must use
`StringComparison.Ordinal` or `OrdinalIgnoreCase` with a root that ends at a
separator boundary. An enclosing `if` holds the call alone or joined by `&&`. A
preceding `if` with no `else` ends in `return`, `throw`, `continue`, or `break`,
and holds the call under one `!` joined by `||`. The guard must be the statement
before the delete, and a write to the guarded symbol voids it. A negated test
around the delete and an `||` condition do not count.

A local alias counts as the value it holds. A `Path.Combine` under a temp path
stays silent when a later segment comes from a variable, because the rule cannot
see a `..` or a rooted value in it.

No code fix. The safe root belongs to the caller.

### E128191: PFX import without `EphemeralKeySet`

Severity: **Warning**. Flags a certificate import whose flags omit
`X509KeyStorageFlags.EphemeralKeySet`. Without the flag, the import persists the
private key in the key store, and the key outlives the process. The flag is not
supported for PFX loading on macOS.

The rule reports two shapes: an `X509Certificate2` constructor with a `fileName`
parameter, and one of the `X509CertificateLoader` methods `LoadPkcs12`,
`LoadPkcs12FromFile`, `LoadPkcs12Collection`, or
`LoadPkcs12CollectionFromFile`. It
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
generic call still matches. The receiver must be `this` or implicit, so a call
through another instance of the same type does not report.

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

### E128202: Unvalidated JWT Claims Used as an Identity

Severity: **Info**. Reports decoded JWT claims that reach an authenticated
identity in `HttpContext.User` or an `AuthenticationTicket`.

`BodyAnalysis` records definitions, writes, additions, sinks, and validation calls.
`Search` follows each source once per local, value kind, and source position.
It checks every decoded token that reaches the sink. Subclasses count too.

Detection follows these sources and uses:

- `ReadJwtToken`, `ReadJsonWebToken`, `ReadToken`, and string token constructors.
- Any member read of a decoded token, such as `Claims`, `Payload`, or `GetClaim`.
  `TryGetClaim`, `TryGetPayloadValue`, and `TryGetHeaderValue` define their `out` local.
- `ClaimsIdentity` constructors with a nonempty or non-constant authentication type,
  or a base identity that is authenticated.
- Claims and identity collections, conditional access, and filtered local claims.
- `AddClaim`, `AddClaims`, `Add`, `AddRange`, `AddIdentity`, and `AddIdentities`.
- `foreach` collection inputs and lambda parameter inputs from the enclosing call.
- Captured locals in lambda and local function bodies. Definitions after the capture count.
- Pattern variables from `is`, `switch` statement, and `switch` expression patterns.
- Conditional and coalesce principals, checked per branch.

Additions after the sink count for an identity, because a principal or ticket
retains identity references. A claim collection is copied when the identity is
built, so only an earlier add reaches it. A local definition hides earlier
definitions when its block contains the use. `??=` and lambda writes to a parent
local count as definitions. Opaque writes stop tracking after their position:
compound assignments, deconstruction, and `ref` arguments.

Validation clears a report only for the same unchanged token:

- A preceding synchronous `ValidateToken` call returns a `ClaimsPrincipal`.
  The call occupies a statement, assignment, or single-variable initializer.
  A `try` block that validates counts when every catch returns or throws.
- An earlier failed `IsValid` check ends in `return`, `throw`, or `yield break`.
  `yield return` does not exit. A `goto` or label in the block stops the scan.
- The sink sits in a true `IsValid` branch or an else branch after a failed check.
  True checks include `== true` and `&&` chains.
- The result local holds one `ValidateTokenAsync` definition, with or without
  `ConfigureAwait`. Both string and decoded `SecurityToken` arguments count.
- A token write between the validation and its check removes the clearance.
  A field or auto-property token clears only for the same receiver and no write.
- `JsonWebTokenHandler.ValidateToken` returns a result, so it needs the same
  unchanged `IsValid` check as the async form.
- Lambdas inherit earlier parent validation and guards.
  Local functions inherit only parent guards that contain their declaration.

A bare `ValidateTokenAsync` call does not clear the report. The caller must
check `IsValid`. An identity with constant null or empty authentication type
is not authenticated by contract.

A definition later in a shared loop reaches the sink on the next iteration,
unless a later definition in that loop replaces it or the loop declares the local.
A token read after the sink needs validation before that read. A token rewritten
after the sink voids a validation that sits outside the loop. The rule skips a
local function that no call or method group reaches. Helper-method flows stay out of scope. A probe found no installed analyzer with this shape.
Sonar S5659 covers JWT.Net only. No code fix applies because validation needs
application policy.

## Related

- [analyzers.md](../analyzers.md): strategy, configuration split, suppression policy
- [rule-details-performance.md](rule-details-performance.md): performance rule details
- [rule-details-reliability.md](rule-details-reliability.md): reliability rule details
- [redundant-rules.md](redundant-rules.md): rules that another analyzer also covers
