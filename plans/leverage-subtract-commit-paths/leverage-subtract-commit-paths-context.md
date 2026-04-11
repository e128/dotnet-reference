# Context: leverage-subtract-commit-paths
*Created: 2026-04-11T14:33:35Z*

## Axis: Subtraction

## Evidence

### Three commit paths in catalog

| Path            | Type   | Triggers                                                        | Calls                         |
| --------------- | ------ | --------------------------------------------------------------- | ----------------------------- |
| `/commit`       | Skill  | "commit", "ready to commit", "preflight"                        | `scripts/internal/commit.sh`  |
| `smart-commit`  | Agent  | "just commit", "fast commit", "commit without asking", "auto-commit" | `scripts/internal/commit.sh` |
| `yeet`          | Skill  | "yeet", "push", "ready to push"                                 | commit.sh + git push          |

### Claimed distinction (from smart-commit.md)

> "Distinct from the /commit skill in that it never stops to ask per-file staging questions."

This distinction is only valid if `/commit` currently has per-file staging gates. Given `confirmation-reducer` agent exists specifically to remove those gates, and has likely been run, the gates may already be absent. Even if they remain: the fix is to remove the gates from `/commit`, not to maintain a second agent.

### Implementation identity

Both paths ultimately call `scripts/internal/commit.sh` with the same arguments. Both draft a conventional commit message. Both skip CI only on explicit instruction. The workflow steps in `smart-commit.md` and `/commit`'s SKILL.md are near-identical.

### Maintenance burden

When `scripts/internal/commit.sh` changes (e.g., new PII scan patterns, new trailer format), developers must verify both `/commit` and `smart-commit` still work correctly. This is the classic "two implementations of the same thing" maintenance tax.

## Why This Is a Real Subtraction (Not Just Consolidation)

The system is cleaner without `smart-commit` as a standalone agent. The `/commit` skill is already keyword-routed and the user-facing entry point. `smart-commit` as an agent adds catalog noise without adding capability. Retiring `smart-commit` (or making it a thin wrapper) reduces: catalog size, trigger ambiguity, and `confirmation-reducer`'s audit surface.

## Runners-Up Table

See `leverage-subtract-simplifier-merge-context.md` — same candidate set ranked there.
