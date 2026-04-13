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
model: sonnet
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

3. Stage and scan:
   ```bash
   scripts/internal/stage.sh --include-new
   scripts/internal/precommit.sh
   ```
   If precommit fails → stop and report the PII finding.

4. Review staged changes:
   ```bash
   scripts/diff.sh --staged
   ```

5. Commit via `internal/commit.sh` with `--skip-precommit` (already ran in step 3):
   ```bash
   scripts/internal/commit.sh --skip-precommit "Your commit message here"
   ```

6. Report: `{hash} {subject}` — do not push.

## Rules

- **Do not push** — leave push to `/yeet` or explicit user instruction
- **One commit only** — if changes span clearly unrelated concerns, note it in the output
  but still commit everything together (user can split manually if they want)
- **Lode-only warning** — if all staged files are in `lode/` with no code changes, warn:
  "Committing documentation only — no code changes staged"
