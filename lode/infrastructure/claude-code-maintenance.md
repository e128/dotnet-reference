# Harness Maintenance
*Updated: 2026-08-24T15:55:44Z*

## Harness Portability Capability Map

`AGENTS.md` is the portable source of truth. Any coding agent (Claude Code,
Codex CLI, Cursor, Aider, or another tool) can read `AGENTS.md`, `scripts/`,
and `lode/` and get the full working toolkit. `CLAUDE.md` imports
`AGENTS.md` with `@AGENTS.md` and adds only the layer below it, which has no
equivalent on another harness.

Two supported harnesses load the same instruction set in this repo:

- Claude Code loads `CLAUDE.md` plus every file in `.claude/rules/*.md`.
- opencode loads project `AGENTS.md` plus the same rule files through the
  `instructions` field in `opencode.json`. It also reads project-level
  `.claude/skills/*/SKILL.md` natively.
- Subagent definitions live once in `.claude/agents/*.md`. A generated mirror
  in `.opencode/agents/` serves opencode. `/yeet` keeps the mirror current.

| Layer                          | Portable across harnesses | Notes                                                              |
| ------------------------------- | -------------------------- | ------------------------------------------------------------------- |
| `AGENTS.md`                     | Yes                         | Cross-harness rules: communication, workflow, .NET, git, gotchas   |
| `scripts/*.sh`                  | Yes                         | Plain bash. Any agent with shell access can call them              |
| `lode/`                         | Yes                         | Plain markdown project memory. No harness-specific format          |
| `.claude/rules/*.md`            | Yes                         | Shared domain rules. Claude Code auto-loads the directory. opencode loads the same files through `instructions` in `opencode.json` |
| `.claude/skills/`               | Yes                         | Skills (`SKILL.md`). Claude Code loads them through the skill tool. opencode reads project-level `.claude/skills/` natively |
| `prompts/SystemPrompt.txt`      | Yes (content), no (launch)  | The Lode Coding methodology. Content applies on any harness. The `--append-system-prompt` injection mechanism is Claude CLI only |
| `scripts/lode.sh`, `lode.nu`, `lode.ps1`, `lode-ollama.nu` | No | Claude CLI wrappers that inject `prompts/SystemPrompt.txt`. On another harness, read that file directly at session start instead |
| `scripts/lode-opencode.nu`, `lode-opencode-lib.nu` | Partial | OpenCode wrapper: launches `opencode` against an Ollama backend with `prompts/SystemPrompt.txt` injected as the opening message (OpenCode has no persistent system-prompt flag). One-time local provider config: [opencode-ollama-setup.md](opencode-ollama-setup.md) |
| `CLAUDE.md`                     | No                          | Claude Code entry point. Imports `AGENTS.md`, adds a thin Claude-only overlay |
| `.claude/hooks/`                | No                          | Claude Code guardrail hooks. opencode has no hook system; its enforcement lives in `opencode.json` permissions |
| `.claude/settings.json`         | No                          | Claude Code permissions and hook configuration |
| `.claude/agents/*.md`           | Yes (via mirror)            | Source of truth for subagents. Claude Code reads it directly. `opencode-agents.sh sync` generates the `.opencode/agents/` mirror with translated frontmatter and tool names |
| `.opencode/agents/`             | No                          | Generated mirror for opencode. Never hand-edit; regenerate with `scripts/internal/opencode-agents.sh sync` |
| `opencode.json`                 | No                          | opencode config. `instructions` points at the shared rules. `permission` mirrors the approval policy |

When onboarding another harness onto this repo, point it at `AGENTS.md`,
`scripts/help.sh`, and the files under `.claude/rules/`. It loses subagent
orchestration and hook automation unless it ships equivalents. Rules and
skills load on both supported harnesses without duplication.

## Harness Structure

The Claude Code harness for this repo consists of:

- `CLAUDE.md` — always-loaded instructions, imports `AGENTS.md` and adds a
  Claude-only layer (keep the added layer under 200 lines)
- `.claude/rules/*.md` — domain rules shared with opencode. Claude Code 2.1.220
  loads **every** rule file into every context window, not just filename-matched
  ones. Treat the whole directory as always-loaded budget.
- `opencode.json` — the opencode mirror. `instructions` points at the same
  rule directory. `permission` mirrors the approval policy.
- `.claude/hooks/` — core guardrail hooks
- `.claude/settings.json` — permissions and hook configuration
- `.claude/skills/` — skill directories (see `ls .claude/skills/`)
- `.claude/agents/*.md` — agent definitions (see `ls .claude/agents/`)
- `.opencode/agents/` — generated opencode mirror; regenerate with
  `scripts/internal/opencode-agents.sh sync`
- `scripts/*.sh` — bash scripts; `scripts/internal/*.sh` for skill/agent-only scripts

## Build Infrastructure

- `Directory.Build.props` — shared MSBuild properties (TFM, analyzers, code analysis)
- `Directory.Build.targets` — conditional targets (test projects get `OutputType=Exe` + MTP runner)
- `Directory.Packages.props` — Central Package Management version pins
- `global.json` — SDK version pin + MTP test runner configuration
- `nuget.config` — single source with trusted signers and package source mapping

## Adding Rules

- Cross-harness core rules → `AGENTS.md`
- Domain rules → `.claude/rules/{domain}.md` (both supported harnesses load
  it; keep under 50 lines each)
- Claude-only rules → `CLAUDE.md` (keep the added layer under 200 lines)
- New or changed agent → edit `.claude/agents/*.md`, then run
  `scripts/internal/opencode-agents.sh sync`. `/yeet` runs it automatically.
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
| Lode file conventions and privacy floor      | `prompts/SystemPrompt.txt`            |

The last row is a deliberate exception to the one-owner rule.
`writing-style.md` owns the full STE definition. Both supported harnesses
load it now. `prompts/SystemPrompt.txt` carries the lode-write
subset (STE, the dash ban, and the style self-lint) for injected-launcher
sessions, where no repo config file loads. Do not delete that copy as
duplication. Keep the two in agreement when either one changes.

`prompts/SystemPrompt.txt` owns the lode file conventions: the H1 title on line
1, the italic `Updated` header line in ISO 8601 UTC on line 2, the `lode-map.md` entry
form, and the privacy floor (no absolute home path, no email address, no
secret, no real full name). These rules must reach every harness, so they live
in the injected prompt, not in a per-harness rule file.

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
