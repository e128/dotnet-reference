# Deterministic Scripts

Every repeated operation goes through a canonical script. Never use raw commands when a script exists.

## Script Routing

| Instead of...                          | Use                                             |
|----------------------------------------|-------------------------------------------------|
| `dotnet build`                         | `scripts/build.sh`                              |
| `dotnet test`                          | `scripts/test.sh ClassName` or `--all`           |
| `dotnet format`                        | `scripts/format.sh --changed`                   |
| `git status`                           | `scripts/status.sh [--json]`                    |
| `git diff`                             | `scripts/diff.sh [--json]`                      |
| `git log`                              | `scripts/branch.sh [--json]`                    |
| Ad-hoc `rg` for class/method lookup    | `scripts/find.sh --class\|--method NAME`        |
| Ad-hoc `rg` for callers                | `scripts/find.sh --callers MethodName`           |
| Full-file Read for one method          | `scripts/code-read.sh --method Name PATH`       |
| `cat global.json \| jq .sdk.version`  | `scripts/sdk-version.sh`                        |
| Ad-hoc `rg E128 \| wc -l` chains      | `scripts/analyzer-stats.sh [--json]`            |
| Ad-hoc `fd -e cs \| wc -l` chains     | `scripts/codebase-stats.sh [--json]`            |
| Ad-hoc `fd -e slnx`/`fd -e csproj`/`ls src/` + `IsPackable` grep | `scripts/solution-inventory.sh [--json\|--packable\|--readmes]` |
| Eyeball-diff `scripts/README.md` vs `help.sh` | `scripts/readme-table-diff.sh [--json]`     |
| Ad-hoc `jq` over session JSONL        | `scripts/session-mine.sh <subcmd> [--json]`     |
| Ad-hoc `fd`/`ls` over agents+skills   | `scripts/catalog-stats.sh [--json]`             |
| Manual analyzer release file checks    | `scripts/internal/analyzer-release-check.sh`    |
| Manual agent discovery for reviews     | `scripts/internal/review-agents.sh [--json]`    |
| Manual trigger-phrase overlap checks   | `scripts/internal/overlap-detect.sh [--json]`   |
| Manual plan-age checks                 | `scripts/internal/stale-plans.sh [--days N] [--json]` |
| Manual settings.json allow-list gap scan | `scripts/internal/settings-gap.sh [--json]`   |
| `dotnet list ... --outdated/--vulnerable` | `scripts/dep-check.sh [--outdated] [--json]` |
| Parsing raw MSBuild `File.cs(l,c): error CODE` lines | `scripts/diagnostics.sh [--group\|--code ID\|--diff a b] [--json]` |
| Eyeballing analyzer README rule table vs source | `scripts/readme-table-diff.sh --analyzer [--json]` |
| Ad-hoc `PackageReference` classify + cross-project heat map | `scripts/nuget-heat-map.sh [--json]` |
| Ad-hoc SDK/`TargetFramework`/Docker `FROM` pin scan | `scripts/runtime-matrix.sh [--json]` |
| Ad-hoc `#pragma warning disable`/`[SuppressMessage]` grep | `scripts/suppression-scan.sh [--json]` |
| Ad-hoc `PackageVersion` orphan / ProjectReference edge set-diff | `scripts/deps-graph.sh [--orphans] [--json]` |
| Hand-classifying a changeset (docs-only/code/mixed) | `scripts/status.sh --classify [--json]` |
| Ad-hoc slash-command freq / redundant-CI / runner-fallback JSONL scans | `scripts/session-mine.sh slash-freq\|redundant-ci\|runner-fallback [--json]` |
| Classifying diff files mechanical vs substantive | `scripts/internal/mechanical-diff.sh [--json]` |
| Sizing/splitting a review diff for delivery | `scripts/internal/cr-diff-deliver.sh <difffile>` |
| Locating + parsing the latest review plan's findings | `scripts/internal/review-findings.sh [--include-low] [--json]` |

## Build Budget

Track build cycles per session with `scripts/build-budget.sh tick`. Warns at 5 builds, hard-stops at 10. Batch fixes before rebuilding.

## Post-Format Safety

After `scripts/format.sh` runs, use `scripts/format-invalidate.sh` to list which `.cs` files were modified. Re-read those files before editing them.
