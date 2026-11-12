# Harness Maintenance
*Updated: 2026-11-12T00:54:46Z*

## Instruction Loads

`AGENTS.md` owns portable session policy and task-reference pointers.
`CLAUDE.md` imports it with `@AGENTS.md` and adds only Claude Code mechanics.
Claude Code loads the unscoped markdown files under `.claude/rules/` at startup.
opencode loads that directory through the `instructions` glob in `opencode.json`.

The core rule directory contains policy needed before an action: routing, approval, verification, privacy, and writing constraints.
Task-specific reference lives outside that directory. Read it when the condition in `AGENTS.md` applies.
Both harnesses follow the same pointers. Adding an import or splitting an unscoped file does not reduce loaded text.

Claude Code supports `paths` frontmatter for path-scoped rules.
Do not assume opencode applies that frontmatter when loading its instruction glob.
This repository uses explicit reference pointers instead of harness-specific scoping.

Claude Code can read `AGENTS.md` directly when no applicable Claude instruction file exists.
The explicit import remains the entry point because `CLAUDE.md` also carries Claude-only mechanics.
See [Claude Code Memory](https://code.claude.com/docs/en/memory) for import and loading behavior.

## Rule Ownership

Each requirement has one owner. Entry points link to the owner instead of copying its detailed contract.

- `AGENTS.md`: session startup, task pointers, test execution order, supported targets, design questions, and git workflow.
- `CLAUDE.md`: `AskUserQuestion`, project configuration placement, and Claude hook locations.
- `deterministic-scripts.md`: command-to-script routing and flags.
- `keyword-shortcuts.md`: phrase triggers.
- `auto-approvals.md`: action approval and harness permission precedence.
- `quality-gates.md`: format, build, tests, shell checks, suppression approval, and nullable diagnostics.
- `read-before-edit.md`: file-read requirements and invalidation triggers.
- `token-efficiency.md`: result reuse and context economy.
- `writing-style.md`: artifact prose and self-lint.
- `security.md`: configuration boundaries, pooled-buffer handling, and repository privacy.
- `common-gotchas.md`: file tools, path casing, Bash selection, local data, and scratch files.
- `agent-vs-skill-routing.md`: invocation, agent definitions, and generated mirrors.

`prompts/SystemPrompt.txt` owns the shared Lode method and its bootstrap conventions.
Its Lode-writing subset deliberately repeats the prose and privacy floor needed before repository instructions exist.
Keep that subset consistent with the core rules. Do not remove it merely as duplication.

## Task Reference Ownership

The `AGENTS.md` task pointers select these references:

- `lode/practices.md`: design principles, module ownership, interface depth, and chat style.
- `lode/dotnet/csharp-conventions.md`: C# idioms, time and HTTP contracts, async rules, and reuse searches.
- `lode/dotnet/testing.md`: test authoring, runner configuration, SDK checks, and test investigations.
- `lode/dotnet/analyzers/authoring.md`: registration, analysis APIs, generated code, code fixes, and test snippets.
- `lode/infrastructure/shell-authoring.md`: tool prerequisites and shell-authoring verification.
- `lode/infrastructure/shipping.md`: shipping-script branch guards, verification order, and unpushed-commit handling.

The setup guidance has no separate file or import.
Its design questions and implicit-state-machine rule live in `AGENTS.md`.
Its module rules live in `lode/practices.md`. The Dangerous Operations section in setup guidance is excluded.

## Harness-Specific Assets

Portable assets are `AGENTS.md`, core rules, scripts, Lode references, and the shared prompt.
These assets use ordinary markdown or Bash contracts.

Claude Code assets are `CLAUDE.md`, `.claude/hooks/`, and `.claude/settings.json`.
opencode assets are `opencode.json`, `.opencode/plugins/`, and the generated agent trees.
Do not move a portable rule into a Claude-only asset.

Claude Code reads project skills from `.claude/skills/`.
opencode reads that compatibility path as well as its native skill paths.
opencode does not use `.claude/agents/` as its agent discovery path.

## Agent Mirrors

`.claude/agents/` is the source of truth.
`scripts/internal/opencode-agents.sh sync` generates `.opencode/agents/` and `.opencode/agent-assets/`.
`/yeet` runs the sync when agent sources change.
Never hand-edit the generated trees.

opencode discovers its agent directory recursively. Keep non-agent assets in the generated asset tree, not inside `agents/`.
Its agent frontmatter uses an ordered `permissions` array. The mirror translates the source permissions into that format.
Definitions inherit the session model rather than pinning a model or version.

## Enforcement

Instruction text supplies context. Hooks and script guards enforce operations.
`.claude/hooks/block-raw-git` and `block-raw-commands` enforce the routing contract.
The opencode guardrail plugin invokes the same hook scripts.
A wrapper path does not authorize a push, history rewrite, or protected deletion.

Claude Code evaluates deny, ask, then allow.
opencode evaluates permissions in order and uses the last match. Its policies can tighten that result.
The approval owner is `.claude/rules/auto-approvals.md`.
See [opencode Guardrails](opencode-guardrails.md) for tool interception and permission mapping.

## Shared Prompt and Launchers

`prompts/SystemPrompt.txt` carries the Lode method into launcher sessions.
opencode also lists it in `instructions`.
A harness that receives neither must read the file as directed by `AGENTS.md`.

The `dotnet-reference.sh` and `dotnet-reference-go.sh` launchers use the Claude CLI with their configured endpoints.
`dotnet-reference-oc.sh` uses the opencode CLI.
The launcher contracts live in the script catalog, not in session policy.

## Maintenance Checks

1. Assign each changed requirement to its owner.
2. Replace references to retired files throughout skills, scripts, and Lode documents.
3. Update `lode-map.md` when adding, moving, or removing a Lode reference.
4. Run prose and size checks on changed Lode files.
5. Inspect `/memory` and `/context all` in a fresh Claude Code session when validating actual loads.
6. Inspect opencode loads before claiming identical runtime behavior.
7. Verify script behavior with isolated fixtures before running a real external action.

Use [Instruction File Maintenance](instruction-file-maintenance.md) for evidence collection and rule retirement.

## Related

- [Shell Authoring](shell-authoring.md): prerequisites, shell lint, and script discovery.
- [Script Performance](script-performance.md): Bash contracts and verification techniques.
- [Shipping](shipping.md): branch and history safety in the shipping script.
- [Roslyn Navigator MCP](roslyn-navigator-mcp.md): repository-local tool configuration.
- [Podman](podman.md): container runners and smoke checks.
- [Project Structure](../dotnet/project-structure.md): SDK, package, and build configuration.
