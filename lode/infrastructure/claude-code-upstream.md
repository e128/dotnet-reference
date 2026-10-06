# Claude Code Upstream Reference
*Updated: 2026-10-06T19:17:31Z*

Baseline snapshot of official Claude Code harness guidance. Config health checks use it. This file
covers harness features only: no model names, model defaults, context-window sizes, or pricing.

## Current Version: 2.1.292

The installed version is 2.1.291. The changelog top entry is 2.1.292.

## Harness Changes Since 2.1.155

| Version | Change                                                                                                                                                                                                                                                                            |
| ------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2.1.292 | Agent tool takes an `effort` parameter. A subagent with `permissionMode: auto` no longer enters auto mode when auto mode is unavailable. Agent names allow 256 characters at most. `claude plugin install --marketplace <source>` added.                                          |
| 2.1.290 | Plugin hook event `tool.check` carries `agentId`. `claude plugin validate` lists `gatingHooks`.                                                                                                                                                                                   |
| 2.1.289 | Subagent `AGENTS.md` in a subdirectory attaches when you @-mention a file under it.                                                                                                                                                                                               |
| 2.1.288 | Path-scoped `.claude/rules` and nested `CLAUDE.md` files load when Write or Edit changes a file in scope. `/code-review --max-findings <n>\|all` added.                                                                                                                           |
| 2.1.287 | `InstructionsLoaded` hook omits `agent_id` and `agent_type` for subagent file access. `claude attach <name>` and `claude logs <name>` accept part of a session name. The skill listing shows both the `SKILL.md` name and the folder name when they differ.                       |
| 2.1.286 | `/hooks` opens one list of hooks grouped by event. Whole-tool `Bash` allow rules prompt for shell writes to files the file tools refuse.                                                                                                                                          |
| 2.1.285 | `claude plugin configure <plugin>` added. `claude plugin install --config <server>.<key>=<value>` sets bundled MCP server options. `allowedProviders` managed setting added.                                                                                                      |
| 2.1.284 | Subagents with worktree isolation load the project `CLAUDE.md` once.                                                                                                                                                                                                              |
| 2.1.282 | Skill folders in the `anthropic-skills` namespace stopped loading, then a later entry reverted the `claude-ai` reservation.                                                                                                                                                       |
| 2.1.280 | A typed `/skills` opens the skills menu.                                                                                                                                                                                                                                          |
| 2.1.277 | `AGENTS.md` support: with no `CLAUDE.md`, Claude Code reads `AGENTS.md`. Change it under "Project instructions" in `/config`.                                                                                                                                                     |
| 2.1.275 | Skills and plugins from a claude.ai account sync to terminal sessions. Opt out with `syncClaudeAiSkills: false` or `syncClaudeAiPlugins: false`.                                                                                                                                  |
| 2.1.271 | `omitClaudeMd` agent field needs this version or later. Auto mode applies default-mode permission rules to inline `!` commands in skills.                                                                                                                                         |
| 2.1.269 | `omitClaudeMd` added to agent frontmatter and `--agents` JSON. `/reload-skills` count matches the slash menu.                                                                                                                                                                     |
| 2.1.268 | `--json` added to `claude plugin install`, `uninstall`, `update`, `enable`, and `disable`.                                                                                                                                                                                        |
| 2.1.265 | `--plugin-dir` accepts a folder of plugins. `effort:` frontmatter applies to commands, skills, and subagents.                                                                                                                                                                     |
| 2.1.264 | `--append-subagent-system-prompt-file` added.                                                                                                                                                                                                                                     |
| 2.1.260 | `/reload-plugins` available in headless sessions. Permission rules with text after the closing parenthesis report as invalid settings.                                                                                                                                            |
| 2.1.259 | `managedMcpServers` managed setting added. Plugin symlinked component paths outside the plugin directory are refused. Frontmatter `model:` on commands and skills works in interactive sessions.                                                                                  |
| 2.1.257 | `permissions.blockReadsOutsideWorkingDirectories` option and a one-time auto mode prompt added.                                                                                                                                                                                   |
| 2.1.251 | `PreModelSwitch` and `PostModelSwitch` hook events added.                                                                                                                                                                                                                         |
| 2.1.248 | `experimental.cacheTtl` agent field added (needs this version). `--permission-prompts none` added for headless hosts.                                                                                                                                                             |
| 2.1.247 | Auto mode tab in `/permissions` shows classifier rules.                                                                                                                                                                                                                           |
| 2.1.246 | Startup warning for `Bash` allow rules with a wildcard before the subcommand. `maxTurns` needs this version.                                                                                                                                                                      |
| 2.1.243 | `--agents` exits with an error on invalid JSON or invalid agent definitions. `promptCacheTtl` and `subagentPromptCacheTtl` settings added.                                                                                                                                        |
| 2.1.238 | Plugin marketplaces accept `headersHelper` on a URL marketplace or a catalog entry.                                                                                                                                                                                               |
| 2.1.233 | Skills synced from claude.ai do not shadow local commands. Their `!` commands and `@` files do not run.                                                                                                                                                                           |
| 2.1.232 | Bash input redirections get permission checks. Nested git repositories need their own trust confirmation.                                                                                                                                                                         |
| 2.1.224 | `archive` plugin source installs a zip over HTTPS with optional SHA-256 pinning.                                                                                                                                                                                                  |
| 2.1.223 | Agent, skill, and command `.md` files that start with a UTF-8 BOM load.                                                                                                                                                                                                           |
| 2.1.221 | Plugins accept `"."` as a `skills` path. Plugins installed from `/plugin` activate when safe.                                                                                                                                                                                     |
| 2.1.219 | `DirectoryAdded` hook fires after `/add-dir`. Subagents spawn nested subagents to depth 3 by default.                                                                                                                                                                             |
| 2.1.218 | `context: fork` skills run in the background by default (`background: false` opts out). Skill and plugin frontmatter booleans accept `yes`, `no`, `on`, `off`, `1`, `0`. `/review` aliases `/code-review`, which runs as a background subagent.                                   |
| 2.1.217 | Agent names reject `:`. A cap of 20 concurrent subagents applies (`CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS`).                                                                                                                                                                        |
| 2.1.216 | The Task tool `mode` parameter is ignored. Subagents inherit the parent permission mode.                                                                                                                                                                                          |
| 2.1.215 | Claude no longer runs `/verify` and `/code-review` on its own.                                                                                                                                                                                                                    |
| 2.1.214 | `SessionStart` hooks report source `"fork"` for forked sessions. Single-segment `dir/**` hook `if:` conditions match only `<cwd>/dir`.                                                                                                                                            |
| 2.1.212 | Subagent forking is on by default (`subagent_type: "fork"`). A cap of 200 spawns per session applies (`CLAUDE_CODE_MAX_SUBAGENTS_PER_SESSION`). `/fork` starts a background session. Subagents run in the background by default.                                                  |
| 2.1.203 | A footer badge shows manual permission mode.                                                                                                                                                                                                                                      |
| 2.1.200 | The `default` permission mode is named "Manual". `--permission-mode manual` and `"defaultMode": "manual"` work.                                                                                                                                                                   |
| 2.1.198 | The `/agents` wizard is removed: edit `.claude/agents/` directly. `claude agents` background sessions fire the `Notification` hook.                                                                                                                                               |
| 2.1.196 | `${CLAUDE_PROJECT_DIR}` substitution in skills needs this version.                                                                                                                                                                                                                |
| 2.1.186 | `Stop` and `SubagentStop` hooks return `hookSpecificOutput.additionalContext`. Skill frontmatter keys `display-name`, `default-enabled`, `fallback`, and `metadata.*` accept kebab-case, snake_case, and camelCase. Malformed `SKILL.md` YAML loads the body with empty metadata. |
| 2.1.181 | `${user_config.*}` in plugin hook shell commands is rejected. Use exec form `args` or `$CLAUDE_PLUGIN_OPTION_<KEY>`.                                                                                                                                                              |
| 2.1.169 | `disableBundledSkills` setting and `CLAUDE_CODE_DISABLE_BUNDLED_SKILLS` variable hide bundled skills.                                                                                                                                                                             |
| 2.1.162 | `/plugin list` with `--enabled` and `--disabled` filters added.                                                                                                                                                                                                                   |
| 2.1.157 | Plugins in `.claude/skills` load without a marketplace. `claude plugin init <name>` scaffolds one.                                                                                                                                                                                |

