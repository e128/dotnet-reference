# Solution Audit — Checks Catalog

All 13 dimensions with severity mappings and edge cases.

## Overview

| #   | Dimension             | Agent | Key checks                                              |
| --- | --------------------- | ----- | ------------------------------------------------------- |
| D1  | Dependency Graph      | A     | Circular deps, direction violations, redundant refs     |
| D2  | Solution Sync         | A     | Orphans, folder mismatches, missing test projects       |
| D3  | CPM Compliance        | B     | Hardcoded versions, overrides, unused central packages  |
| D4  | Package Health        | B     | Test-only leaks, deprecated, license compliance         |
| D5  | Framework Consistency | C     | TFM drift, multi-target without justification           |
| D6  | IVT & Encapsulation   | C     | Stale targets, legacy syntax, naming mismatches         |
| D7  | Build Config          | C     | Directory.Build.props correctness, analyzer setup       |
| D8  | Analyzer Config       | C     | .globalconfig/.editorconfig consistency                 |
| D9  | NuGet Config          | B     | nuget.config hygiene, audit properties                  |
| D10 | Suppression Hygiene   | C     | Unjustified pragmas, broad suppressions, security rules |
| D11 | Output Type & AOT     | C     | OutputType conventions, AOT chain, PublishAot on Library|
| D12 | Public API Surface    | C     | Public types/methods missing XML doc summaries          |
| D13 | Lock Files & Pruning  | B     | RestorePackagesWithLockFile, lock committed, pruning     |

## Severity Rules

### Agent A — Structure

**D1 — Dependency Graph** (build a directed graph from ProjectReferences):
- Circular dependency (DFS) → `[CRITICAL]`
- src→test or src→benchmark reference → `[CRITICAL]`
- Isolated project (0 edges) → `[LOW]`
- Redundant transitive ref (A→B→C and A→C) → `[MEDIUM]`
- Unused ProjectReference (A→B but A uses no public type from B) → `[HIGH]`. Grep A's
  source for B's root namespace and public type names; no hit → unused. **Skip** edges with
  `OutputItemType="Analyzer"` or `ReferenceOutputAssembly="false"`. For removal, defer to `/prune-deps`.
- Return adjacency list in `ADJACENCY: ... END_ADJACENCY` block for Mermaid.

**D2 — Solution Sync:**
- Folder assignment vs disk path mismatch → `[HIGH]`
- Orphan .csproj on disk but not in solution → `[HIGH]`
- src library without corresponding test project → `[MEDIUM]`
- Duplicate project entries → `[HIGH]`

### Agent B — Packages

**D3 — CPM Compliance:**
- `Version=` on any PackageReference in .csproj → `[CRITICAL]`
- `VersionOverride` attribute → `[CRITICAL]`
- Analyzer packages in .csproj instead of Directory.Build.props → `[MEDIUM]`
- Orphaned central package — `PackageVersion` referenced by no project AND not a transitive pin →
  `[MEDIUM]`. With `CentralPackageTransitivePinningEnabled=true`, version-only pins are legitimate —
  confirm via `dotnet list <SLN> package --include-transitive` (or a `<!-- Transitive pin -->` comment)
  before flagging.

**D4b — Unused PackageReference** (code-usage):
- Direct `PackageReference` whose root namespace/types appear nowhere in source → `[MEDIUM]`
  (downgrade to `[LOW]` when the namespace is uncertain).
- **Skip** analyzers / `PrivateAssets="all"` source-only packages, test SDK/runner packages
  (`xunit.*`, `Microsoft.Testing.Extensions.*`), and runtime-only/DI-glue packages. For removal, defer to `/prune-deps`.

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
- `NU1903` (high) / `NU1904` (critical) not in WarningsAsErrors → `[HIGH]`

