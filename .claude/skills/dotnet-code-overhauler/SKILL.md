---
name: dotnet-code-overhauler
version: "0.9"
description: >
  Systematic .NET code overhaul loop. Establishes a test baseline, modernizes language usage,
  fixes cross-cutting design issues, runs performance, concurrency, and security reviews, and
  verifies all CI tests pass. Presents severity-rated findings for user-directed action at each step.
  Triggers on: code overhaul, modernize codebase, .NET modernization, overhaul loop, code review pass,
  fix all warnings, language modernization, primary constructors, collection expressions, overhaul solution.
argument-hint: "[solution-file or directory]"
allowed-tools: Read, Glob, Grep, Bash, Edit, Write, Agent
---

# .NET Code Overhauler

Systematic overhaul loop combining language modernization, design review, and specialist analysis.
Every step produces findings for user approval before any code is changed.

**Step detail files:** When you reach a step marked `-> read steps/stepN.md`, Read that file from
`${CLAUDE_SKILL_DIR}/steps/stepN.md` before proceeding.

## Usage

```
/dotnet-code-overhauler [scope]
```

Scope is a solution file (`.sln`/`.slnx`) or directory. Resolution order:
1. **Solution file given** -> use directly
2. **Directory given** -> Glob for `.slnx`, then `.sln`; if one found use it; if multiple, ask; if none, search parent directories
3. **No scope** -> treat as `.`

