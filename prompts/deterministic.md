# Deterministic Script Discovery

## Goal

Find repeated behaviors in my Claude Code sessions that should be deterministic
bash scripts. A "deterministic script" replaces an ad-hoc multi-turn Claude
behavior with a repeatable, token-free automation (bash script + optional
hook + optional keyword shortcut).

## Input: 3 data sources, 7-day window

1. **Session transcripts** — `scripts/session-health.sh --days 7 --json`.
   Look for:
    - Multi-tool sequences that repeat across sessions (3+ tool calls, same pattern)
    - Repeated Read patterns (same file read 3+ times per session)
    - Recurring agent spawns for simple tasks
    - Any bash command sequence run 2+ times identically

2. **Hook blocks & violations** — review recent session logs for hook failures.
   Look for: patterns where I repeatedly hit a hook block and work around it
   (indicates a missing script that would do it right the first time).

3. **Lode cross-reference** — `rg "pattern|workflow|manual|TODO" lode/ -l`.
   Look for: documented patterns that describe multi-step processes without
   a corresponding script in `scripts/`.

## Scoring: token-savings proxy (max 12)

Each candidate scores 0-3 on four dimensions. The primary signal is estimated
token reduction.

| Dimension        | 0           | 1                | 2                  | 3                    |
| ---------------- | ----------- | ---------------- | ------------------ | -------------------- |
| Frequency        | <1x/week    | 1-2x/week        | 3-5x/week          | Daily or more        |
| Token cost/occur | <500 tokens | 500-2k tokens    | 2k-5k tokens       | >5k tokens           |
| Compound value   | Standalone  | Saves 1 re-read  | Eliminates a class  | Prevents cascading   |
|                  |             |                  | of errors           | waste patterns       |
| Automation depth | Script only | Script + hook    | Script + hook +    | Full pipeline:       |
|                  |             |                  | keyword shortcut   | replaces an agent    |

**Threshold**: candidates scoring <5 are excluded from the report.

## Output

### Phase 1: Report (mandatory, always runs)

Produce a markdown table of ALL candidates scoring >= 5, sorted by total score
descending. Include:

| Candidate | Pattern observed | Freq | Token | Compound | Auto | Total | Est. weekly token savings |
| --------- | ---------------- | ---- | ----- | -------- | ---- | ----- | ------------------------ |

Below the table, describe the top 3 candidates in detail:
- What the current ad-hoc behavior looks like (with transcript evidence)
- What the script would do
- What hooks/keywords it would wire up
- Estimated token savings per week

### Phase 2: Approval gate

Ask: "Build these 3? (y/n)" — single gate, not per-candidate.

### Phase 3: Build (only if approved)

For each of the top 3:
1. Create the bash script in `scripts/{name}.sh`
2. Add hook entries to `.claude/settings.json` (via the settings gate workflow)
3. Add keyword shortcut to `.claude/rules/keyword-shortcuts.md`
4. Update `.claude/rules/token-efficiency.md` with the new script's usage line
5. Validate with `bash -n scripts/{name}.sh` and `shellcheck scripts/{name}.sh`

No plan needed — these are small, self-contained scripts, not features.

## Constraints

- Scripts must be bash 5+ (`.sh`), following existing `scripts/*.sh` conventions
- Scripts must accept `--json` flag for structured output
- Scripts must accept `--help` flag
- Never duplicate functionality that an existing script already covers
- Check `scripts/help.sh` before creating anything
