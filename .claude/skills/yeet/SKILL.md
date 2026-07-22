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
A) scripts/status.sh --json               (working-tree status)
B) scripts/status.sh --classify --json    (classification + cs_changed + analyzers_or_scripts_changed)
C) scripts/branch.sh --json               (branch info, ahead/unpushed counts)
```

**Read directly from `status.sh --classify --json` (B):** it emits
`{classification, cs_changed, analyzers_or_scripts_changed}` — no manual derivation.
- `classification` is one of `clean` / `docs-only` / `code` / `mixed`
- `analyzers_or_scripts_changed` is already computed (any changed path under `src/*Analyzers*/` or `scripts/`)

**Then:**
- If `classification == docs-only` AND `--skip-tests` not explicit → auto-enable `--skip-tests`, log: "Docs/config-only change — skipping build+test"
- Cache: `cs_changed`, `analyzers_or_scripts_changed` (from B); `ahead`, `unpushed`, `upstream` (from C, `scripts/branch.sh`); `has_changes` (from A)

**`unpushed` (from C) is not optional to check.** A branch can have a clean working tree and still carry local commits the remote has never seen (no upstream configured, or commits made after the last push). `unpushed > 0` always means step 2's push (and PR creation) must run, even when there is nothing new to commit.

### 1. Format + build + test

**A) Format (unconditional — never skip):**
```bash
scripts/format.sh
```
Run this on every yeet, regardless of classification (`docs-only`, `code`, `mixed`), flags (`--skip-tests`, `--dry-run`), or working tree state. Reaching step 1 without running `scripts/format.sh` is a bug.

Runs on the entire solution. This catches violations introduced by prior commits that local format missed (e.g., analyzer-backed style rules that require restore).

If format produces changes and the working tree was previously clean, those changes become the commit.

After format, re-check working tree state. If still no changes (format found nothing, and `has_changes` was false):
- If `unpushed == 0` → "Nothing to yeet — working tree is clean, nothing unpushed." **Stop.**
- If `unpushed > 0` → nothing to commit, but local commits are missing from the remote. Skip build+test/analyzer/README steps (no `.cs` or script changes to verify) and go straight to step 2's **push-only path**.

**B) Build + test (conditional):**
Skip if `--skip-tests` (explicit or auto-detected docs-only).
```bash
scripts/check.sh --no-format --all
```
If exit code is non-zero → **stop and report failures.**

**C) Analyzer release files (conditional):**
Only if the repo has a Roslyn analyzer project (a `src/*Analyzers*/` project) AND any staged or unstaged `.cs` file changes under it. Resolve the analyzer project name from the changed path (or `scripts/solution-inventory.sh --json`); call it `<AnalyzerProject>` below. Skip this whole step if no analyzer project exists.

**C.1) Version bump:**
```bash
scripts/internal/version-bump.sh <AnalyzerProject>
```
This increments the `<Version>` in the analyzer csproj so the NuGet package ships with a new version. Skip with "Analyzer version bump skipped — no analyzer source changes" if no `.cs` files changed.

If the version was bumped, re-read the csproj before any subsequent edits.

**C.2) Unshipped/Shipped validation:**

```bash
scripts/internal/analyzer-release-check.sh --json
```

Parse the JSON output. If `status` is `"issues"`:
- **`missing`** array → add each ID to Unshipped.md with the correct Rule ID, Category, Severity, and Notes from the analyzer source
- **`duplicates`** array → remove each from Unshipped (Shipped wins)
- **`orphans`** array → remove each orphaned entry from Unshipped.md
- Verify table format consistency: aligned-column markdown, column order `Rule ID | Category | Severity | Notes`

If any fixes are needed, apply them silently (auto-approval per project rules). Log what was fixed.

**D) README freshness (conditional):**
Only if any staged or unstaged changes touch analyzer source (`src/*Analyzers*/`) OR `scripts/`:

Spawn the `readme-auditor` agent:
```
Agent(subagent_type="readme-auditor",
      prompt="Audit all README.md files for staleness and auto-fix drift.")
