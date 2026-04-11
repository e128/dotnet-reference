# Plan: leverage-missed-session-stats
*Created: 2026-04-11T14:33:35Z*

## Analysis Axis
Omission

## Score Breakdown

| Dimension    | Score | Rationale                                                                   |
| ------------ | ----- | --------------------------------------------------------------------------- |
| Novelty      | 2/3   | session-health.sh exists; the missing subcommand is a contract violation     |
| Compound     | 3/3   | weekly-learner, token-optimizer, and tdd-loop-optimizer all call this        |
| User Impact  | 3/3   | Silent failure: agents produce no stats output, mislead session analysis     |
| Automation   | 3/3   | Fixing this unblocks automated session-quality pipelines in 3+ agents        |
| **Total**    | **11/12** |                                                                         |

## Finding

`scripts/session-health.sh` is documented and in `help.sh`. Multiple agents call it with a subcommand and flags that do not exist in the implementation:

```
scripts/session-health.sh stats --days 30 --json     # weekly-learner
scripts/session-health.sh stats --days N --json       # token-optimizer
scripts/session-health.sh --sessions 1               # (expected by token-optimizer)
```

Reading `scripts/session-health.sh`, the script only supports:
- `--baseline` — save current build/format error counts
- `--json` — emit current vs baseline comparison
- No `stats` subcommand, no `--days` flag, no `--sessions` flag

When `weekly-learner` or `token-optimizer` call `session-health.sh stats --days 30 --json`, the script hits the `*` wildcard arm and calls `err "Unknown flag: stats"` then exits non-zero. The calling agents silently continue (or fail in a way that's hard to trace), producing a degraded analysis with no session data.

## Why Highest-Value Omission

This is a contract violation: the documentation and agent code assume a capability that does not exist. Any time `weekly-learner` or `token-optimizer` runs a session analysis, the session statistics phase produces nothing. The agents' highest-value outputs (pattern detection, efficiency recommendations) degrade silently. Score 11/12 because it compounds across three high-effort agents.

## Success Criteria

- `session-health.sh stats --days N --json` returns structured session data (tool call counts, error rates, topic clusters, or a minimal stub that agents can branch on)
- `session-health.sh --sessions N` is accepted without error
- All three agents (weekly-learner, token-optimizer, tdd-loop-optimizer) produce non-empty stats output when invoked
- `bash -n` and `shellcheck` pass on updated script

## Phased Implementation

### Baseline
- Read `scripts/session-health.sh` in full — confirm the missing subcommands
- Read `weekly-learner.md`, `token-optimizer.md` in full — extract all `session-health.sh` call sites and expected JSON schema
- Document what schema the agents actually consume (to know minimum viable implementation)

### Implement
Option A (preferred): implement `stats` subcommand reading Claude Code JSONL logs from `~/.claude/projects/*/` and emitting tool-count, error-count, and topic stats as JSON.
Option B (fallback): implement `stats` as a documented stub that returns `{"status":"unavailable","reason":"no session data source configured"}` — at minimum preventing the hard exit and allowing agents to degrade gracefully.

Minimum changes:
1. Add `stats` subcommand dispatch
2. Add `--days N` and `--sessions N` flag parsing (store values, pass to stats handler)
3. Emit valid JSON matching the schema weekly-learner consumes

### Verify
- `scripts/session-health.sh stats --days 7 --json | jq .`
- `scripts/session-health.sh stats --sessions 1 --json | jq .`
- Re-run weekly-learner on a short window and confirm it no longer exits on stats phase
- `scripts/check.sh --no-format` if any C# touched (N/A for bash-only change)
