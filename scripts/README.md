# Scripts

All scripts use bash 5+ and live in `scripts/`. Run `scripts/help.sh` for a live catalog.

## Core Workflow

| Script       | Purpose                                              | Key flags                         |
| ------------ | ---------------------------------------------------- | --------------------------------- |
| `build.sh`   | Build the solution or a specific project             | `--verbose`, `--project`          |
| `test.sh`    | Run tests (defaults to CI category)                  | `--all`, `--verbose`, `--trait`   |
| `format.sh`  | Run jb cleanupcode then dotnet format                | `--check`, `--changed`, `--no-jb` |
| `check.sh`   | Composed: format + build + test                      | `--all`, `--no-format`            |
| `ci.sh`      | Full CI pipeline                                     | `--skip-format`, `--skip-test`    |
| `docker.sh`  | Docker build/run/test/stop/clean                     | `--no-cache`                      |

## Git & Status

| Script       | Purpose                                              | Key flags                    |
| ------------ | ---------------------------------------------------- | ---------------------------- |
| `status.sh`  | Git status with structured output                    | `--json`                     |
| `diff.sh`    | Diff summary                                         | `--json`, `--files`, `--staged` |
| `branch.sh`  | Branch info vs base                                  | `--json`, `--human`          |
| `assert.sh`  | Fail-fast pre-commit gates                           | `--build-pass`, `--clean-working-tree` |

## Utilities

| Script              | Purpose                                         |
| ------------------- | ----------------------------------------------- |
| `ts.sh`             | ISO 8601 timestamp; optionally updates a file   |
| `help.sh`           | List all scripts with descriptions              |
| `context.sh`        | Combines status + diff + plans in one call      |
| `loop.sh`           | Poll-until-condition with timeout               |
| `update.sh`         | Check for outdated NuGet packages               |
| `gh-actions-update.sh` | Check GitHub Actions for outdated versions   |
| `lint-yaml.sh`      | Validate YAML syntax                            |

## Lode & Plans

| Script              | Purpose                                         |
| ------------------- | ----------------------------------------------- |
| `lode-ts.sh`        | Update timestamps on lode files                 |
| `lode-summary.sh`   | Find and display lode content by section        |
| `task.sh`           | Task management: check/next/progress            |

## Coverage & Analysis

| Script              | Purpose                                         |
| ------------------- | ----------------------------------------------- |
| `coverage-areas.sh` | Test coverage heuristic by namespace/project    |
| `session-health.sh` | Session analytics: error trends, tool counts, bash commands |
| `violation-scan.sh` | Scan for .NET anti-patterns and rule violations  |

## Internal Scripts

These are invoked by skills and agents only — not intended for direct use:

| Script                 | Purpose                                      |
| ---------------------- | -------------------------------------------- |
| `internal/commit.sh`   | Commit helper with co-author trailer         |
| `internal/precommit.sh`| PII scan on staged files                     |
| `internal/stage.sh`    | Stage modified + new files, excluding secrets|
| `internal/plan-close.sh`| Verify tasks complete, then remove plan dir |
| `internal/plan-gate.sh`| Phase gate prerequisite verification         |
| `internal/plan-context.sh`| List active plans, roadmap items, or details |
| `internal/plan-path.sh`| Resolve a plan's canonical path by partial name|
| `internal/version-bump.sh`| Increment <Version> in a project's .csproj  |
| `internal/lode.sh`   | Legacy Claude CLI wrapper (SystemPrompt.txt) |
