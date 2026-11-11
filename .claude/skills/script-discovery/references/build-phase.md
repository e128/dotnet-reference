# Build Phase (Phase 5)

*Part of [script-discovery](../SKILL.md). Extracted to reduce SKILL.md size.*

For each of the top N candidates:

## 5a. Create the script

- File: `scripts/{name}.sh` (or `scripts/internal/{name}.sh` if invoked only by skills/agents)
- Must `source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"` and use its helpers (`ok`, `err`, `dim`, etc.)
- Must accept `--json` flag for structured output in agent contexts
- Must accept `--help` flag
- Follow existing `scripts/*.sh` conventions
- Target bash 4.0 or later. `scripts/lib.sh` re-execs under Homebrew Bash when the interpreter is older
- Prefer wrapping existing scripts over reimplementing logic
- See the `bash-patterns` skill for style and structure

## 5b. Wire up keyword shortcut

Add an entry to `.claude/rules/keyword-shortcuts.md` in the appropriate row.
Update the timestamp via `scripts/ts.sh`.

## 5c. Register the hook (only when the script enforces a guardrail)

1. Write the hook under `.claude/hooks/`. It reads the tool payload on stdin,
   exits 2 with a reason on stderr to block, and exits 0 to allow.
2. Register it in `.claude/settings.json` under `hooks`. Editing that file is
   ask-tier, so get approval first. See `.claude/rules/auto-approvals.md`.
3. Add the matching entry to `.opencode/plugins/e128-guardrails/index.ts`.
   See `lode/infrastructure/opencode-guardrails.md`. The CI job named `lint`
   fails when the plugin names a hook script that does not exist.

A script needs no hook when no tool call should be blocked or rewritten.

## 5d. Register in the routing table

If the script is a top-level (non-internal) tool, add a row to the table in
`.claude/rules/deterministic-scripts.md`. Keep `scripts/README.md` in step,
then confirm with `scripts/help.sh` and `scripts/readme-table-diff.sh`.

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
scripts/lint-shell.sh scripts/{name}.sh
scripts/help.sh | rg {name}        # verify it appears in the catalog
```

Smoke-test each script with `--help` and with live data.
