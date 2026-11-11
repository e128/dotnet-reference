# Async Fire-And-Forget Context
*Updated: 2026-11-11T16:24:00Z*

## Problem

A task that nothing observes loses its exception. Four shapes reach that state,
and each was proposed as an E128 rule.

## Decisions

| Decision | Reason |
| -------- | ------ |
| Probe before code | Descriptor text cannot prove which syntax a rule visits |
| Drop the discarded-awaitable rule | MA0134 and VSTHRD110 fire on the same statement |
| Drop the `async`-lambda rule | AsyncFixer03, MA0147, and VSTHRD101 fire on the same lambda |
| Drop the suppressed-throw rule | No owner, and no use of the API in `src/` |
| Drop the dependency-event extension | The open case is a long-lived type that needs no unsubscribe |
| Keep the event rule at E128176 | The disposable case is already reported there |
| Extend `build.sh` for probes | The guardrail blocks raw `dotnet build`, and no script reached `.claude/tmp/` |

## Evidence

- Probe project: `.claude/tmp/async-probe/`. One snippet per shape, one build.
- Probe result table:
  [redundant-rules.md](../../lode/dotnet/analyzers/redundant-rules.md#async-drop-family).
- `EventSubscriptionLeakAnalyzer.OnSymbolStart` returns unless the subscriber is
  `IDisposable`, and it already collects dependency events.
- `AsyncVoidAnalyzer` registers `MethodDeclaration` only, but `VSTHRD101`,
  `MA0147`, and `AsyncFixer03` cover the lambda shape.

## Environment Note

The repository pins SDK 11.0.100, and `dotnet` on `PATH` resolves the Homebrew
10.0.403 install. Put `/usr/local/share/dotnet` first on `PATH` before a build
or a probe.

## Runners-Up

- Rethrow policy rules for the library API. Rejected: library specific.
- A `ConfigureAwait(false)` rule. Rejected: CA2007 owns it.
