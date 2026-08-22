# Project Instructions for Claude

*Last updated: 2026-08-22*

@AGENTS.md

Everything above is cross-harness and lives in `AGENTS.md`, the portable
source of truth for any coding agent. Everything below is Claude Code only.
Detail lives in `.claude/rules/`, `lode/`, and skills. Load detail on demand.

## Claude Code-Only Rules

- `.claude/rules/*.md` — always-loaded domain rules. See
  [deterministic-scripts.md](.claude/rules/deterministic-scripts.md) for
  script routing and
  [keyword-shortcuts.md](.claude/rules/keyword-shortcuts.md) for phrase
  triggers.
- When explaining something to the user, use the Visualize skill.
- Use `AskUserQuestion` for a structured question. Never ask inline.
- Use a relevant skill (`Skill` tool) or subagent (`Agent` tool). Spawn a
  subagent only for genuinely independent work, then synthesize the
  findings.
- Put config in `.claude/` (project level), not `~/.claude/` (global).
- Write working files to `.claude/tmp/`. **Never write to `/tmp`.** Lode
  scraps go in `lode/tmp/`.
- Never write an absolute user profile path. Use `~` or a repo-relative
  path.
- Never set `model:` in an agent or a skill. All inherit the session
  model. See
  [agent-vs-skill-routing.md](.claude/rules/agent-vs-skill-routing.md).

## Configuration Tiers

Three loaded tiers plus a wrapper: `AGENTS.md` (portable, harness-agnostic
core) → `CLAUDE.md` and `.claude/rules/*.md` (always loaded, Claude-Code-only
overlay) → `lode/` (knowledge, not instructions). `prompts/SystemPrompt.txt`
wraps all three. See
[claude-code-maintenance.md](lode/infrastructure/claude-code-maintenance.md)
§ Adding Rules and § Rule File Ownership for where a new instruction lands.

**Which-home test.** Ask: would this rule hold for any coding agent working
in this repo, or only for Claude Code's own tool, hook, skill, or subagent
mechanics? Portable answer → `AGENTS.md`. Claude-Code-mechanics answer →
`CLAUDE.md` or `.claude/rules/`.

Full AI assistant preferences: [lode/practices.md](lode/practices.md).
Capability map (what is portable versus Claude-only):
[lode/infrastructure/claude-code-maintenance.md](lode/infrastructure/claude-code-maintenance.md).
