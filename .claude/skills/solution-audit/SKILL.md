---
name: solution-audit
description: >
  Audit .NET solution health across 10 dimensions: dependency graph, solution sync,
  CPM compliance, package health, framework consistency, IVT & encapsulation, build
  config, analyzer config, NuGet config, and suppression hygiene. Works with any
  .NET solution (.slnx or .sln). Parses all config files once, spawns 3 parallel
  agents, and produces a severity-grouped report with a Mermaid dependency graph.
  Triggers on: solution audit, audit solution, project graph, dependency audit,
  solution health, audit projects, check solution.
argument-hint: "[--no-graph] [--min-severity LEVEL] [--dry-run]"
allowed-tools: Read, Glob, Grep, Bash, Agent
effort: high
---

# Solution Audit

10-dimension audit of a .NET solution's structural health. Parses project and config
files once in the orchestrator, spawns 3 parallel analysis agents, and produces a
severity-grouped report with a Mermaid dependency graph.

## Usage

```
/solution-audit                     # Full audit
/solution-audit --no-graph          # Skip Mermaid generation
/solution-audit --min-severity HIGH # Filter to HIGH+ only
/solution-audit --dry-run           # Parse and show project table only
```

## Architecture

```
/solution-audit (skill — orchestrator)
  Phase 1: Parse solution + all .csproj + Directory.Build.props/.targets
           + Directory.Packages.props + nuget.config + global.json
           + .globalconfig + .editorconfig + suppression scan
  Phase 2: Spawn 3 agents in parallel
    ├─ Agent A: Structure    (D1 dependency graph, D2 solution sync)
    ├─ Agent B: Packages     (D3 CPM, D4 package health, D9 NuGet config)
    └─ Agent C: Config       (D5 framework, D6 IVT, D7 build, D8 analyzers, D10 suppressions)
  Phase 3: Collect results, generate Mermaid, build report
  Phase 4: Print report
```

Orchestrator parses files (once) and generates the Mermaid; agents receive structured text
and analyze. 3 agents (not 10) because dimensions cluster by shared data needs. All Phase 1
commands are read-only — proceed through parsing without prompting.

---

## Phase 1: Parse & Build Structured Data

### 1.1 Enumerate the solution and projects

```bash
scripts/solution-inventory.sh --json
```

Returns the solution file, every project (`path`, `kind` = src/test, `packable`), and
the README inventory in one call. If `solution` is empty, error and stop.

### 1.2–1.9 Parse all config sources

Read `references/parse-steps.md` for detailed extraction steps covering: global.json,
solution file, all .csproj files, Directory.Build.props, Directory.Packages.props,
nuget.config, .globalconfig/.editorconfig, and suppression scan.

### 1.10 Scan for orphans

Use the `projects[].path` list from `scripts/solution-inventory.sh --json` (step 1.1).
Compare against the solution file project list. Flag any on disk but not in solution.

### 1.11 Build structured project table

Combine all data into a text table. Format as markdown for agent consumption:

```markdown
## Projects
| Project | Folder | SDK | TFM | Output | Refs | IVT | Packages |
|---------|--------|-----|-----|--------|------|-----|----------|

## Solution Folders
/src/: ProjectA, ProjectB ...
/tests/: ProjectA.Tests ...

## Directory.Build.props Defaults
TargetFramework: net10.0
...

## Central Packages (Directory.Packages.props)
PackageA (1.0.0), PackageB (2.3.0) ...

## NuGet Config
Has <clear />: yes, Sources: nuget.org (HTTPS, V3) ...

## SDK Info
SDK: 10.0.201, TFM: net10.0, Runner: mtp

## Orphans
(none)

## Suppressions
src/Foo.cs:42: #pragma warning disable CA1234
...
```

**If `--dry-run`:** Print the project table and stop.

---

## Phase 2: Spawn 3 Parallel Agents

Spawn all in a **single message**. Use `subagent_type: "general-purpose"`. Each agent
applies the severity rules for its dimensions from
[references/checks-catalog.md](references/checks-catalog.md) (Severity Rules + Edge Cases).

| Agent | Dimensions       | Pass to it                                                                                                  |
| ----- | ---------------- | ----------------------------------------------------------------------------------------------------------- |
| A     | D1, D2           | Project table, folder map, orphan list, each project's source dir (for the D1 usage grep)                   |
| B     | D3, D4, D4b, D9  | Project table, Directory.Packages.props (with comments), nuget.config, SDK info, source dirs (D4b grep)     |
| C     | D5–D8, D10       | Project table, Directory.Build.props, .globalconfig, .editorconfig, suppression grep results                |

Before spawning, run `dotnet list <SLN> package --include-transitive` and pass the result
to Agent B so it can distinguish orphaned central packages from transitive pins. Agent A
returns its adjacency list in an `ADJACENCY: ... END_ADJACENCY` block for Mermaid.

---

## Phase 3: Collect & Generate

1. **Parse agent outputs** — extract `[SEVERITY] target: description` lines
2. **Generate Mermaid** (unless `--no-graph`):
   - Solid arrows (`-->`) for src→src references
   - Dashed arrows (`-.->`) for test→src references
   - Red edges for circular deps
   - Color-code: src=blue, test=green
3. **Group by severity** — CRITICAL → HIGH → MEDIUM → LOW
4. **Apply `--min-severity` filter** if provided

---

## Phase 4: Report

Print a structured report with: header (solution name, project/package counts, SDK, TFM, finding totals), findings grouped by severity (CRITICAL → HIGH → MEDIUM → LOW) with `[D#] target: description` format, dependency graph (Mermaid block), and verdict.

**Verdict:** **PASS** (no CRITICAL/HIGH) | **WARN** (HIGH but no CRITICAL) | **FAIL** (CRITICAL present)

---

## Overlap

`/solution-audit` only *reports* — it never fixes. For overlapping concerns, use the focused tool:
- **D1/D3/D4b (dependency rot)** → `/prune-deps` removes confirmed-dead entries with a verification build.
- **Config/package health** → `/dotnet-overhaul` Step 2 fixes the same ground. Run one or the other, not both.

## Guidelines

- **Don't fix during audit** — produce findings; let the user decide
- **No external state** — no baseline, no tmp files; the report is the output
- **Repo-agnostic** — no hardcoded project names; prefer `scripts/*.sh`, fall back to raw commands
