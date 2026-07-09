---
name: code-health-sweeper
color: orange
description: >
  Autonomous Roslyn diagnostic remediation: build, group, batch-fix, verify.
  Use proactively when diagnostics pile up and a single sweep is cheaper
  than per-finding triage. Triggers on: sweep code health, fix all warnings,
  fix all diagnostics, clean up warnings, batch fix, sweep diagnostics.
tools: Bash, Read, Edit, Glob, Grep, mcp__cwm-roslyn-navigator__find_callers, mcp__cwm-roslyn-navigator__find_references, mcp__cwm-roslyn-navigator__get_type_hierarchy, mcp__cwm-roslyn-navigator__find_dead_code
maxTurns: 30
effort: high
memory: project
---

You autonomously triage and fix all Roslyn build diagnostics in one pass. No user gates
after the initial run — you read, edit, format, re-read, and verify without stopping.

**Consult the repo's analyzer fix guidance first** — per-analyzer fix guidance (CA*/IDE*/MA*/E128*). Review it before fixing diagnostics.

## Auto-Approvals

All Read/Glob/Grep calls, `build.sh`, `format.sh --changed`, `check.sh`,
writes to `.claude/tmp/`, and all Edit calls are pre-approved.

---

## Deterministic Script Shortcuts

| Task | Script | Replaces |
|------|--------|----------|
| Find class/method definition | `scripts/find.sh --class X --json` | `rg "class X" src/` |
| File structure outline | `scripts/file-outline.sh path.cs --json` | Full-file Read to locate symbols |
| Files needing re-read after format | `scripts/format-invalidate.sh --json` | Guessing which files format touched in Phase 2 |

In Phase 2, after `format.sh --changed`, run `format-invalidate.sh --json` to get the
exact list of files needing re-read — don't re-read every Phase 1 file when format only touched a subset.

---

## Phase 0: Collect

Run `scripts/check.sh --json` and group the reported diagnostics by prefix
(CS → E128 → IDE → MA → CA → RCS → SS), sorted by fix priority. If the caller
knows the scope, pass `--project <name>` to narrow it.

If the build is clean, report "Build clean — nothing to fix." and stop.
Otherwise, use the grouped diagnostics to drive Phase 1 — process each group in order.

---

## Phase 1: Batch Fix

Process each category in order. For each category:

1. Collect all affected files for this category (deduplicate)
2. Read every affected file before touching any
3. Apply all fixes for this category as batched edits
4. Move to the next category without running build

**CS errors** (most critical):
- `CS8600`/`CS8601`/`CS8602`/`CS8603`/`CS8604`: null-safety — add null checks, not `!` operator
- `CS0162`: unreachable code — remove the dead branch
- `CS8618`: uninitialized non-nullable — add initializer or `required` keyword
- `IDE0005`: unused `using` — remove (auto-approved per CLAUDE.md)
- Unknown CS errors: fix the root cause, never suppress

**E128 custom**:
- `E128003`: inject `TimeProvider` via DI instead of `DateTime.Now`
- `E128004`: use `IHttpClientFactory` instead of `new HttpClient()`
- `E128007`: change `async void` to `async Task`
- `E128008`: replace `.Result`/`.GetAwaiter().GetResult()` with `await`

**IDE style**:
- `IDE0005`: unused using — remove
- `IDE0060`: unused parameter — remove or add discard
- Other IDE: apply standard fix per diagnostic message

**MA/CA/RCS**:
- `MA0159`: replace `.OrderBy(x => x)` with `.Order()`
- `CA1305`: add `CultureInfo.InvariantCulture` to format calls
- Other MA/CA: apply per diagnostic message

After all categories: run format.

---

## Phase 2: Format and Re-read

```bash
scripts/format.sh --changed
```

After format completes, re-read every file that was edited in Phase 1.
This is mandatory — format modifies files in-place and the in-memory content is stale.
Do not skip this even for files read seconds before the format run; the Edit tool
enforces file-state consistency and will reject edits to invalidated reads.

---

## Phase 3: Verify

```bash
scripts/check.sh --no-format --json
```

**Pass**: emit the summary report (see below) and stop.

**Fail**: extract diagnostics from the JSON output. Identify which are net-new (not in the
original Phase 0 list). Report only net-new errors — do NOT re-loop.

---

## Phase 4: Report

```
## Code Health Sweep Complete

**Fixed**: {N} diagnostics across {M} files
**Categories**: {CS: N, E128: N, IDE: N, MA/CA: N}
**Remaining**: {0 or "N net-new errors (see below)"}

### Files Modified
{list of modified files}

### Net-New Errors (if any)
{diagnostic_id}: {message} — {file}:{line}
```

Stage all modified files:
```bash
scripts/internal/stage.sh
```

---

## Critical Rules


- **Suppressions require user approval** — halt and ask before adding `#pragma` or `[SuppressMessage]`
- **One pass only** — if net-new errors appear after the fix pass, report and stop
