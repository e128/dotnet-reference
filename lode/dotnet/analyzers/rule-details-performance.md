# Performance Rule Details
*Updated: 2026-11-09T23:44:15Z*

Behavior notes for the performance rules. The complete rule table, the
severity, and the code-fix status live in `src/E128.Analyzers/README.md`. The
remaining rules live in [rule-details.md](rule-details.md). The hot-path rules
for analyzer code itself live in [performance.md](performance.md).

### E128066: Linear lookup in loop (O(n²))

Flags linear-time methods (`.Contains`, `.Any`, `.IndexOf`, `.Remove`, `.Exists`, `.Find`, `.FindAll`, `.FindIndex`) on collection types inside loops. The loops are `for`, `foreach`, `while`, and `do`. It also flags the LINQ lambda callbacks `.Where`, `.Select`, `.SelectMany`, `.Any`, `.All`, `.First`, `.Count`, and `.ForEach`.

**Exclusions**:
- `System.String` receivers: `string.Contains(substring)` and `string.IndexOf(substring)` are O(n) substring operations, not collection lookups. `System.String` implements `IEnumerable<char>`, which would otherwise cause false positives
- O(1) lookup types: `ISet<T>`, `IReadOnlySet<T>`, `IDictionary<K,V>`, `IReadOnlyDictionary<K,V>`, `FrozenSet<T>`, `FrozenDictionary<K,V>`, `ImmutableHashSet<T>`, `ImmutableDictionary<K,V>`, checked via `SymbolEqualityComparer` against types resolved at `CompilationStart`
- An `IQueryable<T>` receiver, because the cost is a database round trip and not an in-memory scan. E128180 owns that shape.
- Constant-bound `for` loops (for example `for (int i = 0; i < 10; i++)`): literal bound means total work is bounded
- Also detects `.Where(...).Count()` chains (linear scan + count = O(n) per call)

**Code fix**: For `.Contains` and `.Any`, inserts `var xSet = x.ToHashSet();` before the enclosing loop and rewrites the receiver. Other methods lack automatic fixes (remediation is context-specific). Ensures `using System.Linq;` is present.

Uses `RegisterCompilationStartAction` to pre-resolve O(1) type symbols, then `RegisterSyntaxNodeAction` on `InvocationExpression`. Falls back to `CandidateSymbols[0]` when `GetSymbolInfo().Symbol` is null (overloaded methods).

### E128067: String concatenation in loop

Overlaps S1643 and SS058 (see [redundant-rules.md](redundant-rules.md)). Flags `string +=` inside loops. Each iteration allocates a new string, creating O(n²) total allocations. Registers on `SyntaxKind.AddAssignmentExpression` and checks `SpecialType.System_String`. Code fix is a placeholder (string-to-StringBuilder refactoring is too invasive for automation).

### E128068: Sort in loop (O(n² log n))

Flags `.Sort()` (on `List<T>` or `System.Array`), `.OrderBy()`, `.OrderByDescending()`, `.ThenBy()`, `.ThenByDescending()` (on `System.Linq.Enumerable`) inside loops. Sorting on every iteration is almost always a bug. Sort once before the loop or use a sorted collection. No code fix (remediation is context-specific).

### E128069: List.Insert(0, ...) in loop (O(n²))

Flags `list.Insert(0, ...)` inside loops. Each insert at index 0 shifts all existing elements, making the overall loop O(n²). Verifies the first argument is literal `0` and the containing type is `List<T>`. No code fix. Use `LinkedList<T>.AddFirst` or collect-and-reverse instead.

E128153 covers `list.RemoveAt(0)` in the same analyzer and with the same shifting cost. Use a `Queue<T>`, a `LinkedList<T>`, or a forward index instead.

### E128180: Entity Framework Core query executed inside a loop

Severity: **Warning**. Flags a materializing Entity Framework Core call inside a
`for`, `foreach`, `while`, or `do` body. Each iteration costs one database round
trip. The call names include `Find`, `Contains`, `Last`, `ElementAt`, `Average`,
`LongCount`, `ToDictionary`, and `ToHashSet`, which E128066 skips on an
`IQueryable<T>` receiver. A `foreach` with a deconstruction pattern counts as a
loop.

`AsEnumerable` and `AsAsyncEnumerable` do not run a query on their own. The rule
reports either one only when it is the source of a `foreach` that runs inside
another loop. A query syntax expression (`from ... select`) is read when a
materializing call consumes it.

**Exclusions**:

- A `foreach` source expression and a `for` initializer, which run once. A
  `while` condition and a `do` condition still report, because they run per
  iteration.
- A receiver that is a `List<T>`, an array, a plain `IEnumerable<T>`, or an
  `IQueryable<T>` chain with no EF root.
- Any compilation without `Microsoft.EntityFrameworkCore.DbSet\`1`.

The receiver walk strips parentheses and casts, then follows a member access or
an invocation receiver back to an EF type.

No code fix. The batch rewrite depends on the query shape.

### E128185: Entity Framework Core `SaveChanges` inside a loop

Severity: **Warning**. Flags a `SaveChanges` or `SaveChangesAsync` call on an
Entity Framework Core context inside a loop body. Each iteration costs one
database round trip per row.

**Exclusions**:

- A call in a `foreach` source expression or a `for` initializer, which runs once.
- A type named `DbContext` outside the EF namespace. The match reads the symbol.
- Any compilation without `Microsoft.EntityFrameworkCore.DbContext`.

