# Plan: leverage-subtract-gate-reducers
*Created: 2026-04-11T14:33:35Z*

## Analysis Axis
Subtraction

## Score Breakdown

| Dimension    | Score | Rationale                                                                   |
| ------------ | ----- | --------------------------------------------------------------------------- |
| Novelty      | 2/3   | Both agents exist; absorbing one into the other is directionally new        |
| Compound     | 2/3   | Reduces catalog size and trigger ambiguity across all sessions               |
| User Impact  | 1/3   | Rarely causes wrong agent invocations but adds friction in catalog review    |
| Automation   | 2/3   | Removing one agent reduces weekly-learner's catalog audit surface            |
| **Total**    | **7/12** |                                                                         |

## Finding

`confirmation-reducer` and `skill-loop-optimizer` both remove prompt gates from skill files:

- `confirmation-reducer`: "Cross-reference CLAUDE.md's auto-approve list against all SKILL.md files and find prompt gates that ask for user confirmation on operations that are already auto-approved."
- `skill-loop-optimizer` Phase 3, item 1: "Remove illegal prompt gates — gates that block on always-safe operations... Prompting before reading files → remove gate entirely... Prompting before writing `.claude/tmp/` → remove gate entirely..."

These are the same operation described twice in two different agents. The distinction: `confirmation-reducer` works across *all* skills in one pass referencing CLAUDE.md policy, while `skill-loop-optimizer` works on *one* skill and focuses on loop structure too.

Given the leverage-subtract-simplifier-merge plan already proposes merging `skill-loop-optimizer` into `simplification-agent`, the correct sequencing is: after that merge, re-evaluate whether `confirmation-reducer` can be absorbed into the merged agent as a `--policy-audit` flag. This plan captures that second-order subtraction.

## Recommended Action

After `leverage-subtract-simplifier-merge` completes: absorb `confirmation-reducer`'s cross-catalog CLAUDE.md-policy-audit logic into `simplification-agent` as a `--policy-audit` flag. Retire `confirmation-reducer` as standalone agent.

Note: This plan is sequentially dependent on `leverage-subtract-simplifier-merge`. Do not execute this plan before that one.

Do NOT delete files in this plan run. Create plan only.

## Success Criteria

- `simplification-agent --policy-audit` performs all operations currently in `confirmation-reducer`
- `confirmation-reducer.md` deprecated/removed from catalog
- Zero overlap in gate-removal triggers across remaining agents
- `catalog-pruner` reports no DUPLICATE status for gate-removal domain

## Phased Implementation

### Baseline
- Read `confirmation-reducer.md` in full; extract Phase 1 policy load + Phase 2 scan logic
- Confirm `leverage-subtract-simplifier-merge` plan is complete
- Read merged `simplification-agent.md` to find correct injection point for `--policy-audit`

### Implement
1. Add `--policy-audit` flag to `simplification-agent` that loads CLAUDE.md auto-approve list and cross-references all SKILL.md files (Phase 1-2 from `confirmation-reducer`)
2. Merge `confirmation-reducer` trigger phrases into `simplification-agent` description
3. Mark `confirmation-reducer.md` as deprecated with redirect notice

### Verify
- `simplification-agent --policy-audit` produces same gate-removal candidates as `confirmation-reducer` did
- Trigger phrase audit: no remaining routing ambiguity
- `catalog-pruner` reports DUPLICATE count drop for this domain
