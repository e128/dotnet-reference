# Skill and Agent Autonomy

## Prompt Suppression

Inside a skill or an agent, never prompt the user to approve any of these:

- Read, Glob, or Grep tool calls
- Writes to `.claude/tmp/`
- Shell commands that only read state: `rg`, `fd`, `cat`, `head`, `ls`, `wc`,
  `git diff`, `git log`, and any `scripts/*.sh` call

These calls are always safe.

## Re-read After Format

[read-before-edit.md](read-before-edit.md) defines the re-read triggers. In
agent context the triggers are non-negotiable. After any `format.sh --changed`
call, re-read every file you intend to edit, then edit it.
