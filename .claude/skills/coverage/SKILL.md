---
name: coverage
description: >
  Code coverage skills: fix low coverage, enforce thresholds in CI, or audit coverage.config.xml
  for drift. Determines mode from user intent automatically.
  Triggers on: coverage fix, low coverage, improve coverage, fix low coverage, coverage gate,
  coverage threshold, enforce coverage, minimum coverage, coverage enforcement, add coverage gate,
  audit coverage config, coverage config drift, coverage.config.xml, check coverage targets,
  fix coverage config, stale coverage config, coverage missing project, coverage work, coverage skill.
argument-hint: "[fix | gate | config-audit]"
---

# Coverage Skill

Determine mode from user intent, then execute.

| User wants to... | Mode | Key phrases |
|-------------------|------|-------------|
| Fix classes with low test coverage | **Fix** (below) | coverage fix, low coverage, improve coverage |
| Add/maintain coverage threshold in CI | **Gate** (below) | coverage gate, coverage threshold, enforce coverage |
| Audit coverage.config.xml for drift | **Config Audit** (below) | audit coverage config, coverage config drift, coverage.config.xml |

If ambiguous, ask which coverage task they need.

---

## Mode: Fix

Parse coverage output, identify lowest-covered classes, route to `fill-test-gaps` for test generation.

### Input Formats

Accepts three formats (pasted as `$ARGUMENTS` or message body):
- **Format A: Spectre Console ANSI** — `[#hex]-  NN.N%[/]  ClassName`
- **Format B: Pipe/tab table** — `| Class | Line Coverage | Branch Coverage |`
- **Format C: Summary text** — `ClassName  NN.N%`

### Workflow

1. **Parse input** — extract `(class_name, coverage_percentage)` pairs
2. **Rank and filter** — sort ascending, remove 100% and test classes, take bottom 15
3. **Present selection** — show ranked list, ask: "Which classes? (numbers, 'all', or 'top 5')"
4. **Locate source files** — `fd -e cs {ClassName}.cs src/`
5. **Dispatch to fill-test-gaps** — spawn agents (up to 3 parallel) for selected classes
6. **Summary** — report before coverage + tests generated

### Rules

- Never skip the selection prompt
- Parallel limit of 3 fill-test-gaps agents
- This skill parses output — it does not run coverage. Direct users to run coverage tools first.

---

## Mode: Gate

Add or verify a coverage threshold enforcement step in CI.

### Steps

1. Read `.github/workflows/ci.yml` and `coverage.config.xml`
2. Determine threshold (user-specified or default 80% line coverage)
3. If no enforcement exists, propose a `reportgenerator` step that fails the build below threshold
4. Wait for user approval before writing
5. Verify `reportgenerator` is in dotnet tool manifest
6. Apply changes + validate YAML

### Rules

- Never lower an existing threshold without approval
- Always wait for approval before writing workflow changes
- If `coverage.config.xml` is missing, warn — run config-audit first

---

## Mode: Config Audit

Cross-reference `coverage.config.xml` against `the solution file` to catch silent drift.

### Steps

1. Read `coverage.config.xml` — extract `<ModulePath>` patterns in Include/Exclude
2. Read `the solution file` — derive expected DLL names per project
3. Classify includes: match against src/ DLLs. Flag **dead entries** (no matching project) and **missing coverage** (no include pattern)
4. Classify excludes: verify test/benchmark assemblies still exist
5. Render audit report with missing/dead/valid counts
6. If issues found, ask once: "Apply suggested fixes?" — add missing, remove dead, preserve comments

### Notes

- DLL name may differ from project name — check `.csproj` for `<AssemblyName>` override
- Only `src/` projects warrant coverage inclusion
- Regex patterns use .NET syntax — escape dots as `\.`
