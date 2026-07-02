---
name: script-discovery
description: >
  Find repeated behaviors in Claude Code sessions that should be deterministic
  bash scripts. A "deterministic script" replaces an ad-hoc multi-turn Claude
  behavior with a repeatable, token-free automation (bash script + optional
  hook + optional keyword shortcut). Scores candidates on token-savings potential
  and builds the top N if approved. Supports --scan-skills mode to statically
  analyze agent/skill files for inline bash that should be extracted to scripts.
  Triggers on: script discovery, find script candidates, what should be a script,
  repeated patterns, deterministic scripts, automate patterns, session patterns,
  discover scripts, what keeps repeating, find automations, script opportunities,
  review agents for scripts, extract scripts from skills, deterministic portions.
  Not for: token optimization plans (use token-optimizer), weekly session analysis
  (use weekly-learner), or single-session review (use token-optimizer --current).
argument-hint: "[--days N] [--top N] [--report-only] [--scan-skills]"
---

# Script Discovery

## Flags

Parse `$ARGUMENTS`, stripping each flag before processing remaining text:

- `--days N`: session history window (default 7)
- `--top N`: candidates to detail and optionally build (default 3)
- `--report-only`: produce report, skip approval gate and build (Phases 4–5)
- `--scan-skills`: static analysis mode → run Phase 1b instead of 1a (reads agent/skill files for inline bash to extract, then updates those files to reference the new scripts after building)

Default (no `--scan-skills`) is session-mining mode (Phase 1a).

---

## Phase 1a: Session Mining (default mode)

Skip if `--scan-skills`. Run these in parallel — they are independent:

```bash
scripts/session-mine.sh all --days 7 --json
scripts/session-health.sh tool-counts --days 7 --json
scripts/session-health.sh bash-commands --days 7 --category --json
scripts/session-health.sh stats --days 7 --json
scripts/session-health.sh topics --days 7 --json
scripts/session-health.sh errors --days 7 --json
scripts/help.sh
rg "pattern|workflow|manual|TODO" lode/ -l 2>/dev/null || true
```

`session-mine.sh all` provides tool frequencies, repeated commands, most-read files, and agent spawns.

**Data source failure handling.** If any source returns an error or `status: "skip"`, log it and continue — do not abort. Note it in the Phase 3 "Data gaps" section.

### Error patterns (proxy for hook blocks)

From the `errors` output, recurring categories that indicate a missing script:

| Error category    | What it suggests                                      |
| ----------------- | ----------------------------------------------------- |
| write-before-read | Missing pre-read / re-read tracking script            |
| path-not-found    | Needs a targeted-read or path-resolution script       |
| bash-failure      | Fragile manual command chaining needs a robust script |
| hook-denied       | Repeated blocks suggest a missing "right way" script  |

### Lode cross-reference

From the `rg` output: lode files describing multi-step processes with no corresponding `scripts/` script, or `TODO`/`manual:` annotations describing token-burning workflows.

---

## Phase 1b: Static Skill/Agent Analysis (--scan-skills mode)

Skip Phase 1a. Statically analyze all agent and skill files for inline bash patterns that should be deterministic scripts.

> **Gotcha:** the `block-raw-commands` PreToolUse hook denies any Bash command whose text contains a prohibited literal (e.g. `dotnet format`) — even inside an `echo`, comment, or `rg` pattern — so a scan that greps for routing drift can block itself. Probe via regex (`rg 'dotnet +format'`, not the bare literal) or use the Grep/Read tools instead of embedding the literal in Bash.

### 1b.1 Inventory

```bash
scripts/catalog-stats.sh --json
```

### 1b.2 Read each file, identify extractable patterns

For each file found, look for:

