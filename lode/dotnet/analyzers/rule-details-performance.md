# Performance Rule Details
*Updated: 2026-11-05T04:39:50Z*

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

Disabled in `.globalconfig` (see [redundant-rules.md](redundant-rules.md)). Flags `string +=` inside loops. Each iteration allocates a new string, creating O(n²) total allocations. Registers on `SyntaxKind.AddAssignmentExpression` and checks `SpecialType.System_String`. Code fix is a placeholder (string-to-StringBuilder refactoring is too invasive for automation).

### E128068: Sort in loop (O(n² log n))

Flags `.Sort()` (on `List<T>` or `System.Array`), `.OrderBy()`, `.OrderByDescending()`, `.ThenBy()`, `.ThenByDescending()` (on `System.Linq.Enumerable`) inside loops. Sorting on every iteration is almost always a bug. Sort once before the loop or use a sorted collection. No code fix (remediation is context-specific).

### E128069: List.Insert(0, ...) in loop (O(n²))

Flags `list.Insert(0, ...)` inside loops. Each insert at index 0 shifts all existing elements, making the overall loop O(n²). Verifies the first argument is literal `0` and the containing type is `List<T>`. No code fix. Use `LinkedList<T>.AddFirst` or collect-and-reverse instead.

E128153 covers `list.RemoveAt(0)` in the same analyzer and with the same shifting cost. Use a `Queue<T>`, a `LinkedList<T>`, or a forward index instead.

### E128180: Entity Framework Core query executed inside a loop

Severity: **Warning**. Flags a materializing Entity Framework Core call inside a
`for`, `foreach`, `while`, or `do` body. Each iteration costs one database round
trip.

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

## Related

- [rule-details.md](rule-details.md): the remaining rule details
- [performance.md](performance.md): analyzer hot-path rules
- [redundant-rules.md](redundant-rules.md): rules disabled because another analyzer covers the shape
