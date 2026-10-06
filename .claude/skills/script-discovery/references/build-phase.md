# Build Phase (Phase 5)

*Part of [script-discovery](../SKILL.md). Extracted to reduce SKILL.md size.*

For each of the top N candidates:

## 5a. Create the script

- File: `scripts/{name}.sh` (or `scripts/internal/{name}.sh` if invoked only by skills/agents)
- Must `source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"` and use its helpers (`ok`, `err`, `dim`, etc.)
- Must accept `--json` flag for structured output in agent contexts
- Must accept `--help` flag
- Follow existing `scripts/*.sh` conventions
- Prefer wrapping existing scripts over reimplementing logic
- See the `bash-patterns` skill for style and structure

## 5b. Wire up keyword shortcut

Add an entry to `.claude/rules/keyword-shortcuts.md` in the appropriate row.
Update the timestamp via `scripts/ts.sh`.

## 5c. Update token-efficiency rules

If the script replaces a routed raw command, add a row to
`.claude/rules/deterministic-scripts.md`.

## 5d. Register in the routing table

If the script is a top-level (non-internal) tool, add a row to the table in
`.claude/rules/deterministic-scripts.md`. Keep `scripts/README.md` in step.

## 5e. Update affected agents/skills (--scan-skills mode only)

Only run this step when `--scan-skills` was the active mode.

For each agent/skill file that contained the inline pattern identified in
Phase 1b.4:

1. Re-read the file
2. Replace the inline bash block or ad-hoc command chain with a reference
   to the new `scripts/{name}.sh` script
3. Verify the file reads correctly after the edit

## 5f. Final validation

```bash
bash -n scripts/{name}.sh
shellcheck scripts/{name}.sh 2>/dev/null || true
scripts/help.sh | rg {name}        # verify it appears in the catalog
```

Smoke-test each script with `--help` and with live data.