- **Inline bash blocks** — fenced ` ```bash ` blocks containing multi-command sequences that could be a single `scripts/*.sh` call
- **Ad-hoc command chains** — `rg ... | sort | uniq`, `fd ... | xargs wc -l`, `for f in ... done` loops, `2>&1` redirects, `| tail`/`| head` on scripts
- **Repeated data gathering** — the same commands appearing in 2+ different agents/skills (candidate for a shared script)
- **Deterministic validation** — checks with no LLM judgment needed (comparing file lists, counting entries, parsing YAML fields)

**Density is a coarse signal — judge by execution, not block count.** A high inline-bash-block count does not make a skill a good candidate. Explicitly discount or exclude *teaching/reference* skills whose fenced ` ```bash ` blocks are illustrative examples rather than commands the skill invokes (e.g. `bash-patterns`), and the `script-discovery` skill itself. Ask "is this logic the skill executes deterministically every run?" — not "how many bash blocks does it have?". The real candidate may rank low on raw density yet be the only genuinely-extractable deterministic logic.

### 1b.3 Cross-reference with existing scripts

Check each inline pattern against `scripts/help.sh` output — skip patterns already covered.

### 1b.4 Score and report

Use the Phase 2 scoring rubric. After approval, Phase 5 builds the scripts AND updates each affected agent/skill file to reference the new script instead of inline bash.

---

## Phase 2: Identify & Score Candidates

### Candidate identification patterns (session-mining mode)

1. **Repeated reads** — files read 3+ times/session across multiple sessions. Each full-file read that could be a targeted query is waste.
2. **Agent over-spawning** — agent types spawned 10+/week for tasks a script could handle.
3. **Error pattern clusters** — `write-before-read` / `path-not-found` spikes indicate a missing invalidation/pre-read script.
4. **Hook block workarounds** — repeated blocks suggest a missing "right way".
5. **Lode-documented manual workflows** — lode files describing multi-step processes with no corresponding script.

### Deduplication gate

Before scoring, check each candidate against existing scripts with `scripts/help.sh`.
If an existing script covers ≥80% of the candidate's functionality, exclude it.

### Scoring rubric

See [references/scoring-rubric.md](references/scoring-rubric.md) for the scoring table and threshold.

---

## Phase 3: Report (always runs)

Produce a markdown table of ALL candidates scoring >= 5, sorted by total descending:

```
| Candidate | Pattern observed | Freq | Token | Compound | Auto | Total | Est. weekly token savings |
```

Below the table, describe the top N candidates in detail:
- Current ad-hoc behavior (with transcript evidence — counts, file names, agent types)
- What the script would do (subcommands, flags)
- What hooks/keywords it would wire up
- Estimated token savings per week

If any source returned `status: "skip"` or errored, add a **Data gaps** section listing what was unavailable and a recommended fix.

Write the report to `.claude/tmp/script-discovery/report.md`, then output findings to conversation.

---

## Phase 4: Approval Gate

**Skip if `--report-only`.**

Use `AskUserQuestion`: "Build these N scripts? (name-x, name-y, name-z)" with
options "Yes, build all N" / "No, report only". Single gate, not per-candidate.

---

## Phase 5: Build (only if approved)

See [references/build-phase.md](references/build-phase.md). No plan needed — small self-contained scripts, not features.

---

## Constraints

- Scripts are bash (`.sh`), follow `scripts/*.sh` conventions, `source lib.sh` (use `ok`, `err`, `dim`, etc.)
- Accept `--json` and `--help`; pass `bash -n` and `shellcheck`
- Never duplicate an existing script's functionality — check `scripts/help.sh` first
- Evidence-based only — every candidate cites specific frequency data; never create a script for an unobserved pattern
- Read before editing; re-read after any `format.sh` run
- Use `scripts/ts.sh` for timestamps; run post-change verification on affected lode files

## Self-Improvement

At end of run, build this payload; spawn `skill-self-updater` only if ≥1 section is non-empty.

```markdown
## Self-Improvement Report: script-discovery
*Run: {one-line description of what was done}*

### Errors Encountered
- {error type}: {root cause} — {what triggered it}

### User Corrections / Redirects
- {what the user corrected} — {what assumption in SKILL.md was wrong}

### Undocumented Edge Cases
- {input/state not covered by SKILL.md} — {how it was handled} — {suggested addition}
```

Spawn passes this block as the prompt. Clean run (all sections empty) → do not spawn.
