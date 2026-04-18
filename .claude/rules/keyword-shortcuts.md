# Keyword Shortcuts

These phrases in user messages always invoke the corresponding script or skill.

| User says                                                                              | Invokes                                     |
| -------------------------------------------------------------------------------------- | ------------------------------------------- |
| `run tests` / `test this` / `run targeted tests` / `build and test`                   | `bash scripts/test.sh <FullyQualifiedClassName>` |
| `run all tests` / `full test suite` / `test --all`                                     | `bash scripts/test.sh --all`                |
| `build` / `compile` / `run build` / `build project`                                   | `bash scripts/build.sh [--project <name>]`  |
| `am I good` / `check this` / `verify changes`                                         | `scripts/check.sh`                          |
| `what changed` / `show status` / `git status`             | `scripts/status.sh`           |
| `show diff` / `diff summary`                              | `scripts/diff.sh`             |
| `next task` / `task progress`                              | `scripts/task.sh next`        |
| `format check` / `check format`                           | `scripts/format.sh --check`   |
| `fix format` / `apply format`                             | `scripts/format.sh --changed`         |
| `format no jb` / `skip jb format`                        | `scripts/format.sh --changed --no-jb` |
| `get timestamp` / `iso timestamp`                          | `scripts/ts.sh`               |
| `run ci` / `full ci`                                       | `scripts/ci.sh`               |
| `preflight` / `ready to commit`                            | `/yeet --dry-run`             |
| `coverage areas` / `coverage heuristic`                    | `scripts/coverage-areas.sh`   |
| `docker build` / `docker test` / `docker run`             | `scripts/docker.sh`           |
| `check actions` / `outdated actions`                       | `scripts/gh-actions-update.sh`|
| `lint yaml` / `check yaml`                                 | `scripts/lint-yaml.sh`        |
| `lode summary` / `lode section`                            | `scripts/lode-summary.sh`     |
| `check updates` / `outdated packages`                      | `scripts/update.sh`           |
| `poll until` / `wait for build` / `loop until`            | `scripts/loop.sh`             |
| `scan violations` / `check anti-patterns` / `violation scan` | `scripts/violation-scan.sh` |
