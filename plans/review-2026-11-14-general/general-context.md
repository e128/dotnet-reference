# Review Context: days 2 Union Review

*Updated: 2026-11-14T00:42:34Z*

## Diff Summary

Base `be24a59`, head `b70fb1c`, 16 commits, 140 files, 8044 insertions, 2701 deletions.
The diff was delivered as 17 slices of 40KB or less at file boundaries.

## Vectors Run

| Vector | Result |
| ------ | ------ |
| `scripts/build.sh` | exit 0, 0 warnings, 0 errors |
| `scripts/diagnostics.sh --group` | no diagnostics |
| `scripts/violation-scan.sh` | 4 hits, all analyzer source fixtures |
| `scripts/suppression-scan.sh --json` | test-only pragmas, pre-existing |
| `scripts/analyzer-stats.sh --json` | rule inventory only |
| `scripts/deps-graph.sh --orphans --json` | two pins, both classified |
| `scripts/nuget-heat-map.sh --json` | 13 packages, no finding |
| `scripts/internal/mechanical-diff.sh --json` | no mechanical files |
| Roslyn navigator | MCP server not queried, no result |

## Reviewers Run

| Reviewer | Coverage | Outcome |
| -------- | -------- | ------- |
| security-reviewer | slices 2, 3, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 | 1 High, 4 Medium, 10 Low |
| code-reviewer | slices 1 through 17 | 9 High, 21 Medium, 40 Low |
| adversarial-review | three attackers over slices 1-17, one defender | 4 High, 5 Medium, 8 Low |
| code-review | full range, effort high | 10 findings, no severity split |
| ponytail-review | main-context lean pass | 6 lean findings |
| mr-sparkle-dotnet | build, diagnostics, suppressions, packages | no gate breach |
| code-reviewer slice 7 | docs and config | 1 Medium, 4 Low |

## Dedup Report

The defender pass validated the 13 merged Critical and High findings.

| Merged from | Reviewers | Result |
| ----------- | --------- | ------ |
| Nested fix drops outer `*` | 3 | CONFIRMED High |
| Class not expanded in atom comparison | 2 | CONFIRMED High |
| Start-info local never binds | 4 | CONFIRMED High |
| Compression stream in `Compress` mode | 2 | CONFIRMED High |
| Identifier-text bound detection | 4 | CONFIRMED High |
| `ContentLength` guard fails open | 2 | CONFIRMED High |
| Guarded catch-all treated as exhaustive | 4 | CONFIRMED High |
| `this.p1` and `this.p2` share a root | 2 | Downgraded Medium |
| `{1,}` rewrite leaves the diagnostic | 2 | Downgraded Medium |
| `Position` assignment counts as a read | 1 | Downgraded Medium |
| Type-wide `MaxResponseContentBufferSize` | 2 | Downgraded Medium |
| Tests bypass the shared runner | 2 | Downgraded Medium |
| Target-typed `new(...)` missed | 3 | Downgraded Medium |

Dropped findings: none.

A late High finding was missing from the defender batch: `lode/dotnet/release-tracking.md:170`.
The main context confirmed it against source. `AnalyzerReleases.Shipped.md` carries release
3.1.0 with E128227, and `E128.Analyzers.csproj` pins version 3.1.0, while the table ends at
3.0.0. Confirmed High.

## Notes

- `scripts/internal/cr-target.sh` reports 15 untracked files that do not exist on disk. The
  file list is otherwise correct. Treat the untracked array as unreliable.
- Build clean lowers unconfirmed Medium and Low compliance findings to advisory.
- Tests were not run during the review. The adversarial functionality attacker ran the
  shipping-script suite (30 of 30 cases) and the union-switch tests (7 of 7).
- The three adversarial attackers ran as subagents. Their defender was folded into the single
  merged defender pass.

Related: [plan](general-plan.md), [tasks](general-tasks.md).
