# Analyzer Fix Infrastructure Context

*Updated: 2026-10-31T22:20:00Z*

## Target

| Field | Value                                             |
| ----- | ------------------------------------------------- |
| Base  | `main` at the start of the session                |
| Head  | uncommitted working tree                          |
| Scope | `src/E128.Analyzers`, the build metadata, the tests |

## Measurements

Counts are as of 2026-10-31. Recount them before a new decision cites them.

| Technique               | Fix providers |
| ----------------------- | ------------- |
| `DocumentEditor`        | 2 of 116      |
| `Formatter.Annotation`  | 9 of 116      |
| `SyntaxGenerator`       | 0 of 116      |
| `Simplifier` annotation | 0 of 116      |
| Speculative bind        | 0 of 116      |
| `IOperation`            | 1 of 116      |

## Current State

- The analyzer and the fix providers share one assembly, which references
  Workspaces and suppresses RS1038.
- Every fix provider returns a `FixAllProvider`. Twenty-two return null and the
  rest use `BatchFixer`, except the rename fixes.
- `EnforceExtendedAnalyzerRules` is on. The artifacts output layout is on. The
  test runner is Microsoft Testing Platform. The Roslyn package is 5.9.
- A fix provider test compiles its `FixedCode`, so a tested shape cannot emit a
  compiler error. An untested shape can.

## Decisions

- Repair an unsafe fix before adopting a technique. Three fixes were unsafe.
- Keep a repaired fix minimal. Do not restructure a provider in the same change
  that repairs it.
- Defer `DocumentEditor`, the assembly split, and the banned-symbol file. Each
  one changes many files or adds a package.
