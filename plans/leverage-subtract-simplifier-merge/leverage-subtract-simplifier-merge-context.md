# Context: leverage-subtract-simplifier-merge
*Created: 2026-04-11T14:33:35Z*

## Axis: Subtraction

## Evidence

### Trigger phrase overlap (extracted from agent frontmatter)

| Trigger phrase                  | simplification-agent | skill-loop-optimizer |
| ------------------------------- | -------------------- | -------------------- |
| "simplify prompts"              | yes                  | no                   |
| "skill simplification"          | yes                  | no                   |
| "optimize skill"                | no                   | yes                  |
| "skill too slow"                | no                   | yes                  |
| "reduce skill turns"            | no                   | yes                  |
| "make skill faster"             | no                   | yes                  |
| "collapse phases"               | no                   | yes                  |
| "skill efficiency fix"          | no                   | yes                  |
| "skills need simplifying"       | yes                  | implied by above     |
| "over-scaffolded skills"        | yes                  | implied by above     |
| "skill friction"                | yes                  | yes (implied)        |

### Workflow phase overlap

`skill-loop-optimizer` Phase 3, items 1-4:
- Remove illegal prompt gates
- Collapse consecutive read phases
- Batch edits before build
- Demote report-only stops

`simplification-agent` Phase 3 (scoring):
- Scores for compensatory scaffolding density
- Identifies: prompt gates asking unnecessary confirmations, over-specified step-by-step procedures, duplicate instructions

The same artifacts (illegal prompt gates, over-specified procedures) appear in both agents' scope.

### Three-agent chain currently required

To optimize a skill for both scaffolding and loop structure: `simplification-agent` (analyze) → `skill-loop-optimizer` (apply structural) → `skill-self-updater` (apply content). Two of three agents do analysis of the same file type.

## Why This Beats the Runner-Up

Runner-up was `leverage-subtract-commit-paths` (score 8). Both score above threshold and both get plans. simplification-agent/skill-loop-optimizer merger wins the ordering slot because the compound effect is higher — it also enables the third subtraction (leverage-subtract-gate-reducers) by giving a merged agent a home for the `--policy-audit` flag.

## Runners-Up Table

| Candidate                              | Novelty | Compound | Impact | Automation | Total | Notes                              |
| -------------------------------------- | ------- | -------- | ------ | ---------- | ----- | ---------------------------------- |
| simplification-agent + skill-loop merge | 3      | 2        | 2      | 2          | 9     | WINNER                             |
| commit path consolidation              | 2       | 2        | 2      | 2          | 8     | Gets its own plan                  |
| confirmation-reducer absorption        | 2       | 2        | 1      | 2          | 7     | Gets its own plan (dependent)      |
| lode-sync vs lode-capture overlap      | 2       | 1        | 1      | 1          | 5     | Below threshold; domains are distinct |
| loop.sh vs assert.sh overlap           | 1       | 1        | 1      | 1          | 4     | Below threshold; use cases differ  |
| roadmap smart.sh deferred entry        | 1       | 1        | 1      | 0          | 3     | Below threshold; cosmetic           |