Older harness changes (2.1.74 to 2.1.155) that still matter:

| Version         | Change                                                                                                                                                                                                                    |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2.1.152-2.1.154 | Skills and commands accept `disallowed-tools`. `/reload-skills` added. `SessionStart` hooks return `reloadSkills: true` and `sessionTitle`. `MessageDisplay` hook event added. `defaultEnabled: false` in `plugin.json`.  |
| 2.1.153         | Subagent frontmatter MCP servers honor `--strict-mcp-config` and managed MCP policy.                                                                                                                                      |
| 2.1.145         | `claude agents --json` lists live sessions. `Stop` and `SubagentStop` hook input adds `background_tasks` and `session_crons`.                                                                                             |
| 2.1.141-2.1.144 | `claude agents` accepts `! <command>`. `/goal` added. `/resume` lists background sessions. Hook JSON output accepts `terminalSequence`.                                                                                   |
| 2.1.139-2.1.140 | Hook `args: string[]` exec form. `subagent_type` matching ignores case and separators. `worktree.baseRef` (2.1.133).                                                                                                      |
| 2.1.121         | `${CLAUDE_EFFORT}` available in skills. `PostToolUse` input adds `duration_ms`.                                                                                                                                           |
| 2.1.105-2.1.107 | Skill listing cap raised to 1,536 characters. `PreCompact` hook can block compaction. Plugin `monitors` manifest key.                                                                                                     |
| 2.1.99          | `disableSkillShellExecution` setting.                                                                                                                                                                                     |
| 2.1.97-2.1.78   | Plugin agents accept `effort`, `maxTurns`, and `disallowedTools`. Hook events `PermissionDenied`, `StopFailure`, `PostCompact`, `TaskCreated`, `CwdChanged`, and `FileChanged`. Hook `if` field. `${CLAUDE_PLUGIN_DATA}`. |
| 2.1.83          | `managed-settings.d/` drop-in directory. Agents accept `initialPrompt`.                                                                                                                                                   |
| 2.1.80          | `effort` frontmatter for skills and commands.                                                                                                                                                                             |
| 2.1.50-2.1.51   | `isolation: worktree` for agents. `claude agents` command. `WorktreeCreate` and `WorktreeRemove` hooks.                                                                                                                   |
| 2.1.32-2.1.33   | `InstructionsLoaded` hook. `agent_id` and `agent_type` in hook events. `memory` agent field. `Agent(agent_type)` allowlist in `tools`.                                                                                    |
| 2.1.0           | `context: fork`, `agent`, and `hooks` in skill frontmatter. `hooks` in agent frontmatter. `once: true` for hooks.                                                                                                         |

