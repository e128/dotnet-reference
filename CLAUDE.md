# Project Instructions for Claude

*Last updated: 2026-08-27T14:50:41Z*

@AGENTS.md

This file adds only Claude Code mechanics. Portable content lives in
`AGENTS.md` and `.claude/rules/*.md`. Tier structure and the which-home test:
[lode/infrastructure/claude-code-maintenance.md](lode/infrastructure/claude-code-maintenance.md).

## Claude Code-Only Rules

- Use the Visualize skill for explanations.
- Use `AskUserQuestion` for structured questions, never inline.
- Put config in `.claude/` at project level, not `~/.claude/` at global level.
- Guardrail hooks live in `.claude/hooks/`. opencode enforcement lives in
  `opencode.json` permissions.