# Async Fire-And-Forget Analyzer Plan
*Updated: 2026-11-11T15:41:28Z*

Ship three analyzers and one extension that cover the unobserved-task defect
family described in
[lode/dotnet/analyzer-candidates-async.md](../../lode/dotnet/analyzer-candidates-async.md).
A probe phase runs first, because the coverage claim against the installed rule
sets is unverified.

## Scope

| Step | Rule | Shape | Category | ID |
| ---- | ---- | ----- | -------- | -- |
| 1 | Discarded awaitable | A statement drops a `Task` or `ValueTask` outside an `async` method | E128-Reliability | E128223 |
| 2 | `async` lambda to void delegate | An `async` anonymous function converts to a `void`-returning delegate | E128-Design | E128224 |
| 3 | Suppressed-throw await | `ConfigureAwaitOptions.SuppressThrowing` with no fault check | E128-Reliability | E128225 |
| 4 | Dependency event subscription | Extends `EventSubscriptionLeakAnalyzer` | E128-Reliability | E128176 |

The IDs continue after E128222, the last assigned value in
`src/E128.Analyzers/AnalyzerReleases.Unshipped.md`. Step 4 takes no new ID,
because E128176 already owns the event-lifetime defect.

## Success Criteria

- Each step passes `scripts/check.sh` and adds test classes that cover one
  positive snippet and one negative snippet per exclusion.
- The probe result for each step is recorded in
  `redundant-rules.md` and in `lode/dotnet/analyzer-candidates-async.md`.
- Step 4 reports no diagnostic on the existing E128176 test corpus, so the two
  descriptors do not collide.
- The analyzer README table and `AnalyzerReleases.Unshipped.md` list every new
  rule.

## Phases

### Phase 0: Probe

Build one scratch project under `.claude/tmp/async-probe/` with the installed
rule sets. Confirm which of `VSTHRD100`, `VSTHRD110`, `S3168`, `CA2012`, and
`MA0040` fire on the candidate snippets. Drop a candidate that already has an
owner. Record the result before any code is written.

### Phase 1: E128223, discarded awaitable

Register `ExpressionStatement` and resolve the expression type once through
`RegisterCompilationStartAction`. Report only when the type is
`System.Threading.Tasks.Task`, `Task<T>`, or `ValueTask`, and the enclosing
member is not `async`. Exclusions are listed in the candidates file.

### Phase 2: E128224, async lambda to void delegate

Register `AnonymousFunctionExpression`. Read the converted type with
`GetTypeInfo`. Report when the function carries the `async` modifier and the
converted type returns `void`. Add the event-handler case as a positive, since
an `async` handler lambda has the same defect.

### Phase 3: E128225, suppressed-throw await

Register `AwaitExpression`. Match a `ConfigureAwait` call whose argument
initializes `ConfigureAwaitOptions` with the `SuppressThrowing` flag. Scan the
enclosing block for a later read of `IsFaulted` or `IsCanceled` on the same
receiver.

### Phase 4: E128176 extension

Add a second descriptor to `EventSubscriptionLeakAnalyzer`. Collect `+=` on an
event whose receiver is a constructor-assigned field, and report when the type
removes nothing and implements no dispose interface. Keep the descriptor text
distinct, so a reader knows which lifetime rule fired.

### Phase 5: Docs and release

Update `src/E128.Analyzers/README.md`, `AnalyzerReleases.Unshipped.md`, the
rule-detail lode files, and close the candidate entries.

## Out Of Scope

- Code fixes. Each repair depends on local policy, and a wrong fix ships a
  silent defect.
- A rule for `.Result` and `.Wait()`: E128008 owns it.
- A rule for the library's own APIs. This repository does not reference
  `AsyncAwaitBestPractices`.

## Risks

| Risk | Response |
| ---- | -------- |
| A third-party rule already reports step 1 or 2 | Phase 0 drops the step before code is written |
| Step 1 is noisy on a project that uses a scheduler helper | Exempt an argument that a helper method already consumes |
| Step 4 collides with the existing E128176 descriptor | Run the existing test file after the change |
| Runtime async in .NET 11 changes the awaitable type shape | Check `lode/dotnet/net11-upgrade.md` before Phase 1 |

## Related

- [async-analyzers-context.md](async-analyzers-context.md): evidence and
  decisions
- [async-analyzers-tasks.md](async-analyzers-tasks.md): the task checklist
