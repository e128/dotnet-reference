---
name: benchmark-ci
description: >
  Run benchmarks and check regressions, add benchmark gating to CI, or audit GitHub
  Actions workflows. Determines mode from user intent.
when_to_use: >
  run benchmarks, benchmark check, check for regressions, benchmark regression,
  update baseline, run bench, perf check, performance regression, benchmark gate,
  ci benchmark, review github actions, audit ci yaml, workflow audit, benchmark work, ci skill.
argument-hint: "[run | gate | yaml-review | --plan]"
---

# Benchmark & CI Skill

Determine mode from user intent, then execute.

| User wants to... | Mode | Key phrases |
|-------------------|------|-------------|
| Run benchmarks and check regressions | **Runner** (below) | run benchmarks, benchmark check, check for regressions, update baseline, perf check |
| Add benchmark regression gating to CI | **Gate** (below) | benchmark gate, ci benchmark, add benchmark to ci, gating benchmarks |
| Audit GitHub Actions workflow YAML | **YAML Review** (below) | review github actions, audit ci yaml, check workflow versions, outdated actions |

If ambiguous, ask which task they need.

---

## Mode: Runner

Run benchmarks and compare against the stored baseline in `benchmarks/baselines.json`.

### Determine intent

- "update baseline" or "save baseline" -> **Update Baseline** path
- Otherwise -> **Run and Compare** path

### Run and Compare

1. Run the benchmark suite (BenchmarkDotNet, Release config) with an optional
   `--filter {ClassName}`, then diff the results against `benchmarks/baselines.json`
2. Interpret output — Status per benchmark: `OK`, `IMPROVED`, `REGRESSED`, `ALLOC_UP`, `NEW`
3. If `REGRESSED`/`ALLOC_UP`: report table with baseline/current/change, suggest investigating
4. If clean: report "All benchmarks clean. {N} improved, {M} stable."

### Update Baseline

1. Confirm intent (overwrites `benchmarks/baselines.json`)
2. Re-run the suite and write the current results to `benchmarks/baselines.json`
3. Stage: `git add benchmarks/baselines.json`

### Notes

- Default regression threshold is 20%. Override with `--threshold N`
- Benchmarks run in Release configuration
- Baseline changes should be committed alongside the code that caused them

---

## Mode: Gate

Add or verify a benchmark regression gate in the CI workflow.

### Steps

1. Read `.github/workflows/ci.yml` — check for existing benchmark job
2. Check `benchmarks/baselines.json` exists
3. If no benchmark job, propose YAML for a `benchmark` job (runs on main pushes only, runs the benchmark suite with a 20% regression threshold, uploads artifacts)
4. **SHA-pin all actions** — reuse existing SHAs from the workflow (never tag references)
5. Use `AskUserQuestion` to get approval before writing workflow changes:
```
AskUserQuestion({
  questions: [{
    question: "Apply benchmark gate changes to CI workflow?",
    header: "CI gate",
    options: [
      { label: "Apply", description: "Write changes to ci.yml" },
      { label: "Cancel", description: "No changes made" }
    ],
    multiSelect: false
  }]
})
```
6. Apply changes + validate YAML syntax

### Rules

- Always SHA-pin actions; avoid tag references
- Run benchmarks on main only, never on PRs
- Use `AskUserQuestion` for approval before writing workflow changes

---

## Mode: YAML Review

Audit `.github/workflows/*.yml` for outdated actions and improvement opportunities.

### Steps

1. Glob `.github/workflows/*.yml`, read all
2. Extract pinned actions (`uses: owner/repo@ref`)
3. Fetch latest versions: `gh api repos/{owner}/{repo}/releases/latest --jq '.tag_name'`
4. Audit for redundant patterns: duplicate `dotnet restore`, missing `--no-build`, missing `paths:` filters
5. Render findings table (Severity: HIGH/MEDIUM/LOW)
6. If `--plan` flag AND HIGH findings: create a plan for the CI improvements

### Notes

- Requires `gh` CLI authenticated
- If `gh auth` fails, report findings without version comparisons
