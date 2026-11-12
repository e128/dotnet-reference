# Claude Code Instructions

@AGENTS.md

## Claude Code Mechanics

- Use `AskUserQuestion` for structured questions, never inline.
- Put project configuration in `.claude/`, not user-level configuration.
- Guardrail hooks live in `.claude/hooks/`. opencode uses `.opencode/plugins/` and `opencode.json` permissions.

Portable policy lives in `AGENTS.md` and `.claude/rules/`.
Task-specific reference pointers live in `AGENTS.md`. Use them before editing their subject.
The harness layout lives in [Harness Maintenance](lode/infrastructure/claude-code-maintenance.md).
