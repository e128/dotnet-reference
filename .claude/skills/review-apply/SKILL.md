---
name: review-apply
description: >
  Batch-apply findings from the most recent --local review. One confirmation, one test run.
when_to_use: "dispatched by review router"
argument-hint: "[--include-low]"
user-invocable: true
---

# Review — Apply Mode

Batch-apply findings from the most recent `--local` review. One confirmation, one test run.

## Phase 1: Load Findings

Load parsed findings from the latest `plans/review-*` dir (`tasks.md` preferred, `context.md` fallback), grouped by file with severity:
```bash
scripts/internal/review-findings.sh --json          # add --include-low to keep LOW findings
```
Each finding has `file`, `line`, `severity`, and `text`. LOW is skipped unless `--include-low`. If the script reports `"plan":null` → "No saved review found. Run `review --local` first."

Additionally skip: needs-verification, advisory-only, timed-out agents, known exceptions (see [references/known-exceptions.md](references/known-exceptions.md)).

## Phase 1.5: Edit Plan

Map each finding → exact file + line + planned edit. Group by file. If >10 files, warn "Large blast radius ({N} files)." Verify accuracy against current code (code may have changed since review).

## Phase 2: Triage

Display confirmation table. Auto-apply CRITICAL + HIGH + MEDIUM. Skip LOW unless `--include-low`.

## Phase 3: Batch Apply

Read all affected files first (parallel Reads), then apply all edits. Group findings by file, apply all in one Edit per file. Hold the test run until every fix is applied.

**Cascade stop rule:** If >5 test failures in files outside the edit plan, stop and report the cascade. Don't chase it.

**AskUserQuestion before:** public method/interface changes, >10 line deletions, test expectation changes.

## Phase 4: Verify

Spawn `build-validator` agent for changed files.

## Phase 4.5: Bounded Reflection Loop

Skip if Phase 4 passed cleanly and all fixes were mechanical. Cap N=2. Self-review → verify → cross-review per iteration.

## Phase 5: Report

```
Applied Findings
================
N findings applied across M files
Targeted tests: {N passed, M failed}
Files modified: ...
Not applied (manual review needed): ...
```

## Rules

- Read every file before editing. Re-read after format.
- One batch, one test run — run tests only after all fixes land.
- Skip LOW by default; skip "needs verification" findings.
- Lode is out of scope — leave lode untouched by review findings.
- Tool-call budget: >100 calls → stop and report progress.

## References

- [references/known-exceptions.md](references/known-exceptions.md)
