# Claude Code Maintenance
*Updated: 2026-08-15T17:15:32Z*

## Harness Portability Capability Map

`AGENTS.md` is the portable source of truth. Any coding agent (Claude Code,
Codex CLI, Cursor, Aider, or another tool) can read `AGENTS.md`, `scripts/`,
and `lode/` and get the full working toolkit. `CLAUDE.md` imports
`AGENTS.md` with `@AGENTS.md` and adds only the layer below it, which has no
equivalent on another harness.

| Layer                          | Portable across harnesses | Notes                                                              |
| ------------------------------- | -------------------------- | ------------------------------------------------------------------- |
| `AGENTS.md`                     | Yes                         | Cross-harness rules: communication, workflow, .NET, git, gotchas   |
| `scripts/*.sh`                  | Yes                         | Plain bash. Any agent with shell access can call them              |
| `lode/`                         | Yes                         | Plain markdown project memory. No harness-specific format          |
| `prompts/SystemPrompt.txt`      | Yes (content), no (launch)  | The Lode Coding methodology. Content applies on any harness. The `--append-system-prompt` injection mechanism is Claude CLI only |
| `scripts/lode.sh`, `lode.nu`, `lode.ps1`, `lode-ollama.nu` | No | Claude CLI wrappers that inject `prompts/SystemPrompt.txt`. On another harness, read that file directly at session start instead |
| `scripts/lode-opencode.nu`, `lode-opencode-lib.nu` | Partial | OpenCode wrapper: launches `opencode` against an Ollama backend with `prompts/SystemPrompt.txt` injected as the opening message (OpenCode has no persistent system-prompt flag) |
| `CLAUDE.md`                     | No                          | Claude Code entry point. Imports `AGENTS.md`, adds Claude-only rules |
| `.claude/rules/*.md`            | No                          | Claude Code 2.1.220 loads every rule file into every context window, not just filename-matched ones. Treat the whole directory as always-loaded budget |
| `.claude/hooks/`                | No                          | Claude Code guardrail hooks. No equivalent automation hook system on another harness |
| `.claude/skills/`               | No                          | Claude Code skills (`Skill` tool). No equivalent extensibility layer on another harness |
| `.claude/agents/*.md`           | No                          | Claude Code subagents (`Agent` tool). No equivalent orchestration layer on another harness |
| `.claude/settings.json`         | No                          | Claude Code permissions and hook configuration |

When onboarding another harness onto this repo, point it at `AGENTS.md` and
`scripts/help.sh`. It loses skill auto-invocation, subagent orchestration,
and hook automation, and gains nothing to replace them. Those capabilities
stay Claude Code only until the other harness ships an equivalent.

## Harness Structure

The Claude Code harness for this repo consists of:

- `CLAUDE.md` — always-loaded instructions, imports `AGENTS.md` and adds a
  Claude-only layer (keep the added layer under 200 lines)
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

- Cross-harness rules → `AGENTS.md`
- Claude-only rules → `CLAUDE.md` (keep the added layer under 200 lines)
- Domain-specific Claude-only rules → `.claude/rules/{domain}.md` (keep under 50 lines each)
- Knowledge → `lode/` (not AGENTS.md, CLAUDE.md, or rules)

## Rule File Ownership

Each instruction has exactly one owning file. Never restate it elsewhere.
Link instead.

| Content                                      | Owner                                |
| -------------------------------------------- | ------------------------------------- |
| `command → script` routing and script flags  | `deterministic-scripts.md`            |
| User-phrase triggers                         | `keyword-shortcuts.md`                |
| Context-economy behavior                     | `token-efficiency.md`                 |
| Re-read triggers                             | `read-before-edit.md`                 |
| Prose and doc style (STE)                    | `writing-style.md`                    |
| Lode-write style (cross-harness copy)        | `prompts/SystemPrompt.txt`            |

The last row is a deliberate exception to the one-owner rule.
`writing-style.md` owns the full STE definition, and Claude Code is the only
harness that loads it. `prompts/SystemPrompt.txt` carries the lode-write
subset (STE plus the dash ban) so the rule reaches OpenCode and Ollama
sessions. Do not delete that copy as duplication. Keep the two in agreement
when either one changes.

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
