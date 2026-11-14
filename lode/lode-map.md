# Lode Map
*Updated: 2026-11-13T22:43:20Z*

## Generated Docs

- `docs/` — multi-audience wiki (markdown, Mermaid) built by `/repo-map-wiki`. Entry point: `docs/index.md`. Presentation layer only; this lode remains authoritative.

## Root Files

- [summary.md](summary.md): One-paragraph project snapshot
- [terminology.md](terminology.md): Domain vocabulary
- [practices.md](practices.md): Coding practices and AI preferences
- [rob-pikes-rules.md](rob-pikes-rules.md): Rob Pike's 5 Rules of Programming
- [dependency-policy.md](dependency-policy.md): NuGet dependency selection criteria

## coding-standards/

- [solid.md](coding-standards/solid.md): SOLID principles applied to C#
- [smalltalk-patterns.md](coding-standards/smalltalk-patterns.md): Smalltalk Best Practice Patterns for C#

## dotnet/

- [build-performance.md](dotnet/build-performance.md): Build timing, analyzer cost, and the --fast path
- [project-structure.md](dotnet/project-structure.md): .NET 11 solution format, CPM, build props/targets, global.json
- [net11-upgrade.md](dotnet/net11-upgrade.md): .NET 11 rules for two SDK bands, conditional TFM list, analyzer reach, unions, closed hierarchies, runtime async
- [testing.md](dotnet/testing.md): MTP, xUnit v3, test authoring, and test-script contracts
- [csharp-conventions.md](dotnet/csharp-conventions.md): C# idioms, async contracts, and reuse searches
- [code-coverage.md](dotnet/code-coverage.md): In-process MTP coverage, Cobertura reports, area mapping, and the CI floors
- [test-patterns.md](dotnet/test-patterns.md): Test family patterns (A-D), fake conventions, naming rules
- [architecture-testing.md](dotnet/architecture-testing.md): ArchUnitNET structural invariant tests (layers, naming, sealed)
- [analyzers.md](dotnet/analyzers.md): Deny-by-default strategy, analyzer packages, suppression policy
- [release-tracking.md](dotnet/release-tracking.md): AnalyzerReleases.Shipped/Unshipped.md format, RS2000-RS2008 rules, release workflow
- [performance.md](dotnet/performance.md): Runtime knobs, profiling ladder, safe C# hot-path tips
- [aspnet-core-performance.md](dotnet/aspnet-core-performance.md): Web request path, Zstandard compression decision, request logging
- [benchmarks.md](dotnet/benchmarks.md): BenchmarkDotNet starter project: classes, invariants, the sealing-gate gap, and the deferred comparison
- [unsafe-code.md](dotnet/unsafe-code.md): No-unsafe-code policy, E128 rule coverage map, known gaps
- [implicit-state-machine-rule.md](dotnet/implicit-state-machine-rule.md): E128218 contract, detector families, scope, and noise budget
- [implicit-state-machine-rule-design.md](dotnet/implicit-state-machine-rule-design.md): Field rules, evidence, registration sketch, and test coverage
- [implicit-state-machine-rule-examples.md](dotnet/implicit-state-machine-rule-examples.md): Positive and negative snippets with the expected report

### dotnet/analyzer-candidates/

- [index.md](dotnet/analyzer-candidates/index.md): Open register for every analyzer candidate, with schema, scores, and the capture ledger
- [catalog.md](dotnet/analyzer-candidates/catalog.md): Settled rules, rejected ideas, guidance-document rule maps, and the ID map
- [skipped-decisions.md](dotnet/analyzer-candidates/skipped-decisions.md): Every declined shape with its reason and site count
- [net11-conversion-sweep.md](dotnet/analyzer-candidates/net11-conversion-sweep.md): Open .NET 11 conversion shapes with call-site evidence
- [external-sweep.md](dotnet/analyzer-candidates/external-sweep.md): Closed 2026-11-05 external rule-set sweep, which leaves no candidate open
- [performance-sweep.md](dotnet/analyzer-candidates/performance-sweep.md): Closed twenty-practice performance sweep that shipped E128203 to E128217, with probe evidence
- [async-sweep.md](dotnet/analyzer-candidates/async-sweep.md): Closed async fire-and-forget sweep, every shape already owned

### dotnet/analyzers/

