# Roadmap Rule Details: E128232 to E128237
*Updated: 2026-11-22T04:00:12Z*

Detection shape, exclusions, and code fix behavior for the six rules that
shipped from `plans/roadmap.md`. The rule table and message text live in
`src/E128.Analyzers/README.md`. The register rows moved to
[analyzer-candidates/catalog.md](../analyzer-candidates/catalog.md).

## E128232: Dynamic Keyword Ban

Reports an identifier named `dynamic` in a type position. The keyword defeats
compile-time checking, trimming, and AOT.

- Detection: `IdentifierNameSyntax` with the name `dynamic`, gated by
  `SyntaxFacts.IsInNamespaceOrTypeContext`. No parent-kind list.
- Excludes a `QualifiedName` or `AliasQualifiedName` parent, and a using
  directive with no alias.
- A semantic gate compares the bound type against
  `Compilation.DynamicType`, so a user type, alias, namespace, or variable
  named `dynamic` stays silent.
- No code fix. A replacement type carries application intent.

## E128233: Parallel For Over A Per-Item Body

Reports the three-argument `Parallel.For(from, to, body)` whose lambda holds
no loop and no await. The runtime invokes one delegate per index.

- Overload check: exactly 3 parameters over an int or long range, with an
  `Action<T>` or `Action<T, ParallelLoopState>` body, bound to the framework
  `Parallel` by symbol.
- Body: a simple or parenthesized lambda with the matching parameter count, or
  a method group that returns void and takes the matching parameters.
- An async body reports with the `Parallel.ForEachAsync` remedy and no fix.
- Expression bodies report only statement expressions, so the fix can place
  the expression in a statement position.
- Silent when the body mutates the parameter (assignment, increment, ref or
  out pass), because the fix makes it the loop variable.
- Silent when the body uses the name `range`, when the receiver is a
  using-static bare `For`, and on the `For<TLocal>` overload and a user `For`.
- A named argument maps to its parameter, so the call still reports and fixes.
- The fix for a method group invokes it inside the generated loop, with a loop
  variable name that does not collide with a symbol in scope.
- The fix uses the range type for the loop variable and keeps a
  `ParallelLoopState` parameter on the generated lambda.
- Code fix: rewrites to `Parallel.ForEach(Partitioner.Create(from, to),
  range => ...)`, keeps the receiver text, and inserts
  `using System.Collections.Concurrent;` when absent. Declines when a foreign
  symbol named `Partitioner` is in scope. The scope check compares the
  containing namespace, not a metadata name lookup.

## E128234: FileStream Captured By A Parallel Body

Reports a `FileStream` local, parameter, or field referenced inside the body
of a parallel call. One report per invocation, anchored at the first use.

- Gates on both `Parallel` and `FileStream` symbols at compilation start.
- Parallel calls in scope: the static `For`, `ForEach`, and `Invoke` members.
  `ForEachAsync` stays silent.
- A symbol whose declaration sits inside the body is a per-worker stream, so
  it stays silent.
- A derived `FileStream` type reports, through the shared base-type walk.
- No code fix. The remedy moves the read helper to `File.OpenHandle` and
  `RandomAccess.Read` at an explicit offset, and that change crosses a helper
  boundary.

## E128235: Readonly Field Re-Read Inside A Loop

Reports the outermost loop that reads a `readonly` array field of the
enclosing type once per iteration. One report per loop cluster.

- Region per iteration: the `for` condition, incrementors, and body, the
  `while` and `do` condition and body, and the `foreach` body only. The `for`
  initializer and the `foreach` source run once, so they stay out.
- The walk enters nested lambdas, because a captured field still reloads. It
  stops at local function bodies, which run on call.
- Silent on a mutable field, a non-array readonly field, a field of another
  type, any write to the field in the region (a constructor can assign a
  readonly field mid-loop), a `nameof(field)` in the region, an embedded
  statement with no enclosing block, and a field name with no free derived
  local name.
- One field per diagnostic. A loop reading two qualifying fields reports the
  first, then re-reports after that fix.
- Code fix: inserts one local before the loop, typed from the field, and
  rewrites only the references inside that loop. The local name derives from
  the field name and falls back to a suffix on a collision. Fix-all reserves
  sibling names so one snapshot emits no duplicate locals.

## E128236: Unconditional Delay In A Test

Reports a delay call inside an xUnit test method: `Thread.Sleep`, static
`SpinWait.SpinUntil`, or a `Task.Delay` over a literal duration with no
CancellationToken argument.

- Name gate before the symbol bind, per the bind-late cost rule.
- Literal duration: an integer literal, or `TimeSpan.FromMilliseconds` and
  the other time factories over a numeric literal, bound to `System.TimeSpan`.
- A delay inside any loop stays silent, because E128211 owns the loop case.
- Silent on a token argument, a computed duration, a non-test method, a user
  type that owns `Sleep` or `Delay`, and an unbound call in incomplete code.
- NUnit and MSTest methods stay silent in the first release.
- No code fix. The signal the test should await carries application intent.

## E128237: Test Disabled Outside The xUnit Skip Form

Reports a test disabled by a foreign mechanism. E128168 owns the xUnit `Skip`
argument, and this rule stays disjoint from it.

Part A, the `[Ignore]` attribute:

- Test attributes in scope: xUnit `Fact` and `Theory`, NUnit `Test` and
  `TestCase`, MSTest `TestMethod` and `DataTestMethod`, resolved with
  `MetadataTypes.Find` so an ambiguous reference still registers.
- Ignore attributes: NUnit and MSTest `IgnoreAttribute`, gated on
  compilation presence.
- A first positional reason that matches `e128_tracked_ref_pattern` stays
  silent. No reason reports. A non-constant reason stays silent, matching the
  E128168 read of a non-constant `Skip`.
- Report anchored on the `Ignore` attribute node.

Part B, a test inside `#if false`:

- Walks `DescendantTrivia` in document order and collects only under a
  condition that is the literal `false` token. A symbol condition such as
  `DEBUG` evaluates false when the symbol is undefined, so the literal check
  keeps it undecidable and silent. Note that
  `IfDirectiveTriviaSyntax.ConditionValue` is a plain bool, `False` for an
  undefined symbol, so it cannot distinguish the two.
- An `#else` or `#elif` branch at collection depth 1 ends the collection,
  because that branch is active or condition dependent. A nested directive
  at a deeper depth keeps the collection.
- Disabled text parses wrapped in a type, because the text holds class
  members without their enclosing type and a bare parse yields no method
  declaration. Each span maps back past the wrapper prefix.
- No code fix. Re-enabling a test needs intent the tree does not carry.

## Related

- [analyzer-candidates/catalog.md](../analyzer-candidates/catalog.md): the
  settled rows for these IDs
- [analyzer-candidates/slopwatch-sweep.md](../analyzer-candidates/slopwatch-sweep.md):
  the source read for E128232, E128236, and E128237
- [rule-details-reliability.md](rule-details-reliability.md): the resource
  lifetime rules, which stay disjoint from E128234
