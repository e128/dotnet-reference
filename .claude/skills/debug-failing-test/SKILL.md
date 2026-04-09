---
name: debug-failing-test
description: >
  End-to-end investigation of a single failing unit test. Accepts a test name or
  pasted xUnit output, runs the test in isolation, reads the test and source files,
  categorizes the failure, and proposes a targeted fix. Distinct from fix-ci which
  handles batch CI output; this skill goes deep on one test at a time.
  Triggers on: debug this test, investigate failing test, why is this test failing,
  fix failing test, test debugging, debug test, investigate test failure, test fails,
  single test debug, test broken, why does this test fail.
argument-hint: "<test-name or pasted xUnit output>"
allowed-tools: Read, Glob, Grep, Bash, Agent
---

Investigate a single failing test end-to-end and propose a fix.

## Step 1: Extract test identifier

From `$ARGUMENTS` or the conversation, identify:
- Full test class name (e.g. `MyService_WhenFoo_DoesBar`)
- Or partial name to filter on
- Or paste of xUnit output (extract the failed test name from it)

If ambiguous, ask once: "Which test should I investigate?"

## Step 2: Run the test in isolation

```bash
scripts/test.sh --json {TestName}
```

Capture: the assertion failure message, exception type, stack trace (first 5 frames only).

## Step 3: Locate and read test file

Locate the file by test class name (try `{TestClass}Tests.cs` and `{TestClass}Test.cs` in `tests/`). Read the full test method that failed, plus the `[SetUp]`/constructor and any shared fixtures used by the test class.

## Step 4: Locate and read source under test

From the test method, identify the system under test and locate its source file in `src/`. Focus on the method(s) called by the failing test. Do not read the entire file unless the class is small.

## Step 5: Categorize the failure

Determine which category best fits:

| Category | Indicators |
|----------|-----------|
| **Assertion mismatch** | Expected X but got Y; test logic or implementation drift |
| **Null reference / missing setup** | NullReferenceException, missing mock setup, incorrect fixture |
| **Flaky / race condition** | Passes locally sometimes; involves async, timers, shared state |
| **Missing implementation** | NotImplementedException, method doesn't exist, interface not wired |
| **Environment / config** | Missing file, wrong path, env variable not set, connection string |

## Step 6: Check sibling tests

Before proposing a fix, check the same test class for other methods sharing the same root cause pattern. If siblings share the bug, note them: "This fix will also resolve {N} sibling tests with the same pattern."

## Step 7: Propose fix

Output:

```
## Test Debug: {TestName}

**Category**: {category}
**Root cause**: {1-2 sentence explanation}

### Fix

{code change or setup correction}

### Also affects
- {sibling test 1} — same root cause
- {sibling test 2} — same root cause

### Verify
scripts/test.sh --json {TestClass}
```

Do NOT apply the fix without user confirmation — present it and wait.

## Notes

- For CI batch failures (many tests), use `/fix-ci` instead
- For flaky async tests, refer to concurrency patterns in `.claude/skills/dotnet-overhaul/steps/step6-patterns.md`
- Always check sibling tests — the "fix the class not the instance" rule applies here too
- If the test was previously passing (use `scripts/diff.sh --json` to review recent commits), check recent
  commits to find the regression-introducing change