**D13 — Lock Files & Pruning:**
- Application (`OutputType=Exe`) without `RestorePackagesWithLockFile=true` → `[MEDIUM]`
- `packages.lock.json` not committed for an application project → `[MEDIUM]`
- CI restore not using `--locked-mode` → `[MEDIUM]`
- Library with committed `packages.lock.json` → `[LOW]`
- `RestoreEnablePackagePruning` explicitly false for net10.0+ → `[MEDIUM]`

### Agent C — Config & Quality

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

**D11 — Output Type & AOT:**
- Library in `src/` with `PublishAot=true` → `[HIGH]`
- `OutputType=Exe` project in `tests/` folder → `[MEDIUM]`
- `PublishAot=true` with a pre-release dependency (often lacks trim annotations) → `[HIGH]`
- `OutputType` mismatch with folder convention → `[MEDIUM]`
- `PublishAot=true` dependency missing `IsAotCompatible` → `[LOW]`

**D12 — Public API Surface:**
- Public type without an XML doc summary → `[MEDIUM]`
- Public method without XML doc → `[LOW]`

## Edge Cases

### D1 — Dependency Graph
- **Test → Exe**: valid when testing a CLI tool's public API
- **Standalone tools**: Exe with no inbound refs is intentional — flag as LOW not MEDIUM
- **Analyzer project**: often has no inbound refs from src (consumed as NuGet) — not isolated

### D2 — Solution Sync
- **Analyzer project without test project**: the analyzer test project may use a different naming convention (e.g., `MyAnalyzers.Tests` not `MyAnalyzers.Test`)
- **Shared test project**: one test project covering multiple src projects is valid

### D3 — CPM Compliance
- **Roslyn analyzer authoring**: projects targeting `netstandard2.0` may need explicit version pins for `Microsoft.CodeAnalysis.*` due to API surface requirements

### D4 — Package Health
- **SonarAnalyzer.CSharp**: LGPL-3.0 but always used with `PrivateAssets="all"` — LOW not CRITICAL
- **Analyzer packages**: never ship in output, so license is informational only
- **License patterns** (defaults when no explicit metadata): `Microsoft.*`/`System.*` → MIT; `NuGet.*`/`xunit.*`/`Roslynator.*` → Apache-2.0; `NSubstitute` → BSD-3-Clause; `BenchmarkDotNet` → MIT; `SonarAnalyzer.*` → LGPL-3.0 (verify `PrivateAssets="all"`). Approved: MIT, Apache-2.0, BSD-2/3-Clause, ISC. GPL/LGPL/AGPL as a runtime dependency (no `PrivateAssets="all"`) → CRITICAL; unknown/polyform → HIGH.

### D6 — IVT & Encapsulation
- **CLI tools**: kebab-case AssemblyName with PascalCase namespace is convention, not a mismatch

### D7 — Build Config
- **Analyzer projects**: may legitimately override `TargetFramework` to `netstandard2.0`
- **`IlcFoldIdenticalMethodBodies`**: valid in props (affects all configurations)

### D11 — Output Type & AOT
- **Exe with `PublishAot=true` in `src/`**: valid for CLI tools and standalone executables — not a library violation
- **Benchmark projects as Exe**: valid (BenchmarkDotNet requires an Exe entry point)
- **Test projects**: the Test SDK sets `OutputType` implicitly — do not flag for a missing `OutputType`

### D12 — Public API Surface
- **Test/benchmark projects**: skip entirely — not public API even when marked `public`
- **Generated code** (`*.g.cs`, `*.Generated.cs`, `obj/`) and `Program`/`Main` entry points: skip
- **Extension-method classes**: are public API — should have docs

### D13 — Lock Files & Pruning
- **Library lock files**: not recommended (consumers resolve their own graph) — LOW informational
- **First restore after enabling pruning**: a larger-than-normal lock diff is expected, not a finding
- **`--locked-mode`**: a CI-pipeline check — the skill can only flag the absence of `RestorePackagesWithLockFile` in a project, not inspect the pipeline
