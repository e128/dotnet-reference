# Scoring Rubric

*Part of [script-discovery](../SKILL.md). Extracted to reduce SKILL.md size.*

## Candidate scoring (max 12)

Each candidate scores 0-3 on four dimensions. Primary signal is estimated token reduction.

| Dimension        | 0           | 1               | 2                       | 3                             |
| ---------------- | ----------- | --------------- | ----------------------- | ----------------------------- |
| Frequency        | <1x/week    | 1-2x/week       | 3-5x/week               | Daily or more                 |
| Token cost/occur | <500 tokens | 500-2k tokens   | 2k-5k tokens            | >5k tokens                    |
| Compound value   | Standalone  | Saves 1 re-read | Eliminates error class  | Prevents cascading waste      |
| Automation depth | Script only | Script + hook   | Script + hook + shortcut | Full pipeline: replaces agent |

**Threshold**: exclude candidates scoring <5.

For `--scan-skills` mode, the Frequency dimension measures how many agents/skills
contain the pattern: 1 file = 0, 2 files = 1, 3-4 files = 2, 5+ files = 3.
