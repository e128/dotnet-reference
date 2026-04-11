# Context: leverage-missed-session-stats
*Created: 2026-04-11T14:33:35Z*

## Axis: Omission

## Evidence

### Broken call sites

| Agent             | Call                                                    | Result                      |
| ----------------- | ------------------------------------------------------- | --------------------------- |
| weekly-learner    | `scripts/session-health.sh stats --days 30 --json`      | `err "Unknown flag: stats"` |
| token-optimizer   | `scripts/session-health.sh stats --days N --json`       | `err "Unknown flag: stats"` |
| token-optimizer   | `scripts/session-health.sh --sessions 1`                | `err "Unknown flag: --sessions"` |
| tdd-loop-optimizer| `scripts/session-health.sh stats --days 30 --json`      | `err "Unknown flag: stats"` |

### Script reality (from reading `scripts/session-health.sh`)

Flags actually supported:
- `--baseline` — save error counts
- `--json` — emit current vs baseline

No `stats` subcommand. No `--days` flag. No `--sessions` flag. The script's `while` loop hits the `*` arm and calls `err "Unknown flag: $1"; exit 1` for any of these.

### Impact

Three high-effort agents (weekly-learner maxTurns=25, token-optimizer maxTurns=30, tdd-loop-optimizer maxTurns=35) silently degrade their session analysis output whenever they invoke this subcommand. Users running these agents for session retrospectives receive incomplete analysis without any clear error message at the agent level (agents may catch the exit code and skip the stats phase, or fail in hard-to-trace ways).

## Why This Beats the Runner-Up

Runner-up was `assert.sh --plan-complete` stub (score 7). That stub returns "skip" status — it's non-failing (just skipped), so the damage is smaller. The `session-health.sh stats` gap is a hard exit 1 on three agents used in high-effort analytical workflows. Score 11 vs 7.

## Runners-Up Table

| Candidate                        | Novelty | Compound | Impact | Automation | Total | Reason runner-up                             |
| -------------------------------- | ------- | -------- | ------ | ---------- | ----- | -------------------------------------------- |
| session-health.sh stats gap      | 2       | 3        | 3      | 3          | 11    | WINNER                                       |
| assert.sh --plan-complete stub   | 1       | 2        | 2      | 2          | 7     | Non-failing stub; skip ≠ error               |
| coverage-areas.sh bc silent zero | 1       | 1        | 1      | 1          | 4     | Cosmetic issue only; below threshold         |
| lode size gate not enforced      | 1       | 1        | 1      | 1          | 4     | Human discipline issue, not a code contract  |
| Docker test missing assertion    | 1       | 1        | 1      | 1          | 4     | Isolated to docker.sh workflow               |
