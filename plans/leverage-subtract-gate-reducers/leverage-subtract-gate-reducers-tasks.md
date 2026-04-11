# Tasks: leverage-subtract-gate-reducers
*Created: 2026-04-11T14:33:35Z*

## Prerequisites

- [ ] `leverage-subtract-simplifier-merge` plan must be complete before starting Phase 2

## Phase 1: Baseline

- [ ] Read `confirmation-reducer.md` in full — extract Phase 1 (policy load) and Phase 2 (scan logic) workflow steps exactly
- [ ] Read merged `simplification-agent.md` — find the correct injection point for `--policy-audit` flag
- [ ] Confirm `confirmation-reducer` trigger phrases are not already in `simplification-agent`

## Phase 2: Implement

- [ ] Add `--policy-audit` flag to `simplification-agent.md`:
  - Phase: load CLAUDE.md auto-approve list (same as confirmation-reducer Phase 1)
  - Phase: scan all SKILL.md files for gates that block on auto-approved operations (same as confirmation-reducer Phase 2)
  - Output: same gate-removal candidate table format
- [ ] Absorb `confirmation-reducer` trigger phrases into `simplification-agent` description
- [ ] Update `confirmation-reducer.md`: change description to "Deprecated — use simplification-agent --policy-audit. Will be removed." and strip workflow content
- [ ] Search CLAUDE.md and keyword shortcuts for `confirmation-reducer` references — redirect

## Phase 3: Verify

- [ ] `simplification-agent --policy-audit` produces same gate-removal candidates as `confirmation-reducer` did on a known-state skill file
- [ ] No remaining active trigger phrases route to `confirmation-reducer`
- [ ] `catalog-pruner` DUPLICATE count drops for gate-removal domain
- [ ] Grep `.claude/` for `confirmation-reducer` references — only deprecated notice remains
