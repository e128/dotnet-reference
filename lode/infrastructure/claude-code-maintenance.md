# Claude Code Maintenance
*Updated: 2026-08-02T00:00:00Z*

## Harness Structure

The Claude Code harness for this repo consists of:

- `CLAUDE.md` — always-loaded instructions (keep under 200 lines)
- `.claude/rules/*.md` — domain rules. Claude Code 2.1.220 loads **every** rule
  file into every context window, not just filename-matched ones. Treat the
  whole directory as always-loaded budget.
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

## Rule File Ownership

Each instruction has exactly one owning file. Never restate it elsewhere —
link instead.

| Content                                      | Owner                                |
| -------------------------------------------- | ------------------------------------- |
| `command → script` routing and script flags  | `deterministic-scripts.md`            |
| User-phrase triggers                         | `keyword-shortcuts.md`                |
| Context-economy behavior                     | `token-efficiency.md`                 |
| Re-read triggers                             | `read-before-edit.md`                 |
| Prose and doc style (STE)                    | `writing-style.md`                    |

`deterministic-scripts.md` exceeds the 50-line guideline by design. It holds
one routing table for every script. Splitting it would recreate the
triple-duplication it replaced.

All rule files are written in Simplified Technical English. See
`.claude/rules/writing-style.md`.

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
