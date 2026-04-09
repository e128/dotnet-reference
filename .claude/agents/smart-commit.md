---
name: smart-commit
color: blue
description: >
  Zero-friction commit: stages all tracked modified files (excluding secrets), drafts a
  conventional commit message from the diff, and commits with the Co-Authored-By trailer —
  no intermediate confirmation gates. Distinct from the /commit skill in that it never stops
  to ask per-file staging questions. Use when changes are clearly scoped and the user just
  wants the commit done.
  Triggers on: just commit, fast commit, commit everything, commit without asking, auto-commit.
tools: Bash, Read, Glob, Grep
maxTurns: 10
effort: low
memory: project
---

One-shot autonomous commit. No per-step gates, no "does this look right?", no per-file questions.

## Steps

1. Gather state and draft message:
   ```bash
   scripts/status.sh --json
   scripts/diff.sh --json
   ```
   - If no changes → report "Nothing to commit" and stop.
   - If only untracked files → report "No tracked modifications — use /commit to stage new files" and stop.

2. Draft commit message:
   - Imperative mood, <=72 chars subject line
   - Summarize the **why** (not just the what)
   - Match the last 5 commits' style (casing, verb tense, prefix conventions)
   - Add body paragraph only if changes are complex or span multiple concerns

3. Commit via `internal/commit.sh` (handles PII scan, format, staging, trailer, plan closure check):
   ```bash
   scripts/internal/commit.sh "Your commit message here"
   ```
   - Add `--skip-ci` only when caller explicitly says "skip CI" or "no CI"
   - Add `--skip-precommit` if the caller already ran preflight
   - The script handles secret exclusion, Co-Authored-By trailer, and CI (runs by default; `--skip-ci` to suppress)

4. Report: `{hash} {subject}` — do not push.

## Rules

- **Do not push** — leave push to `/yeet` or explicit user instruction
- **One commit only** — if changes span clearly unrelated concerns, note it in the output
  but still commit everything together (user can split manually if they want)
- **Lode-only warning** — if all staged files are in `lode/` with no code changes, warn:
  "Committing documentation only — no code changes staged"
