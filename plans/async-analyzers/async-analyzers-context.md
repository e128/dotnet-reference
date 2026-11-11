# Async Fire-And-Forget Context
*Updated: 2026-11-11T15:41:28Z*

## Problem

A task that nothing observes loses its exception. The runtime stores the
exception on the task, and the garbage collector reclaims it without a trace.
The application then continues with partial state and no log line.

Four shapes reach that state:

1. A statement drops the returned task.
2. An `async` lambda converts to a `void`-returning delegate.
3. A suppressed-throw await drops the exception by design, with no fault check.
4. A subscriber to an injected dependency event can outlive its own usefulness
   and leaks.

## Evidence

- Source library: `TheCodeTraveler/AsyncAwaitBestPractices`. It ships
  `SafeFireAndForget`, `AsyncCommand`, and `WeakEventManager`. It ships no
  analyzers, so each rule below is an inference from its documented guidance.
- In-repo gap 1: `AsyncVoidAnalyzer` registers `MethodDeclaration` only. An
  `async` lambda never reports.
- In-repo gap 2: `EventSubscriptionLeakAnalyzer` collects a static event and an
  own-instance event. A dependency event on a constructor-assigned field stays
  open.
- In-repo gap 3: no rule or package option in the repository names
  `ConfigureAwaitOptions.SuppressThrowing`.
- Compiler coverage: CS4014 reports a dropped awaitable inside an `async`
  method only. A dropped task in a constructor, a synchronous method, or a
  top-level statement compiles clean.

## Decisions

| Decision | Reason |
| -------- | ------ |
| Probe before code | The installed rule sets overlap in the async area, and the overlap is unverified |
| Extend E128176 instead of a new ID | The defect family is the same, and one owner avoids a double report |
| No code fix on any step | Every repair depends on handler policy, dispose ownership, or a fault branch |
| Report a `ValueTask` drop in the same rule as a `Task` drop | The defect is identical, and a second rule would split the reader's attention |
| Keep the event rule at medium noise and off by default in the root config | A dependency event subscription is often correct |

## Runners-Up

- A rule for `shouldAlwaysRethrowException`. Rejected: library API specific.
- A rule for `onException` that itself drops a task. Rejected: the general
  dropped-awaitable rule covers it as a positive snippet.
- A rule that requires `ConfigureAwait(false)` in library code. Rejected:
  CA2007 already owns it.

## Open Questions

- Does `VSTHRD100` or `S3168` report the lambda shape, not only the method
  shape? Phase 0 decides.
- Does the .NET 11 runtime async work change the analyzer view of a method
  body? Check `lode/dotnet/net11-upgrade.md` before Phase 1.

## Related

- [async-analyzers-plan.md](async-analyzers-plan.md)
- [async-analyzers-tasks.md](async-analyzers-tasks.md)
