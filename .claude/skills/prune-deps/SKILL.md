---
name: prune-deps
description: >
  Find and remove unused NuGet packages and stale project references in a .NET
  solution. Reviews Directory.Packages.props for PackageVersion entries no project
  references, reviews every .csproj for PackageReference and ProjectReference entries
  the project's code no longer uses, then removes approved entries and verifies the
  build. Avoids the false-positive traps (transitive pins, analyzers, runtime-only
  packages). Triggers on: unused packages, prune dependencies, dead dependencies,
  remove unused packages, unused package references, unused project references, stale
  references, clean up Directory.Packages.props, unneeded references, dependency prune,
  orphaned packages, are these packages still used.
argument-hint: "[--report-only] [--no-mcp] [--project NAME]"
allowed-tools: Read, Glob, Grep, Bash, Edit, Agent
effort: high
---

# Prune Dependencies

Find unused NuGet packages and stale references, then remove the confirmed-dead ones.

Three targets:
1. **Orphaned central packages** — `PackageVersion` in `Directory.Packages.props` that no project references and that isn't a deliberate transitive pin.
2. **Unused `PackageReference`** — a direct package reference whose namespaces/types the project's code never uses.
3. **Unused `ProjectReference`** — a project-to-project reference where the referencing project uses no public type from the referenced project.

**The hard part is avoiding false positives.** A package with zero `using` directives is not automatically unused — analyzers, source generators, runtime-only packages, and transitive pins all look "unused" to a naive scan. Read `references/false-positives.md` before flagging anything.

## Usage

```
/prune-deps                  # Full audit; remove approved findings; verify build
/prune-deps --report-only    # Read-only — produce findings, never edit
/prune-deps --no-mcp         # Skip roslyn MCP; grep-only usage analysis
/prune-deps --project Core   # Scope code-usage analysis to one project
```

## Autonomy

Phase 1–3 are read-only (file reads, grep, `dotnet list`, MCP queries) — pre-approved
per the auto-approval policy; proceed without prompting. **Removals (Phase 5) require a
single `AskUserQuestion` approval gate** per the repo's "ask before fixing" convention.
`--report-only` skips Phases 4–5 entirely.

---

## Phase 1: Inventory

Gather the raw facts. Read-only.

1. **Projects** — `scripts/solution-inventory.sh --json` → solution file, every project
   (`path`, `kind` = src/test, `packable`). If `solution` is empty, error and stop.
2. **Central package versions** — Read `Directory.Packages.props`. Extract every
   `PackageVersion Include="X"`. Note `CentralPackageTransitivePinningEnabled` (it's
   `true` here — see Directory.Build.props), which changes how orphans are judged.
   Capture the comment immediately above each entry — `<!-- Transitive pins -->` and
   similar markers signal intentional pin-only entries.
3. **Direct package references** — Grep all `.csproj` **and** `Directory.Build.props`
   for `PackageReference Include="X"`. Record which project (or "all", for
   Directory.Build.props) references each, plus `PrivateAssets`/`IncludeAssets`.
   ```bash
   rg -o 'PackageReference Include="[^"]+"' --no-filename -g '*.csproj' -g 'Directory.Build.props'
   ```
4. **Project references** — Grep all `.csproj` for `ProjectReference Include="X"`.
   Build the referencing→referenced edge list.

---

## Phase 2: Orphaned Central Packages

A `PackageVersion` entry is a **candidate orphan** if no `PackageReference` (in any
csproj or Directory.Build.props) matches its name.

For each candidate, classify before flagging (see `references/false-positives.md`):

- **Transitive pin** (comment marks it, or it appears in the restore graph as a
  transitive dependency) → **NOT unused**. With transitive pinning enabled, version-only
  entries that pin a transitively-referenced package are legitimate. Confirm presence:
  ```bash
  dotnet list "$SLN" package --include-transitive 2>/dev/null | rg -i 'PackageName'
  ```
  If the package shows as transitive in any project, the pin is doing its job — skip.
