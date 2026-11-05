# Reliability Rule Details: Concurrency
*Updated: 2026-11-05T15:20:29Z*

Behavior notes for the reliability rules that cover async and concurrent code.
The complete rule table, the severity, and the code-fix status live in
`src/E128.Analyzers/README.md`. The other reliability rules live in
[rule-details-reliability.md](rule-details-reliability.md).

### E128175: `SemaphoreSlim` wait with no release guarantee

Severity: **Warning**. Flags a `SemaphoreSlim.Wait` or `WaitAsync` call that no
later `try` whose `finally` calls `Release` pairs with. The rule compares the
receiver symbol of the wait and the receiver symbol of the release, so a release
on a different semaphore does not count.

**Exclusions**:

- A timeout overload, because the try-acquire shape holds no permit on the false path.
- A wait inside an enclosing `try` that already releases. That shape over-releases
  when the wait is cancelled, which is a different defect.
- A wait whose receiver is not bound. The buffer does not compile.
- A wait that is not a statement of a block, for example inside an `if` condition.

The scan of the following statements ends at a `return`, `throw`, `goto`, or
`yield` that sits outside a nested lambda. A `break` and a `continue` exit only an
inner loop, so neither ends the scan.

Known gap: a `continue` in a loop body skips a later release, and the rule does
not report it. Reporting the shape would also report a `break` that exits only a
nested loop.

No code fix. The repair moves the wait out of a `try` and wraps the rest of the
block, and the statement range depends on intent.

### E128182: `IAsyncEnumerable` value enumerated twice

Severity: **Warning**. Flags a second enumeration of one `IAsyncEnumerable<T>`
value inside one method body. A second loop can re-run the source, and a
single-pass source such as a channel reader yields nothing the second time.

The rule tracks a local, a parameter, and a field, grouped through
`SymbolEqualityComparer.Default`. An enumeration site is an `await foreach`
statement or a direct `GetAsyncEnumerator()` call, with `WithCancellation`,
`ConfigureAwait`, and parentheses unwrapped. Reporting starts at the second
site.

**Exclusions**:

- An inline-created sequence, which resolves to no tracked symbol.
- Two enumerations in the two branches of one `if`, which never run together.
- An enumeration after an assignment to the same value, which starts a new count.
- A parameter that carries `EnumeratorCancellationAttribute`.
- An iterator body that carries `yield return` or `yield break`.
- A field read on another instance.

The first site travels as an additional location, so the reader can jump to it.

No code fix. Materialize the sequence once, and the rewrite depends on the
source.

### E128183: `Task.WhenAny` result that nothing observes

Severity: **Warning**. Flags a `Task.WhenAny` call whose result is discarded, or
stored in a local that the enclosing block never reads. The returned task never
completes faulted, so a fault in every task in the set stays unobserved.

**Exclusions**:

- An awaited result, which observes the winner.
- A returned, yielded, stored, or passed result. The caller may observe it.
- A result consumed by `Task.WhenAll`.
- A discard assignment, `_ = Task.WhenAny(...)`.
- A declarator outside a block. The rule cannot see the later reads.

The match is on the symbol: the method must sit on
`System.Threading.Tasks.Task`. A user type with its own `WhenAny` stays silent.
The read scan covers the enclosing block only.

No code fix. The repair adds a second await of the winner.

### E128186: `await` inside a `finally` block

Severity: **Warning**. Flags an `await` expression in the body of a `finally`
clause. The awaited call runs while the original exception propagates, so a
fault there replaces the original failure.

The rule registers `SyntaxKind.FinallyClause`. From the reported expression it
walks ancestors. A nested `FinallyClauseSyntax` or `TryStatementSyntax` ends the
walk, so an `await` in a nested `try` stays silent, and an `await` in a nested
`finally` reports once at the inner clause. An `await` reached inside a local
function or a lambda body stays silent, because that body runs on demand.

**Exclusions**:

- An `await` in a `try` block or a `catch` clause.
- An `await` inside a local function or a lambda declared in the `finally` body.
- An `await` inside a nested `try` statement.

No code fix. Flush before the `try`, and dispose synchronously in the `finally`.

### E128187: `Parallel.ForEachAsync` without a degree cap

Severity: **Info**. Flags a `Parallel.ForEachAsync` call that passes no
`ParallelOptions` argument, or an inline `new ParallelOptions` initializer that
sets no `MaxDegreeOfParallelism` member. The call then runs at most
`Environment.ProcessorCount` operations at once, which is the documented default.
An I/O-bound body can need a different cap, so the rule asks for an explicit one.

The rule resolves `System.Threading.Tasks.Parallel` and `ParallelOptions` once.
It reads the initializer syntactically, so it needs no semantic model call.

**Exclusions**:

- An initializer that assigns `MaxDegreeOfParallelism`, whatever the value,
  including `-1` and a computed expression.
- Options held in a variable, a parameter, a field, or a factory call.
- A user type named `Parallel`.
- A compilation without `System.Threading.Tasks.Parallel`.

No code fix. The cap value depends on the workload.

### E128159: `TaskCompletionSource` options and a shared completer

One rule reports two shapes. The first is a source created without
`TaskCreationOptions.RunContinuationsAsynchronously`. The second is a
`SetResult`, `SetException`, or `SetCanceled` call on a source that a field
holds, because a second caller then throws `InvalidOperationException`.

The second shape reads a field receiver only. A local, a parameter, a property,
and a method result stay silent, because the rule cannot prove the source is
shared. `TrySetResult`, `TrySetException`, and `TrySetCanceled` return false
instead of throwing, so the rule never reports them.

Two descriptors share the `E128159` id. The test harness sets
`MarkupOptions = MarkupOptions.UseFirstDescriptor`.

## Related

- [rule-details-reliability.md](rule-details-reliability.md): the other
  reliability rules
- [rule-details.md](rule-details.md): the remaining rule details
- [pooled-buffer-rules.md](pooled-buffer-rules.md): buffer ownership rules
- [redundant-rules.md](redundant-rules.md): rules disabled because another
  analyzer covers the shape
