# Agent vs Skill Routing

These rules apply on any harness with skill and subagent tools.

## The Core Distinction

- A **skill** has a name that starts with `/`. Invoke it through the skill
  tool.
- An **agent** has a name that ends in `-agent`. Invoke it as a subagent and
  set `subagent_type`.

**Never invoke an agent through the skill tool.**

## Frontmatter Conventions

Agent definitions live in `.claude/agents/*.md` and, for opencode, in
`.opencode/agent/*.md`. Both use YAML frontmatter:

**Never set `model:` in an agent or a skill.** All inherit the session model.
An explicit model pins a version and bypasses model selection in the UI. That
creates upgrade friction and split behavior. Omit the field.

**Never set `effort:` unless the task genuinely requires non-default effort.**
Most agents inherit.

## Canonical Paths

A repo-specific agent supersedes a generic plugin.

| Action      | Use                   | Not                                       |
| ----------- | --------------------- | ----------------------------------------- |
| Commit      | `smart-commit` agent  | `devex:commit`, `commit-commands:commit`  |
| Ship (push) | `/yeet` skill         | raw `git push`                            |
| Lode write  | `/lode-capture` skill | `lode-sync` agent, which batch-syncs only |

## Write Code Before You Run Tests

This rule governs when you **run** tests, not when you **write** them. It does
not contradict AGENTS.md § Workflow (TDD). TDD still authors the failing test
first, with
an `Assert.Fail` stub. This rule governs the implementation loop only.

- Write all the implementation code first. Then verify.
- Never burn a turn running tests against a stub.
- Trust `scripts/task.sh` and `scripts/test.sh` to verify.
