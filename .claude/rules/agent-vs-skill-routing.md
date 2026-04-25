# Agent vs Skill Routing

## The Core Distinction

- **Skills** (names starting with `/`) → invoke via the `Skill` tool
- **Agents** (names ending in `-agent`) → invoke via the `Agent` tool with `subagent_type`

**Never use the `Skill` tool to invoke an agent.**

## Agent & Skill Frontmatter Conventions

**No `model:` field in agents or skills.** All agents and skills inherit the session model. Explicitly setting a model pins it to a specific version and bypasses model selection in the UI — this creates upgrade friction and split behavior. Omit the field entirely; inheritance is correct.

**No `effort:` field unless the task genuinely requires non-default effort.** Most agents should inherit.

## Write Code Before Running Tests

- **Write all implementation code first**, then verify
- Do NOT burn turns running tests against stubs or before code exists
- Trust `scripts/task.sh` and `scripts/test.sh` to handle verification
