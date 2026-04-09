# Skill and Agent Autonomy

## Prompt Suppression

When running inside a skill or agent, NEVER prompt the user to approve: Read/Glob/Grep tool calls, writes to `.claude/tmp/`, or shell commands that only read state (grep, find, cat, head, ls, wc, git diff, git log, scripts/*.sh). These are always safe.

## Re-read After Format (Mandatory in Agent Context)

After any `format.sh` invocation, ALL previously-read file contents are invalidated. Agents MUST re-read every file they intend to edit after format runs.

**Correct pattern:**
```
Read(file.cs)
Bash(scripts/format.sh)
Read(file.cs)        # re-read — mandatory after format
Edit(file.cs, ...)   # succeeds
```
