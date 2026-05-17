---
name: script-discovery
description: >
  Find repeated behaviors in Claude Code sessions that should be deterministic
  bash scripts. A "deterministic script" replaces an ad-hoc multi-turn Claude
  behavior with a repeatable, token-free automation (bash script + optional
  hook + optional keyword shortcut). Scores candidates on token-savings potential
  and builds the top 3 if approved.
  Triggers on: script discovery, find script candidates, what should be a script,
  repeated patterns, deterministic scripts, automate patterns, session patterns,
  discover scripts, what keeps repeating, find automations, script opportunities.
  Not for: token optimization plans (use token-optimizer), weekly session analysis
  (use weekly-learner), or single-session review (use token-optimizer --current).
argument-hint: "[--days N]"
---

# Script Discovery

Find ad-hoc multi-turn Claude behaviors that should be deterministic bash scripts.
Score each candidate on token-savings potential and build the top 3 if approved.

## Phase 1: Gather Data (7-day window)

Default window is 7 days. Override with `--days N` from `$ARGUMENTS`.

Run all of these in parallel — they are independent:

```bash
scripts/session-mine.sh all --days 7 --json
scripts/session-health.sh tool-counts --days 7 --json
scripts/session-health.sh bash-commands --days 7 --json
scripts/session-health.sh bash-commands --days 7 --category --json
scripts/session-health.sh stats --days 7 --json
scripts/session-health.sh topics --days 7 --json
scripts/session-health.sh errors --days 7 --json
scripts/help.sh
rg "pattern|workflow|manual|TODO" lode/ -l 2>/dev/null || true
```

`session-mine.sh all` provides tool frequencies, repeated commands, most-read files, and agent spawns — use it instead of ad-hoc `jq` pipelines over JSONL transcripts.

### Error patterns (proxy for hook blocks)

From the `errors` output, look for: recurring error categories that indicate
a missing script (e.g., repeated `write-before-read` → missing pre-read script,
repeated `bash-failure` → fragile manual commands needing a robust script).

### Lode cross-reference

From the `rg` output, look for: documented patterns describing multi-step
processes without a corresponding script in `scripts/`.

---

## Phase 2: Score Candidates

Each candidate scores 0-3 on four dimensions (max 12). Primary signal is
estimated token reduction.

| Dimension        | 0           | 1                | 2                  | 3                    |
| ---------------- | ----------- | ---------------- | ------------------ | -------------------- |
| Frequency        | <1x/week    | 1-2x/week        | 3-5x/week          | Daily or more        |
| Token cost/occur | <500 tokens | 500-2k tokens    | 2k-5k tokens       | >5k tokens           |
| Compound value   | Standalone  | Saves 1 re-read  | Eliminates a class  | Prevents cascading   |
|                  |             |                  | of errors           | waste patterns       |
| Automation depth | Script only | Script + hook    | Script + hook +    | Full pipeline:       |
|                  |             |                  | keyword shortcut   | replaces an agent    |

**Threshold**: candidates scoring <5 are excluded from the report.

---

## Phase 3: Report (mandatory, always runs)

Produce a markdown table of ALL candidates scoring >= 5, sorted by total score
descending:

| Candidate | Pattern observed | Freq | Token | Compound | Auto | Total | Est. weekly token savings |
| --------- | ---------------- | ---- | ----- | -------- | ---- | ----- | ------------------------ |

Below the table, describe the top 3 candidates in detail:
- What the current ad-hoc behavior looks like (with transcript evidence)
- What the script would do
- What hooks/keywords it would wire up
- Estimated token savings per week

Write the report to `.claude/tmp/script-discovery/report.md`.
Output findings to conversation after writing.

---

## Phase 4: Approval gate

Ask via `AskUserQuestion`: "Build these 3? (y/n)" — single gate, not per-candidate.

---

## Phase 5: Build (only if approved)

For each of the top 3:

1. **Create the bash script** in `scripts/{name}.sh`
   - Must follow existing `scripts/*.sh` conventions (see `scripts/lib.sh`)
   - Must accept `--json` flag for structured output
   - Must accept `--help` flag
   - Must `source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"`
   - Must pass `bash -n` and `shellcheck` validation

2. **Add keyword shortcut** to `.claude/rules/keyword-shortcuts.md`

3. **Update token-efficiency rule** in `.claude/rules/token-efficiency.md`
   with the new script's usage line

4. **Verify** the script appears in `scripts/help.sh` output

5. **Validate** syntax:
   ```bash
   bash -n scripts/{name}.sh
   shellcheck scripts/{name}.sh 2>/dev/null || true
   ```

No plan needed — these are small, self-contained scripts, not features.

---

## Constraints

- Scripts must be bash (`.sh`), following existing `scripts/*.sh` conventions
- Scripts must `source lib.sh` and use its helpers (`ok`, `err`, `dim`, etc.)
- Never duplicate functionality that an existing script already covers
- Check `scripts/help.sh` before creating anything
- Evidence-based only — every candidate must cite specific frequency data
- No speculative scripts — only create if the pattern was actually observed
- Read every file before editing
- Use `scripts/ts.sh` for all timestamps
