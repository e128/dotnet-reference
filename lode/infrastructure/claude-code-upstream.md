# Claude Code Upstream Reference
*Updated: 2026-05-28T20:38:16Z*

Baseline snapshot of official Claude Code guidance. Used for periodic config health checks.

## Current Version: 2.1.154

Key recent changes (2.1.139-2.1.154):

| Version      | Notable Changes                                                                                                                                              |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 2.1.154      | Opus 4.8 defaults to high effort (`/effort xhigh` for demanding tasks); dynamic workflows (orchestrate tens-hundreds of agents); lean system prompt default; `/simplify` cleanup-only; `claude agents` `! <cmd>` background shell; stdio MCP gets `CLAUDE_CODE_SESSION_ID`+`CLAUDECODE=1` |
| 2.1.153      | `skipLfs` for git plugin marketplaces; subagent MCP servers now honor strict MCP config + managed policies; `--strict-mcp-config` keeps inline `mcpServers` from explicit agent defs |
| 2.1.152      | **Skills/commands can set `disallowed-tools` in frontmatter**; `/reload-skills` command; `SessionStart` hooks can return `reloadSkills: true`+`sessionTitle`; `MessageDisplay` hook event; `plugin.json` `defaultEnabled: false` |
| 2.1.149      | `/usage` per-category breakdown (skills, subagents, plugins, MCP); `allowAllClaudeAiMcps` managed setting; PowerShell+worktree sandbox security fixes        |
| 2.1.147      | `/simplify` renamed to `/code-review` with effort levels; pinned background sessions stay alive (`Ctrl+T`)                                                   |
| 2.1.145      | `claude agents --json` for scripting; `agent_id`+`parent_agent_id` OTEL spans; `/plugin` previews commands/agents/skills/hooks pre-install                   |
| 2.1.144      | `/resume` for background sessions; `/model` change is session-only (press `d` for default); "extra usage" → "usage credits"                                  |
| 2.1.142      | `claude agents` CLI flags: `--add-dir`, `--settings`, `--mcp-config`, `--plugin-dir`, `--permission-mode`, `--model`, `--effort`, `--dangerously-skip-permissions`; fast mode → Opus 4.7 |
| 2.1.140      | Agent tool `subagent_type` matching now case/separator-insensitive                                                                                           |
| 2.1.139      | Agent view (Research Preview): unified session list; `/goal` command (completion condition); hook `args: string[]` exec form; MCP stdio gets `CLAUDE_PROJECT_DIR` |

Key prior changes (2.1.127-2.1.138):

| Version      | Notable Changes                                                                                                                     |
| ------------ | ----------------------------------------------------------------------------------------------------------------------------------- |
| 2.1.138      | Internal fixes only                                                                                                                 |
| 2.1.137      | VSCode extension activation failure on Windows fixed                                                                                |
| 2.1.136      | `autoMode.hard_deny` classifier blocking; MCP OAuth race fix; plan mode write-block respects Edit allow rules; WSL2 image paste     |
| 2.1.133      | `worktree.baseRef` setting; `sandbox.bwrapPath`+`sandbox.socatPath` for custom binary paths; effort level exposed to hooks+Bash env |
| 2.1.132      | `CLAUDE_SESSION_ID` env var in Bash tool; alternate-screen renderer opt-out                                                         |
| 2.1.131      | VS Code extension Windows activation fix; Mantle auth fix                                                                           |
| 2.1.129      | Plugin URL fetching; synchronized output force-enable; auto-update prompting                                                        |
| 2.1.128      | Random session colors; MCP tool counting; `--channels` console auth support                                                         |

Key prior changes (2.1.91-2.1.126):

