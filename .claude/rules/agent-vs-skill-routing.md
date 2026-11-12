# Agent and Skill Routing

## Invocation

Invoke a skill through the skill tool. Invoke an agent through the subagent tool with its declared name.
Never invoke an agent through the skill tool.
A repository-specific agent takes precedence over a generic plugin agent.

Use the `smart-commit` agent for commit requests and `/yeet` for shipping requests.
The agent calls `scripts/internal/commit.sh`. The routing table describes the underlying script contracts, not competing workflows.
Use `/lode-capture` for one insight. The `lode-sync` agent handles batch synchronization.

## Definitions

`.claude/agents/` is the agent source of truth.
`scripts/internal/opencode-agents.sh sync` generates `.opencode/agents/` and `.opencode/agent-assets/`.
Never hand-edit either generated tree.

Agent and skill definitions inherit the session model. Do not add a `model:` pin.
Set an `effort:` field only when explicitly requested.
opencode agent frontmatter supports ordered `permissions` entries with `action`, `resource`, and `effect` fields.
Claude Code agent frontmatter does not use that permission field.

## Related

- [Deterministic Scripts](deterministic-scripts.md): backend script routing.
- [Action Approval](auto-approvals.md): approval within scripts, skills, and agents.
- [Read Before Edit](read-before-edit.md): mandatory invalidation triggers.
- [Harness Maintenance](../../lode/infrastructure/claude-code-maintenance.md): discovery and generated mirrors.

Test authoring and execution order live in `AGENTS.md` Workflow, not this router.
