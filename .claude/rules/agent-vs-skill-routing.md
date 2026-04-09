# Agent vs Skill Routing

## The Core Distinction

- **Skills** (names starting with `/`) → invoke via the `Skill` tool
- **Agents** (names ending in `-agent`) → invoke via the `Agent` tool with `subagent_type`

**Never use the `Skill` tool to invoke an agent.**

## Write Code Before Running Tests

- **Write all implementation code first**, then verify
- Do NOT burn turns running tests against stubs or before code exists
- Trust `scripts/task.sh` and `scripts/test.sh` to handle verification
