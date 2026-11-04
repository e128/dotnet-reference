# Rule Detail Notes
*Updated: 2026-11-04T13:43:18Z*

Behavior notes for individual E128 rules. The complete rule table, the
severity, and the code-fix status live in `src/E128.Analyzers/README.md`. This
file records the detection shape and the exclusion that the code does not state
on its own. [analyzers.md](../analyzers.md) holds the analyzer strategy and the
configuration split.

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
- **Tier E**: Opaque write-to-disk. A local variable takes the result of an awaited call whose name matches `*ToDisk*`, `*ToFile*`, `*SaveTo*`, `*WriteTo*`, or `*DownloadTo*`, in any case. A `File.Read*` call follows, with a path taken from a property of that result, such as `result.HtmlFilePath.FullName` or `result.FilePath`. No code fix is offered, because no in-memory source expression exists. The diagnostic still fires.

**Correlation rules**:
- Path-key normalization (`Ident:`, `Member:`, `OpaqueWriteRoot:`, `Expr:`, `FileInfo:`) so `p`, `this.p`, `fi.FullName`, `result.FilePath`, `fetchResult.HtmlFilePath.FullName`, and `Path.Combine(...)` all match the corresponding read against the same source
- Block-scope linear domination: the write must precede the read on the same execution path. A write inside a conditional branch does not match a read outside it
- Path reassignment between write and read (`p = other;`) disqualifies the match
- Stream-writer variables track the constructor call and the `.Write*(...)` calls through to disposal. A multi-write disposal still reports but attaches no `SourceExpression`, so the code fix is skipped rather than writing a wrong rewrite. Examples are `BinaryWriter`, a `FileStream` with several `.Write` calls, and `AppendAllText`.

**Code fix**: replaces the read expression, or the whole `await ...` for an async read, with the captured in-memory source expression. A same-kind match uses the source directly. A text to bytes match wraps in `Encoding.UTF8.GetBytes(...)`. A bytes to text match wraps in `Encoding.UTF8.GetString(...)`. The fix adds `using System.Text;`. `BatchFixer` is sufficient, because each fix is a local expression replacement.

**Test-project exemption**: `tests/.globalconfig` sets `dotnet_diagnostic.E128064.severity = none`. Fixtures for file-I/O analyzers (E128028, E128056, E128057) legitimately round-trip through disk to exercise the analyzer under test.

### E128063: Mid-name underscore in private static member

Severity: **Error**. Flags private/internal static members whose name contains an underscore at index ≥ 2 (for example `Nots_supportedExtensions`, `Creates_enrichmentJsonOptions`, `Spectres_terminal`). These are artifacts of IDE1006 batch-rename operations that mangle identifiers by inserting underscores at word boundaries instead of adjusting capitalization. Excludes: leading underscore (`_foo`), Hungarian prefix (`s_foo`, `m_foo`, `t_foo`), const fields, `op_` operator methods, `__` double-underscore patterns, and compiler-generated property accessors. Code fix uses `SequentialRenameFixAllProvider` (not `BatchFixer`) and removes the mid-name underscore by PascalCasing each segment.

### E128066: Linear lookup in loop (O(n²))

Flags linear-time methods (`.Contains`, `.Any`, `.IndexOf`, `.Remove`, `.Exists`, `.Find`, `.FindAll`, `.FindIndex`) on collection types inside loops. The loops are `for`, `foreach`, `while`, and `do`. It also flags the LINQ lambda callbacks `.Where`, `.Select`, `.SelectMany`, `.Any`, `.All`, `.First`, `.Count`, and `.ForEach`.

**Exclusions**:
- `System.String` receivers: `string.Contains(substring)` and `string.IndexOf(substring)` are O(n) substring operations, not collection lookups. `System.String` implements `IEnumerable<char>`, which would otherwise cause false positives
- O(1) lookup types: `ISet<T>`, `IReadOnlySet<T>`, `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>`, `FrozenSet<T>`, `FrozenDictionary<K,V>`, `ImmutableHashSet<T>`, `ImmutableDictionary<K,V>`, checked via `SymbolEqualityComparer` against types resolved at `CompilationStart`
- Constant-bound `for` loops (for example `for (int i = 0; i < 10; i++)`): literal bound means total work is bounded
- Also detects `.Where(...).Count()` chains (linear scan + count = O(n) per call)

