---
name: yeet
description: >
  Ship it — formats, builds, tests, commits, and pushes.
  Accepts --skip-tests flag to skip build+test when caller already verified them.
  Triggers on: ship it, yeet, push it, commit and push, deploy this, we're done,
  preflight, preflight check, quality check, pre-commit check, ready to commit.
argument-hint: "[--skip-tests] [--dry-run]"
---

# Yeet Skill

Ship it. Quality gate + commit + push in one autonomous pass.

## Modes

| Invocation            | Behavior                                        |
| --------------------- | ----------------------------------------------- |
| `/yeet`               | Full: PII + format + build+test + commit + push |
| `/yeet --skip-tests`  | Fast path: PII + format only; skips build+test  |
| `/yeet --dry-run`     | Quality gate only — no commit or push           |

`--dry-run` replaces the retired `/preflight` skill.

## Steps

### 0. Gather context (parallel batch)

Run all in a single parallel message:

```
A) scripts/status.sh --json        (working-tree status)
B) scripts/branch.sh --json        (branch info, ahead/behind counts)
```

**Derive from status JSON:**
- Classification: all `.md`/`.json`/`.yml`/`.yaml`/`.txt` = `docs-only`; any `.cs`/`.csproj` = `code`; else `mixed`
- If `docs-only` AND `--skip-tests` not explicit → auto-enable `--skip-tests`, log: "Docs/config-only change — skipping build+test"
- Cache: `cs_changed`, `ahead`, `has_changes`, `analyzers_or_scripts_changed`
- Set `analyzers_or_scripts_changed = true` if any changed file path starts with `src/*Analyzers*/` or `scripts/`

### 1. Format + build + test

**A) Format (MANDATORY — never skip):**
```bash
scripts/format.sh
```
This step is **unconditional**. Run it on every yeet, regardless of classification (`docs-only`, `code`, `mixed`), flags (`--skip-tests`, `--dry-run`), or working tree state. No exception. If you reach step 1 without running `scripts/format.sh`, you have a bug.

Runs on the entire solution. This catches violations introduced by prior commits that local format missed (e.g., analyzer-backed style rules that require restore).

If format produces changes and the working tree was previously clean, those changes become the commit.

After format, re-check working tree state. If still no changes (format found nothing, and `has_changes` was false) → "Nothing to yeet — working tree is clean." **Stop.**

**B) Build + test (conditional):**
Skip if `--skip-tests` (explicit or auto-detected docs-only).
```bash
scripts/check.sh --no-format --all
```
If exit code is non-zero → **stop and report failures.**

**C) README freshness (conditional):**
Only if any staged or unstaged changes touch analyzer source (`src/*Analyzers*/`) OR `scripts/`:
```
/readme-check --skip-threshold
```
This audits all READMEs against current repo state and auto-fixes drift (e.g., stale rule tables, missing scripts, wrong version in install snippet). The Analyzers README is packed into the NuGet package — stale content ships to nuget.org if not caught here.

If readme-check produces edits, they become part of this commit. No separate commit.

Skip with "README check skipped — no analyzer or script changes" if neither path is touched.

**If `--dry-run`**: report quality gate results and **stop here.** Do not continue to step 2.

### 2. Stage + commit + push

- **Stage** — `scripts/internal/stage.sh --include-new` (stages all modified tracked + new untracked, excluding secrets)
- **PII scan** — `scripts/internal/precommit.sh` (checks staged files for home paths; stop if fail)
- If lode files staged, show brief summary table (path + one-line change description)
- **Squash** — use `ahead` from cached step 0:
    - `ahead > 1`: `git reset --soft $(git merge-base main HEAD)` then re-stage and commit as one
    - 1 or 0: proceed normally
- **Craft commit message** — always generate a fresh message from the actual diff, never reuse a prior commit message:
    - Run `scripts/diff.sh --json` and inspect staged stats and affected projects
    - Synthesize a **conventional commit** summary: `type(scope): imperative summary` covering the full changeset
    - If the branch had multiple distinct concerns, name both in the subject or use a multi-line body
    - Subject line must be <=72 chars; use a body for detail when > 1 major concern
    - Never truncate the subject — if the auto-generated one ends in `...`, it is wrong
- **Commit** — `scripts/internal/commit.sh "message"`
- **Push** — `git push` (with `-u origin <branch>` if no upstream set)
- **Create PR** — if the current branch is not `main`, create a pull request:
  ```bash
  gh pr create --title "<commit subject line>" --body "<body>"
  ```
    - Title: reuse the commit subject line (the `type(scope): summary` part)
    - Body: generate a `## Summary` with 1-3 bullet points covering the changeset, a `## Test plan` with bulleted checklist, and the Claude Code footer
    - If a PR already exists for this branch, skip PR creation silently
    - Report the PR URL at the end

## Rules

- **All pending changes ship together** — never unstage, cherry-pick, or exclude files from the commit. Everything in the working tree goes into one commit. Do not ask whether to include specific files.
- **Fully autonomous** — no user prompts during execution
- **Stop on failure** — PII fail, build fail, or test fail halts the pipeline
- **Single commit per push** — squash local commits when `ahead > 1`
- **Do NOT auto-commit or push again** after completing these steps — one-time action
- **`--dry-run` stops after step 1** — quality check only, no side effects
- **Format is unconditional** — `scripts/format.sh` runs on EVERY yeet. No flag, classification, or condition skips it. This is the #1 rule.
- **Re-read gate** — after format runs (step 1A), all `.cs` file contents are stale. Do NOT Edit any `.cs` file after step 1 without re-reading first.

## Troubleshooting

- **PII scan finds home directory paths** — replace with relative paths or env-var substitution
- **Format changes files unexpectedly** — expected after editorconfig updates; review diff, re-run build
- **Build passes but tests fail** — do not commit; fix tests first