A loop that runs both a query and a save reports E128180 and E128185. The two
costs differ: a read round trip and a write round trip.

No code fix. The batch boundary depends on the transaction scope.

### E128190: `FileStream` created without asynchronous I/O

Severity: **Warning**. Flags a `FileStream` construction that cannot run
asynchronous I/O. An async read then blocks a pool thread.

The rule resolves `FileStream` and `FileOptions` once. It reads the constructor
parameters, so positional and named arguments both bind. It reports when
`useAsync` is omitted or constant `false`, and the options argument is omitted
or constant without the `Asynchronous` bit.

**Exclusions**:

- `useAsync: true`.
- An options constant that includes `FileOptions.Asynchronous`.
- A non-constant argument.
- The `SafeFileHandle` overload, which has no asynchronous switch.
- A user type named `FileStream`.
- A `FileStreamOptions` argument, which the rule does not model.

No code fix. The buffer size and the async flag are caller choices.

### E128203: HTTP request inside a loop

Flags an `HttpClient` request call inside a loop body. Methods: `SendAsync`,
`GetAsync`, `PostAsync`, `PutAsync`, `DeleteAsync`, `PatchAsync`. The receiver
must derive from `HttpClient`. The rule does not require an `await`.

Exclusions: a `for` loop with a numeric literal bound, a call in a loop header, a
body that calls `Task.Delay` or `Thread.Sleep` (a retry or poll loop), and a
lambda or local function body.

No code fix. The batch contract is application policy.

### E128204: Database command inside a loop

Flags a `DbCommand` execution inside a loop body. Methods:
`ExecuteNonQueryAsync`, `ExecuteScalarAsync`, `ExecuteReaderAsync`. The receiver
must derive from `DbCommand`. The rule does not require an `await`.

E128093 owns the synchronous method shape, so this rule stays silent on it.
Exclusions: a `for` loop with a numeric literal bound, a call in a loop header,
and a body that calls `Task.Delay` or `Thread.Sleep`.

No code fix. The batch boundary depends on the transaction scope.

### E128205: Collection filled in a loop with a known count

Flags a `List<T>`, `Dictionary<K,V>`, `HashSet<T>`, `Queue<T>`, or `Stack<T>`
created with no capacity, when the next statement fills it in a loop with a
known count.

The count comes from the `foreach` source or from a `Count` or `Length` bound in
a `for` condition. The source expression must be a chain of names, so the fix
never runs a call twice. The loop must be the next statement in the same block.

Exclusions: a loop with a `break` that exits this loop, a local that the loop
never fills through its own fill method (`Add`, `Enqueue`, or `Push`), and a
capacity set in an object initializer. A `SortedSet<T>` or
`SortedDictionary<K,V>` stays silent, because neither has an `(int)` constructor.

The code fix passes the count to the constructor. The analyzer passes the count
text in the diagnostic property `CapacityExpression`, so the fix reads no second
analysis.

### E128206: Empty loop body

Flags a `while` or `do` loop with an empty body. The loop spins a core until
another thread changes the condition.

The rule stays silent when the condition calls a method, because a loop such as
`while (reader.Read())` does its work in the condition.

No code fix. The signal type is a caller decision.

### E128207: Reflection lookup inside a loop

Flags a `System.Type` member lookup inside a loop body. Methods: `GetProperty`,
`GetMethod`, `GetField`, `GetMember`, `GetConstructor`, `GetEvent`,
`GetNestedType`, `GetInterface`.

A lookup that references a loop variable stays silent. So does a lookup with a
call in its receiver or argument, and a lookup in a body-declared local. The
value changes per iteration, so a hoist would be wrong. E128090 owns test
methods.

Exclusions: a `for` loop with a numeric literal bound.

No code fix. The hoisted declaration needs a caller-chosen name.

### E128208: `ContainsKey` then indexer on a `ConcurrentDictionary`

Flags `map.ContainsKey(key)` as the condition of an `if` that then reads
`map[key]`. The pair does two lookups and races with a concurrent writer that
removes the key between them.

The rule matches the receiver and the key by symbol, or by constant value for a
literal key. It reads `ConcurrentDictionary<TKey,TValue>` only. CA1854 covers
`Dictionary<K,V>`. An indexer write stays silent, because `TryGetValue` is not
the remedy for a write.

No code fix. The replacement depends on the `if` body and the fallback value.

### E128209: `GetBytes` length inside a loop

Flags `Encoding.GetBytes(value).Length` inside a loop body. The call allocates a
byte array only to read its length. `GetByteCount` returns the same number with
no allocation.

The receiver must derive from `Encoding`. The rule reads only the direct
`.Length` use. A byte array that the code uses for anything else stays silent.

The code fix rewrites the member access to `GetByteCount` with the same argument
list.

### E128210: `new Regex` inside a loop

Flags a `Regex` construction inside a loop body. Each iteration compiles the
pattern.

The rule reads a constant and a non-constant pattern. The creation must not
reference a value that changes per iteration, because a hoist is then wrong. A
local declared in the loop body and a call in the pattern both count as
per-iteration values.

No code fix. The hoisted field name and the `GeneratedRegex` shape are caller
choices.

Tier 2 rules E128211 to E128217 live in
[rule-details-performance-tier2.md](rule-details-performance-tier2.md).

## Related

- [rule-details.md](rule-details.md): the remaining rule details
- [performance.md](performance.md): analyzer hot-path rules
- [redundant-rules.md](redundant-rules.md): rules that another analyzer also covers
