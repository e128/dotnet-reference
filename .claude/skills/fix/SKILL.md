---
name: fix
description: >
  Fix build errors in batch. Parses pasted CI/build output, classifies errors,
  applies minimal fixes, validates. For test investigation or CI divergence,
  use /fix-investigate instead.
when_to_use: >
  fix these, fix this, fix this error, CI failed, build errors, compiler error,
  these need fixing.
argument-hint: "[pasted CI/build error output]"
---

# Fix (Batch Build Errors)

You are a build-error fixer. Resolve pasted build errors in one pass. For test investigation or CI divergence, route to `/fix-investigate`.

**Routing check:** If the input contains "investigate", "debug this test",
"why is this test failing", "works locally", or "CI divergence" — tell the
user: `Use /fix-investigate for test debugging and CI divergence.`

## Step 1: Parse errors

Extract from `$ARGUMENTS` or the triggering message:

| Field     | Example                              |
|-----------|--------------------------------------|
| File path | `src/MyProject/Foo.cs`               |
| Line/col  | `(42,15)`                            |
| Code      | `CS8604`, `CA1031`, `IDE0005`        |
| Message   | `Possible null reference argument`   |

## Step 2: Classify and group

| Type                  | Codes         | Fix approach                         |
|-----------------------|---------------|--------------------------------------|
| Null reference        | CS8600-CS8610 | Add null check -- never use `!`      |
| Unused import         | IDE0005       | Remove the using                     |
| Unreachable code      | CS0162        | Remove the unreachable block         |
| General exception     | CA1031        | Specific type or `when` filter       |
| Async without await   | CS1998        | Remove `async` or add `await`        |
| Missing await         | CS4014        | Add `await` or `_ =` discard        |
| Build-blocking other  | --            | Read the line, apply minimal fix     |

## Step 3: Fix in batch

Read each affected file. Apply the minimal fix for each error. For multiple
errors in the same file, fix them all in one Edit call.

**Rules:**
- One file = one Edit call
- Replace error handling rather than removing it
- Fix only the reported violation -- leave logic unchanged
- Avoid `!` (null-forgiving operator)

## Step 4: Validate

```bash
scripts/check.sh --all --no-format --json
```

## Step 5: Report

```
Fix Summary

Errors fixed: N
Files modified: [list]
Build: PASS / FAIL

Remaining (if any):
- file.cs(L,C): code -- reason fix was not applied
```

If validation reports new errors introduced by the fix, resolve before reporting.

## Rules

- **Minimal changes only** -- fix the reported violation, nothing else
- **Always validate** -- run the build after applying fixes, every time
- **One pass** -- fix all errors upfront, validate once
- **If ambiguous** -- apply conservative option, note in summary
