# Deterministic Scripts

Route every repeated operation through a canonical script. Never run the raw
command when a script exists. This table is the single owner of command
routing. `keyword-shortcuts.md` owns user-phrase triggers only.

Every script prints terse JSON with `--json`. `build.sh` and `test.sh` print
terse JSON by default. Pass `--verbose` for the full log.

## Script Routing

| Instead of...                                                    | Use                                                             |
| ---------------------------------------------------------------- | ---------------------------------------------------------------- |
| `dotnet build`                                                   | `scripts/build.sh [--project NAME] [--verbose]`                 |
| `dotnet test`                                                    | `scripts/test.sh ClassName` or `--all`                          |
| `dotnet format`                                                  | `scripts/format.sh --changed` or `--check`                      |
| format, build, and test run separately                           | `scripts/check.sh`                                              |
| a full CI run                                                    | `scripts/ci.sh`                                                 |
| session-start orientation                                        | `scripts/context.sh`                                            |
| `git status`                                                     | `scripts/status.sh [--json] [--classify]`                       |
| `git diff` or `git diff --cached`                                | `scripts/diff.sh [--json] [--staged]`                           |
| `git log` or `git rev-list --count`                              | `scripts/branch.sh [--json]`                                    |
| `git add` of modified tracked files                              | `scripts/internal/stage.sh [--include-new]`                     |
| `git commit`                                                     | `scripts/internal/commit.sh MESSAGE`                            |
| `git push`                                                       | the `/yeet` skill                                               |
| `date -u`                                                        | `scripts/ts.sh`                                                 |
| discovering what scripts exist                                   | `scripts/help.sh`                                               |
| Read → Edit → Read on a `tasks.md` file                          | `scripts/task.sh`                                               |
| ad-hoc `rg` for a class or method                                | `scripts/find.sh --class\|--method NAME`                        |
| ad-hoc `rg` for callers                                          | `scripts/find.sh --callers MethodName`                          |
| a full-file Read to reach one method                             | `scripts/code-read.sh --method NAME PATH`                       |
| a full-file Read to learn a file's structure                     | `scripts/file-outline.sh PATH [--json]`                         |
| ad-hoc callers, callees, or interface lookup for a type          | `scripts/deps.sh TYPE [--callers\|--callees\|--interfaces]`     |
| `cat global.json \| jq .sdk.version`                             | `scripts/sdk-version.sh`                                        |
| ad-hoc `rg E128 \| wc -l` chains                                 | `scripts/analyzer-stats.sh [--json]`                            |
| reading analyzer metadata files one by one                       | `scripts/analyzer-context.sh [--json]`                          |
| ad-hoc `fd -e cs \| wc -l` chains                                | `scripts/codebase-stats.sh [--json]`                            |
| ad-hoc `fd -e slnx\|csproj`, `ls src/`, or `IsPackable` greps    | `scripts/solution-inventory.sh [--json\|--packable\|--readmes]` |
| an eyeball diff of `scripts/README.md` against `help.sh`         | `scripts/readme-table-diff.sh [--json]`                         |
| an eyeball diff of the analyzer README rule table against source | `scripts/readme-table-diff.sh --analyzer [--json]`              |
| ad-hoc `jq` over session JSONL                                   | `scripts/session-mine.sh <subcmd> [--json]`                     |
| ad-hoc `jq` over JSONL for error trends and tool counts          | `scripts/session-health.sh [<subcmd>] [--json]`                 |
| ad-hoc `fd` or `ls` over `.claude/agents/` and `.claude/skills/` | `scripts/catalog-stats.sh [--json]`                             |
| hand-editing the opencode agent mirror                              | `scripts/internal/opencode-agents.sh [sync\|--json]`            |
| `dotnet list ... --outdated\|--vulnerable`                       | `scripts/dep-check.sh [--outdated] [--json]`                    |
| parsing raw MSBuild `File.cs(l,c): error CODE` lines             | `scripts/diagnostics.sh [--group\|--code ID\|--diff a b]`       |
| ad-hoc `PackageReference` classify or cross-project heat map     | `scripts/nuget-heat-map.sh [--json]`                            |
| ad-hoc SDK, `TargetFramework`, or Docker `FROM` pin scans        | `scripts/runtime-matrix.sh [--json]`                            |
| ad-hoc `#pragma warning disable` or `[SuppressMessage]` greps    | `scripts/suppression-scan.sh [--json]`                          |
| ad-hoc `PackageVersion` orphan or `ProjectReference` set-diff    | `scripts/deps-graph.sh [--orphans] [--json]`                    |
| a manual `wc -l` before a lode write                             | `scripts/lode-guard.sh <file>`                                  |
| hand-editing an `*Updated:*` timestamp in a lode file            | `scripts/lode-ts.sh [--changed] [--stale] [FILE...]`            |
| a manual clean-tree, build-pass, or test-pass check              | `scripts/assert.sh [--clean-working-tree] [--build-pass]`       |
| hand-editing `<Version>` in a `.csproj`                          | `scripts/internal/version-bump.sh <ProjectName>`                |
| manual analyzer release file checks                              | `scripts/internal/analyzer-release-check.sh [--json]`           |
| manual agent discovery for reviews                               | `scripts/internal/review-agents.sh [--json]`                    |
| manual trigger-phrase overlap checks                             | `scripts/internal/overlap-detect.sh [--json]`                   |
| manual plan-age checks                                           | `scripts/internal/stale-plans.sh [--days N] [--json]`           |
| a manual `settings.json` allow-list gap scan                     | `scripts/internal/settings-gap.sh [--json]`                     |
| hand-classifying a diff as mechanical or substantive             | `scripts/internal/mechanical-diff.sh [--json]`                  |
| sizing or splitting a review diff for delivery                   | `scripts/internal/cr-diff-deliver.sh <difffile>`                |
| locating and parsing the latest review plan's findings           | `scripts/internal/review-findings.sh [--include-low] [--json]`  |
| manual plan-directory path resolution                            | `scripts/internal/plan-path.sh NAME`                            |
| a manual list of active plans and roadmap items                  | `scripts/internal/plan-context.sh [--active-only] [--json]`     |
| a manual phase-prerequisite check                                | `scripts/internal/plan-gate.sh --plan NAME [--json]`            |
| a manual plan-completion check plus directory removal            | `scripts/internal/plan-close.sh --plan NAME [--dry-run]`        |

`session-mine.sh` subcommands: `tool-freq`, `repeated-commands`, `most-read`,
`agent-spawns`, `slash-freq`, `redundant-ci`, `runner-fallback`, and `all`.

Two files are never invoked directly. `scripts/lib.sh` is a sourced library.
`scripts/internal/lode.sh` is the Claude wrapper that injects
`SystemPrompt.txt`.

Skills own the `scripts/internal/` entries. Call one directly only when no
skill covers the step.

## Build Budget

Track build cycles with `scripts/build-budget.sh tick`. The script warns at 5
builds and hard-stops at 10. Batch the fixes before you rebuild.

## Post-Format Safety

After `scripts/format.sh` runs, run `scripts/format-invalidate.sh` to list the
modified `.cs` files. Re-read those files before you edit them.
