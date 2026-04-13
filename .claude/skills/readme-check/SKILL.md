---
name: readme-check
description: >
  Audits all README.md files in the repo for staleness after significant code
  changes and auto-fixes drifted tables. Discovers READMEs dynamically. Skips
  automatically when fewer than 50 C# files changed in recent history.
  Triggers on: check readme, readme stale, is readme up to date, readme check,
  readme accuracy, readme outdated, update readme.
allowed-tools: Read, Glob, Grep, Bash, Edit
flags:
  --skip-threshold: Skip Step 1 (threshold check) and proceed directly to the audit.
                    Use when the caller (e.g. /yeet) has already computed the count
                    and confirmed it meets the threshold.
---

Audits all README.md files for staleness after significant code changes. Only triggers a review when the change volume warrants it.

## README discovery

Discover all READMEs dynamically in Step 2. Common locations:

| File                          | Audience     | Key content to verify                     |
| ----------------------------- | ------------ | ----------------------------------------- |
| `README.md`                   | Contributors | Project table, script table, .NET version |
| `scripts/README.md`           | Contributors | Script tables match `scripts/help.sh`     |
| `src/*/README.md`             | Package users| Rule tables, version, install snippets    |

## Step 1: Check threshold

**Skip this step if invoked with `--skip-threshold`** — proceed directly to Step 2.

Count `.cs` file changes in the last 30 commits:
```
scripts/status.sh --history 30
```

If **fewer than 50** `.cs` files changed, report "README check skipped — below threshold" and stop.

## Step 2: Discover READMEs

```bash
fd README.md --type f --exclude obj --exclude bin --exclude .git
```

Verify the inventory above is still complete. If a new README exists, flag it.

## Step 3: Audit each README

### Root README.md
1. Read `README.md`
2. Cross-reference project tables against the solution file and `src/` directory
3. Cross-reference script table against `scripts/help.sh` output
4. Check .NET version against `Directory.Build.props` or `global.json`
5. Verify lode links resolve to existing files

### scripts/README.md
1. Read `scripts/README.md`
2. Run `scripts/help.sh` and compare every script listed in the README against actual output
3. Check that flags documented match actual script `--help` or argument parsing
4. Verify `internal/` script table matches `ls scripts/internal/*.sh`

### src/*/README.md (packable projects)
For each README found under `src/` in a packable project (`<IsPackable>true</IsPackable>`):
1. Read the README
2. If the project contains analyzers, grep all `DiagnosticId` constants from `*Analyzer.cs` — every rule must appear in the rule table
3. Grep all `*CodeFixProvider.cs` files — the "Code Fix" column must be Yes for rules that have one, No otherwise
4. Check `<Version>` in the `.csproj` matches any installation snippet in the README
5. Verify rule categories and titles match the `DiagnosticDescriptor` fields

## Step 4: Report

- If no drift found across any README — report "README audit: PASS — no drift" and stop
- If drift found — present all findings grouped by file, with specific proposed edits

## Step 5: Apply fixes

1. Apply all fixes using the Edit tool
2. Do NOT stage or commit — the caller handles that (e.g., `/yeet` stages and commits as one unit)

## Rules

- **Audit all READMEs, not just root** — every README in the inventory must be checked
- **Skip if below threshold** — don't waste time auditing after minor changes
- **No agent spawn** — audit directly using Read + Glob + Grep
- **Auto-fix drift** — apply corrections directly; only ask for approval on structural changes
- **Don't restructure** — match the existing style and organization of each README
- **Analyzer README is NuGet-facing** — accuracy is critical; verify every rule ID, title, category, and code fix flag
