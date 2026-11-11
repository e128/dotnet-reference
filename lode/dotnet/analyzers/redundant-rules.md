# Overlapping E128 Rules
*Updated: 2026-11-11T16:29:38Z*

An E128 rule overlaps when an installed third-party rule fires on the same code. Every overlapping rule is enabled at `error` in `.globalconfig`, so one construct can report two diagnostics. A fix for either diagnostic clears both.

This file keeps its old name so existing links resolve. Until 2026-11-09 the eight rules below were set to `none` as redundant.

| Overlapping rule | Also reported by         |
| ---------------- | ------------------------ |
| E128008          | VSTHRD002, SS034, MA0042 |
| E128020          | RCS1242                  |
| E128067          | S1643, SS058             |
| E128075          | CA5394, S2245            |
| E128088          | IDE0010, SS018, SS019    |
| E128096          | VSTHRD002, SS034         |
| E128162          | IDISP016                 |
| E128193          | IDISP003                 |

E128162 and E128193 were probed against IDisposableAnalyzers. On the probe
snippet, IDISP016 fired on the `Dispose()` line and E128162 fired on the read
that follows. IDISP003 fired on the same assignment line as E128193.

E128007 (async void) overlaps S3168 and VSTHRD100. Probe method: build a scratch project with one snippet per rule, then compare which diagnostic IDs fire on the same line. `scripts/build.sh --project` accepts a project name or a path to a `.csproj` file, so a scratch project under `.claude/tmp/` builds through the same script.

## Async Drop Family

Probe run 2026-11-11 in `.claude/tmp/async-probe/` against every installed rule
set. Every shape that the async candidate rules targeted has an owner, so no
E128 rule covers this family.

| Shape                                                | Fires                        |
| ---------------------------------------------------- | ---------------------------- |
| A statement drops a `Task` returned from a call       | MA0134, VSTHRD110            |
| A statement drops a `ValueTask` returned from a call  | MA0134, VSTHRD110            |
| An `async` lambda assigns to `Action`                 | AsyncFixer03, MA0147, VSTHRD101 |
| An `async` lambda assigns to `EventHandler`           | AsyncFixer03, MA0147, VSTHRD101 |
| An `async` lambda assigns to `Func<Task>` (control)   | none                         |
| `await` over `ConfigureAwait(SuppressThrowing)`       | none                         |
| Dependency event subscription (control)               | none                         |

`VSTHRD100` did not fire on the lambda shapes. It reports a method body only.
The drop shapes report at every statement level, not only inside an `async`
method, so the compiler rule CS4014 adds no coverage.

Related: [analyzers.md](../analyzers.md), [release-tracking.md](../release-tracking.md).
