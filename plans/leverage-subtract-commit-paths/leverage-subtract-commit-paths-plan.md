# Plan: leverage-subtract-commit-paths
*Created: 2026-04-11T14:33:35Z*

## Analysis Axis
Subtraction

## Score Breakdown

| Dimension    | Score | Rationale                                                              |
| ------------ | ----- | ---------------------------------------------------------------------- |
| Novelty      | 2/3   | Three commit paths exist; consolidating is directionally new           |
| Compound     | 2/3   | Reduces confusion in yeet skill, smart-commit agent, and commit skill  |
| User Impact  | 2/3   | Users choosing between "commit", "just commit", "fast commit" daily    |
| Automation   | 2/3   | Fewer paths to maintain; one place to update PII scan logic            |
| **Total**    | **8/12** |                                                                    |

## Finding

Three separate commit paths exist in the catalog:

| Path                          | Where              | Trigger phrases                                  |
| ----------------------------- | ------------------ | ------------------------------------------------ |
| `/commit` skill               | `.claude/skills/`  | "commit", "ready to commit", "preflight"         |
| `smart-commit` agent          | `.claude/agents/`  | "just commit", "fast commit", "commit without asking", "auto-commit" |
| `yeet` skill                  | `.claude/skills/`  | "yeet", "push", "ready to push"                  |

The `/commit` skill and `smart-commit` agent have near-identical behavior: both call `scripts/internal/commit.sh`, both stage tracked modifications, both draft a conventional commit message. The claimed distinction — `smart-commit` "never stops to ask per-file staging questions" — applies only if `/commit` has per-file confirmation gates. If those gates have already been removed (by `confirmation-reducer`), the two are functionally identical.

`yeet` wraps commit + push in one action, so it is genuinely distinct and must stay.

The three-path situation creates: (1) users unsure which to call, (2) two places to update when `commit.sh` behavior changes, (3) `confirmation-reducer` must audit two skill files for the same class of gates.

## Recommended Action

Consolidate `/commit` skill and `smart-commit` agent into one path. The agent wins (more capable routing, can call `scripts/internal/commit.sh` cleanly). Deprecate the `/commit` skill or make it a thin alias that invokes `smart-commit`. Retain `yeet`.

Do NOT delete files in this plan run. Create plan only.

## Success Criteria

- One canonical commit path (excluding yeet) handles all commit trigger phrases
- CLAUDE.md keyword table updated to point all commit triggers to one destination
- `confirmation-reducer` audits only one file for commit-related gates
- `yeet` remains unchanged and fully functional

## Phased Implementation

### Baseline
- Read `/commit` SKILL.md and `smart-commit.md` in full; list every step and decision point
- Identify any gate in `/commit` that `smart-commit` does not have (the claimed distinction)
- Read `scripts/internal/commit.sh` — confirm both paths use it identically

### Implement
1. If `/commit` has no gates that `smart-commit` lacks: update `/commit` SKILL.md to be a one-line redirect to `smart-commit` agent
2. Merge all trigger phrases from `/commit` into `smart-commit.md` description
3. Update CLAUDE.md keyword table: all commit shortcuts → `smart-commit`
4. If `/commit` has unique gates: absorb them into `smart-commit` first, then redirect

### Verify
- Trigger phrase grep across all skill/agent files — confirm no routing ambiguity
- `scripts/help.sh` — confirm skill catalog reflects the change
- `confirmation-reducer` dry-run — one fewer file to audit
