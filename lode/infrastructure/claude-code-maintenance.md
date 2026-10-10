# Harness Maintenance
*Updated: 2026-10-10T14:42:33Z*

## Harness Portability Capability Map

`AGENTS.md` is the portable source of truth. Any coding agent (Claude Code,
Codex CLI, Cursor, Aider, or another tool) can read `AGENTS.md`, `scripts/`,
and `lode/` and get the full working toolkit. `CLAUDE.md` imports
`AGENTS.md` with `@AGENTS.md` and adds only the layer below it, which has no
equivalent on another harness.

Claude Code 2.1.277 added native `AGENTS.md` support. When a folder has no
`CLAUDE.md`, Claude Code reads `AGENTS.md` directly with no import needed.
When `CLAUDE.md` is present, as in this repo, Claude Code still prioritizes
it. This repo keeps `CLAUDE.md` for its Claude-only overlay (the Visualize
skill mandate, the `AskUserQuestion` mandate, the `.claude/` config location
rule, and the hooks pointer), not to trigger a fallback load. Those four
rules have no home in `AGENTS.md`, since another harness does not carry
those tools.

Two supported harnesses load instructions in this repo:

- Claude Code loads `CLAUDE.md` plus every file in `.claude/rules/*.md`.
- opencode v2 loads `AGENTS.md` only. It resolves no `instructions` glob, so
  the rule directory reaches it through the read list in `AGENTS.md`. It
  reads project-level `.claude/skills/*/SKILL.md` through a compatibility
  path.
- Subagent definitions live once in `.claude/agents/*.md`. A generated mirror
  in `.opencode/agents/` serves opencode. `/yeet` keeps the mirror current.

| Layer                          | Portable across harnesses | Notes                                                              |
| ------------------------------- | -------------------------- | ------------------------------------------------------------------- |
| `AGENTS.md`                     | Yes                         | Cross-harness rules: communication, workflow, .NET, git, gotchas   |
| `scripts/*.sh`                  | Yes                         | Plain bash. Any agent with shell access can call them              |
| `lode/`                         | Yes                         | Plain markdown project memory. No harness-specific format          |
| `.claude/rules/*.md`            | Partial                     | Shared domain rules. Claude Code auto-loads the directory. opencode resolves no `instructions` glob, so `AGENTS.md` directs it to read the matching file |
| `.claude/skills/`               | Yes                         | Skills (`SKILL.md`). Claude Code loads them through the skill tool. opencode reads project-level `.claude/skills/` through a compatibility path |
| `prompts/SystemPrompt.txt`      | Yes (content), no (launch)  | The Lode Coding methodology. Content applies on any harness. The `--append-system-prompt` injection mechanism is Claude CLI only |
| `scripts/internal/lode.sh`, `lode.nu`, `lode.ps1`, `lode-ollama.nu` | No | Claude CLI wrappers that inject `prompts/SystemPrompt.txt`. On another harness, read that file directly at session start instead |
| `scripts/lode-opencode.nu`, `lode-opencode-lib.nu` | Partial | OpenCode wrapper: launches `opencode` with `prompts/SystemPrompt.txt` as the opening message. opencode has no persistent system prompt flag. The model comes from the `model` key in `opencode.json`. The wrapper drops a `--model` flag, because opencode reads the config key. Ollama needs no provider config, because v2 probes the local server |
| `CLAUDE.md`                     | No                          | Claude Code entry point. Imports `AGENTS.md`, adds a thin Claude-only overlay |
| `.claude/hooks/`                | No                          | Claude Code guardrail hooks. opencode has no hook system. Its enforcement lives in the `permissions` array in `opencode.json` |
| `.claude/settings.json`         | No                          | Claude Code permissions and hook configuration |
| `.claude/agents/*.md`           | Yes (via mirror)            | Source of truth for subagents. Claude Code reads it directly. `opencode-agents.sh sync` generates the `.opencode/agents/` mirror with translated frontmatter |
| `.opencode/agents/`             | No                          | Generated mirror for opencode. Never hand-edit. Regenerate with `scripts/internal/opencode-agents.sh sync` |
| `.opencode/agent-assets/`       | No                          | Generated copies of agent asset directories. They sit outside `agents/`, because opencode v2 walks that tree recursively |
| `opencode.json`                 | No                          | opencode v2 config. The `permissions` array mirrors the approval policy |

When onboarding another harness onto this repo, point it at `AGENTS.md`,
`scripts/help.sh`, and the files under `.claude/rules/`. It loses subagent
orchestration and hook automation unless it ships equivalents. Skills load on
both supported harnesses without duplication. Claude Code auto-loads the rule
directory. opencode reads the matching rule file on demand.

## Harness Structure

The Claude Code harness for this repo consists of:

- `CLAUDE.md`. Always-loaded instructions. It imports `AGENTS.md` and adds a
  Claude-only layer. Keep the added layer under 200 lines.
- `.claude/rules/*.md`. Domain rules shared with opencode. Claude Code 2.1.220
  loads every rule file into every context window, not only the
  filename-matched ones. Treat the whole directory as always-loaded budget.
