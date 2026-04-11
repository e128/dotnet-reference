# Context: leverage-subtract-gate-reducers
*Created: 2026-04-11T14:33:35Z*

## Axis: Subtraction

## Evidence

### Overlapping gate-removal operations

`confirmation-reducer.md` Phase 2:
> "For each SKILL.md, scan for patterns that create unnecessary gates: AskUserQuestion on auto-approved ops — gate asks approval on something in the policy list"

`skill-loop-optimizer.md` Phase 3, item 1:
> "Remove illegal prompt gates (gates that block on always-safe operations) — Prompting before reading files → remove gate entirely. Prompting before writing `.claude/tmp/` → remove gate entirely. Prompting before running grep/ls/wc/git-diff → remove gate entirely."

Both agents produce the same artifact: a list of gate-removal edits to SKILL.md files.

### Distinction

- `confirmation-reducer`: cross-catalog, policy-driven (loads CLAUDE.md auto-approve list)
- `skill-loop-optimizer`: per-skill, heuristic-driven (identifies read/write/cmd gates by pattern)

These two approaches are complementary but should live in one agent, not two.

### Sequential dependency on leverage-subtract-simplifier-merge

This plan cannot be executed before `leverage-subtract-simplifier-merge` completes, because the target host agent (`simplification-agent`) needs to be merged first. This plan adds `--policy-audit` to the already-merged agent.

## Why Score Is 7 (Not Higher)

User impact is 1/3 because: the routing ambiguity between these two agents is subtle (different trigger phrases, different scope — per-skill vs all-skills). Users rarely invoke both in the same session. The main benefit is catalog cleanliness and reduced audit surface, not session-visible behavior improvement.

## Runners-Up Table

See `leverage-subtract-simplifier-merge-context.md` — this candidate ranks #3 in the subtraction axis.
