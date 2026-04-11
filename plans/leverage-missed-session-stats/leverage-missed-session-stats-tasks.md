# Tasks: leverage-missed-session-stats
*Created: 2026-04-11T14:33:35Z*

## Phase 1: Baseline

- [ ] Read `scripts/session-health.sh` in full — document every supported flag
- [ ] Read `weekly-learner.md` — extract all `session-health.sh` call signatures and expected JSON schema
- [ ] Read `token-optimizer.md` — extract all `session-health.sh` call signatures
- [ ] Read `tdd-loop-optimizer.md` — extract all `session-health.sh` call signatures
- [ ] Compile unified list of needed subcommands: `stats`, flags `--days N`, `--sessions N`
- [ ] Determine minimum viable JSON schema agents consume (e.g., `tool_counts`, `error_rate`, `topics`)

## Phase 2: Implement

- [ ] Add `stats` subcommand dispatch to `scripts/session-health.sh`
- [ ] Add `--days N` flag parsing (integer, store for use by stats handler)
- [ ] Add `--sessions N` flag parsing (integer, store for use by stats handler)
- [ ] Implement `stats` handler — choose approach:
  - **Option A**: Parse Claude Code JSONL logs from `~/.claude/projects/*/` filtered by age (`--days`) or count (`--sessions`)
  - **Option B**: Graceful stub returning `{"status":"unavailable","reason":"no session data source configured"}` — prevents hard exit, allows agents to degrade cleanly
- [ ] Ensure `--json` flag works in combination with `stats` subcommand
- [ ] Emit JSON matching the schema agents consume (or document delta if stub approach)

## Phase 3: Verify

- [ ] `bash -n scripts/session-health.sh` — syntax check passes
- [ ] `shellcheck scripts/session-health.sh` — no warnings
- [ ] `scripts/session-health.sh stats --days 7 --json | jq .` — valid JSON, exit 0
- [ ] `scripts/session-health.sh stats --sessions 1 --json | jq .` — valid JSON, exit 0
- [ ] `scripts/session-health.sh stats --days 30 --json | jq .` — matches schema weekly-learner expects
- [ ] Manually trace weekly-learner stats phase — confirm non-empty output
- [ ] `scripts/session-health.sh --baseline` — original behavior unchanged
- [ ] `scripts/session-health.sh` (no flags) — no regression
