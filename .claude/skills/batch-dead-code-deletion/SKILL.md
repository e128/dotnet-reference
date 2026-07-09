---
name: batch-dead-code-deletion
description: >
  Batch-verify and delete dead code: rg-confirm zero callers for N symbols,
  delete files, run the build once, stage results.
when_to_use: >
  batch dead code, delete dead code, dead code deletion, bulk delete dead,
  batch delete types, remove dead types, prune dead code, dead code cleanup.
argument-hint: >
  Space-separated symbol names, optionally prefixed with --dry-run.
  Example: /batch-dead-code-deletion FooService BarHelper IWidgetFactory
  Example: /batch-dead-code-deletion --dry-run FooService BarHelper
allowed-tools: Bash, Read, Glob, Grep, Write, Edit
---

# Batch Dead-Code Deletion

Verify and delete N dead symbols in one pass. Accepts class names, interface
names, or method names.

## Step 1: Parse Arguments

- If `--dry-run` is present: set DRY_RUN mode (report only, no deletions)
- Remaining tokens are the symbol list
- If no symbols provided: print usage and stop

## Step 2: Classify Each Symbol

For each symbol, confirm it is dead before touching it. Classify into one of:
`DEAD` (no non-definition references), `ALIVE` (referenced in `src/`),
`TEST-ONLY` (referenced only under `tests/`), or `ANCHOR` (only reference is a
`typeof(X).Assembly` marker in an architecture-test fixture).

1. **Find the defining file(s)** — `scripts/find.sh --class {Symbol}` (or
   `rg -l "class {Symbol}|interface {Symbol}|record {Symbol}" src/`).
2. **Count references** — `rg -w "{Symbol}" src/ tests/ -g '*.cs'`. Exclude the
   definition site. For **extension methods**, the class name never appears at
   call sites — grep the public *method* names instead.
3. **Cross-check (optional)** — the `find_dead_code` Roslyn MCP tool can suggest
   candidates, but **don't trust it alone** (~33% false-positive rate). The `rg`
   caller count is authoritative.
4. **Flag multi-type files** — if the defining file declares more than one type,
   mark it `multi_type` (edit, don't delete).

## Step 3: Execute Deletions (skip in DRY_RUN)

For each DEAD symbol in a single-type file:

```bash
git rm src/{defining_file}
```

For each DEAD symbol in a `multi_type` file:
- Read the file
- Edit to remove only the dead type's definition block
- `git add` the modified file

**Delete only DEAD symbols — leave ANCHOR, TEST-ONLY, and ALIVE symbols in place.**

For ANCHOR symbols: the `typeof(X).Assembly` reference in the architecture-test
fixture must be repointed to another public type from the same assembly before
deletion.

## Step 4: Verify Build (skip in DRY_RUN)

```bash
scripts/check.sh --no-format --json
```

One run covers all deletions. If the build fails:
- Parse errors for CS0246 (missing type) or similar
- Report which deletion caused the break
- Print recovery: `git checkout -- src/{file}` for each failed file

## Step 5: Stage Results (skip in DRY_RUN)

```bash
scripts/internal/stage.sh --include-new
```

## Step 6: Report

```
Dead-Code Deletion Report

  Symbols checked:  N
  Deleted:          N (files: ...)
  Multi-type edits: N (removed type X from file Y, ...)
  Skipped (alive):  N (SymbolA: N refs in src/)
  Skipped (anchor): N (SymbolC: typeof ref in file)
  Skipped (test):   N (SymbolD: N refs in tests/ only)
  Build status:     PASS / FAIL
  Recovery:         git checkout -- src/{file}
```

## Rules

- **Delete only after classification confirmation.** Every symbol must be
  confirmed `DEAD` (zero non-definition references) before deletion.
- **Don't trust `find_dead_code` alone.** ~33% false-positive rate — confirm with `rg`.
- **Extension class gotcha.** Grep the public method names, not the class name.
- **Multi-type files: edit, don't delete.**
- **One build run.** After all deletions, not per symbol.
- **Stage only — don't commit.** The caller handles commits.
- **DRY_RUN is default-safe.** No files modified; report what would happen.
