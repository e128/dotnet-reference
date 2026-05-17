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

## Build Budget

Track build cycles per session with `scripts/build-budget.sh tick`. Warns at 5 builds, hard-stops at 10. Batch fixes before rebuilding.

## Post-Format Safety

After `scripts/format.sh` runs, use `scripts/format-invalidate.sh` to list which `.cs` files were modified. Re-read those files before editing them.
