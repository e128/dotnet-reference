# Claude Code Maintenance
*Updated: 2026-07-22T00:20:07Z*

## Harness Structure

The Claude Code harness for this repo consists of:

- `CLAUDE.md` — always-loaded instructions (keep under 200 lines)
- `.claude/rules/*.md` — contextually-loaded domain rules
- `.claude/hooks/` — core guardrail hooks
- `.claude/settings.json` — permissions and hook configuration
- `.claude/skills/` — skill directories (see `ls .claude/skills/`)
- `.claude/agents/*.md` — agent definitions (see `ls .claude/agents/`)
- `scripts/*.sh` — bash scripts; `scripts/internal/*.sh` for skill/agent-only scripts

## Build Infrastructure

- `Directory.Build.props` — shared MSBuild properties (TFM, analyzers, code analysis)
- `Directory.Build.targets` — conditional targets (test projects get `OutputType=Exe` + MTP runner)
- `Directory.Packages.props` — Central Package Management version pins
- `global.json` — SDK version pin + MTP test runner configuration
- `nuget.config` — single source with trusted signers and package source mapping

## Adding Rules

- Universal rules → `CLAUDE.md` (keep under 200 lines)
- Domain-specific rules → `.claude/rules/{domain}.md` (keep under 50 lines each)
- Knowledge → `lode/` (not CLAUDE.md or rules)

## Script Conventions

All scripts are bash 5+ and live in `scripts/`. They source `scripts/lib.sh` for shared functions. Scripts that support `--json` must produce valid JSON output. `scripts/help.sh` auto-discovers all scripts by reading the second line of each `.sh` file.

## Podman

- Noble-based images (`sdk:10.0-noble`, `aspnet:10.0-noble`), runtime installs only `curl` (healthcheck) and cleans apt caches — no FIPS provider (not available via apt on stock Ubuntu Noble; see [podman.md](podman.md))
- `compose.yaml` with security hardening (`read_only`, `no-new-privileges`, `cap_drop: ALL`)
- `scripts/podman.sh` — build, run, test, stop, clean commands
- `podman machine` as the container VM runtime on macOS (rootless, no daemon)
- `PodmanSmokeTests` detects Podman availability via `podman info` and skips gracefully when unavailable (no test failures)

## Prerequisites

- `rg` (ripgrep) — used by agents, skills, and scripts for fast search
- `fd` — used by scripts for file discovery
- `jq` — used for JSON parsing in scripts
- `bash` 5+ — required for associative arrays and modern features
- `podman` — container runtime; on macOS run `podman machine init && podman machine start` once after install
- `jb` (JetBrains ReSharper CLI) — used by `scripts/format.sh` for semantic cleanup before `dotnet format`; gracefully skipped if absent; install with `dotnet tool install -g JetBrains.ReSharper.GlobalTools`