## Subagent Frontmatter Fields

All fields for `.claude/agents/*.md` YAML frontmatter, as listed in the sub-agents docs:

| Field             | Required | Notes                                                                                                                                                       |
| ----------------- | -------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `name`            | Yes      | Unique identifier. Hooks receive it as `agent_type`. Cannot contain `:`. Filename may differ.                                                               |
| `description`     | Yes      | When Claude delegates to this subagent. Combined descriptions over 15,000 tokens trigger a warning.                                                         |
| `tools`           | No       | Comma-separated string or YAML list. Inherits all tools when omitted. `Agent(agent_type)` limits spawnable subagents.                                       |
| `disallowedTools` | No       | Denylist removed from the inherited or listed tools. A specifier such as `Bash(git push *)` removes the whole tool.                                         |
| `model`           | No       | Model override or `inherit`.                                                                                                                                |
| `permissionMode`  | No       | `default`, `acceptEdits`, `auto`, `dontAsk`, `bypassPermissions`, `plan`, or `manual` (alias for `default`). Ignored for plugin subagents.                  |
| `maxTurns`        | No       | Maximum agentic turns. Output is partial at the limit. Needs 2.1.246 or later.                                                                              |
| `skills`          | No       | Skills preloaded at startup (full content). Unlisted skills stay callable through the Skill tool.                                                           |
| `mcpServers`      | No       | Server name references or inline definitions. Ignored for plugin subagents.                                                                                 |
| `hooks`           | No       | Lifecycle hooks scoped to the subagent. `Stop` converts to `SubagentStop`. Ignored for plugin subagents.                                                    |
| `memory`          | No       | `user`, `project`, or `local`. Paths: `~/.claude/agent-memory/<name>/`, `.claude/agent-memory/<name>/`, `.claude/agent-memory-local/<name>/`.               |
| `background`      | No       | `true` keeps the subagent in the background even when Claude requests foreground.                                                                           |
| `omitClaudeMd`    | No       | `true` skips user, project, and local `CLAUDE.md`. Managed policy files still load. Ignored under `--agent` or the `agent` setting. Needs 2.1.271 or later. |
| `effort`          | No       | `low`, `medium`, `high`, `xhigh`, or `max`. Overrides the session level. Default: inherit.                                                                  |
| `isolation`       | No       | `worktree` runs the subagent in a temporary git worktree. Claude Code removes it when no changes exist.                                                     |
| `color`           | No       | `red`, `blue`, `green`, `yellow`, `purple`, `orange`, `pink`, or `cyan`.                                                                                    |
| `initialPrompt`   | No       | First user turn when the agent runs as the main session through `--agent` or the `agent` setting. Ignored for plugin subagents.                             |
| `experimental`    | No       | Map of experimental options. `cacheTtl` accepts `5m` or `1h`. Read only from subagent files. Needs 2.1.248 or later.                                        |

