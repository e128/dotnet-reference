# Scoring Rubric for Strategic Analysis
*Updated: 2026-05-25T00:00:00Z*

Shared scoring rubric used by `leverage-advisor`, `token-optimizer`, and other agents
that need to score and prioritize candidates.

## Four-Dimension Rubric (0–3 per dimension, max 12)

| Dimension       | 0                        | 1                          | 2                             | 3                                     |
| --------------- | ------------------------ | -------------------------- | ----------------------------- | ------------------------------------- |
| **Novelty**     | Already exists/planned   | Variation on existing thing | New angle on existing domain  | Genuinely new, no overlap             |
| **Compound**    | Isolated                 | Helps one other component  | Improves 2–3 areas            | Makes the whole system better         |
| **User impact** | Marginal, rarely noticed | Sometimes noticeable       | Noticeable most sessions      | Immediately obvious every session     |
| **Automation**  | No automation effect     | Reduces one manual step    | Eliminates a class of steps   | Enables a new automated flow          |

## Subtraction Scoring Adjustments

When scoring items for removal (subtractions):
- **Compound** = how much better is the system *without* this?
- **Automation** = does removing this reduce maintenance burden or automation noise?

## Default Thresholds

| Context          | Plan threshold |
| ---------------- | -------------- |
| `leverage-advisor` | ≥ 7/12       |
| `token-optimizer`  | ≥ 5/9        |

## Related

- [agent-patterns.md](agent-patterns.md) — plan creation conventions
- [scaffolding-heuristics.md](scaffolding-heuristics.md) — H1–H6 scoring for prompt simplification
