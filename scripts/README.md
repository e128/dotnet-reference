# Scripts

Bash scripts (`.sh`) use bash 5+ and live in `scripts/`. Run `scripts/help.sh` for a live catalog. Nushell scripts (`.nu`) require [Nushell](https://www.nushell.sh/) and are not listed by `help.sh`.

## Core Workflow

| Script                | Purpose                                                   | Key flags                         |
| --------------------- | --------------------------------------------------------- | --------------------------------- |
| `build.sh`            | Build the solution or a specific project                  | `--verbose`, `--project`          |
| `test.sh`             | Run tests (defaults to CI category)                       | `--all`, `--verbose`, `--trait`   |
| `format.sh`           | Run jb cleanupcode then dotnet format                     | `--check`, `--changed`, `--no-jb` |
| `check.sh`            | Composed: format + build + test                           | `--all`, `--no-format`            |
| `ci.sh`               | Full CI pipeline                                          | `--skip-format`, `--skip-test`    |
| `docker.sh`           | Docker build/run/test/stop/clean                          | `--no-cache`                      |
| `verify-and-ship.sh`  | Verify, commit, and push (format → check → commit → push) | —                                 |

## Git & Status

| Script              | Purpose                                                                 | Key flags                              |
| ------------------- | ----------------------------------------------------------------------- | -------------------------------------- |
| `status.sh`         | Git status with structured output                                       | `--json`                               |
| `diff.sh`           | Diff summary                                                            | `--json`, `--files`, `--staged`        |
| `branch.sh`         | Branch info vs base                                                     | `--json`, `--human`                    |
| `assert.sh`         | Fail-fast pre-commit gates                                              | `--build-pass`, `--clean-working-tree` |
| `git-forensics.nu`  | Git history forensics (churn, contributors, bugs, velocity, firefights) | `--since`, `--top`, `--json`           |

## Utilities

| Script                 | Purpose                                                       |
| ---------------------- | ------------------------------------------------------------- |
| `ts.sh`                | ISO 8601 timestamp; optionally updates a file                 |
| `help.sh`              | List all scripts with descriptions                            |
| `context.sh`           | Combines status + diff + plans in one call                    |
| `loop.sh`              | Poll-until-condition with timeout                             |
| `update.sh`            | Check for outdated NuGet packages                             |
| `dep-check.sh`         | Check NuGet dependencies for outdated and vulnerable packages |
| `gh-actions-update.sh` | Check GitHub Actions for outdated versions                    |
| `lint-yaml.sh`         | Validate YAML syntax                                          |
| `sdk-version.sh`       | Read SDK version from global.json                             |
| `build-budget.sh`      | Per-session build cycle budget enforcer                       |
| `format-invalidate.sh` | List .cs files modified by last format run                    |

## Lode & Plans

| Script              | Purpose                                                          |
| ------------------- | ---------------------------------------------------------------- |
| `lode-ts.sh`        | Update timestamps on lode files                                  |
| `lode-summary.sh`   | Find and display lode content by section                         |
| `lode-guard.sh`     | Lode file size guard: check line count before appending          |
| `task.sh`           | Task management: check/next/progress                             |
| `lode.nu`           | Nushell wrapper: launch claude with SystemPrompt.txt injected    |
| `lode-ollama.nu`    | Nushell wrapper: launch claude via Ollama backend (default glm-5:cloud) |

## Code Navigation & Analysis

| Script               | Purpose                                                        | Key flags                                       |
| -------------------- | -------------------------------------------------------------- | ----------------------------------------------- |
| `find.sh`            | Deterministic symbol lookup (class/method/callers/refs/file)   | `--class`, `--method`, `--callers`, `--refs`, `--file`, `--json` |
| `file-outline.sh`    | File structure outline with line ranges for targeted reading   | `--json`, `--method`, `--section`               |
| `code-read.sh`       | Extract method/class/section source by name                    | `--method`, `--class`, `--section`, `--line`, `--json` |
| `deps.sh`            | Type dependency graph (callers, callees, interfaces)           | `--callers`, `--callees`, `--interfaces`, `--json` |

## Coverage & Analysis

| Script                 | Purpose                                                                         |
| ---------------------- | ------------------------------------------------------------------------------- |
| `analyzer-context.sh`  | Analyzer project context: version, rules, fix providers, and public API surface |
| `analyzer-stats.sh`    | Analyzer rule and source file statistics                                        |
| `catalog-stats.sh`     | Inventory all agents and skills with frontmatter fields, description lengths, and line counts |
| `codebase-stats.sh`    | Codebase file and LOC statistics by project                                     |
| `coverage-areas.sh`    | Test coverage heuristic by namespace/project                                    |
| `session-health.sh`    | Session analytics: error trends, tool counts, bash commands                     |
| `session-mine.sh`      | Mine Claude Code session transcripts for repeated patterns                      |
| `violation-scan.sh`    | Scan for .NET anti-patterns and rule violations                                 |

## Internal Scripts

These are invoked by skills and agents only — not intended for direct use:

| Script                               | Purpose                                                                                    |
| ------------------------------------ | ------------------------------------------------------------------------------------------ |
| `internal/analyzer-release-check.sh` | Validate analyzer release files (Unshipped/Shipped) against source DiagnosticId constants |
| `internal/commit.sh`                 | Commit helper with co-author trailer                                                       |
| `internal/lode.sh`                   | Legacy Claude CLI wrapper (SystemPrompt.txt)                                               |
| `internal/overlap-detect.sh`         | Detect trigger-phrase overlap between agents and skills                                    |
| `internal/plan-close.sh`             | Verify tasks complete, then remove plan dir                                                |
| `internal/plan-context.sh`           | List active plans, roadmap items, or details                                               |
| `internal/plan-gate.sh`              | Phase gate prerequisite verification                                                       |
| `internal/plan-path.sh`              | Resolve a plan's canonical path by partial name                                            |
| `internal/precommit.sh`              | PII scan on staged files                                                                   |
| `internal/review-agents.sh`          | Discover code-review-relevant agents dynamically from .claude/agents/                      |
| `internal/stage.sh`                  | Stage modified + new files, excluding secrets                                              |
| `internal/stale-plans.sh`            | List plan directories older than N days with no recent modifications                       |
| `internal/version-bump.sh`           | Increment \<Version\> in a project's .csproj                                               |
