---
name: claude-revision
description: >
  Periodic health check for the entire Claude Code configuration — agents, skills, CLAUDE.md, and lode
  memory. Optionally fetches latest Anthropic guidance (web research is skippable with --no-web),
  reviews all agents for model/tool/memory optimization, reviews all skills for token efficiency,
  audits CLAUDE.md files, checks agent memory health and git tracking, and writes a dated revision log
  to lode/ for cross-session continuity. Run monthly or after bulk agent/skill changes.
  Triggers on: claude revision, periodic audit, agent health check, skill optimization, claude config
  review, monthly claude audit, revision audit, config health, claude maintenance, agents audit,
  skills audit, claude audit.
argument-hint: "[--no-web] [--agents-only] [--skills-only] [--self] [--archive]"
---

# Claude Revision

Periodic health check for the Claude Code configuration. Reviews agents, skills, CLAUDE.md, and lode
memory against the latest official guidance. Writes findings to the revision log for cross-session
continuity — the log at `lode/infrastructure/claude-revision-log.md` is this skill's persistent memory
(skills have no `memory:` field; lode is the only cross-session store).

## Scope Flags

- `--no-web` — skip Phase 1 (web research); fast local run
- `--agents-only` — run Phases 0, 2, 5–6 only
- `--skills-only` — run Phases 0, 3, 4, 5–6 only
- `--self` — include `claude-revision` itself in Phase 3 skill review (normally skipped)
- `--archive` — move older entries in the revision log to an archive file before running

## Phase 0: Load Last Run Context & Doctor Check

Read `lode/infrastructure/claude-revision-log.md` if it exists. Surface:
- Date of last run and deferred items
- Agent/skill counts (to detect additions or removals since last run)

If the file doesn't exist, this is the first run — proceed without prior context.

**Archive check:** Count entries in the `## Runs` section. If the log exceeds 200 lines, warn the user
and suggest running with `--archive` to move older entries to `claude-revision-log-archive.md`. If
`--archive` flag is present, do the archive before proceeding.

### 0b. Doctor Check

Run `claude doctor` (60s timeout). Parse output: errors → HIGH, warnings → MEDIUM, clean → note and continue. If errors found, present immediately before Phase 1.

### 0c. Skill/Agent Description Budget Audit

Claude Code allocates ~1% of context window (fallback 8,000 chars) for the combined skill+agent listing
at startup. Each entry's `description` + `when_to_use` is capped at 1,536 characters — anything beyond
is silently truncated, degrading the model's ability to route to the right skill/agent.

```bash
scripts/catalog-stats.sh --json
```

Parse `desc_chars` per entry from the `catalog` array. Flag entries:
- **Over 1,536 chars** → HIGH: silently truncated at startup
- **Over 1,000 chars** → MEDIUM: at risk if context budget shrinks or more skills are added
- **Under 50 chars** → LOW: may be too terse for accurate routing

Sum all `desc_chars` values. If the total exceeds 8,000 chars, flag HIGH —
low-priority entries are being dropped from startup context entirely.

## Phase 1: Web Research _(skip with `--no-web`)_

Spawn `sme-researcher` agent with these targets:
- `https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md` — **primary source**: version history with all new features, breaking changes, and deprecations. Filter entries since the last revision log date.
- `https://code.claude.com/docs/en/sub-agents` — agent config (model, tools, memory, maxTurns)
- `https://code.claude.com/docs/en/skills` — skill architecture, trigger patterns
- `https://platform.claude.com/docs/en/agent-sdk/overview` — SDK patterns
- `https://github.com/anthropics/skills` — official skill examples: frontmatter conventions, trigger phrasing, file structure, and reference implementations
- `https://github.com/anthropics/skills/commits/main/` — recent commits: detect new skills, structural changes, or pattern shifts since the last run date

Compare against the last revision log entry date (from Phase 0). Report new or changed guidance, or
"No new guidance since YYYY-MM-DD" if unchanged.

## Phases 2–4: Run Directly (No Sub-Agents)

### Phase 2: Agent Review

Run the catalog inventory (already fetched in Phase 0c — reuse the output):

```bash
scripts/catalog-stats.sh --json
```

Filter the `catalog` array to entries where `type == "agent"`. The JSON includes `tools`, `maxTurns`, `memory`, `model`, `isolation`, `total_lines`, `desc_chars`, and `in_keywords` per entry.

Evaluate against these criteria:

| Check | Criteria |
|-------|----------|
| Model | Inherit from parent context (omit `model` parameter). Reserve explicit model routing for rare cases only. |
| Tools | Allowlist present? Unnecessary tools increase attack surface and context load |
| maxTurns | Iterating agents (build→fix→test loops) need an explicit limit to prevent runaway |
| memory | Agents that learn codebase-specific patterns across sessions should have `memory: project` |
| Overlap | Does it duplicate another agent? Should it be merged or differentiated? |
| Description | Clear trigger keywords? Unambiguous when to use vs. similar agents? |
| Desc length | Over 1,536 chars → HIGH (truncated at startup). Cross-ref Phase 0c findings. |

Do not read individual agent files unless a specific field is missing from the grep output.

Return: table of `(agent, lines, model, memory, issues)`. Flag issues HIGH/MEDIUM/LOW.

### Phase 3: Skill Review

Reuse the `scripts/catalog-stats.sh --json` output from Phase 0c. Filter the `catalog` array to
entries where `type == "skill"`. The JSON includes `total_lines`, `meaningful_lines`, `desc_chars`,
`tools`, `maxTurns`, and `in_keywords` per entry.