## Skill Frontmatter Fields

All fields for `.claude/skills/*/SKILL.md` YAML frontmatter, as listed in the skills docs:

| Field                      | Required    | Notes                                                                                                            |
| -------------------------- | ----------- | ---------------------------------------------------------------------------------------------------------------- |
| `name`                     | No          | Command name in the `/` menu. Defaults to the directory name.                                                    |
| `description`              | Recommended | Claude uses it to decide when to apply the skill. Truncated at 1,536 characters with `when_to_use`.              |
| `when_to_use`              | No          | Extra trigger context. Appended to `description` and counts toward the cap.                                      |
| `argument-hint`            | No          | Autocomplete hint, such as `[issue-number]`.                                                                     |
| `arguments`                | No          | Named positional arguments for `$name` substitution. String or YAML list.                                        |
| `disable-model-invocation` | No          | `true` stops automatic invocation. Also blocks subagent preload and scheduled tasks. Default `false`.            |
| `user-invocable`           | No          | `false` hides the skill from the `/` menu. Default `true`.                                                       |
| `allowed-tools`            | No          | Tools allowed without a prompt for the invocation turn. Space or comma separated string, or YAML list.           |
| `disallowed-tools`         | No          | Tools removed from Claude's pool while the skill is active. Clears at the next user message.                     |
| `model`                    | No          | Model override or `inherit`. With `context: fork`, it sets the forked subagent.                                  |
| `effort`                   | No          | `low`, `medium`, `high`, `xhigh`, or `max`. Overrides the session level.                                         |
| `context`                  | No          | `fork` runs the skill in an isolated subagent context.                                                           |
| `agent`                    | No          | Subagent type for `context: fork`: `Explore`, `Plan`, `general-purpose`, or a custom agent.                      |
| `background`               | No          | With `context: fork`, `false` waits for the result in the invoking turn. Default `true`. Needs 2.1.218 or later. |
| `hooks`                    | No          | Hooks registered at invocation and kept for the session.                                                         |
| `paths`                    | No          | Glob patterns that limit automatic activation. String or YAML list.                                              |
| `shell`                    | No          | `bash` (default) or `powershell`, for `!` command blocks.                                                        |
| `metadata`                 | No          | Free-form key-value map for tooling. Claude Code ignores the contents.                                           |
| `license`                  | No          | Agent Skills spec field. Claude Code accepts it and takes no action.                                             |
| `compatibility`            | No          | Agent Skills spec field (500 characters at most). Claude Code takes no action.                                   |

