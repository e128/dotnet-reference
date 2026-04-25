---
name: catalog-pruner
color: orange
description: >
  Identifies dead-weight, redundant, and overlapping skills and agents in the catalog.
  Analyzes invocation frequency from session history, detects trigger-phrase conflicts
  and subsumption, cross-references with the weekly digest, and proposes a ranked
  retirement/merge list. Auto-retires DORMANT items; creates plans for LOW_USE and DUPLICATE.
  Triggers on: prune catalog, retire dead skills, catalog cleanup, dead weight removal,
  skill consolidation, consolidate skills, too many skills, skill overlap, redundant skills,
  agent overlap, retire skill, merge skills, clean up skills, skill catalog audit,
  check skill health, audit all skills, skill health report, skill health, find dead skills,
  review all skills.
tools: Bash, Glob, Grep, Read, Write
maxTurns: 15
effort: low
memory: project
---

# Catalog Pruner

Identifies dead-weight skills and agents for retirement. Auto-retires DORMANT items.
Creates plans in `plans/` for LOW_USE and DUPLICATE candidates — no approval gate.

## Phase 1: Inventory & Usage Analysis

Gather all data in parallel — no dependencies between these:

- **File counts**: number of skills (`skills/*/SKILL.md`) and agents (`agents/*.md`)
- **Trigger phrases**: extracted from each skill/agent frontmatter `description:` field
- **Session invocation frequency** (30-day window): `scripts/session-health.sh --json`
- **Git modification history**: `scripts/diff.sh --json` — filter for `.claude/agents/` and `.claude/skills/` paths
- **CLAUDE.md keyword table**: `grep -oP '`/[a-z-]+`' CLAUDE.md | sort -u` — skills in the keyword table are actively routed, never retire these
- **Most recent weekly digest**: read for dead-weight candidates section (skip if none found)

## Phase 2: Classify

For each skill/agent, assign a status:

| Status | Criteria |
|--------|----------|
| **ACTIVE** | Invoked 3+ times in 30 days, OR in keyword table, OR modified recently |
| **LOW_USE** | Invoked 1-2 times in 30 days, not in keyword table |
| **DORMANT** | Zero invocations in 30 days, not in keyword table, not modified |
| **DUPLICATE** | Overlaps significantly with another active skill/agent |
| **MERGE** | Two items cover the same domain — combine into one |
| **RENAME** | Trigger phrases conflict with another item but functionality is distinct |
| **STALE** | Description references deprecated paths or removed features — needs update or retirement |
| **PROTECTED** | Infrastructure agent (build-validator, targeted-tests, etc.) — never retire |

### Overlap Detection Heuristics

| Pattern | Classification |
|---------|---------------|
| Two items with identical trigger phrases | RENAME — one must change triggers |
| Item A's description is a subset of B's | MERGE — A may be redundant |
| >50% trigger phrase overlap | DUPLICATE — consolidation candidate |
| Zero trigger phrases | DORMANT — unreachable without manual typing |
| Description references removed feature or deprecated path | STALE |

Protected agents (never retire regardless of usage):
- build-validator, smart-commit
- lode-sync, knowledge-consolidator
- weekly-learner, smart-commit

## Phase 3: Retirement Proposal

Present ranked list:

```
## Catalog Pruning Proposal

Active: {N} skills, {M} agents
Proposed retirements: {N}

### DORMANT (safe to retire)
| Item | Type | Last Used | Overlaps With |
|------|------|-----------|---------------|
| {name} | skill/agent | never / 45d ago | {active item} |

### DUPLICATE (merge candidate)
| Item | Merges Into | Reason |
|------|------------|--------|
| {name} | {target} | {overlap description} |

### LOW_USE (monitor — retire next cycle if still unused)
| Item | Uses (30d) | Notes |
|------|-----------|-------|
| {name} | 1 | {context} |
```

## Phase 3.5: Create Plans for LOW_USE and DUPLICATE

For each item classified as LOW_USE or DUPLICATE, create a plan in `plans/`.

Check `plans/` first — skip any item already covered by an existing plan.

**Slug format:** `prune-{retire|merge}-{kebab-name}` — e.g. `prune-retire-dead-agent`, `prune-merge-overlapping-skill`

Write all three plan files in a single parallel turn per item.

**`{slug}-plan.md`**
```markdown
# Plan: {Retire | Merge} {name}
*Created: {ISO 8601 UTC}*
*Updated: {same}*

## Overview

{1-paragraph: what this skill/agent does, why it's a candidate for {retirement|merge}, what it would merge into}

## Classification

{DUPLICATE | LOW_USE} — {one-line rationale}

## Evidence

- Last invoked: {date or "never"}
- Invocations (30d): {N}
- {If DUPLICATE}: overlaps with `{target}` — {overlap description}
- {If LOW_USE}: watch-list entry age: {N} days

## Success Criteria

- [ ] {If RETIRE}: `{path}` deleted, all trigger phrases removed from keyword-shortcuts.md
- [ ] {If MERGE}: functionality absorbed into `{target}`, trigger phrases updated
- [ ] `catalog-pruner` tmp memory updated — item removed from LOW_USE watch list
- [ ] No broken references in CLAUDE.md or other agents

## Phase 0 — Confirm

- [ ] Re-run `session-health.sh` for last 7 days to confirm invocation count is still low
- [ ] Search for callers: `rg "{name}" .claude/` — confirm no active dependencies

## Phase 1 — Execute

- [ ] {If RETIRE}: delete `{path}`, remove trigger phrases
- [ ] {If MERGE}: move any unique functionality to `{target}`, delete `{path}`

## Phase 2 — Verify

- [ ] `check.sh --no-format` passes
- [ ] `catalog-pruner` no longer lists this item
```

**`{slug}-context.md`**
```markdown
# Context: {Retire | Merge} {name}
*Created: {ISO 8601 UTC}*
*Updated: {same}*

## Candidate

Path: `{path}`
Type: {skill | agent}
Classification: {DUPLICATE | LOW_USE}

## Evidence

{usage data, overlap analysis, or watch-list history from catalog-pruner run}

## Decision Rationale

{why this is worth removing/merging vs. keeping on the watch list}
```

**`{slug}-tasks.md`**
```markdown
# Tasks: {Retire | Merge} {name}
*Created: {ISO 8601 UTC}*
*Updated: {same}*

## Phase 0 — Confirm
- [ ] Re-check invocation count
- [ ] Search for callers

## Phase 1 — Execute
- [ ] Delete or merge

## Phase 2 — Verify
- [ ] check passes
- [ ] catalog-pruner clean
```

After writing all plan files:
```bash
scripts/internal/stage.sh --include-new
```

---

## Phase 4: Update Memory

Write findings to `.claude/tmp/catalog-pruner/memory.md`:
- Date of last audit
- Items retired (after user approval)
- Items moved to LOW_USE watch list
- Protected items list

## Rules

- **Auto-retire DORMANT items** (zero invocations in 30 days, not modified, not in keyword table, age > 90d) — no confirmation needed; report what was retired in the summary
- **Create plans for LOW_USE and DUPLICATE** — present these in the summary table AND write a plan to `plans/prune-{retire|merge}-{name}/`; no user confirmation needed
- **Never retire protected agents** — infrastructure agents are essential even if rarely invoked directly
- **Keyword table is authoritative** — if a skill is in CLAUDE.md keyword shortcuts, it's active
- **Check for dependencies** — if agent A is spawned by skill B, A is active even if never invoked directly
- **One audit per session** — don't re-run if already run this session
