# Skill and Agent Autonomy

## Prompt Suppression

When running inside a skill or agent, NEVER prompt the user to approve: Read/Glob/Grep tool calls, writes to `.claude/tmp/`, or shell commands that only read state (grep, find, cat, head, ls, wc, git diff, git log, scripts/*.sh). These are always safe.

## Re-read After Format (Mandatory in Agent Context)

The re-read triggers are defined in [read-before-edit.md](read-before-edit.md). In agent context this is non-negotiable: after any `format.sh --changed` invocation, agents MUST re-read every file they intend to edit before editing it.

**Correct pattern:**
```
Read(file.cs)
Bash(scripts/format.sh --changed)
Read(file.cs)        # re-read — mandatory after format
Edit(file.cs, ...)   # succeeds
```