Boolean fields accept `true`, `false`, `yes`, `no`, `on`, `off`, `1`, and `0`.

Only `name`, `description`, `license`, `compatibility`, `metadata`, and `allowed-tools` work in
claude.ai uploads and the Skills API. Other keys fail packaging.

String substitutions: `$ARGUMENTS`, `$ARGUMENTS[N]`, `$N`, `$name`, `${CLAUDE_SESSION_ID}`,
`${CLAUDE_EFFORT}`, `${CLAUDE_SKILL_DIR}`, and `${CLAUDE_PROJECT_DIR}` (2.1.196+). Plugin skills also
get `${CLAUDE_PLUGIN_ROOT}` and `${CLAUDE_PLUGIN_DATA}`. Arguments expand first, then `${CLAUDE_*}`.

### Skill Description Budget

- Per-skill cap: 1,536 characters (`description` plus `when_to_use`). Verified 2026-10-06.
- Total listing budget scales with the context window. The 2026-10-06 fetch did not confirm the
  earlier figures (1% of the window, 8,000-character fallback, `SLASH_COMMAND_TOOL_CHAR_BUDGET`).
  Treat them as unverified.

### Skill Compaction Behavior

After auto-compaction, Claude Code re-attaches the most recent invocation of each active skill:

- Each skill keeps its first 5,000 tokens.
- All re-attached skills share a 25,000-token combined budget, filled most recent first.
- Older skills can drop out when the budget runs out.

### Skill Size Guideline

Keep `SKILL.md` under 500 lines. Move large reference material to supporting files in the skill
directory.

## Subagent Limits and Defaults

- Subagents run in the background by default. `subagent_type: "fork"` inherits the full conversation.
- Nesting depth defaults to 3. Set `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH=1` to disable nesting.
- Concurrent subagent cap: 20 (`CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS`).
- Per-session spawn cap: 200 (`CLAUDE_CODE_MAX_SUBAGENTS_PER_SESSION`). `/clear` resets it.
- Subagents inherit the parent permission mode unless frontmatter sets `permissionMode`.

## File Naming

- **Skills:** `SKILL.md` in a directory under `.claude/skills/`.
- **Agents:** Any `.md` file in `.claude/agents/`. The `name` field sets the agent name.
- **Commands:** `.claude/commands/*.md` still works. Skills win when names collide.
- **Project instructions:** `CLAUDE.md`. With no `CLAUDE.md`, Claude Code reads `AGENTS.md` (2.1.277+).

## Conventions from Official Examples (anthropics/skills)

- Official examples consistently use only `name` and `description`.
- `compatibility` is an optional field in the Agent Skills spec, not specific to Claude Code.
- Skills support `!`backtick`` shell preprocessing for dynamic context.
- The official repo uses Apache 2.0 for example skills. Document skills are source-available.

## Sources

- https://raw.githubusercontent.com/anthropics/claude-code/refs/heads/main/CHANGELOG.md (2026-10-06)
- https://code.claude.com/docs/en/sub-agents (2026-10-06)
- https://code.claude.com/docs/en/skills (2026-10-06)
- https://github.com/anthropics/skills/commits/main/ (2026-05-28, not refetched)
