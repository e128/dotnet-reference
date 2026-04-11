# Tasks: leverage-subtract-simplifier-merge
*Created: 2026-04-11T14:33:35Z*

## Phase 1: Baseline

- [ ] Read `simplification-agent.md` in full — list all phases, tools, trigger phrases, maxTurns
- [ ] Read `skill-loop-optimizer.md` in full — list all phases, tools, trigger phrases, maxTurns
- [ ] Read `skill-self-updater.md` frontmatter — confirm it is the designated apply layer
- [ ] Produce a phase-by-phase comparison table: which phases overlap, which are unique to each

## Phase 2: Implement

- [ ] Add `--apply` flag to `simplification-agent.md` that executes structural optimizations (gate removal, phase collapse, batch-edit enforcement) from `skill-loop-optimizer` Phase 3-4
- [ ] Absorb all `skill-loop-optimizer` trigger phrases into `simplification-agent` description field
- [ ] Update `simplification-agent.md` tools list to include `Edit` (needed for `--apply` mode)
- [ ] Update `skill-loop-optimizer.md`: change description to "Deprecated — use simplification-agent. Triggers redirected." and strip workflow content
- [ ] Search CLAUDE.md and `.claude/rules/keyword-shortcuts.md` for any `skill-loop-optimizer` entries — redirect to `simplification-agent`

## Phase 3: Verify

- [ ] Read merged `simplification-agent.md` — confirm all former `skill-loop-optimizer` capabilities present
- [ ] No trigger phrase in `skill-loop-optimizer.md` that is not now in `simplification-agent.md`
- [ ] `catalog-pruner` (if runnable) — DUPLICATE count drops for this domain
- [ ] Grep `.claude/` for `skill-loop-optimizer` references — only deprecated notice remains
