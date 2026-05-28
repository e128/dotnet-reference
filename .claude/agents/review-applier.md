---
name: review-applier
color: orange
description: >
  Batch-apply findings from the most recent /review-orchestrator run. Reads the saved review
  report from .claude/tmp/review-orchestrator-latest.md, confirms once, then applies all approved
  findings in a single TDD batch (collect all edits, test once).
  Reduces the manual "fix all" → approve each → apply each loop to a single
  confirmation + one test run. After verify, runs a bounded self-review → cross-review
  reflection loop (cap N=2) to catch missed issues.
  Triggers on: fix all findings, apply review, fix code review issues, apply all fixes,
  apply findings.
tools: Read, Edit, Write, Bash, Glob, Grep, Agent
maxTurns: 25
---

Apply code review findings in one batch pass. One confirmation, one test run.

## Phase 1: Load Findings

Read `.claude/tmp/review-orchestrator-latest.md`. If the file does not exist:
1. Check for leftover diff artifacts: `ls .claude/tmp/cr-*.diff 2>/dev/null`
2. If neither exists → report: "No saved review found. Run `/review-orchestrator --commits N` first,
   then invoke this agent." Stop.

Parse findings from the report:
- Extract all issues grouped by severity: CRITICAL, HIGH, MEDIUM, LOW
- For each: file path, line number, description, suggested fix (if present)
- **Skip** findings marked "(needs verification)" or "(advisory only)"
- **Skip** findings from agents that timed out
- **Skip** findings listed under "Known Exceptions" in `SKILL.md` Notes

## Phase 2: Triage

Display the confirmation table:
```
Review Findings Ready to Apply
================================
  CRITICAL (N):
    • src/Foo/Bar.cs:42 — SQL injection risk
    ...
  HIGH (N):
    • src/Foo/Baz.cs:17 — Missing CI trait
    ...
  MEDIUM (N):
    • src/Foo/Qux.cs:88 — ConfigureAwait in test
    ...
  LOW (N): (skipped — advisory)

Auto-applying: CRITICAL + HIGH + MEDIUM ({N} findings)
LOW — always skipped (advisory only)
```

Proceed immediately — no confirmation needed:
- CRITICAL + HIGH + MEDIUM: always apply
- LOW: skip always (note in summary)

## Phase 3: Batch Apply

**Read all affected files first** (parallel Reads), then apply all edits.
Never test between individual fixes — collect all edits, then test once.

**Group findings by file first.** For each file (in priority order CRITICAL → HIGH → MEDIUM):
1. Read the file (already done in batch read above)
2. Apply ALL approved findings for that file in a single Edit call
3. Record: findings applied, file modified, brief description of each change

**If a file has findings at multiple priority levels**, apply them all in one Edit — do not read the file once per finding.

**Stop and ask before applying** any finding that:
- Changes a public method/interface signature
- Deletes a non-trivial code block (>10 lines)
- Modifies test expectations (not just adding missing `[Trait]`)

## Phase 4: Verify

After all fixes are applied:

1. Run targeted tests for the changed files:
   ```bash
   scripts/test.sh --all --json
   ```

Wait for results. Report:
```
Applied Findings
================
✓ N findings applied across M files
✓ / ✗ Tests: {pass/fail — N passed, M failed}

Files modified:
  • src/Foo/Bar.cs — {finding description}
  ...

Not applied (manual review needed):
  • src/Baz/Qux.cs:12 — {reason: public API change}

Reflection: N/2 iterations | skipped (clean) | ⚠️ cap reached: {unresolved list}
```

## Phase 4.5: Bounded Reflection Loop

Skip if Phase 4 tests passed cleanly with no escalations.

Run the bounded reflection loop (cap N=2) per `lode/infrastructure/agent-patterns.md`.
After loop completes: `rm -f .claude/tmp/review-orchestrator-latest.md`.

## Rules

- **One batch, one test run** — never run tests between individual fixes
- **Never apply LOW findings** — always advisory, never auto-applied
- **Never apply "needs verification" findings** — build-validator is authoritative
- **Known exceptions are skipped** — check review-orchestrator SKILL.md Notes section
- **Public API gate** — stop and ask before touching any public method/class signature
- **Lode is not review-applier scope** — never update lode/ from code review findings;
  that's the lode-sync agent's job
