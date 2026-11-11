# Instruction File Maintenance
*Updated: 2026-11-11T14:38:11Z*

`AGENTS.md` and `CLAUDE.md` are a memory of failures. A line earns a place only
when a model failed once and the line stops the repeat. This file owns the
maintenance method. The tier structure and the portable-versus-Claude-only map
live in [claude-code-maintenance.md](claude-code-maintenance.md).

## Rules

- One line, one failure. State what broke and what to do instead.
- Put the evidence in brackets at the end of the line.
- Give no background and no philosophy.
- Edit the file from evidence, never from memory. When a model fails, fix the
  file before you fix the code.
- Read the file at the start of every session. Keep it as short as the rules
  allow.

```text
[ran tests from the wrong dir, PR #212]
```

## Passes

1. **Collect.** Read logs, PR comments, and chat history. List every moment a
   model went wrong. Write one line per failure with the evidence path. Write
   no file yet.
2. **Distill.** Turn each failure into one instruction a model can follow
   without context. Merge duplicates. Drop what happened once and cannot
   happen again.
3. **Structure.** Order lines by how early a session needs them: run commands,
   tests, lint, untouched areas, style, project traps. Keep a three-line
   header: what the repo is, how to verify a change, where to look first.
4. **Prune.** Read the file as a model would at the start of a session. Cut
   every line that does not change a decision. Cut the oldest failures first,
   not the newest.
5. **Test.** Open a fresh session with the file and nothing else. Run the
   task. Strengthen a line behind every repeated failure. Mark a never-needed
   line as a deletion candidate.
6. **Handoff.** Commit the file with a message that lists the failures it
   covers. Remove a line after 30 days without use.

## Stop Condition

Stop when a fresh session completes the task with zero repeated failures.

## Related

- [claude-code-maintenance.md](claude-code-maintenance.md): tier structure,
  rule ownership, and the which-home test.
