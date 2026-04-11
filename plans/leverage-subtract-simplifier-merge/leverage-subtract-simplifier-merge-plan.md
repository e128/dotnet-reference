# Plan: leverage-subtract-simplifier-merge
*Created: 2026-04-11T14:33:35Z*

## Analysis Axis
Subtraction

## Score Breakdown

| Dimension    | Score | Rationale                                                                |
| ------------ | ----- | ------------------------------------------------------------------------ |
| Novelty      | 3/3   | Both agents currently exist and overlap; removing one is net-new clarity |
| Compound     | 2/3   | Removing one reduces trigger-phrase routing ambiguity for all sessions   |
| User Impact  | 2/3   | Duplicate triggers create wrong-agent invocations every session           |
| Automation   | 2/3   | Reduces catalog-pruner maintenance load; fewer stale agents to audit     |
| **Total**    | **9/12** |                                                                      |

## Finding

`simplification-agent.md` and `skill-loop-optimizer.md` are two agents with deeply overlapping purpose:

- Both analyze skill/agent SKILL.md files for optimization opportunities
- `skill-loop-optimizer` triggers on: "optimize skill, skill too slow, reduce skill turns, fix skill loops, skill is prompting too much, make skill faster, collapse phases, skill efficiency fix"
- `simplification-agent` triggers on: "simplify prompts, scaffolding audit, compensatory scaffolding, prompt bloat, skill simplification, prompt drift, model upgrade audit, **over-scaffolded skills, skills need simplifying**, skill friction, **skills need updating**"

The bolded phrases are semantically equivalent to `skill-loop-optimizer` triggers. A user saying "this skill needs simplifying" could route to either agent. When catalog-pruner audits usage, it has to track two separate agents for what is functionally one domain: "make skill files better."

The key distinction claimed: `simplification-agent` scores scaffolding density and supports `--save-baseline`/`--compare` drift tracking, while `skill-loop-optimizer` makes targeted edits. But `skill-self-updater` already handles the "apply changes" role. This creates a three-agent chain (simplification-agent → skill-loop-optimizer → skill-self-updater) where two of three overlap in analysis.

## Recommended Action

Merge `skill-loop-optimizer`'s phase-collapse and prompt-gate-removal logic into `simplification-agent` as an `--apply` flag. Retire `skill-loop-optimizer` as a standalone agent. The `skill-self-updater` remains the apply-layer for content changes; `simplification-agent --apply` handles structural loop and gate optimizations.

Do NOT delete files in this plan run. Create plan only.

## Success Criteria

- Single agent handles both scaffolding analysis and loop/gate optimization
- Zero overlap in trigger phrases between remaining catalog agents
- `catalog-pruner` reports zero DUPLICATE status entries in this domain
- Former `skill-loop-optimizer` triggers reroute correctly to merged agent

## Phased Implementation

### Baseline
- Read both agents in full; extract all trigger phrases, tool lists, maxTurns, and workflow phases
- Identify exactly which phases from `skill-loop-optimizer` are not covered by `simplification-agent`
- Confirm `skill-self-updater` is the designated apply layer (read its frontmatter)

### Implement
1. Add `--apply` flag to `simplification-agent` that executes phases 3-4 from `skill-loop-optimizer` (apply prompt-gate removals, phase collapses) using Edit tool
2. Absorb `skill-loop-optimizer` trigger phrases into `simplification-agent` description
3. Update `skill-loop-optimizer.md` to redirect: change description to "Deprecated — use simplification-agent. Will be removed."
4. Remove `skill-loop-optimizer.md` entry from any keyword routing tables

### Verify
- Read merged agent — confirm all former `skill-loop-optimizer` capabilities present
- Run `catalog-pruner` — confirm DUPLICATE count drops by 1
- Confirm no CLAUDE.md keyword table entries point to `skill-loop-optimizer`
