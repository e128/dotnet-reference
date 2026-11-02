---
name: mr-sparkle-dotnet
description: >
  .NET overlay for mr-sparkle. Scopes the diff, runs the repo diagnostic, violation,
  suppression and analyzer scripts, adds Roslyn anti-pattern and cycle detection,
  flags removable NuGet packages, and supplies the repo review rubrics. Dispatched by
  mr-sparkle. Never edits code.
when_to_use: dispatched by mr-sparkle
argument-hint: "[days N | commits N] [--perf] [--test-quality]"
user-invocable: true
---

# Mr. Sparkle: .NET Overlay

Adds the .NET-specific work to a `mr-sparkle` run. This skill reviews only. It never edits code.

It supplies four things that the core `mr-sparkle` skill cannot assume: repo diff scoping, a tool-driven analyzer lens, the repo review rubrics, and the removable-package check.

## 1. Scope the Diff

Start from the `files` array that `mr-sparkle` resolved with `scripts/internal/cr-target.sh --json`. When no file list was handed in, run that command yourself first. Run these steps in order and hand the result to the other reviewers.

1. Keep the `.cs`, `.csproj`, `.props`, `.targets`, and solution paths from `files`.
2. Exclude mechanical files with `scripts/internal/mechanical-diff.sh --json`. Report the excluded count.
3. Deliver the diff to each reviewer with `scripts/internal/cr-diff-deliver.sh <difffile>`. Follow the strategy it prints. Delete `.claude/tmp/cr-*.diff` after the run.

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

- [../mr-sparkle/references/review-rubrics.md](../mr-sparkle/references/review-rubrics.md): seven rubric checklists and the test quality rubric.
- [../mr-sparkle/references/cross-file-consistency.md](../mr-sparkle/references/cross-file-consistency.md): the multi-file consistency rubric.
- [../mr-sparkle/references/perf-patterns.md](../mr-sparkle/references/perf-patterns.md): the anti-pattern catalog for `--perf`.

Add the test quality rubric when the diff touches `tests/**`. Add the performance rules when the run passes `--perf`.

## 4. Add Repo Rules

When the diff adds a `.sh` file, check it against `.claude/rules/shellcheck-validation.md`. When the diff adds a `.cs` file, check it against `.claude/rules/quality-gates.md` and `.claude/rules/dotnet-anti-patterns.md`. Report a breach as a finding. Do not fix it.

[references/dotnet-lens.md](references/dotnet-lens.md) holds the lens detail and the source of each rule.

## 5. Review for Removable NuGet Packages

Report only. This step never edits `Directory.Packages.props` or a `.csproj`. Run it whenever the diff touches any of these: `*.csproj`, `Directory.Packages.props`, `Directory.Build.props`, `packages.lock.json`.

| Step                     | Command                                  |
| ------------------------ | ---------------------------------------- |
| Orphaned central pins    | `scripts/deps-graph.sh --orphans --json` |
| Package sharing heat map | `scripts/nuget-heat-map.sh --json`       |

A `PackageVersion` with no direct `PackageReference` is a candidate, not a finding. Classify each one before you report it.

1. Read the comment above the entry. An entry under a `<!-- Transitive pins -->` marker is deliberate. The `transitive_pin` field is `true`.
2. Confirm the package is absent from the restore graph: `dotnet list <SLN> package --include-transitive | rg -i '<Name>'`. A package that appears as transitive is used. Skip it.
3. Skip non-code packages: analyzers, source generators, `PrivateAssets="all"` build assets, and runtime-only or DI-glue packages. They carry no `using` directive, so a scan calls them dead.

The full skip list and the case for each trap live in [../prune-deps/references/false-positives.md](../prune-deps/references/false-positives.md). Do not copy it here.

Report a surviving candidate as a Low finding. Give the package name, the owning `Directory.Packages.props` entry, and the evidence that nothing references it. When a project reference exists but no code uses it, report the project and the `.csproj` line. Removal is the job of the `prune-deps` skill, not this review.

## 6. Run the External Guidance Prompt

Run this section only on an explicit request. Its instructions govern that step, including the edit that the prompt asks for. Fetch both URLs at run time. Never vendor their text here, because the upstream files change.

```text
Read https://raw.githubusercontent.com/EgorBo/BlazingFastDotnet/main/SKILL.md and use it to improve the performance of this app. Measure a baseline first, profile to find the actual bottleneck, apply one change at a time, and keep only changes with a measurable win.

Inspect the current codebase for issues listed in https://raw.githubusercontent.com/dotnet/docs/refs/heads/main/docs/standard/unsafe-code/best-practices.md Do not attempt to make minimal reproductions or fixes. Focus on identifying potential safety and code quality issues. Report two groups: 1) correctness issues, 2) anti-patterns
```