| Version      | Notable Changes                                                                            |
| ------------ | ------------------------------------------------------------------------------------------ |
| 2.1.126      | Model picker shows gateway `/v1/models`; `claude project purge`; `--dangerously-skip-permissions` covers `.claude/`+`.git/`; OAuth paste-to-terminal fallback; OTel `claude_code.skill_activated` event adds `invocation_trigger`; fixed deferred tools in `context: fork` skills; Agent SDK hang fixed for malformed parallel tool names; security fix for `allowManagedDomainsOnly`; images >2000px auto-downscaled |
| 2.1.119      | Config persistence to `~/.claude/settings.json`; custom PR URL templates; OAuth/MCP fixes |
| 2.1.118      | Vim visual modes; `cost`+`stats` merged into `usage`; MCP tools callable from hooks       |
| 2.1.117      | Forked subagents on external builds; concurrent MCP startup; persistent model selection    |
| 2.1.116      | Fast session resume for large files; inline thinking indicators; concurrent stdio servers  |
| 2.1.114-115  | Permission dialog crash fixes; stability                                                   |
| 2.1.113      | Native binary execution (was bundled JS); network domain blocking; security hardening      |
| 2.1.112      | Bugfix: Opus 4.7 unavailability in auto mode resolved                                      |
| 2.1.111      | `xhigh` effort (Opus 4.7); auto mode for Max; `/ultrareview`; `/effort` slider             |
| 2.1.110      | `/tui` fullscreen rendering; push notification tool; improved plugin system                |
| 2.1.108      | Prompt cache TTL; `/recap` session summary; Skill tool discovers built-in cmds             |
| 2.1.105      | `EnterWorktree` path param; `PreCompact` hook; plugin background monitors                  |
| 2.1.101      | Sub-agents inherit MCP; worktree Read/Edit fixed; SDK → "Claude Agent SDK"                 |
| 2.1.98       | Subprocess sandboxing; bash hardening; git worktree isolation for agents                   |
| 2.1.96       | Default effort → `high`; skill `name` used for stable invocation names                     |
| 2.1.91       | MCP 500K result size; `disableSkillShellExecution`; edit tool efficiency                   |

## Subagent Frontmatter Fields

All fields for `.claude/agents/*.md` YAML frontmatter:

| Field             | Required | Default     | Notes                                                        |
| ----------------- | -------- | ----------- | ------------------------------------------------------------ |
| `name`            | Yes      | -           | Lowercase letters and hyphens                                |
| `description`     | Yes      | -           | Used for auto-delegation matching                            |
| `tools`           | No       | inherit all | Allowlist; string or YAML list                               |
| `disallowedTools` | No       | -           | Denylist; applied before `tools`                             |
| `model`           | No       | `inherit`   | `sonnet`, `opus`, `haiku`, full ID, or `inherit`             |
| `permissionMode`  | No       | `default`   | `default`, `acceptEdits`, `auto`, `dontAsk`, `bypassPermissions`, `plan` |
| `maxTurns`        | No       | -           | Max agentic turns before stop                                |
| `skills`          | No       | -           | Preloaded skill names (full content injected at startup)     |
| `mcpServers`      | No       | -           | Inline defs or string refs to configured servers             |
| `hooks`           | No       | -           | Lifecycle hooks scoped to this agent                         |
| `memory`          | No       | -           | `user`, `project`, or `local`                                |
| `background`      | No       | `false`     | Always run as background task                                |
| `effort`          | No       | inherit     | `low`, `medium`, `high`, `xhigh`, `max`                      |
| `isolation`       | No       | -           | `worktree` for git worktree isolation                        |
| `color`           | No       | -           | Display color in UI                                          |
| `initialPrompt`   | No       | -           | Auto-submitted first turn when run via `--agent`             |

## Skill Frontmatter Fields

All fields for `.claude/skills/*/SKILL.md` YAML frontmatter:

