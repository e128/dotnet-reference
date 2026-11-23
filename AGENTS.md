# AGENTS.md

E128.Reference ships Roslyn analyzers and a .NET 11 reference solution.
This file owns portable session policy. `.claude/rules/` owns core contracts.
Task-specific reference lives in `lode/`. Claude-only mechanics live in `CLAUDE.md`.

## Session Start

1. Run `scripts/context.sh` for status, changes, and active work.
2. Read `lode/lode-map.md`, `lode/terminology.md`, and `lode/summary.md`.
3. If your harness does not load `.claude/rules/*.md`, read those core rule files.
4. If your launcher does not inject `prompts/SystemPrompt.txt`, read it directly.
5. Read the task references below before editing their subject.

Claude Code and opencode load the core rule directory. Neither loader automatically loads the task references below.
Skills in `.claude/skills/` work on both harnesses. Agent definitions live in `.claude/agents/`.
The generated `.opencode/agents/` and `.opencode/agent-assets/` trees serve opencode. Never edit those generated trees.
After an agent definition changes, run `scripts/internal/opencode-agents.sh sync`. `/yeet` also runs that sync.

## Communication

- Write terse chat prose as a senior engineer speaking to a peer. Use the Chat Style section of `lode/practices.md`.
- Explain with a Mermaid diagram when prose is harder to follow.
- Never give a time estimate.
- Ask only when a decision is materially ambiguous, risky, or requires approval. Otherwise use the sensible default.
- Write timestamps in ISO 8601 UTC.
- Follow `.claude/rules/writing-style.md` for written artifacts.

## Workflow

- Route repeated operations through `.claude/rules/deterministic-scripts.md`. Use `scripts/help.sh` to discover scripts.
- Write tests before implementation. Finish implementation before running tests. A failing RED run is not required.
- Write no `//` or `/* */` comment in `src/`. Record a non-obvious constraint in `lode/decisions/` instead. `scripts/comment-scan.sh` blocks the rest.
- Follow `.claude/rules/quality-gates.md` for verification after the final edit.
- Test observable behavior. Validate user-facing changes in the real interface when applicable.
- Keep changes focused. Fix the root cause when a friction point repeats.
- Search for existing code before writing a helper. Share internal code instead of copying it or widening a public API.
- Use five subagents when five independent workstreams justify them. Use fewer when work overlaps or coordination costs outweigh the gain.
- Keep subagent edits disjoint and synthesize their results. Never create work to meet an agent count.
- Update matching Lode files when code, configuration, scripts, or structure changes. Remove content when its subject disappears.
- Follow `prompts/SystemPrompt.txt` for the Lode method. `lode/` is the authoritative project memory.

## .NET Constraints

- Target .NET 11 and later. Analyzer assemblies also support `netstandard2.0`. The base analyzer build loads in a .NET 10.0 host, and the union build needs the .NET 11.0 host.
- Do not add .NET Framework targets, APIs, references, conditional branches, compatibility tests, or polyfills.
- Seal every non-abstract class. E128005 and ArchUnitNET enforce this constraint.
- Prefer immutability, then memory, then CPU, then parallelism.
- Follow `.claude/rules/security.md` for configurable security values and pooled buffers.
- Follow `.claude/rules/quality-gates.md` for suppressions, nullable diagnostics, and format requirements.
- Keep ReferenceTrimmer installed and enabled, with experimental symbol analysis. Preserve XML documentation settings. With documentation generation off, symbol mode misses references used only in `cref` attributes. Inspect runtime, reflection, plugin, build, and test use before removing a reported reference. Remove only high-confidence project-local unconditional items. Ask before shared, imported, conditional, central-pin, or published package-contract changes. Verify affected builds, tests, and documented runtime checks, and restore exact removals that break them.
- For a proven runtime dependency, you can add a narrow item-level ReferenceTrimmer exception: package `TreatAsUsed="true"`, project `NoWarn="RT0002"`, or assembly `NoWarn="RT0001"`. Preserve existing metadata, record evidence and authorization, and ask about uncertain cases. Add no blanket suppression. Preserve test source and expectations during reference cleanup.
- Keep SDK analyzer policy and every Meziantou `Usage`, `Performance`, and `Security` rule enabled as errors, including disabled-by-default rules. ConfigureAwait rules (CA2007, MA0004, xUnit1030, RCS1090, S3216, VSTHRD111, and equivalents) are the exception: they stay at `none` in `.globalconfig`. Never enable one, and never change source to satisfy one. After an authorized Meziantou version change, refresh the version-matched rule inventory and verify effective per-ID settings. Preserve other analyzer policies and ask before changing explicit overrides or adding suppressions outside the preauthorized ReferenceTrimmer runtime exceptions.
- Fix production analyzer findings first with behavior-preserving corrections. Exclude tests and generated code from bulk fixes. Leave existing tests unchanged unless a compile or analyzer error requires a minimal correction. Record why. Preserve assertions, expected values, cases, fixtures, and coverage. Ask before changing public contracts or observable behavior. Verify the build and actual test execution.

