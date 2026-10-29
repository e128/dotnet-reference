---
name: mr-sparkle-dotnet
description: >
  .NET overlay for mr-sparkle. Scopes the diff, runs the repo diagnostic, violation,
  suppression and analyzer scripts, adds Roslyn anti-pattern and cycle detection, and
  supplies the repo review rubrics. Dispatched by mr-sparkle. Never edits code.
when_to_use: dispatched by mr-sparkle
argument-hint: "[days N | commits N] [--perf] [--test-quality]"
user-invocable: true
---

# Mr. Sparkle: .NET Overlay

Adds the .NET-specific work to a `mr-sparkle` run. This skill reviews only. It never edits code.

It supplies three things that the core `mr-sparkle` skill cannot assume: repo diff scoping, a tool-driven analyzer lens, and the repo review rubrics.

## 1. Scope the Diff

Run these steps in order and hand the result to the other reviewers.

1. List the changed files with `scripts/diff.sh --files`.
2. Keep `.cs`, `.csproj`, `.props`, `.targets`, and solution files.
3. Exclude mechanical files with `scripts/internal/mechanical-diff.sh --json`. Report the excluded count.
4. Deliver the diff to each reviewer with `scripts/internal/cr-diff-deliver.sh <difffile>`. Follow the strategy it prints. Delete `.claude/tmp/cr-*.diff` after the run.

A shell script change also falls under `scripts/lint-shell.sh`. Note it, and do not run the gate.

## 2. Run the .NET Lens

This lens reads tool output. It does not guess. Spawn it as one subagent, or run the steps in the main context when the step needs a session-only MCP server.

| Step                      | Command                              |
| ------------------------- | ------------------------------------ |
| Build diagnostics         | `scripts/diagnostics.sh --group`     |
| Analyzer and pattern scan | `scripts/violation-scan.sh`          |
| Suppression inventory     | `scripts/suppression-scan.sh --json` |
| Analyzer rule stats       | `scripts/analyzer-stats.sh --json`   |

Report these as findings.

- A new diagnostic in a changed file.
- A new suppression. The quality gate needs user approval for every suppression.
- A null-forgiving operator that hides CS8600 through CS8604.
- A repeated manual fix that a new analyzer rule would prevent. Report it in prose only. Never create a plan.

When `cwm-roslyn-navigator` answers, also run `detect_antipatterns` and `detect_circular_dependencies` on the solution. Map a cross-project cycle to High and a namespace cycle to Medium. Skip generated code. When the server does not answer, return `Roslyn navigator: MCP server not running, skipped`.

## 3. Supply the Rubrics

Point the correctness reviewer at these files. Do not copy their text.

- [../review-local/references/review-rubrics.md](../review-local/references/review-rubrics.md): seven rubric checklists and the test quality rubric.
- [../review-local/references/cross-file-consistency.md](../review-local/references/cross-file-consistency.md): the multi-file consistency rubric.
- [../review-local/references/perf-patterns.md](../review-local/references/perf-patterns.md): the anti-pattern catalog for `--perf`.

Add the test quality rubric when the diff touches `tests/**`. Add the performance rules when the run passes `--perf`.

## 4. Add Repo Rules

When the diff adds a `.sh` file, check it against `.claude/rules/shellcheck-validation.md`. When the diff adds a `.cs` file, check it against `.claude/rules/quality-gates.md` and `.claude/rules/dotnet-anti-patterns.md`. Report a breach as a finding. Do not fix it.

[references/dotnet-lens.md](references/dotnet-lens.md) holds the lens detail and the source of each rule.
