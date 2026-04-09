---
name: error-audit
description: >
  Weekly error triage skill. Runs scripts/session-health.sh --json to get error counts
  by category, surfaces the top-3 recurring patterns with ranked fix suggestions,
  and identifies actionable root causes (flag errors, write-before-read, parallel
  cascade failures, hook-denied patterns, etc.). Use when the user asks "what's breaking",
  "what errors keep happening", or wants a session health report.
  Triggers on: error audit, what's breaking, error report, session health,
  what errors keep happening, why do I keep getting errors, recurring errors,
  tool errors this week, fix recurring errors.
  Not for: fixing a specific one-off error (use /fix-ci).
allowed-tools: Bash, Read
---

Surface recurring tool-error patterns and ranked fix actions. No interactive steps.

## Step 1: Run session health scan

```bash
scripts/session-health.sh --json
```

Parse the JSON output. Key fields:
- `total_errors` — total tool errors in the window
- `prev_total` — baseline total (0 if no baseline saved)
- `total_trend` — direction symbol
- `has_baseline` — whether a saved baseline exists
- `categories` — array of `{ name, count, prev, delta, trend }`

## Step 2: Rank categories

Sort `categories` by `count` descending. Take the top 3 (or all if fewer than 3).

## Step 3: Map categories to root causes and fixes

| Category          | Root cause                                                                  | Fix                                                                               |
|-------------------|-----------------------------------------------------------------------------|-----------------------------------------------------------------------------------|
| `bash-failure`    | Bash tool exited non-zero (build fail, pre-commit block, hook block, rm error) | Read the full exit text; fix the underlying build/script/hook error            |
| `write-before-read` | Edit/Write called before Read on the same file                            | Always Read before Write/Edit; re-read after every format/check/build step        |
| `file-modified`   | Formatter/hook mutated a file between Read and Edit                         | Re-read after every format, check, or pre-commit hook run                         |
| `parallel-cascade` | Risky call batched with safe calls; failure silently aborted siblings      | Split risky calls into their own batch                                            |
| `path-not-found`  | Absolute or unexpanded path; file doesn't exist                             | Use repo-relative paths; verify with Glob before Bash                             |
| `permission-denied` | Tool not in allowed-tools list, or agent modifying its own config         | Add tool to allowed-tools; agents must not self-modify `.claude/`                 |
| `http-error`      | WebFetch/external API call failed (also ECONNREFUSED, TLS errors)           | Run HTTP calls alone (not batched); retry once before escalating                  |
| `timeout`         | WebFetch or Grep timed out                                                  | Run timeout-prone calls alone; reduce scope for Grep                              |
| `edit-not-found`  | `old_string` in Edit doesn't match current file content                     | Re-read file immediately before Edit; use larger context string                   |
| `file-too-large`  | File exceeds token limit                                                    | Use `limit:` and `offset:` parameters on Read                                    |
| `eisdir`          | Tried to read a directory as a file                                         | Use Glob or `ls` to list directories; Read only file paths                        |
| `user-rejected`   | User denied a tool use prompt                                               | Informational only — not a bug; review what prompted the denial                   |
| `hook-denied`     | Pre-tool hook blocked the call                                              | Expected behavior — do not retry; fix the call to use the correct equivalent      |
| `tool-api-error`  | Wrong parameter name for a tool (e.g. `file_path` instead of `path`)       | Check tool schema; common: Grep uses `path`, not `file_path`                      |
| `other`           | Uncategorized                                                               | Check raw error text in session logs for patterns                                 |

## Step 4: Report

Output a concise audit block:

```
## Error Audit — [window]d window

Total: N errors  [trend vs baseline]

Top patterns:
  1. <category> — N errors [trend]
     Root cause: <one sentence>
     Fix: <one concrete action>

  2. <category> — N errors [trend]
     ...

  3. <category> — N errors [trend]
     ...

Baseline: [saved on date / not set — run `scripts/session-health.sh --baseline` to save one]
```

## Step 5: Propose targeted rule additions (if applicable)

If `write-before-read` is in the top 3: suggest adding "Read each file before editing"
to any agent task prompts that modify files.

If `parallel-cascade` is in the top 3: risky calls must run in their own batch, never alongside safe calls.

If `file-modified` is in the top 3: any shell command that runs format/check/build invalidates
all previously-read files. Re-read before Edit.

If `tool-api-error` is in the top 3: search session logs for `InputValidationError` text
to find which tool/parameter caused the failure. Common offender: Grep called with
`file_path` instead of `path`.

If `hook-denied` is growing: this is a **positive signal** — hooks are catching violations.
Review what's being blocked and ensure the correct equivalents are in place.

## Rules

- **Read-only** — this skill only runs `scripts/session-health.sh` and reports. It never modifies files.
- **One pass** — run the scan once, report all top patterns together.
- **No baseline save** — do not run `--baseline` unless the user explicitly asks to reset the baseline.
