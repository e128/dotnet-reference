---
name: lode-audit
description: >
  Audits lode/ documentation for content accuracy — checks whether each doc correctly
  describes the current codebase. Distinct from lode-sync (which only checks timestamps).
  Produces a staleness table with git-verified findings, then makes targeted updates only
  on the sections you approve. Use after large refactors, renames, or when the lode feels
  out of sync with the code.
  Triggers on: audit lode, lode out of date, lode accuracy check,
  is the lode correct, lode stale, lode content review, audit documentation,
  check lode, lode drift, lode wrong, lode inaccurate, lode needs correction.
  Not for: updating a single known lode file (just edit it directly), or checking timestamps
  only (use lode-sync agent).
allowed-tools: Read, Glob, Grep, Bash, Edit, Write, Agent
---

Audit lode/ documentation for content accuracy against the current codebase.

## Phase 0: Resume Check

```bash
cat .claude/tmp/lode-audit/state.md 2>/dev/null
```

If `state.md` exists, load it and skip phases already marked DONE. Otherwise:

```bash
mkdir -p .claude/tmp/lode-audit
```

Initialize `state.md`:
```
# Lode Audit State
Started: {date -u +"%Y-%m-%dT%H:%M:%SZ"}
```

---

## Phase 1: Enumerate Lode Files

Read `lode/lode-map.md` to extract all referenced `.md` file paths. For each path, extract the `*Updated:*` timestamp (format: `YYYY-MM-DDTHH:MM:SSZ`) and record the file path, timestamp (or "no timestamp"), and line count.

Save to `.claude/tmp/lode-audit/files.md`:
```
# Lode Files
| File | Updated | Lines |
|------|---------|-------|
| lode/summary.md | 2026-02-15T00:00:00Z | 42 |
...
```

Write to state.md: `- Phase 1 (Enumerate): DONE — {N} files found -> files.md`

---

## Phase 2: Staleness Assessment

Use `scripts/lode-ts.sh --stale --json` to get a structured staleness report for all lode files in one call. This returns each file's `days_ago` and `commits_since` (commits to src/tests/scripts/.claude since the file's timestamp).

Classification:
- `commits_since > 0` -> **POTENTIALLY STALE**
- No timestamp in file -> **NO TIMESTAMP** (not included in --stale output; check Phase 1 files.md for these)
- `commits_since == 0` -> **CURRENT**

Save to `.claude/tmp/lode-audit/assessment.md`:
```
# Staleness Assessment
| File | Updated | Commits Since | Status |
|------|---------|---------------|--------|
| lode/dotnet/testing.md | 2026-01-10 | 23 | POTENTIALLY STALE |
...
```

Write to state.md: `- Phase 2 (Assess): DONE — {N} stale, {M} current, {K} no-timestamp -> assessment.md`

---

## Phase 3: Present Findings and Get Approval

Display the assessment table, sorted by: POTENTIALLY STALE first (descending commit count),
then NO TIMESTAMP, then CURRENT.

**Output format:**

```
Lode Audit Results

  # | File                          | Updated    | Commits | Status
----+-------------------------------+------------+---------+------------------
  1 | lode/dotnet/testing.md        | 2026-01-10 |      23 | POTENTIALLY STALE
  2 | lode/infrastructure/ci.md     | 2026-01-28 |      11 | POTENTIALLY STALE
  ...
 12 | lode/summary.md               | (none)     |       — | NO TIMESTAMP
 15 | lode/dotnet/patterns.md       | 2026-03-01 |       0 | CURRENT
```

Ask: **"Which entries do you want to update? Enter numbers (e.g. 1,3,5), 'all stale', or 'none'."**

Wait for response. Save approved list to `.claude/tmp/lode-audit/approved.md`.

Write to state.md: `- Phase 3 (Triage): DONE — user approved {N} files -> approved.md`

---

## Phase 4: Targeted Updates

For each approved file, spawn a targeted agent:

```
Agent: general-purpose, max_turns: 12
Prompt:
  Audit and update the lode file: {file_path}

  Read it, verify its factual claims (class names, method signatures, file paths,
  configuration values, counts, architectural relationships) against current code
  using targeted Grep/Glob/Read — do NOT explore unrelated areas. Report what is
  CORRECT / STALE / MISSING, then apply the minimal edits to make it accurate.
  Preserve existing structure and prose style.
  Update the timestamp: `scripts/lode-ts.sh {file_path}`
```

After each agent completes, record in state.md:
`- Phase 4 ({file_path}): DONE — {summary of changes}`

Report progress as each file completes. After all approved files are done:

---

## Phase 5: Cleanup and Summary

1. Report a final summary table: files updated, key corrections made.
2. Delete checkpoint files:
```bash
rm -rf .claude/tmp/lode-audit/
```
3. Remind: stage the updated lode files before next commit.

---

## Rules

- **Targeted verification only** — each agent reads the relevant code for its file only.
- **Preserve prose style** — corrections should read like the rest of the file.
- **Never auto-approve** — Phase 3 always waits for user input before modifying any file.
- **Commit count is a signal, not a verdict** — POTENTIALLY STALE means "check this", not "definitely wrong".