- [summary.md](dotnet/analyzers/summary.md): Domain snapshot, reading order, shared invariants
- [shared-helpers.md](dotnet/analyzers/shared-helpers.md): Shared analyzer and code fix helper inventory and contracts
- [new-analyzer-checklist.md](dotnet/analyzers/new-analyzer-checklist.md): Pre-flight checks, netstandard2.0 pitfalls, nullable Roslyn API surprises
- [authoring.md](dotnet/analyzers/authoring.md): Analyzer registration, analysis APIs, code fixes, and test snippets
- [configuration.md](dotnet/analyzers/configuration.md): Option surfaces, key naming, severity keys, MSBuild properties, additional files
- [symbol-lookups.md](dotnet/analyzers/symbol-lookups.md): Documentation comment IDs, accessibility, interface implementation, data and control flow, scoped registration
- [testing.md](dotnet/analyzers/testing.md): Markup forms, code fix assertions, option injection, Roslyn version alignment
- [redundant-rules.md](dotnet/analyzers/redundant-rules.md): E128 rules that overlap a third-party analyzer
- [code-fix-patterns.md](dotnet/analyzers/code-fix-patterns.md): Code fix structure: decision list, code-aware library, AddUsing, span widening, tables, registration
- [code-fix-output.md](dotnet/analyzers/code-fix-output.md): Text the fix writes and keeps: literals, formatter, inserted members, trivia, rewrite shape guards
- [code-fix-techniques.md](dotnet/analyzers/code-fix-techniques.md): Fix-all providers, DocumentEditor, speculative bind, error-count guard, generator, attribute update
- [performance.md](dotnet/analyzers/performance.md): Analyzer hot-path rules: bind late, resolve types once, scan each scope once
- [package-layout.md](dotnet/analyzers/package-layout.md): two assemblies, NuGet analyzer layout, per-Roslyn packaging, build metadata
- [pooled-buffer-rules.md](dotnet/analyzers/pooled-buffer-rules.md): E128109 stackalloc limits, E128110 pooled buffer zeroing, E128111 use after Return
- [fips-rules.md](dotnet/analyzers/fips-rules.md): FIPS rule map, policy values, shared symbol and constant helpers
- [rule-details.md](dotnet/analyzers/rule-details.md): Per-rule detection shape, exclusions, and code-fix behavior for the E128 rules
- [rule-details-performance.md](dotnet/analyzers/rule-details-performance.md): Detection shape for performance rules E128203 to E128210
- [rule-details-performance-tier2.md](dotnet/analyzers/rule-details-performance-tier2.md): Detection shape for performance rules E128211 to E128217
- [rule-details-reliability.md](dotnet/analyzers/rule-details-reliability.md): Detection shape for the reliability rules
- [rule-details-reliability-concurrency.md](dotnet/analyzers/rule-details-reliability-concurrency.md): Detection shape for the async and concurrent reliability rules
- [rule-details-reliability-resources.md](dotnet/analyzers/rule-details-reliability-resources.md): Detection shape for the resource lifetime and shared state rules
- [rule-details-security.md](dotnet/analyzers/rule-details-security.md): Detection shape for the security rules
- [rule-details-union.md](dotnet/analyzers/rule-details-union.md): Detection shape for the union rule family E128219 to E128221
- [rule-details-conversion.md](dotnet/analyzers/rule-details-conversion.md): Detection shape for the .NET 11 conversion rules E128223 to E128226

## dotnet-reference/

- [dep-map.md](dotnet-reference/dep-map.md): NuGet deps, container runtime images, SDK pins + project structure for the production projects and the benchmark project

## infrastructure/

- [claude-code-maintenance.md](infrastructure/claude-code-maintenance.md): Harness maintenance: Claude Code and opencode v2 capability map, discovery rules, rule ownership
- [instruction-file-maintenance.md](infrastructure/instruction-file-maintenance.md): AGENTS.md and CLAUDE.md as a memory of failures: the six maintenance passes and the stop condition
- [opencode-guardrails.md](infrastructure/opencode-guardrails.md): opencode v2 plugin hook mapping for Claude Code guardrails, permission and shell hook contracts
- [roslyn-navigator-mcp.md](infrastructure/roslyn-navigator-mcp.md): Local Roslyn Navigator MCP server: tool manifest, harness config, verification
- [claude-code-upstream.md](infrastructure/claude-code-upstream.md): Claude Code upstream reference: versions, agent/skill frontmatter fields
- [code-generation-quality.md](infrastructure/code-generation-quality.md): Mechanisms that drive better AI code generation, ranked by impact, plus the review-to-rule loop
- [podman.md](infrastructure/podman.md): Podman build commands, Dockerfile structure, smoke test patterns
- [nuget-trusted-publishing.md](infrastructure/nuget-trusted-publishing.md): OIDC publishing to nuget.org from GitHub Actions, Roslyn analyzer packaging
- [agent-patterns.md](infrastructure/agent-patterns.md): Shared agent patterns: plan convention, budget exhaustion, reflection loop
- [agent-worktree-builds.md](infrastructure/agent-worktree-builds.md): The nested-worktree .globalconfig collision and how a delegated build avoids it
- [scaffolding-heuristics.md](infrastructure/scaffolding-heuristics.md): Simplification-agent heuristic definitions (H1-H6)
- [scoring-rubric.md](infrastructure/scoring-rubric.md): Shared four-dimension scoring rubric for strategic analysis agents
- [secret-scanning.md](infrastructure/secret-scanning.md): gitleaks scan in precommit.sh and the CI lint job
- [script-performance.md](infrastructure/script-performance.md): Bash performance rules: fork avoidance, the subshell trap, the version floor, byte-diff and shellcheck verification
- [shell-authoring.md](infrastructure/shell-authoring.md): Tool prerequisites and shell-authoring checks
- [shipping.md](infrastructure/shipping.md): Shipping-script branch guards, verification order, and unpushed commits