- `opencode.json`. The opencode v2 config. The `permissions` array mirrors
  the approval policy.
- `.claude/hooks/`. Core guardrail hooks.
- `.claude/settings.json`. Permissions and hook configuration.
- `.claude/skills/`. Skill directories. See `ls .claude/skills/`.
- `.claude/agents/*.md`. Agent definitions. See `ls .claude/agents/`.
- `.opencode/agents/`, `.opencode/agent-assets/`. Generated opencode mirror.
  Regenerate with `scripts/internal/opencode-agents.sh sync`.
- `scripts/*.sh`. Bash scripts. `scripts/internal/*.sh` holds skill-only and
  agent-only scripts.

## opencode v2 Discovery

- opencode v2 loads `AGENTS.md` from the workspace toward the home directory.
  The `instructions` field resolves no file, glob, or URL.
- opencode v2 discovers agent definitions under `.opencode/agents/`
  recursively. A markdown file without frontmatter becomes an agent, so
  never place an asset there.
- opencode v2 reads skills from `.opencode/skills/` and from `.claude/skills/`
  as a compatibility path.
- opencode v2 replaces the v1 `permission` map with an ordered `permissions`
  array. Two action names change: `bash` becomes `shell`, and `task` becomes
  `subagent`.
- Every agent starts with the base policy `{ "*", "*", allow }`. Only a deny
  rule changes behavior, so the mirror emits a deny rule for a read-only
  agent.

## Build Infrastructure

- `Directory.Build.props` — shared MSBuild properties (TFM, analyzers, code analysis)
- `Directory.Build.targets` — conditional targets (test projects get `OutputType=Exe` + MTP runner)
- `Directory.Packages.props` — Central Package Management version pins
- `global.json` — SDK version pin + MTP test runner configuration
- `nuget.config` — single source with trusted signers and package source mapping

## Adding Rules

- Cross-harness core rules → `AGENTS.md`
- Domain rules → `.claude/rules/{domain}.md` (Claude Code auto-loads it and
  opencode reads it on demand, so keep it under 50 lines)
- Claude-only rules → `CLAUDE.md` (keep the added layer under 200 lines)
- New or changed agent → edit `.claude/agents/*.md`, then run
  `scripts/internal/opencode-agents.sh sync`. `/yeet` runs it automatically.
- Knowledge → `lode/` (not AGENTS.md, CLAUDE.md, or rules)

### The Which-Home Test

Ask: would this rule hold for any coding agent, or only for Claude Code
mechanics?

- Portable answer → `AGENTS.md` or `.claude/rules/`.
- Claude-mechanics answer → `CLAUDE.md`.

## Configuration Tiers

Three loaded layers plus a wrapper:

1. `AGENTS.md`, the portable core. Both harnesses load it.
2. `.claude/rules/*.md`, shared domain rules. Claude Code auto-loads them.
   opencode reads the matching file on demand.
3. `CLAUDE.md` plus `lode/`. Claude-only overlay, and knowledge.
4. `prompts/SystemPrompt.txt` wraps all three in lode-launcher sessions.

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
`writing-style.md` owns the full STE definition. Claude Code auto-loads it
and opencode reads it on demand. `prompts/SystemPrompt.txt` carries the lode-write
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

All scripts are bash 5+ and live in `scripts/`. They source `scripts/lib.sh` for shared functions. `lib.sh` re-execs under Homebrew Bash when the interpreter is older than Bash 4, so macOS `/bin/bash` 3.2 does not matter to the caller's PATH order. `scripts/internal/precommit.sh` scans staged files for PII and secrets (see [secret-scanning.md](secret-scanning.md)). Scripts that support `--json` must produce valid JSON output. `scripts/help.sh` auto-discovers all scripts by reading the second line of each `.sh` file.

## Podman

- Resolute-based images (`sdk:10.0-resolute`, `aspnet:10.0-resolute`), runtime installs only `curl` (healthcheck) and cleans apt caches — no FIPS provider (not available via apt on stock Ubuntu Resolute; see [podman.md](podman.md))
- `compose.yaml` with security hardening (`read_only`, `no-new-privileges`, `cap_drop: ALL`)
- `scripts/podman.sh` — build, run, test, stop, clean commands
- `podman machine` as the container VM runtime on macOS (rootless, no daemon)
- `PodmanSmokeTests` detects Podman availability via `podman info` and skips gracefully when unavailable (no test failures)

## Prerequisites

- `rg` (ripgrep) — used by agents, skills, and scripts for fast search
- `fd` — used by scripts for file discovery
- `jq` — used for JSON parsing in scripts
- `bash` 5+ (Homebrew Bash on macOS) — required for associative arrays and modern features
- `podman` — container runtime; on macOS run `podman machine init && podman machine start` once after install
- `jb` (JetBrains ReSharper CLI) — used by `scripts/format.sh` for semantic cleanup before `dotnet format`; gracefully skipped if absent; install with `dotnet tool install -g JetBrains.ReSharper.GlobalTools`
