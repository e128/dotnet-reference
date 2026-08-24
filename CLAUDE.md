# Project Instructions for Claude

*Last updated: 2026-08-24*

@AGENTS.md

Everything above is the portable core. This file adds only Claude Code
mechanics. The domain rules in `.claude/rules/*.md` are shared: Claude Code
loads the directory automatically, and opencode loads the same files through
`instructions` in `opencode.json`.

## Claude Code-Only Rules

- When you explain something to the user, use the Visualize skill.
- Use `AskUserQuestion` for a structured question. Never ask inline.
- Put config in `.claude/` at project level, not `~/.claude/` at global level.
- Guardrail hooks live in `.claude/hooks/`. opencode has no hook system, so
  its enforcement lives in `opencode.json` permissions instead.

## Configuration Tiers

Three loaded layers plus a wrapper:

1. `AGENTS.md`, the portable core. Both harnesses load it.
2. `.claude/rules/*.md`, shared domain rules. Both harnesses load them.
3. This file plus `lode/`. Claude-only overlay, and knowledge.
4. `prompts/SystemPrompt.txt` wraps all three in lode-launcher sessions.

**Which-home test.** Ask: would this rule hold for any coding agent, or only
for Claude Code mechanics? Portable answer → `AGENTS.md` or `.claude/rules/`.
Claude-mechanics answer → this file.

Full AI assistant preferences: [lode/practices.md](lode/practices.md).
Capability map (what each harness loads):
[lode/infrastructure/claude-code-maintenance.md](lode/infrastructure/claude-code-maintenance.md).
