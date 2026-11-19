# Claude Code Instructions

@AGENTS.md

## TDD Regression Tracking

- Tag new LLM-written tests before the Red run: `scripts/tdd-track.sh register --changed --origin llm-generated`.
- Run the attribution wrapper in Red/Green loops: `scripts/tdd-track.sh analyze -- <ClassName>`.
- Treat `unverified_red` as the normal Red phase. Treat `confirmed_regression` as a real break.
- Read `harness_or_env_error` as a test or environment fault, not a production regression.
- Inject history for touched files with `scripts/tdd-track.sh report --markdown`.
- Print a current-run summary with `scripts/tdd-track.sh analyze --markdown`.
- Analyze an existing run with `scripts/tdd-track.sh analyze --no-run --trx DIR`.
- Use `--dry-run` to classify without writing the registry or ledger.

The method, schema, and invariants live in
[TDD Regression Tracking](lode/infrastructure/tdd-regression-tracking.md).

## Claude Code Mechanics

- Use `AskUserQuestion` for structured questions, never inline.
- Put project configuration in `.claude/`, not user-level configuration.
- Guardrail hooks live in `.claude/hooks/`. opencode uses `.opencode/plugins/` and `opencode.json` permissions.

Portable policy lives in `AGENTS.md` and `.claude/rules/`.
Task-specific reference pointers live in `AGENTS.md`. Use them before editing their subject.
The harness layout lives in [Harness Maintenance](lode/infrastructure/claude-code-maintenance.md).
