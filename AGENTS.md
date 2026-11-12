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
- Follow `.claude/rules/quality-gates.md` for verification after the final edit.
- Test observable behavior. Validate user-facing changes in the real interface when applicable.
- Keep changes focused. Fix the root cause when a friction point repeats.
- Search for existing code before writing a helper. Share internal code instead of copying it or widening a public API.
- Use five subagents when five independent workstreams justify them. Use fewer when work overlaps or coordination costs outweigh the gain.
- Keep subagent edits disjoint and synthesize their results. Never create work to meet an agent count.
- Update matching Lode files when code, configuration, scripts, or structure changes. Remove content when its subject disappears.
- Follow `prompts/SystemPrompt.txt` for the Lode method. `lode/` is the authoritative project memory.

## .NET Constraints

- Target .NET 11 and later. Analyzer assemblies also support `netstandard2.0`. The analyzer support window is `net11.0`.
- Do not add .NET Framework targets, APIs, references, conditional branches, compatibility tests, or polyfills.
- Seal every non-abstract class. E128005 and ArchUnitNET enforce this constraint.
- Prefer immutability, then memory, then CPU, then parallelism.
- Follow `.claude/rules/security.md` for configurable security values and pooled buffers.
- Follow `.claude/rules/quality-gates.md` for suppressions, nullable diagnostics, and format requirements.

## Git and Scope

- Use a `feature/`, `fix/`, or `refactor/` branch.
- Commit or ship only when requested. Use the `smart-commit` agent for commits and `/yeet` for shipping.
- On a harness without agents or skills, use the scripts named in the routing table with the same approval policy.
- Write concise imperative commit summaries.
- Never add a `Co-Authored-By` trailer yourself. `scripts/internal/commit.sh` appends the name-only trailer.
- Squash only unpushed commits. Preserve pushed history and ship one clean commit per PR.
- Run `scripts/status.sh` before staging. Treat concurrent staged, added, or removed files as existing work.
- Preserve unrelated work. Take no destructive, production, or external action beyond the authorization given.
- Follow `.claude/rules/auto-approvals.md` for action approval and `.claude/rules/security.md` for privacy.
- Ground research in authoritative sources and link important evidence.

## Task References

Read a reference when its condition applies. These files sit outside the always-loaded rule directory.

- Design or architecture review: `lode/practices.md` (Design Principles).
- C# edits or reviews: `lode/dotnet/csharp-conventions.md`.
- Test authoring or investigation: `lode/dotnet/testing.md`.
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
