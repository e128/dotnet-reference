---
name: violation-sweep
description: >
  Scans source and .claude/ files for anti-pattern violations, applies the top
  mechanical fixes, and reports pre/post counts.
when_to_use: >
  violation sweep, sweep violations, fix violations, clean violations,
  sweep all violations, fix anti-patterns, sweep skills.
allowed-tools: Bash, Read, Edit
argument-hint: "[--dry] [--claude]"
---

# Violation Sweep

Scan for anti-pattern violations, apply mechanical fixes, and report counts.

## Steps

### 1. Pre-fix scan

```bash
scripts/violation-scan.sh --json          # source (src/) only
scripts/violation-scan.sh --claude --json # also scan .claude/skills + .claude/agents
```

Pass `--claude` when the skill was invoked with `--claude`. Parse the JSON:
- `violations` — overall count
- `by_pattern` — object keyed by pattern name; each has `{count, reason, files}`

Sort pattern entries by `count` descending. If `violations == 0`: emit
"No violations found." and stop — skip Steps 2–4.

### 2. Fix top mechanical categories (or --dry preview)

For the top 3 patterns by count, apply the fix described in each pattern's
`reason` field. These are mechanical, one-line substitutions:

| Pattern | Fix |
|---------|-----|
| `datetime-now` | Inject `TimeProvider` via DI; use `timeProvider.GetUtcNow()` |
| `new-httpclient` | Inject `IHttpClientFactory`; use `factory.CreateClient()` |
| `async-void` | Change return type to `async Task` (except event handlers) |
| `sync-over-async` | `await` the task instead of `.Result` / `.GetAwaiter().GetResult()` |

For each file in the pattern's `files` list: Read it, apply the fix with Edit.

**If `--dry` was passed:** report the files and intended fix per pattern, then
stop — skip Steps 3 and 4.

### 3. Post-fix scan (normal mode only)

Re-run the same scan command from Step 1 and capture the post-fix total.

### 4. Show the diff, then stage on confirmation (normal mode only)

Present the changed files before staging:

```bash
scripts/diff.sh --json
```

After the user confirms, stage:

```bash
scripts/internal/stage.sh --include-new
```

### 5. Report

Emit a summary table:

```
Violation Sweep

  Pre-fix total:    N violations
  Post-fix total:   N violations  (or: N/A — dry run)
  Categories fixed: pattern-name (N→M), pattern-name (N→M), ...
  Deferred:         pattern-name (needs human review), ...
```

## Rules

- **Stage only — don't commit** — caller handles the commit
- **Skip context-sensitive fixes** — event-handler `async void`, intentional
  `DateTime.Now` in tests, etc. Surface these in the "Deferred" row.
- **`--dry` skips writes and staging** — scan + preview only
- **Top 3 only** — cap fixes at 3 pattern categories per sweep to keep scope bounded
- **Re-Read before Edit after any format run** — a format pass invalidates prior reads
