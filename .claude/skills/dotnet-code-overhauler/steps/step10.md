# Step 10: Final Review

Present the Overhaul Summary and wait for the user to review all changes.
**No git commits or pushes until the user explicitly requests them.**

---

## Test Change Report

Before the summary table, list every test file that was modified:

```markdown
### Modified Tests

| Test File | Change | Reason | Finding |
|-----------|--------|--------|---------|
| OrderServiceTests.cs:45 | Updated method call `Create()` → `CreateAsync()` | Method renamed in M1 fix | M1 |
| UserRepositoryTests.cs:112 | Updated constructor call | Primary constructor change in M5 | M5 |

**No test assertions or logic were changed.**
```

**Rules:**
- List every test file touched, with specific lines changed
- Explain *why* each change was necessary (renamed method, moved class, changed signature)
- Every test change must trace back to a specific finding ID
- If no tests were modified: state "No test files were modified"
- **Never change test assertions or expected values** to make a failing test pass — the production fix is wrong

---

## Renovate Configuration Cleanup

Glob for Renovate config: `renovate.json`, `renovate.json5`, `.renovaterc`, `.renovaterc.json`

**If found**, check for stale exclusions the overhaul supersedes:

1. **`ignoreDeps`** — if the overhaul updated any of these packages and tests pass, remove them.
   The successful upgrade proves the update is safe.

2. **`packageRules` with `enabled: false`** — if the overhaul upgraded past the excluded version,
   remove or update the rule. Example: if a rule skips `SomePackage >= 3.0.0` but overhaul upgraded
   to 3.2.0 with passing tests, delete that rule.

3. **`packageRules` with `allowedVersions`** — if overhaul upgraded beyond the pinned range,
   update the constraint or remove it entirely.

4. **`ignorePaths`** — if overhaul touched files in ignored paths, flag for review.

**Report each stale exclusion found**, explain why it's safe to remove (tests pass, build succeeds),
and apply the change to the Renovate config file.

**If no Renovate config exists:** Skip silently.
**If exclusions weren't touched by the overhaul:** Leave them in place — they may reflect known incompatibilities.

---

## Overhaul Summary

**Assemble from journal files** — do not rely on memory:
1. Read `.claude/tmp/overhauler/progress.md` for per-step summaries and DONE/SKIPPED status
2. Read `.claude/tmp/overhauler/baseline.md` for test baseline values
3. Read `.claude/tmp/overhauler/findings-stepN.md` for each step's findings counts
4. Read each `plans/overhaul-step{N}-*/tasks.md` for fixed/deferred/skipped counts

```markdown
## Overhaul Summary

**Solution:** [solution file path]
**Projects:** N projects (M source + K test)
**Baseline:** X tests passing
**Status:** Awaiting final review — no code committed yet

### Step Results

| Step | Plan | Findings | Fixed | Deferred | Skipped |
|------|------|----------|-------|----------|---------|
| Solution Infra | (mandatory — no plan) | — | .slnx + CPM | — | — |
| Modernize | `plans/overhaul-step3-modernize/` | 12 | 8 | 3 | 1 (INFO) |
| Cross-cutting | `plans/overhaul-step4-cross-cutting/` | 7 | 5 | 2 | 0 |
| Performance | `plans/overhaul-step5-performance/` | 4 | 3 | 1 | 0 |
| Concurrency | `plans/overhaul-step6-concurrency/` | 2 | 2 | 0 | 0 |
| Security | `plans/overhaul-step7-security/` | 3 | 3 | 0 | 0 |
| Cleanup | (automatic — no plan) | — | sorted props + editorconfig | — | — |
| **Total** | | **28** | **21** | **6** | **1** |

### Test Verification
- Baseline: X tests passing
- Final: Y tests passing (Z new tests added)
- Regressions: none

### Deferred Findings
[List each deferred finding with ID, description, and reason for deferral]

### Next Steps
All changes are uncommitted. Ready for your review:
- `git diff` to review all changes
- `git add [specific files]` + `git commit` when satisfied
- Or revert specific files before committing
```

---

## Git Operations

Only now may git operations happen. The user decides when and how:
- Commit all changes as one commit
- Create separate commits per step
- Revert specific changes before committing

**Do NOT run `git commit`, `git push`, or any git write operations unless explicitly requested.**
