---
name: script-discovery
description: >
  Find repeated behaviors in Claude Code sessions that should be deterministic
  bash scripts. A "deterministic script" replaces an ad-hoc multi-turn Claude
  behavior with a repeatable, token-free automation (bash script + optional
  hook + optional keyword shortcut). Scores candidates on token-savings potential
  and builds the top 3 if approved. Supports --scan-skills mode to statically
  analyze agent/skill files for inline bash that should be extracted to scripts.
  Triggers on: script discovery, find script candidates, what should be a script,
  repeated patterns, deterministic scripts, automate patterns, session patterns,
  discover scripts, what keeps repeating, find automations, script opportunities,
  review agents for scripts, extract scripts from skills, deterministic portions.
  Not for: token optimization plans (use token-optimizer), weekly session analysis
  (use weekly-learner), or single-session review (use token-optimizer --current).
argument-hint: "[--days N] [--scan-skills]"
---

# Script Discovery

Find ad-hoc multi-turn Claude behaviors that should be deterministic bash scripts.
Score each candidate on token-savings potential and build the top 3 if approved.

## Mode Selection

Check `$ARGUMENTS` for mode:
- **`--scan-skills`** → Static analysis mode (Phase 1b below). Reads all agent/skill files for inline bash patterns that could be extracted into `scripts/*.sh`. Also updates affected agents/skills to reference the new scripts.
- **Default** → Session-mining mode (Phase 1a below). Mines transcript data for repeated commands.

## Phase 1a: Gather Session Data (default mode, 7-day window)

Default window is 7 days. Override with `--days N` from `$ARGUMENTS`.
Skip this phase if `--scan-skills` mode.

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

## Phase 1b: Static Skill/Agent Analysis (--scan-skills mode)

Skip Phase 1a entirely. Instead, statically analyze all agent and skill files for inline bash patterns that should be deterministic scripts.

### 1b.1 Inventory

```bash
scripts/catalog-stats.sh --json
```

### 1b.2 Read all agent and skill files

Read each file from the catalog. For each file, identify:

- **Inline bash blocks** — `\`\`\`bash` fenced blocks containing multi-command sequences
- **Ad-hoc command chains** — `rg ... | sort | uniq`, `fd ... | xargs wc -l`, `for f in ... done` loops
- **Repeated data gathering** — the same commands appearing in 2+ different agents/skills
- **Deterministic validation** — checks with no LLM judgment needed (e.g., comparing file lists, counting entries, parsing YAML fields)

### 1b.3 Cross-reference with existing scripts

For each inline pattern found, check `scripts/help.sh` output — skip if already covered.

### 1b.4 Score and report

Score using the same Phase 2 rubric below. For `--scan-skills` mode, the Frequency dimension measures how many agents/skills contain the pattern (1 file = 0, 2 files = 1, 3-4 files = 2, 5+ files = 3).

After approval in Phase 4, Phase 5 builds the scripts AND updates each affected agent/skill file to reference the new script instead of inline bash.

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

6. **Update affected agents/skills** (--scan-skills mode only):
   - Read each agent/skill that contained the inline pattern
   - Replace the inline bash with a reference to the new script
   - Verify the agent/skill still reads correctly after the edit

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