## Git and Scope

- Use a `feature/`, `fix/`, or `refactor/` branch.
- Commit or ship only when requested. Use the `smart-commit` agent for commits and `/yeet` for shipping.
- On a harness without agents or skills, use the scripts named in the routing table with the same approval policy.
- Write concise imperative commit summaries.
- Do not name Claude Code or Anthropic in a commit message or PR body. Add no attribution trailer. `scripts/internal/commit.sh` rejects the phrase.
- Squash only unpushed commits. Preserve pushed history and ship one clean commit per PR.
- Include these results under Tests and Tooling in each PR description. Use `scripts/test.sh` and report the revision and test scope.
  - **Analyzers:** For analyzer, configuration, reference cleanup, or analyzer-fix changes, report the relevant package versions and policies. Include reference dispositions, runtime evidence, conflicts, authorized exceptions, fixes, necessary test edits, and build and test results.
  - **Architecture tests:** Report passed and total counts. Describe changes to production projects, `ProjectReference` edges, baselines, rules, and exclusions, with the reason for each. Compare the evaluated graph with the captured baseline when project or reference files change, because frozen tests alone do not discover every graph change.
  - **Pipeline images:** Identify each pipeline's validation result, changed hosted-image labels, required compatibility fixes, and pre-build cleanup paths. Prefer compatible `ubuntu-26.04`, fall back to `ubuntu-24.04`, and do not retain `ubuntu-22.04`. Separate local validation from hosted run results and unresolved template dependencies.
  - These requirements add no hosted gate and do not authorize pull request creation or baseline replacement.
  - When a check did not run, write "not run" and the reason. Never present stale evidence as current.
- Run `scripts/status.sh` before staging. Treat concurrent staged, added, or removed files as existing work.
- Preserve unrelated work. Take no destructive, production, or external action beyond the authorization given.
- Follow `.claude/rules/auto-approvals.md` for action approval and `.claude/rules/security.md` for privacy.
- Ground research in authoritative sources and link important evidence.

## Task References

Read a reference when its condition applies. These files sit outside the always-loaded rule directory.

- Design or architecture review: `lode/practices.md` (Design Principles).
- C# edits or reviews: `lode/dotnet/csharp-conventions.md`.
- Test authoring or investigation: `lode/dotnet/testing.md`.
- TDD regression tracking or failure attribution: `lode/infrastructure/tdd-regression-tracking.md`.
- Analyzer, code fix, or analyzer test work: `lode/dotnet/analyzers/authoring.md`.
- Candidate scoring, discovery, or a rule idea: `lode/dotnet/analyzer-candidates/index.md` (open) and `catalog.md` (settled). Every candidate is recorded there, whichever repository noticed it. A consumer repository keeps no candidate register of its own.
- Analyzer architecture: `lode/dotnet/analyzers/summary.md`.
- Shell edits or tool prerequisites: `lode/infrastructure/shell-authoring.md`.
- Shipping-script changes: `lode/infrastructure/shipping.md`.
- Instruction or harness changes: `lode/infrastructure/claude-code-maintenance.md`.

## Design Thinking

Apply these questions before settling a design and during review:

- Reduce the problem to its irreducible constraints and rebuild it from zero.
- What would a principal architect rewrite before starting?
- Which test breaks the design on day one?
- Which higher-level abstraction removes the problem?

Write no implicit state machine. Flag an existing implicit state machine for refactoring.
Module depth, ownership, and interface rules live in `lode/practices.md` (Design Principles).

## Instruction Maintenance

Change instructions from verified evidence. Give each requirement one owner and link to it elsewhere.
Record the failure behind a changed instruction in its owning file. Keep project knowledge in `lode/`.
The maintenance method lives in `lode/infrastructure/instruction-file-maintenance.md`.
