---
name: readme-check
description: >
  Audits README.md for staleness after significant code changes and auto-fixes
  drifted tables. Skips automatically when fewer than 50 C# files changed in recent history.
  Triggers on: check readme, readme stale, is readme up to date, readme check,
  readme accuracy, readme outdated, update readme.
allowed-tools: Read, Glob, Grep, Bash
flags:
  --skip-threshold: Skip Step 1 (threshold check) and proceed directly to the audit.
                    Use when the caller (e.g. /yeet) has already computed the count
                    and confirmed it meets the threshold.
---

Audits README.md for staleness after significant code changes. Only triggers a review when the change volume warrants it.

## Step 1: Check threshold

**Skip this step if invoked with `--skip-threshold`** — proceed directly to Step 2.

Count `.cs` file changes in the last 30 commits:
```
scripts/status.sh --history 30
```

If **fewer than 50** `.cs` files changed, report "README check skipped — below threshold" and stop.

## Step 2: Audit README

Manually verify README.md against current repo state:
1. Read `README.md`
2. Cross-reference project tables against `E128.Reference.slnx` and `src/` directory
3. Cross-reference script table against `scripts/help.sh` output
4. Check .NET version against `Directory.Build.props` or `global.json`
5. Verify lode links resolve to existing files

## Step 3: Report

- If no drift found — report "README audit: PASS — no drift" and stop
- If drift found — present specific findings and proposed edits, wait for approval

## Step 4: Apply fixes

After approval:
1. Edit README.md using the Edit tool
2. Do NOT stage or commit — the caller handles that (e.g., `/yeet` stages and commits as one unit)

## Rules

- **Skip if below threshold** — don't waste time auditing README after minor changes
- **No agent spawn** — audit README directly using Read + Glob + Grep
- **Propose fixes** — present drifted tables and values, wait for approval
- **Don't restructure** — match the existing README style and organization
