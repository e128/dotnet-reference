# Claude Code Upstream Reference
*Updated: 2026-04-12T23:45:00Z*

Baseline snapshot of official Claude Code guidance. Used for periodic config health checks.

## Current Version: 2.1.101

Key recent changes (2.1.91-2.1.101):

| Version  | Notable Changes                                                                  |
| -------- | -------------------------------------------------------------------------------- |
| 2.1.101  | Sub-agents inherit MCP tools; worktree agents can Read/Edit own files            |
| 2.1.101  | SDK renamed to "Claude Agent SDK"; `/ultraplan`, `/team-onboarding` added        |
| 2.1.101  | OS CA cert store trusted by default; `API_TIMEOUT_MS` respected                  |
| 2.1.98   | Subprocess sandboxing, bash tool hardening, git worktree isolation for agents    |
| 2.1.97   | `/agents` shows running indicators, `/reload-plugins` picks up new skills live   |
| 2.1.96   | Default effort shifted to `high`; skill `name` used for stable invocation names  |
| 2.1.92   | `forceRemoteSettingsRefresh`; removed `/tag` and `/vim` commands                 |
| 2.1.91   | MCP 500K result size; `disableSkillShellExecution` setting; edit tool efficiency  |

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
| `effort`          | No       | inherit     | `low`, `medium`, `high`, `max` (Opus 4.6 only)              |
| `isolation`       | No       | -           | `worktree` for git worktree isolation                        |
| `color`           | No       | -           | Display color in UI                                          |
| `initialPrompt`   | No       | -           | Auto-submitted first turn when run via `--agent`             |

## Skill Frontmatter Fields

All fields for `.claude/skills/*/SKILL.md` YAML frontmatter:

| Field                      | Required    | Notes                                                       |
| -------------------------- | ----------- | ----------------------------------------------------------- |
| `name`                     | No          | Display name; defaults to directory name. Max 64 chars      |
| `description`              | Recommended | Front-load key use case; truncated at 250 chars in listings |
| `argument-hint`            | No          | Autocomplete hint, e.g. `[issue-number]`                    |
| `disable-model-invocation` | No          | `true` = manual-only via `/name`                            |
| `user-invocable`           | No          | `false` = hidden from `/` menu, Claude-only                 |
| `allowed-tools`            | No          | Space-separated or YAML list                                |
| `model`                    | No          | Model override when skill is active                         |
| `effort`                   | No          | `low`, `medium`, `high`, `max`                              |
| `context`                  | No          | `fork` to run in subagent                                   |
| `agent`                    | No          | Subagent type when `context: fork` (default: general-purpose) |
| `hooks`                    | No          | Lifecycle hooks scoped to skill                             |
| `paths`                    | No          | Glob patterns limiting auto-activation                      |
| `shell`                    | No          | `bash` (default) or `powershell`                            |

String substitutions: `$ARGUMENTS`, `$ARGUMENTS[N]`, `$N`, `${CLAUDE_SESSION_ID}`, `${CLAUDE_SKILL_DIR}`.

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

## Sources

- https://github.com/anthropics/claude-code/blob/main/CHANGELOG.md (2026-04-12)
- https://code.claude.com/docs/en/sub-agents (2026-04-12)
- https://code.claude.com/docs/en/skills (2026-04-12)
- https://github.com/anthropics/skills (2026-04-12)
- https://github.com/anthropics/skills/commits/main/ (2026-04-12)
