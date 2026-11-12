# Action Approval

Apply approval to the action, not its filename, script wrapper, skill, or agent.
A script inherits the approval tier of every action it performs.
Routing and read-before-edit requirements still apply to approved actions.

## Denied Actions

- `git push --force` and `git push -f`
- `git reset --hard`

These actions can destroy shared history or uncommitted work. Do not invoke them through another script or session.

## Confirmation Requirements

Get approval before these actions:

- `git push`, `gh pr create`, or `gh release create`
- `git clean -fd`, `git rebase`, or other history rewrites
- Editing `.claude/settings.json`
- An analyzer suppression, including `#pragma`, `[SuppressMessage]`, or a severity downgrade
- Deleting a file outside `lode/`, deleting a significant code block, or changing a public API signature

An explicit `/yeet` invocation authorizes one push and one PR creation for the current branch.
It does not authorize force pushes or other restricted actions.
Preserve already pushed commits. Approval to ship does not authorize rewriting shared history.

## Preapproved Actions

These actions need no additional confirmation:

- Read-only file, search, and shell calls that follow the routing table
- Formatting fixes, unused `using` removal, namespace declarations, and `using` sorting
- Adding a missing test category trait
- Read-only subagent work
- Scratch writes in the harness scratchpad or `.claude/tmp/`
- Lode writes and timestamp updates after verification

Delete a Lode file only when it is committed and has no uncommitted changes.
Inside a skill or agent, do not request approval for preapproved calls.
A script that pushes, creates a PR, deletes protected content, or rewrites history still requires the relevant approval.

## Harness Enforcement

Claude Code permissions live in `.claude/settings.json`. opencode permissions and policies live in `opencode.json`.
Keep their action tiers in agreement when changing enforcement.
Claude Code evaluates deny, ask, then allow. opencode evaluates permissions in order, with the last match winning.
opencode policies can tighten the permission decision.

The raw-command hooks enforce routing, not every action inside a script.
Script guards must enforce their own branch and failure boundaries.
See [opencode Guardrails](../../lode/infrastructure/opencode-guardrails.md) for the mapping and configuration contract.
