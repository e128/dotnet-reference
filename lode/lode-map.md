# Lode Map
*Updated: 2026-08-16T12:40:40Z*

## Generated Docs

- `docs/` — multi-audience wiki (markdown, Mermaid) built by `/repo-map-wiki`. Entry point: `docs/index.md`. Presentation layer only; this lode remains authoritative.

## Root Files

- [summary.md](summary.md) — One-paragraph project snapshot
- [terminology.md](terminology.md) — Domain vocabulary
- [practices.md](practices.md) — Coding practices and AI preferences
- [rob-pikes-rules.md](rob-pikes-rules.md) — Rob Pike's 5 Rules of Programming
- [dependency-policy.md](dependency-policy.md) — NuGet dependency selection criteria

## coding-standards/

- [solid.md](coding-standards/solid.md) — SOLID principles applied to C#
- [smalltalk-patterns.md](coding-standards/smalltalk-patterns.md) — Smalltalk Best Practice Patterns for C#

## dotnet/

- [project-structure.md](dotnet/project-structure.md) — .NET 10 solution format, CPM, build props/targets, global.json
- [testing.md](dotnet/testing.md) — MTP, xUnit v3, test configuration, categories, conventions
- [test-patterns.md](dotnet/test-patterns.md) — Test family patterns (A-D), fake conventions, naming rules
- [architecture-testing.md](dotnet/architecture-testing.md) — ArchUnitNET structural invariant tests (layers, naming, sealed)
- [analyzers.md](dotnet/analyzers.md) — Deny-by-default strategy, analyzer packages, suppression policy
- [release-tracking.md](dotnet/release-tracking.md) — AnalyzerReleases.Shipped/Unshipped.md format, RS2000-RS2008 rules, release workflow
- [analyzer-candidates.md](dotnet/analyzer-candidates.md) — Candidate analyzer ideas and investigation notes

### dotnet/analyzers/

- [new-analyzer-checklist.md](dotnet/analyzers/new-analyzer-checklist.md) — Pre-flight checks, netstandard2.0 pitfalls, nullable Roslyn API surprises
- [code-fix-patterns.md](dotnet/analyzers/code-fix-patterns.md) — Code fix implementation patterns, BatchFixer vs SequentialRename, AddUsing blank line behavior

## dotnet-reference/

- [dep-map.md](dotnet-reference/dep-map.md) — NuGet deps, container runtime images, SDK pins + project structure for all 4 production projects

## infrastructure/

- [claude-code-maintenance.md](infrastructure/claude-code-maintenance.md) — Claude Code harness maintenance notes
- [claude-code-upstream.md](infrastructure/claude-code-upstream.md) — Claude Code upstream reference: versions, agent/skill frontmatter fields
- [code-generation-quality.md](infrastructure/code-generation-quality.md) — Mechanisms that drive better AI code generation, ranked by impact
- [claude-revision-log.md](infrastructure/claude-revision-log.md) — Revision log: dated entries from `/claude-revision` runs
- [podman.md](infrastructure/podman.md) — Podman build commands, Dockerfile structure, smoke test patterns
- [nuget-trusted-publishing.md](infrastructure/nuget-trusted-publishing.md) — OIDC publishing to nuget.org from GitHub Actions, Roslyn analyzer packaging
- [agent-patterns.md](infrastructure/agent-patterns.md) — Shared agent patterns: plan convention, budget exhaustion, reflection loop
- [scaffolding-heuristics.md](infrastructure/scaffolding-heuristics.md) — Simplification-agent heuristic definitions (H1-H6)
- [scoring-rubric.md](infrastructure/scoring-rubric.md) — Shared four-dimension scoring rubric for strategic analysis agents