Flag skills over 250 `total_lines` for review — read only those files. Check for stale agent
references in skills by grepping for known-removed agents from the Phase 0 log context:
`rg "<removed-agent-1>|<removed-agent-2>" .claude/skills/ --with-filename`

Read individual skill files only for skills flagged as over-size or containing stale references.

Evaluate flagged skills against these criteria:

| Check | Criteria |
|-------|----------|
| Size | Over 200 lines? Identify content suitable for lode/ or a referenced sub-file |
| Triggers | Too broad (always fires)? Too narrow (never fires)? Missing key phrases? |
| Overlap | Duplicates content in another skill or CLAUDE.md? |
| Staleness | References removed files, old APIs, or outdated patterns? |
| Type | Operational (workflow steps) vs Reference (knowledge injection) -- both valid; never flag reference skills for lacking workflow steps |
| Desc length | Over 1,536 chars → HIGH (truncated at startup). Cross-ref Phase 0c findings. |

Return: table of `(skill, lines, type, issues)`. Flag issues HIGH/MEDIUM/LOW.
Skip `claude-revision` itself unless `--self` flag is provided or user explicitly requests it.

### Phase 4: CLAUDE.md Audit

Run these bash commands directly (no sub-agent):

1. Check keyword-shortcuts.md for stale agent references:
   `rg "\| [a-z-]+-agent" .claude/rules/keyword-shortcuts.md`
   Extract agent names from output → verify each exists in `.claude/agents/`

2. Check CLAUDE.md and rules for references to removed scripts/agents.
   Build the grep pattern dynamically from removed items listed in the Phase 0 log context:
   `rg "<removed-item-1>|<removed-item-2>" CLAUDE.md .claude/rules/`
   (Substitute actual removed names from the Phase 0 log each run)

3. Check for volatile counts in rules files:
   `rg "[0-9]+ errors? in [0-9]+ days" .claude/rules/`

Only read the full `CLAUDE.md` or `~/.claude/CLAUDE.md` if a specific issue is found by grep.

Evaluate findings against these criteria:

| Type | Flag When |
|------|-----------|
| Redundant | Duplicates a skill, lode entry, or the other CLAUDE.md |
| Verbose | Long explanation where a short rule works; unnecessary examples |
| Memory candidate | One-off learning that belongs in lode/ instead |
| Conflict | Contradicts a skill or project convention |

Return: table of `(file, line, type, finding, recommendation)`.

### Phase 5: Local Health Checks

Run directly (fast — no sub-agent). Two parts:

**5a. Agent Memory Health**

1. Glob `.claude/agent-memory/*/MEMORY.md`
2. For each file: check line count — flag any over 200 lines
3. Cross-check agent frontmatter: which agents declare `memory: project`? Do all have a MEMORY.md? Any with a MEMORY.md but no `memory:` flag?
4. **Git tracking**: run `git ls-files --others --exclude-standard .claude/agent-memory/` to find untracked MEMORY.md files. Untracked files exist only on this machine and are lost on clone or CI.

If untracked files found, report them and offer to `git add` them before moving on.

**5b. Scripts Relevance Check**

1. Get all script names from `scripts/help.sh` output.

2. Cross-reference the `catalog` JSON from Phase 0c — check each script name against all
   agent/skill descriptions and paths. Also check `CLAUDE.md` and `.claude/rules/`:
   `rg -l "script-a|script-b|script-c" .claude/rules/ CLAUDE.md`
   (Substitute the actual script names discovered in step 1)

3. For scripts with zero reference hits, read only their header (~25 lines) to understand purpose.

4. Evaluate against two criteria:
   - **Redundant**: does Claude Code now offer this natively (e.g., built-in git tools, IDE diagnostics, web fetch)? Flag HIGH.
   - **Orphaned**: does the script serve a real purpose but simply lacks a keyword shortcut or help entry? Flag MEDIUM.

5. Return: table of `(script, referenced, assessment, recommendation)`.

**5c. Lode Check**

Check revision-relevant lode files for staleness:

```bash
scripts/lode-ts.sh --stale --json
```

Filter the output to `lode/infrastructure/claude-revision-log.md` and `lode/infrastructure/claude-code-maintenance.md`. Flag entries where `days_ago > 30`, or where the file's content references agents/skills that no longer exist.

## Phase 6: Report & Log

See [references/report-template.md](references/report-template.md) for the report and log entry formats.

Present findings, ask which to address, then append a log entry to `lode/infrastructure/claude-revision-log.md`.

## Guidelines

- Phases 2–4: direct bash/grep, not sub-agents (context savings)
- Don't auto-fix — present findings, wait for user direction
- Phase 5a git check is mandatory — untracked MEMORY.md files vanish on clone
- Phase 5b: unreferenced + native-to-Claude = HIGH removal; unreferenced + useful = MEDIUM add shortcut
- Reference skills are complete as-is — never flag for missing workflow steps

## User Input

$ARGUMENTS

## Self-Improvement (Mandatory)

The revision log is this skill's persistent memory — always append to it at Phase 6.

1. **Always write the Phase 6 log entry** — even if no changes were made; the timestamp prevents drift.
2. **Record deferred items** — log declined findings under "Deferred" with severity so they resurface.
3. **Capture new check criteria** — add new patterns to the Phases 2–5 criteria tables in this workflow.
4. **Note guidance changes** — append new Anthropic best practices to the appropriate lode file.

## Troubleshooting

See [references/troubleshooting.md](references/troubleshooting.md).