In-scope once solution is resolved: all `.cs` files, `.csproj` files, Dockerfiles, CI/CD workflows,
and config files (`.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `.gitignore`,
`renovate.json`, `global.json`, `nuget.config`) at the solution root.
Test projects identified by name containing `Test`/`Tests`, referencing xUnit/NUnit/MSTest, or having `<IsTestProject>true</IsTestProject>` in their `.csproj`.

## When NOT to Use

- **Solution with >500 .cs files** — run a scoped overhaul on one project directory at a time
- **Hotfix or time-sensitive change** — this skill is for planned maintenance windows, not emergency patches
- **Single-issue fix** — if you know exactly what needs fixing, run a targeted analysis using the pattern files in `steps/` instead of running the full loop

## The Overhaul Loop

Each step -> findings table -> user picks what to fix -> **Fix Cycle** -> next step.
**No git commits or pushes until Step 10.**

```
R. Resume Check (always first — read progress journal)
0. Precondition: Detect test convention
1. CI Test Baseline
2. Solution Infrastructure + Strict Analysis (mandatory)
3. Modernize Language Usage        -> plan -> execute
4. Cross-Cutting Design Review     -> plan -> execute
5. Performance Review (specialist) -> plan -> execute
6. Concurrency Review (specialist) -> plan -> execute
7. Security Review                 -> plan -> execute
8. Cleanup & Organization          -> execute
9. Verify CI Tests
10. Final Review (user commits/pushes when satisfied)
```

---

## Step R: Resume Check (Always First)

Before doing anything else, check for an in-progress run:

```bash
cat .claude/tmp/overhauler/progress.md 2>/dev/null
```

If `progress.md` exists:
- Read it to see which steps are `DONE`, `SKIPPED`, or `IN-PROGRESS`
- Read `.claude/tmp/overhauler/baseline.md` to recover test baseline
- Skip any step already marked `DONE` or `SKIPPED`
- Resume from the first step not yet `DONE`
- For steps with a `DONE` status, include their one-line summary in the Step 10 Overhaul Summary

If `progress.md` does not exist, start fresh:

```bash
mkdir -p .claude/tmp/overhauler
```

Initialize `progress.md`:
```markdown
# Overhaul Progress
Solution: [path]
Started: YYYY-MM-DD
```

**Checkpoint after each step:** Append `- Step N ([name]): DONE — [one-line summary]` (or `SKIPPED — [reason]`) to `progress.md`.

**Uncertainty rule:** When unsure about a pattern or library behavior, use an `Explore` agent to research the question before reporting a finding.

## Portability Layers

This skill works in three tiers — core always works, enhanced uses project-specific agents if present.

### Core (always works)
Steps 0-10 use `dotnet` CLI commands, `Explore` agents, and `.claude/tmp/` state. Zero external dependencies.

### Enhanced (when available)
- **`build-validator` agent** -> if present, use for build+test; otherwise use `${CLAUDE_SKILL_DIR}/scripts/build.sh` and `test.sh`
- **`sme-researcher` agent** -> if present, use for uncertainty; otherwise use `Explore` agent
- **`tdd-loop-optimizer` agent** -> if present, use for batch fix cycles; otherwise apply fixes sequentially
- **dev-planning** -> if `/dev-planning` skill exists and >=8 findings, create a plan; otherwise execute directly from `approved-step{N}.md`

### Project-specific (optional)
- **`${CLAUDE_SKILL_DIR}/conventions.md`** -> if present, read for coding standards, analyzer inventory, severity overrides, auto-approved fixes, and test relaxations. If absent, use sensible .NET defaults.
- **`${CLAUDE_SKILL_DIR}/lessons/*.md`** -> if present, read for project-specific false positives and compiler edge cases.

---

## Step 0: Precondition — Detect Test Convention

Detect the test framework (xUnit/NUnit/MSTest) and category convention by grepping test projects for `Trait`, `Category`, `TestCategory` attributes. Also check `.runsettings`, `Directory.Build.props`, and CI workflow files for existing `--filter` arguments.

**Detect test runner** — check `global.json` for `"test": { "runner": "Microsoft.Testing.Platform" }`:
- **MTP detected:** Use `dotnet test --solution <slnx> -- --filter-trait "Category=CI"` syntax. The `--` separator passes args to MTP; `--filter` (VSTest syntax) does NOT work.
- **VSTest (no MTP config):** Use `dotnet test --filter "Category=CI"` syntax.
- **.NET 10 SDK without MTP config:** Flag as a Step 2 finding — MTP is required on .NET 10.

**Record the detected convention** in `.claude/tmp/overhauler/test-convention.md`:
```markdown
# Test Convention
Framework: [xUnit|NUnit|MSTest|mixed]
Runner: [MTP|VSTest]
Category attribute: [exact attribute found]
Filter command: [the exact test invocation to use]
Test count: [number of tests matching]
```

- **Found:** Report and proceed. **Not found:** Ask user to choose: (1) add category attributes, (2) different value, (3) run unfiltered.

---

## Step 1: CI Test Baseline

**CI tests:** Use the exact filter command recorded in `.claude/tmp/overhauler/test-convention.md`.
Do not hardcode a filter — always read the convention file from Step 0.
Record: total, passed, failed, skipped, pre-existing failures.
Stop if failures — user decides whether to proceed with a broken baseline.

**Persist baseline to disk immediately after recording:**
Write `.claude/tmp/overhauler/baseline.md`:
```markdown
# Overhaul Baseline
CI Tests: X passed, Y failed, Z skipped
```
Step 9 reads this file for comparison — never rely on memory across steps.

---

## Step 2: Solution Infrastructure (Mandatory) -> read steps/step2.md

Covers: .slnx conversion, Central Package Management, strict code analysis, `.gitignore` coverage.
This step executes immediately — no findings table, no approval gate.

---

### Analysis Steps Pattern (Steps 3-7)

Steps 3-7 each follow the same cycle: launch analysis agent(s), collect findings to
`.claude/tmp/overhauler/findings-step{N}.md`, present severity-rated findings for approval,
then run the Fix Cycle for approved items. Only domain-specific details are noted below.

## Step 3: Modernize -> read steps/step3.md

TFM & package updates (Agent 0), Dockerfile review (Agent 0b), 4 parallel language agents.
**Findings ID prefixes:** `MI` (infrastructure), `M` (language)

---

## Step 4: Cross-Cutting Design Review -> read steps/step4.md

5 parallel `Explore` agents: error handling, logging, DI & lifetime, organization, SOLID design.
**Findings ID prefix:** `CC`

---

## Step 5: Performance Review -> read steps/step5.md

Explore agent with grep patterns from `steps/step5-patterns.md`.
**Findings ID prefix:** `P`

---

## Step 6: Concurrency Review -> read steps/step6.md

Explore agent with grep patterns from `steps/step6-patterns.md`.
**Findings ID prefix:** `T`

---

## Step 7: Security Review -> read steps/step7.md

Two parts: code security (patterns from `steps/step7-patterns.md`, report only) + supply chain (Actions SHA pinning, Dockerfile digest pins). Merged into one findings table.
**Findings ID prefix:** `S`

---

## Step 8: Cleanup & Organization -> read steps/step8.md

Covers: sort `Directory.Packages.props`, sort `.editorconfig` rules, review/remove stale
suppressions, verify build. Executes immediately — no approval gate.

---

## Step 9: Verify CI Tests -> read steps/step9.md

---

## Step 10: Final Review -> read steps/step10.md

---

## Fix Cycle (Steps 3-8) -> read steps/fix-cycle.md

After user approves findings: create a plan (if dev-planning available and >=8 findings), execute approved fixes, build+test, report results.

## Self-Improvement (Mandatory)

This skill must get better with every use. After completing any overhaul cycle:

1. **Capture modernization patterns** — If a new C#/.NET language feature or API replacement proved effective, add it to the modernization checklist in this SKILL.md.
2. **Record analyzer evolution** — If new Roslyn analyzer rules required code changes, document the rule ID and fix pattern.
3. **Log false-positive findings** — If the overhaul flagged code that was actually correct, add it as a known exception.
4. **Update specialist triggers** — If the concurrency or security specialist agents caught issues that the main overhaul missed, refine their trigger criteria.

### Project-Specific Lessons

If `${CLAUDE_SKILL_DIR}/lessons/` contains `.md` files, read them during Step R (Resume Check) to load project-specific false positives and compiler edge cases.

## CLI Gotcha

`dotnet list` does **not** accept `--solution`. Use the positional argument form: `dotnet list <solution-file> package --outdated`.

## Guidelines

- **Parallel agents** — always launch research agents in a single message
- **Don't fix without approval** — present findings table first; user picks what to fix
- **Minimal fixes** — don't refactor surrounding code; just fix the finding
- **Always build+test** — verify after every batch of fixes
- **Acknowledge intentional patterns** — mark as INFO, not as issues
- **Ignore `ConfigureAwait(false)`** — enforced by analyzers; not a finding for this skill
- **Apply conventions** — if `conventions.md` exists, follow its coding standards. If absent, match the existing code style.
