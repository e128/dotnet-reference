# Fix Cycle (Steps 3-8)

After user approves findings from any step:

**0. Write approved findings:**

Write `.claude/tmp/overhauler/approved-step{N}.md` immediately:
```markdown
# Approved Findings — Step N
- [ID] [description] ([file:line])
...
```
Count the approved findings. This file is the single source of truth for resumption and Step 10 summary.

**1. Organize work (only if ≥ 8 approved findings):**

Fewer than 8 findings: skip plan creation and execute directly from `approved-step{N}.md`.

8 or more findings — **if dev-planning is available**, create a structured plan:
```
plans/overhaul-step{N}-{name}/
├── overhaul-step{N}-{name}-plan.md     ← approved findings as tasks (file:line + fix)
├── overhaul-step{N}-{name}-context.md  ← scope, baseline count, approved vs deferred
└── overhaul-step{N}-{name}-tasks.md    ← one task per finding + build/test tasks at end
```

**If dev-planning is not available**, organize findings in `approved-step{N}.md` with checkboxes and execute directly. The plan structure is an enhancement, not a requirement.

**2. Execute all phases immediately:**
1. For each approved finding: Read → apply minimal fix → Edit
2. **Spawn `build-validator` agent** — do not run `dotnet build` or `dotnet test` directly in main context; use the agent which returns only errors, warnings, and test failures
3. Mark tasks complete as you go; update context.md with final state

**3. Report and continue:** Findings fixed / build result / test count vs baseline.
Fix any failures before proceeding to the next step.

**Fix rules:**
- Fix only what's approved — don't refactor surrounding code
- One logical change per finding; if a fix introduces a new warning, fix it too
- No git commits or pushes until Step 10
- **Tests:** Only change existing tests for refactoring reasons (renamed method, moved class, changed signature). Never change assertions to make a failing test pass — if a test fails, the production fix is wrong
- **New tests:** Must be in the CI category so they appear in `--filter-trait "Category=CI"` runs
- **Unsure?** Ask `sme-researcher` before applying a fix

## Finding ID Prefixes

| Prefix | Step |
|--------|------|
| `MI` | Modernize — Infrastructure (Step 3) |
| `M` | Modernize — Language (Step 3) |
| `CC` | Cross-cutting (Step 4) |
| `P` | Performance (Step 5) |
| `T` | Concurrency/Threading (Step 6) |
| `S` | Security (Step 7) |
