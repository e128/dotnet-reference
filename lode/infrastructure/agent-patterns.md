# Agent Patterns
*Updated: 2026-08-16T12:37:10Z*

Shared behavioral patterns referenced by multiple agents. Agents should reference
this file rather than inlining these patterns.

## Plan Creation Convention

Agents that create plans use the 3-file convention in `plans/{slug}/`:

| File               | Purpose                                                    |
| ------------------ | ---------------------------------------------------------- |
| `{slug}-plan.md`   | Overview, score/evidence, success criteria, phased approach |
| `{slug}-context.md`| Problem description, evidence, decisions, runners-up       |
| `{slug}-tasks.md`  | Phased task checklist matching plan phases                  |

All timestamps via `scripts/ts.sh`. After writing: `scripts/internal/stage.sh --include-new`.

## Budget Exhaustion Protocol

When fewer than 3 turns remain and phases are still in progress:

1. Emit a partial summary: which phases completed, what was written to disk
2. Write progress to `.claude/tmp/{agent-name}/state.md`
3. Do not start a new phase with fewer than 2 turns remaining

## Bounded Reflection Loop

Post-verification quality check. Cap: N=2 iterations.

For each iteration (while `iteration < cap`):

1. **Self-review** each applied change against its original intent
2. **Verify** via targeted tests on modified files
3. **Cross-review** modified files for silent failures
4. If clean, break early. If issues found and under cap, fix and continue.
5. On cap exceeded, emit warning with unresolved findings list.

Emit per-iteration tracking:
```
--- Reflection Loop: Iteration N/2 ---
Self-review: [issues found | clean]
Cross-review: [issues found | clean]
Action: [fixes applied | exiting early -- clean]
```
