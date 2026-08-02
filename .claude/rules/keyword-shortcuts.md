# Keyword Shortcuts

These user phrases always invoke the matching script or skill. This file owns
phrase triggers only. `deterministic-scripts.md` owns command routing and the
full flag list.

| User says                                                          | Invokes                                       |
| ------------------------------------------------------------------ | ---------------------------------------------- |
| `run tests` / `test this` / `run targeted tests` / `build and test`| `scripts/test.sh <FullyQualifiedClassName>`   |
| `run all tests` / `full test suite` / `test --all`                 | `scripts/test.sh --all`                        |
| `build` / `compile` / `run build` / `build project`                | `scripts/build.sh [--project <name>]`          |
| `am I good` / `check this` / `verify changes`                      | `scripts/check.sh`                             |
| `what changed` / `show status`                                     | `scripts/status.sh`                            |
| `show diff` / `diff summary`                                       | `scripts/diff.sh`                              |
| `next task` / `task progress`                                      | `scripts/task.sh next`                         |
| `format check` / `check format`                                    | `scripts/format.sh --check`                    |
| `fix format` / `apply format`                                      | `scripts/format.sh --changed`                  |
| `format no jb` / `skip jb format`                                  | `scripts/format.sh --changed --no-jb`          |
| `get timestamp` / `iso timestamp`                                  | `scripts/ts.sh`                                |
| `run ci` / `full ci`                                               | `scripts/ci.sh`                                |
| `preflight` / `ready to commit`                                    | `/yeet --dry-run`                              |
| `coverage areas` / `coverage heuristic`                            | `scripts/coverage-areas.sh`                    |
| `podman build` / `podman test` / `podman run`                      | `scripts/podman.sh`                            |
| `check actions` / `outdated actions`                               | `scripts/gh-actions-update.sh`                 |
| `lint yaml` / `check yaml`                                         | `scripts/lint-yaml.sh`                         |
| `lode summary` / `lode section`                                    | `scripts/lode-summary.sh`                      |
| `apply updates` / `update packages` / `bump deps`                  | `scripts/update.sh [--apply]`                  |
| `poll until` / `wait for build` / `loop until`                     | `scripts/loop.sh`                              |
| `scan violations` / `check anti-patterns` / `violation scan`       | `scripts/violation-scan.sh`                    |
| `analyzer context` / `analyzer status`                             | `scripts/analyzer-context.sh`                  |
| `verify and ship` / `ship this branch`                             | `scripts/verify-and-ship.sh`                   |
| `lode guard` / `check lode size`                                   | `scripts/lode-guard.sh <file>`                 |
| `analyzer stats` / `rule count`                                    | `scripts/analyzer-stats.sh`                    |
| `codebase stats` / `file counts` / `largest files`                 | `scripts/codebase-stats.sh`                    |
| `session mine` / `transcript analysis` / `session patterns`        | `scripts/session-mine.sh all`                  |
| `session health` / `error trends` / `what keeps failing`           | `scripts/session-health.sh`                    |
| `catalog stats` / `skill count` / `agent count`                    | `scripts/catalog-stats.sh`                     |
| `check releases` / `analyzer releases` / `release check`           | `scripts/internal/analyzer-release-check.sh`   |
| `dep check` / `vulnerable packages` / `outdated packages`          | `scripts/dep-check.sh --json`                  |
| `file outline` / `outline this file` / `show structure`            | `scripts/file-outline.sh <path>`               |
| `type deps` / `who calls this type`                                | `scripts/deps.sh <Type>`                       |
| `bump lode timestamps` / `stale lode`                              | `scripts/lode-ts.sh --changed`                 |
| `capture to lode` / `save this to lode`                            | `/lode-capture`                                |