```

If the agent produces edits, they become part of this commit. No separate commit.

Skip with "README check skipped — no analyzer or script changes" if neither path is touched.

**If `--dry-run`**: report quality gate results and **stop here.** Do not continue to step 2.

### 2. Stage + commit + push

**Push-only path** — no working-tree changes (`has_changes` false and format made none) but `unpushed > 0`:
- `ahead <= 1`: nothing to stage or squash — the single existing commit is already push-ready. Skip straight to **Push** and **Create PR** below.
- `ahead > 1`: the "single commit per push" rule still applies even with no new changes — squash the existing unpushed commits into one before pushing. Run `git reset --soft $(git merge-base main HEAD)`, then re-stage everything (`scripts/internal/stage.sh --include-new`), run the PII scan, craft a commit message from the full squashed diff, and commit — same as the squash sub-step in the normal path below — then continue to **Push** and **Create PR**.

**Normal path** — there are new working-tree changes to commit:
- **Stage** — `scripts/internal/stage.sh --include-new` (stages all modified tracked + new untracked, excluding secrets)
- **PII scan** — `scripts/internal/precommit.sh` (checks staged diff for home paths and email addresses; stop if fail)
- If lode files staged, show brief summary table (path + one-line change description)
- **Squash** — use `ahead` from cached step 0:
    - `ahead > 1`: `git reset --soft $(git merge-base main HEAD)` then re-stage and commit as one
    - 1 or 0: proceed normally
- **Craft commit message** — always generate a fresh message from the actual diff, never reuse a prior commit message:
    - Run `scripts/diff.sh --staged --json` to inspect staged stats
    - Synthesize a **conventional commit** summary: `type(scope): imperative summary` covering the full changeset
    - If the branch had multiple distinct concerns, name both in the subject or use a multi-line body
    - Subject line must be <=72 chars; use a body for detail when > 1 major concern
    - Never truncate the subject — if the auto-generated one ends in `...`, it is wrong
    - **No email addresses** — never put an email in the message or any trailer; `commit.sh` rejects it
- **Commit** — `scripts/internal/commit.sh --skip-precommit "message"` (precommit already ran above)

**Both paths converge here:**
- **Push** — `git push` (with `-u origin <branch>` if no upstream set). This step is mandatory whenever `unpushed > 0`, whether or not a new commit was just made.
- **Create PR** — if the current branch is not `main`, create a pull request:
  ```bash
  gh pr create --title "<commit subject line>" --body "<body>"
  ```
    - Title: reuse the commit subject line (the `type(scope): summary` part)
    - Body: generate a `## Summary` with 1-3 bullet points covering the changeset, a `## Test plan` with bulleted checklist, and the Claude Code footer — never include an email address anywhere in the body
    - If a PR already exists for this branch, skip PR creation silently
    - Report the PR URL at the end

## Rules

- **All pending changes ship together** — never unstage, cherry-pick, or exclude files from the commit. Everything in the working tree goes into one commit. Do not ask whether to include specific files.
- **Fully autonomous** — no user prompts during execution
- **Stop on failure** — PII fail, build fail, or test fail halts the pipeline
- **No email addresses** — never in a commit message, trailer, or PR body. The PII scan blocks real emails in the staged diff and `commit.sh` rejects an email in the message; `user@example.com` placeholders are allowed
- **Single commit per push** — squash local commits when `ahead > 1`
- **Unpushed local commits always ship** — a clean working tree is not a reason to stop if `unpushed > 0`. Check `scripts/branch.sh --json` unconditionally; never rely on working-tree state alone to decide whether a push is needed.
- **Do NOT auto-commit or push again** after completing these steps — one-time action
- **`--dry-run` stops after step 1** — quality check only, no side effects
- **Format is unconditional** — `scripts/format.sh` runs on every yeet; no flag, classification, or condition skips it.
- **Re-read gate** — after format runs (step 1A), all `.cs` file contents are stale. Do NOT Edit any `.cs` file after step 1 without re-reading first.

## Troubleshooting

- **PII scan finds home directory paths** — replace with relative paths or env-var substitution
- **Format changes files unexpectedly** — expected after editorconfig updates; review diff, re-run build
- **Build passes but tests fail** — do not commit; fix tests first
