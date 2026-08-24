---
mode: subagent
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
permission:
  glob: allow
  bash: allow
  write: allow
  grep: allow
  read: allow
---

# Catalog Pruner

Identifies dead-weight skills and agents for retirement. Auto-retires DORMANT items.
Creates plans in `plans/` for LOW_USE and DUPLICATE candidates — no approval gate.

## Phase 1: Inventory & Usage Analysis

Gather all data in parallel — no dependencies between these:

- **Catalog inventory**: `scripts/catalog-stats.sh --json` — returns agents, skills, frontmatter fields, description lengths, keyword-table membership, and line counts in one call
- **Session invocation frequency** (30-day window): `scripts/session-health.sh --json`
- **Git modification history**: `scripts/diff.sh --json` — filter for `.claude/agents/` and `.claude/skills/` paths
- **Trigger-phrase overlap**: `scripts/internal/overlap-detect.sh --json` — returns pairs with shared triggers and overlap percentage
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

### Overlap Detection

Use the output from `scripts/internal/overlap-detect.sh --json` to classify overlaps:

| Overlap percentage | Classification                                      |
| ------------------ | --------------------------------------------------- |
| 100% (identical)   | RENAME — one must change triggers                   |
| ≥50%               | DUPLICATE — consolidation candidate                 |
| 25–49%             | Review — may be acceptable domain adjacency         |

Also check: if Item A's description is a subset of B's → MERGE candidate.
Items with zero trigger phrases → DORMANT (unreachable without manual typing).

Never retire infrastructure agents (build-validator, smart-commit, lode-sync,
knowledge-consolidator, weekly-learner) regardless of usage.

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

For each LOW_USE or DUPLICATE item (skip if plan already exists in `plans/`),
create a plan per the 3-file convention (see `lode/infrastructure/agent-patterns.md`).
Slug: `prune-{retire|merge}-{kebab-name}`.

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
