---
name: fix-ci
description: >
  Fix CI/build errors from pasted output. Two modes: (1) Standard — parses
  pasted compiler errors, analyzer violations, or test failures, applies
  targeted fix, confirms with build-validator. (2) Divergence — for errors
  that pass locally but fail CI; cross-references local-vs-ci-build.md to
  explain the cause, then applies the fix. Accepts $ARGUMENTS or message body.
  Triggers on: fix these, fix this, fix this error, fix these errors, CI failed,
  build errors, fix: [pasted errors], compiler error, test failed, here are
  the errors, these need fixing, error output from CI, works locally not CI,
  CI failed but local passes, github actions failed, why does CI fail,
  CI only failure, fix CI divergence.
  Not for: running a new warning scan on recently modified files (use build-validator).
argument-hint: "[pasted CI/build error output]"
---

Fix CI errors from pasted output in one pass. Handles both local errors and local-vs-CI divergences.

## Step 1: Parse errors

Extract all errors from the pasted text (from `$ARGUMENTS` if provided, otherwise from the triggering message). For each error, identify:

| Field | Format example |
|-------|----------------|
| File path | `src/MyProject/Foo.cs` |
| Line/column | `(42,15)` |
| Error code | `CS8604`, `CA1031`, `NETSDK1045` |
| Message | `Possible null reference...` |

**Error formats to recognize:**

```
src/Foo.cs(42,15): error CS8604: Possible null reference argument
src/Foo.cs(12,5): error CA1031: Do not catch general exception types
  Failed: MyTest.MyMethod — Expected true but was false
```

## Step 2: Detect mode

**Divergence mode** — activate if the user indicates the error passes locally but fails CI (e.g., "works locally", "only fails in CI", "GitHub Actions failed", "CI only"):

-> Go to **Step 2a: Divergence path**

**Standard mode** — activate for all other pasted errors:

-> Go to **Step 2b: Standard path**

---

### Step 2a: Divergence path

Match each error to a known divergence pattern (check `lode/infrastructure/` for relevant docs if available):

| Divergence Type | Typical Symptoms |
|-----------------|-----------------|
| Case sensitivity | File not found on Linux but exists on macOS |
| Missing using | `CS0246` on a type resolved via macOS global usings |
| TFM-specific API | Method exists in local SDK but not CI target TFM |
| Nullable annotations | CI has stricter `<Nullable>enable</Nullable>` |
| Analyzer version | CI NuGet restore fetches newer analyzer with stricter rules |
| Path separator | `\\` in hardcoded paths fails on Linux |

For each error, output:

```
## CI Divergence: {error code}

**Why CI failed:** {explanation of the divergence}
**Fix:** {what was changed and why}
```

If the divergence type is new, consider adding it to `lode/infrastructure/` for future reference.

Apply fixes via Edit, then go to **Step 4: Validate**.

---

### Step 2b: Standard path — Classify errors

Group errors by type to determine fix strategy:

| Type | Codes | Fix approach |
|------|-------|-------------|
| Null reference | CS8600-CS8610 | Add null check or guard clause — never use `!` |
| Unused import | IDE0005 | Remove the using |
| Unreachable code | CS0162 | Remove the unreachable block |
| Catch general exception | CA1031 | Change `Exception` to specific type, or add `when` filter |
| Async without await | CS1998 | Remove `async` or add `await` |
| Missing await | CS4014 | Add `await` or `_ =` discard |
| Test failure | — | Read the assertion message to identify the contract violation |
| Build-blocking (unknown) | — | Read the line and apply minimal fix |

## Step 3: Fix in parallel

Read each affected file. Apply the minimal fix for each error. For multiple errors in the same file, fix them all in one Edit call.

**Rules:**
- One file = one Edit call (never split a file into multiple edits)
- Never remove error handling without replacing it
- Never change logic — only fix the reported violation
- Never use `!` (null-forgiving operator) — add a proper null check

## Step 4: Validate

Spawn build-validator to confirm:

```
Agent: build-validator
Prompt: "Build the solution and verify the errors are resolved. Run CI tests."
```

## Step 5: Report

```
## Fix-CI Summary

Mode: Standard | Divergence
Errors fixed: N
Files modified: [list]
Build: PASS / FAIL

Remaining (if any):
- file.cs(L,C): code — reason fix was not applied
```

If build-validator reports new errors introduced by the fix, resolve them before reporting done.

## Rules

- **Minimal changes only** — fix the reported violation, nothing else
- **Never skip a confirmed build** — always run build-validator after applying fixes
- **One pass** — fix all errors upfront, then validate once
- **Divergence mode always explains** — the user needs to understand the local-vs-CI gap to prevent recurrence
- **If a fix is ambiguous** — apply the conservative option and note it in the summary
