---
name: fix-investigate
description: >
  Investigate failing tests or CI divergence. Deep single-test root-cause analysis,
  or local-vs-CI pattern matching.
when_to_use: >
  investigate failing test, why is this test failing, debug this test,
  test debugging, test broken, works locally not CI, CI failed but local passes,
  fix CI divergence, CI divergence.
argument-hint: "[test name | CI error output | 'CI divergence']"
---

# Fix Investigate

You are a test-failure investigator. Run a deep investigation of failing tests or CI-vs-local divergence. For batch build error fixing, use `/fix` instead.

## Step 0: Detect Mode

| Input pattern                                        | Mode                |
| ---------------------------------------------------- | ------------------- |
| "works locally" / "CI only" / "CI failed but local"  | **CI Divergence**   |
| Single test name or pasted xUnit failure             | **Deep Investigation** |

---

## Mode: CI Divergence

If the repo keeps local-vs-CI build notes (e.g. `lode/infrastructure/local-vs-ci-build.md`),
read them first. Then match each error to a known divergence pattern:

| Divergence Type      | Typical Symptoms                                    |
|----------------------|-----------------------------------------------------|
| Case sensitivity     | File not found on Linux but exists on macOS         |
| Missing using        | CS0246 on a type resolved via macOS global usings   |
| TFM-specific API     | Method exists in local SDK but not CI target TFM    |
| Nullable annotations | CI has stricter `<Nullable>enable</Nullable>`       |
| Analyzer version     | CI NuGet restore fetches newer analyzer             |
| Path separator       | `\\` in hardcoded paths fails on Linux              |

For each error, explain the local-vs-CI gap, apply the fix, then validate with
`scripts/check.sh --all --no-format --json`. If the divergence type is new and
the repo keeps local-vs-CI notes, record it there.

---

## Mode: Deep Investigation

### Step 1: Extract test identifier

From `$ARGUMENTS` or conversation, identify the test class or method name.

### Step 2: Investigate in parallel

Run these simultaneously:

1. `scripts/test.sh {TestName}` — capture assertion message, stack trace
2. Read test file — locate by `{TestClass}Tests.cs` in `tests/`
3. Read source under test — from test method, identify the SUT in `src/`

**Categorize the failure:**

| Category               | Indicators                                              |
|------------------------|---------------------------------------------------------|
| Assertion mismatch     | Expected X but got Y; test logic or implementation drift |
| Null reference / setup | NRE, missing mock setup, incorrect fixture              |
| Flaky / race condition | Passes sometimes; async, timers, shared state           |
| Missing implementation | NotImplementedException, method doesn't exist           |
| Environment / config   | Missing file, wrong path, env variable not set          |

**Check sibling tests** in the same class for shared root cause.

### Step 3: Propose fix

Present the diagnosis with category, root cause, proposed code change, and
affected sibling tests. **Wait for user confirmation before applying.**

For flaky async tests, flag the concurrency hazard (async/timers/shared state) and
recommend a dedicated concurrency review before trusting the fix.

### Step 4: Validate

After user-approved fix: `scripts/test.sh {TestClass}`

## Common Pitfalls

### CS0535 cascade
When implementing interface stubs for CS0535, expression-bodied members don't
allow parameter references. Use block-bodied methods. Unused nullable params
need `GC.KeepAlive(param)`, non-nullable ref params need
`ArgumentNullException.ThrowIfNull(param)`.