| Field                      | Required    | Notes                                                         |
| -------------------------- | ----------- | ------------------------------------------------------------- |
| `name`                     | No          | Display name; defaults to directory name. Max 64 chars        |
| `description`              | Recommended | Front-load key use case; truncated at 1,536 chars per skill   |
| `when_to_use`              | No          | Extra trigger context; appended to `description`; counts toward 1,536-char cap |
| `argument-hint`            | No          | Autocomplete hint, e.g. `[issue-number]`                      |
| `arguments`                | No          | Named positional args for `$name` substitution; space-sep or YAML list |
| `disable-model-invocation` | No          | `true` = manual-only via `/name`; also blocks subagent preload |
| `user-invocable`           | No          | `false` = hidden from `/` menu, Claude-only                   |
| `allowed-tools`            | No          | Space-separated or YAML list                                  |
| `disallowed-tools`         | No          | (CC 2.1.152+) Tools removed from pool while skill active; e.g. block `AskUserQuestion` in a background loop. Restriction clears on next user message |
| `model`                    | No          | Model override when skill is active                           |
| `effort`                   | No          | `low`, `medium`, `high`, `xhigh`, `max`                       |
| `context`                  | No          | `fork` to run in subagent                                     |
| `agent`                    | No          | Subagent type when `context: fork` (default: general-purpose) |
| `hooks`                    | No          | Lifecycle hooks scoped to skill                               |
| `paths`                    | No          | Glob patterns limiting auto-activation                        |
| `shell`                    | No          | `bash` (default) or `powershell`                              |

String substitutions: `$ARGUMENTS`, `$ARGUMENTS[N]`, `$N`, `$name` (named args), `${CLAUDE_SESSION_ID}`, `${CLAUDE_SKILL_DIR}`, `${CLAUDE_EFFORT}` (`low`/`medium`/`high`/`xhigh`/`max`/`ultra`).

### Skill description budget (total across all skills)

- Per-skill cap: 1,536 chars (`description` + `when_to_use` combined)
- Total budget: 1% of context window, fallback 8,000 chars if context window unknown
- Override: `SLASH_COMMAND_TOOL_CHAR_BUDGET` env var

### Skill compaction behavior

After auto-compaction, Claude Code re-attaches the most recent invocation of each active skill:
- Each skill: first 5,000 tokens retained
- All re-attached skills share a **25,000-token combined budget**, filled most-recent-first
- Older skills can be dropped entirely if budget is exhausted

### Skill size guideline

Keep `SKILL.md` under 500 lines (official guidance). Move large reference material to supporting files in the skill directory.

## File Naming

- **Skills:** `SKILL.md` is the standard filename. Lives in a directory under `.claude/skills/`.
- **Agents:** Any `.md` file in `.claude/agents/`. Filename (minus extension) becomes the agent name.
- **Commands:** `.claude/commands/*.md` still works but skills take precedence if names collide.

## Key Conventions from Official Examples (anthropics/skills)

- Only `name` and `description` in frontmatter are consistently used across official examples
- `compatibility` is an optional field in the Agent Skills spec (not Claude Code-specific)
- Max skill name length: 64 characters
- Skills support `!`backtick`` for shell preprocessing (dynamic context injection)
- Official repo uses Apache 2.0 for example skills; document skills are source-available

## API / SDK Notes

- **Agent SDK rename**: Python `claude-agent-sdk` (was `claude-code-sdk`); TypeScript `@anthropic-ai/claude-agent-sdk`. Requires v0.2.111+ for Opus 4.7.
- **Opus 4.7**: `budget_tokens` parameter removed — API returns 400 if sent. Adaptive thinking used instead. `xhigh` effort level added (between `high` and `max`).

## Sources

- https://raw.githubusercontent.com/anthropics/claude-code/refs/heads/main/CHANGELOG.md (2026-05-28)
- https://code.claude.com/docs/en/sub-agents (2026-05-28)
- https://code.claude.com/docs/en/skills (2026-05-28)
- https://github.com/anthropics/skills/commits/main/ (2026-05-28)
- https://code.claude.com/docs/en/agent-sdk/overview (2026-05-08)