**Code fix**: For `.Contains` and `.Any`, inserts `var xSet = x.ToHashSet();` before the enclosing loop and rewrites the receiver. Other methods lack automatic fixes (remediation is context-specific). Ensures `using System.Linq;` is present.

Uses `RegisterCompilationStartAction` to pre-resolve O(1) type symbols, then `RegisterSyntaxNodeAction` on `InvocationExpression`. Falls back to `CandidateSymbols[0]` when `GetSymbolInfo().Symbol` is null (overloaded methods).

### E128067: String concatenation in loop

Disabled in `.globalconfig` (see [redundant-rules.md](redundant-rules.md)). Flags `string +=` inside loops. Each iteration allocates a new string, creating O(n²) total allocations. Registers on `SyntaxKind.AddAssignmentExpression` and checks `SpecialType.System_String`. Code fix is a placeholder (string-to-StringBuilder refactoring is too invasive for automation).

### E128068: Sort in loop (O(n² log n))

Flags `.Sort()` (on `List<T>` or `System.Array`), `.OrderBy()`, `.OrderByDescending()`, `.ThenBy()`, `.ThenByDescending()` (on `System.Linq.Enumerable`) inside loops. Sorting on every iteration is almost always a bug. Sort once before the loop or use a sorted collection. No code fix (remediation is context-specific).

### E128069: List.Insert(0, ...) in loop (O(n²))

Flags `list.Insert(0, ...)` inside loops. Each insert at index 0 shifts all existing elements, making the overall loop O(n²). Verifies the first argument is literal `0` and the containing type is `List<T>`. No code fix. Use `LinkedList<T>.AddFirst` or collect-and-reverse instead.

E128153 covers `list.RemoveAt(0)` in the same analyzer and with the same shifting cost. Use a `Queue<T>`, a `LinkedList<T>`, or a forward index instead.

### E128152: Complexity ceiling

Reports a loop whose varying nesting depth, or a recursion whose argument shrinks by a constant step, passes the `e128_max_complexity` tier. Tiers run `linear`, `linearithmic`, `quadratic`, `cubic`, `exponential`. Constant-trip loops do not raise the depth, so `for (var i = 0; i < 4; i++)` inside a loop stays linear. Recursion that halves its argument stays silent. A cache counts only when the body reads and writes the same field or dictionary local.

Two code actions serve this rule. `ComplexityCeilingSuppressionCodeFixProvider` wraps the reported loop, or the reported recursive method, in a `#pragma warning disable E128152` pair carrying a `// Justification:` comment that E128047 accepts. `ComplexityCeilingMembershipScanCodeFixProvider` rewrites a nested equality scan as a set lookup. It offers the rewrite only when the outer loop is top level and sits in a statement list. The inner body must be one `==` guard that returns on a match without reading the inner variable. The elements must compare by value without a floating point type. The method must never mention the inner collection outside that loop.

### E128070: Pool Rent() capacity guard

Flags `Rent(arg)` calls on pool types (`ArrayPool<T>`, `StringBuilderPool`, or any type with "Pool" in the name) where the capacity argument is not bounded. Passing `int.MaxValue` or an unbounded variable causes an OOM crash. Guard with `Math.Min(value, cap)`.

**Detection**: Registers on `InvocationExpression`, checks for method named `Rent` with a single `int` parameter on a type containing "Pool" in the name. Flags when the argument is `int.MaxValue` (literal or constant), a non-constant variable not assigned from `Math.Min`, or any non-safe-literal expression.

**Exclusions**: `Math.Min(value, cap)` as the direct argument or via a local variable. Small constant literals (for example `Rent(1024)`).

`PoolRentCapacityGuardCodeFixProvider` wraps the argument in `Math.Min`. The cap comes from the `e128_pool_rent_max_capacity` option, which defaults to 1,048,576.

Rules E128109, E128110, and E128111 cover `stackalloc` limits, pooled buffer zeroing, and use after Return. See [pooled-buffer-rules.md](pooled-buffer-rules.md). Rules E128112 to E128129 come from two published guidance documents. See [analyzer-candidates.md](../analyzer-candidates.md) for the ID map and the excluded rows.

## Related

- [analyzers.md](../analyzers.md): strategy, configuration split, suppression policy
- [performance.md](performance.md): analyzer hot-path rules
- [redundant-rules.md](redundant-rules.md): rules disabled because another analyzer covers the shape
