# .NET Lens Detail

*The tool-driven lens that the core `mr-sparkle` skill cannot supply.*

## Provenance

| Part                  | Source                                         |
| --------------------- | ---------------------------------------------- |
| Diff scoping          | `.claude/skills/review-local` Phase 1          |
| Tool lens             | `.claude/skills/review-local` Phase 2 vector 2 |
| Rubric set            | `.claude/skills/review-local/references`       |
| Repo gates            | `.claude/rules/quality-gates.md`               |
| Analyzer candidates   | `analyzer-review-miner` agent, read only       |

The repo `review` skill stays the plan-producing path. This overlay produces findings only.

## Questions the Lens Answers

- Which new diagnostic lands in a changed file?
- Which new suppression appears, and did the user approve it?
- Which null-forgiving operator hides CS8600 through CS8604?
- Which new anti-pattern does the Roslyn navigator report?
- Which cross-project cycle does the change add?
- Which manual fix repeats often enough to become an analyzer rule?

## Tool to Severity Map

| Tool                                  | Finding                                          | Severity |
| ------------------------------------- | ------------------------------------------------ | -------- |
| `scripts/diagnostics.sh --group`      | New error in a changed file                      | Critical |
| `scripts/diagnostics.sh --group`      | New warning in a changed file                    | High     |
| `scripts/suppression-scan.sh`         | New `#pragma warning disable`                    | High     |
| `scripts/suppression-scan.sh`         | New `[SuppressMessage]`                          | High     |
| `scripts/violation-scan.sh`           | Repository anti-pattern hit                      | High     |
| Roslyn `detect_circular_dependencies` | Cross-project cycle                              | High     |
| Roslyn `detect_circular_dependencies` | Namespace cycle                                  | Medium   |
| Roslyn `detect_antipatterns`          | Antipattern finding                              | Medium   |
| Roslyn `detect_antipatterns`          | Finding in generated code                        | Skip     |

## Rules the Lens Enforces

1. A suppression needs user approval under the quality gate. Report it, never justify it.
2. Generated code is out of scope. Skip it and say so.
3. `scripts/check.sh` is the only build, format and test gate. Never run `dotnet` directly.
4. A repeated manual fix becomes an analyzer candidate in prose. Never write a plan file.
5. Report a missing bounds check for `Span`, `stackalloc`, or `unsafe` code as a security finding, not a style finding.
6. Report a pooled buffer that returns without a zero fill as Critical. Analyzer rule E128110 covers `ArrayPool<T>` only.

## Diff Size Handling

`scripts/internal/cr-diff-deliver.sh` prints the delivery strategy for one reviewer slice. Follow it exactly.

- 30KB or less: pass the diff inline in the prompt.
- 30KB to 40KB: write `.claude/tmp/cr-<name>.diff` and let the reviewer read it.
- Above 40KB: split at file boundaries, then merge the sub-slice findings under the one reviewer name.

Never deliver a diff from `/tmp`. A subagent cannot read that path.
