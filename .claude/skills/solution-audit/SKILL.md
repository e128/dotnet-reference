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

**Design decisions:**
- Orchestrator parses files, not agents — parse once, pass structured data to all 3
- Orchestrator generates Mermaid — mechanical string concatenation is more reliable than LLM syntax
- Ad-hoc general-purpose agents — these are analysis-specific with no reuse value
- 3 agents, not 10 — dimensions cluster by shared data needs

**Autonomy:** All Phase 1 commands are read-only (file reads, grep, dotnet list).
Pre-approved per CLAUDE.md auto-approval policy — proceed through parsing without prompting.

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

Spawn all in a **single message**. Use `subagent_type: "general-purpose"`.

### Agent A: Structure (D1, D2)

Pass: project table, folder map, orphan list, and each project's source directory (for
the D1 unused-ProjectReference usage grep).

**D1 — Dependency Graph:**
- Build directed graph from ProjectReferences
- DFS for circular dependencies → `[CRITICAL]`
- src→test or src→benchmark reference → `[CRITICAL]`
- Isolated project (0 edges) → `[LOW]`
- Redundant transitive ref (A→B→C and A→C) → `[MEDIUM]`
- **Unused ProjectReference** (A→B but A uses no public type from B) → `[HIGH]`.
  Grep A's source for B's root namespace and public type names; no hit → unused.
  **Skip** edges with `OutputItemType="Analyzer"` or `ReferenceOutputAssembly="false"`
  (intentional build-order/analyzer refs, not symbol usage). For the removal workflow,
  defer to `/prune-deps`.

**D2 — Solution Sync:**
- Folder assignment vs disk path mismatch → `[HIGH]`
- Orphan .csproj on disk but not in solution → `[HIGH]`
- src library without corresponding test project → `[MEDIUM]`
- Duplicate project entries → `[HIGH]`

Return adjacency list in `ADJACENCY: ... END_ADJACENCY` block for Mermaid.

### Agent B: Packages (D3, D4, D4b, D9)

Pass: project table, Directory.Packages.props content (with comments, for transitive-pin
detection), nuget.config data, SDK info, and each project's source directory (for the D4b
unused-PackageReference usage grep). The orchestrator should run
`dotnet list <SLN> package --include-transitive` in Phase 1 and pass the result so the
agent can distinguish orphans from transitive pins.

**D3 — CPM Compliance:**
- `Version=` on any PackageReference in .csproj → `[CRITICAL]`
- `VersionOverride` attribute → `[CRITICAL]`
- Analyzer packages in .csproj instead of Directory.Build.props → `[MEDIUM]`
- **Orphaned central package** — `PackageVersion` referenced by no project AND not a
  transitive pin → `[MEDIUM]`. With `CentralPackageTransitivePinningEnabled=true`,
  version-only entries that pin a transitive dependency are legitimate — confirm via
  `dotnet list <SLN> package --include-transitive` (or a `<!-- Transitive pin -->`
  comment) before flagging. Don't count `PackageReference`s in Directory.Build.props as
  "no project."

**D4b — Unused PackageReference** (code-usage):
- Direct `PackageReference` whose root namespace/types appear nowhere in the project's
  source → `[MEDIUM]` (downgrade to `[LOW]` when the namespace is uncertain).
- **Skip** analyzers / `PrivateAssets="all"` source-only packages, test SDK/runner
  packages (`xunit.*`, `Microsoft.Testing.Extensions.*`), and runtime-only/DI-glue
  packages — they never surface as `using`. Flagging them is a false positive.
- For the removal workflow, defer to `/prune-deps`.

**D4 — Package Health:**
- Test-only package (xunit, NSubstitute) in non-test project → `[HIGH]`
- Deprecated package → `[HIGH]`
- Known GPL/LGPL runtime dependency → `[CRITICAL]` (PrivateAssets=all downgrades to `[LOW]`)
- SonarAnalyzer.CSharp without PrivateAssets=all → `[HIGH]` (LGPL-3.0)

**D9 — NuGet Config:**
- nuget.config missing → `[HIGH]`
- `<packageSources>` missing `<clear />` → `[HIGH]`
- HTTP source URL → `[CRITICAL]`
- Multiple sources without `<packageSourceMapping>` → `[HIGH]`
- NuGetAudit disabled → `[CRITICAL]`
- NuGetAuditMode not "all" for net10.0+ → `[HIGH]`

### Agent C: Config & Quality (D5, D6, D7, D8, D10)

Pass: project table, Directory.Build.props content, .globalconfig content,
.editorconfig content, suppression grep results.

**D5 — Framework Consistency:**
- Project TFM differs from Directory.Build.props default without justification → `[HIGH]`
- Multi-target without clear reason → `[LOW]`

**D6 — IVT & Encapsulation:**
- IVT target doesn't match any assembly in solution → `[HIGH]`
- Legacy `[assembly: InternalsVisibleTo]` in .cs instead of .csproj → `[MEDIUM]`
- AssemblyName/RootNamespace vs folder name mismatch → `[LOW]`

**D7 — Build Config:**
- `EnforceCodeStyleInBuild` not true → `[HIGH]`
- `EnableNETAnalyzers=true` AND `Microsoft.CodeAnalysis.NetAnalyzers` package both present → `[HIGH]`
- `WarningsNotAsErrors` missing `$(WarningsNotAsErrors);` prefix → `[HIGH]`
- `<Target>` in Directory.Build.props (should be .targets) → `[MEDIUM]`
- `ContinuousIntegrationBuild=true` without `$(CI)` condition → `[HIGH]`

**D8 — Analyzer Config:**
- Same rule in .editorconfig and .globalconfig at different severities → `[HIGH]`
- AnalysisMode in MSBuild AND category-level entries in .globalconfig → `[HIGH]`
- test .globalconfig `global_level` ≤ root → `[HIGH]`

**D10 — Suppression Hygiene:**
- Broad `#pragma warning disable` (no rule IDs) → `[CRITICAL]`
- Suppression of security rules (CA5xxx) → `[HIGH]`
- Suppression without justification comment → `[HIGH]`
- Suppression with justification → `[LOW]` informational

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

## Overlap with /prune-deps

D3 (orphaned central packages), D1 (unused ProjectReference), and D4b (unused
PackageReference) overlap with `/prune-deps`. The distinction: `/solution-audit` *reports*
these alongside its other 9 dimensions as part of a broad read-only health check;
`/prune-deps` is the focused, deeper tool — it uses the roslyn MCP for symbol-accurate
usage, applies the full false-positive taxonomy, and **removes** confirmed-dead entries
with a verification build. Use `/solution-audit` to surface dependency rot in a general
sweep; use `/prune-deps` to actually clean it up.

## Overlap with /dotnet-overhaul

Both skills audit infrastructure config (D7 build, D8 analyzers, D9 NuGet, D4 package health). The distinction: `/solution-audit` is a read-only health check; `/dotnet-overhaul` Step 2 covers the same ground but also applies fixes. Run only one — if doing a full overhaul, use that; use `/solution-audit` for lightweight config-only checks or CI gates.

## Guidelines

- **Parse once, share everywhere** — orchestrator reads all files; agents receive text data
- **Don't fix during audit** — produce findings; let the user decide
- **Parallel everything** — all 3 agents spawn in a single message
- **No external state** — no memory baseline, no tmp files; the report is the output
- **Repo-agnostic** — works with any .NET solution; no hardcoded project names
- **Scripts when available** — use `scripts/*.sh` if present, fall back to raw commands