- **Truly orphaned** (not referenced directly, not present transitively, no pin comment)
  → `[HIGH]` orphaned central package.

---

## Phase 3: Unused References (per project)

For each src/test project, determine real usage. Prefer the roslyn MCP; fall back to
grep when `--no-mcp` or the MCP is unavailable.

### 3a. Unused PackageReference

For each direct `PackageReference` on a project (exclude Directory.Build.props analyzer
block — those are solution-wide and judged separately):

1. **Skip non-code packages** — analyzers, `PrivateAssets="all"` source-only packages,
   test SDK/runner packages (`Microsoft.Testing.Extensions.*`, `xunit.*`), and known
   runtime-only/DI-glue packages don't surface as `using` directives. See
   `references/false-positives.md` for the skip list. Flagging these is almost always wrong.
2. **For the rest**, resolve the package's root namespace(s) and check usage:
   - **MCP**: `find_symbol` / `find_references` for the package's public types in the project.
   - **grep fallback**: `rg "using <RootNamespace>" <projectDir>` plus fully-qualified usage.
3. Zero usage across the project → `[MEDIUM]` unused PackageReference (downgrade to `[LOW]`
   if uncertain — namespace inference is heuristic).

### 3b. Unused ProjectReference

For each `ProjectReference` edge A→B:

1. **MCP**: `get_project_graph` to confirm the edge, then `find_references` on B's public
   types scoped to A. No A-side reference to any B type → unused.
2. **grep fallback**: collect B's root namespace and public type names; `rg` them in A's
   source. No hit → unused.
3. Confirmed no usage → `[HIGH]` unused ProjectReference. **Caveat**: a reference may exist
   purely to force build order or to ship an analyzer/source generator
   (`OutputItemType="Analyzer"` / `ReferenceOutputAssembly="false"`) — check the
   `ProjectReference` attributes before flagging; those are intentional.

---

## Phase 4: Report

Group findings by severity (HIGH → MEDIUM → LOW). For each:

```
[HIGH] Directory.Packages.props: PackageVersion "Foo.Bar" — orphaned
       Not referenced by any project; not present in the restore graph as a transitive dep.
       Remove the <PackageVersion Include="Foo.Bar" .../> line.

[HIGH] tests/X/X.csproj: ProjectReference "../../src/Y/Y.csproj" — unused
       X uses no public type from Y. Remove the <ProjectReference .../> line.

[MEDIUM] src/Z/Z.csproj: PackageReference "Baz" — no detected usage
       No `using Baz` and no fully-qualified Baz.* usage found. Verify before removing.
```

End with a verdict: **CLEAN** (no findings) | **PRUNABLE** (findings present).

If `--report-only`, stop here.

---

## Phase 5: Apply (approval-gated)

1. Present the prunable findings via a single `AskUserQuestion` (multiSelect) — let the
   user pick which to remove. Default-recommend HIGH findings.
2. For approved entries: `Edit` the relevant file to delete the exact line
   (`PackageVersion`, `PackageReference`, or `ProjectReference`). Remove now-dangling
   comments above a deleted `PackageVersion`.
3. **Verify once** after all edits: `scripts/check.sh` (format → build → targeted tests).
   A restore/build failure means a flagged item was actually needed — revert that edit
   and report it as a false positive.
4. If any source `.cs` references the package/project under a name the heuristic missed,
   the build catches it. Do not re-flag it.

---

## Guidelines

- **False positives are the failure mode.** When unsure, downgrade severity and say
  "verify before removing" — never silently delete. Read `references/false-positives.md`.
- **One verification build.** Batch all removals, then run `scripts/check.sh` once.
- **Transitive pins are not orphans.** With `CentralPackageTransitivePinningEnabled=true`,
  version-only entries are a feature, not dead weight.
- **Analyzers live in Directory.Build.props.** They apply to every project and never
  appear as `using`. Never flag them as unused PackageReferences.
- **Repo-agnostic.** No hardcoded project or package names; derive everything from inventory.
- **Update the analyzer README** only if an analyzer package itself is removed (rare).
