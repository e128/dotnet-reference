# Auto-Approvals

This file states the approval policy. Each harness enforces the same policy in
its own config. Keep both configs in agreement when this policy changes:

- Claude Code: `permissions.allow`, `permissions.ask`, and `permissions.deny`
  in `.claude/settings.json`
- opencode: the `permissions` array and `experimental.policies` in `opencode.json`

Every action falls into one of three tiers.

| Tier  | Meaning                 | Claude Code   | opencode                       |
| ----- | ----------------------- | ------------- | ------------------------------ |
| auto  | Runs without a prompt   | `allow`       | base policy of allow           |
| ask   | Prompts first           | `ask`         | `effect` of `ask`              |
| deny  | Always refused          | `deny`        | `experimental.policies`        |

## Precedence

Claude Code evaluates `deny`, then `ask`, then `allow`. Rule specificity never
changes that order, so a broad deny blocks a narrower allow.

opencode evaluates the `permissions` array in order, and the last matching rule
wins. A statement in `experimental.policies` outranks the array and can only
tighten a decision. Use a policy for the deny tier.

The base posture still differs between the harnesses. Claude Code denies an
unlisted Bash command by default. opencode allows every action by default. That
difference is a harness mechanic, not a policy choice. The tiers below are the
part both harnesses share.

## Deny Tier

These actions destroy shared history or uncommitted work.

- `git push --force`
- `git push -f`
- `git reset --hard`

## Ask Tier

These actions reach outside the working copy or rewrite local history.

- `git push`
- `gh pr create`
- `gh release create`
- `git clean -fd`
- `git rebase`
- Editing `.claude/settings.json`

`git push --force` also matches the `git push` ask rule. The deny rule wins on
both harnesses, so a force push never reaches a prompt.

## Auto Tier

Apply these changes silently. They never require user confirmation:

- Removing an unused `using` statement (IDE0005)
- `dotnet format` whitespace and formatting fixes
- Lode timestamp updates
- Adding a missing file-scoped namespace declaration
- Sorting `using` directives
- Adding `[Trait("Category", "CI")]` to a test method that lacks it
- Running a read-only shell command: `git diff`, `git log`, `git status`,
  `ls`, `wc`
- Running any `scripts/*.sh` or `scripts/internal/*.sh`
- Spawning a read-only agent
- Writing to `.claude/tmp/`
- Creating, updating, or deleting a file inside `lode/`. Deletion requires the
  file to be git-committed with no uncommitted changes.

## Withheld Approval

These actions require explicit approval and belong to neither tier above:

- Any analyzer suppression (`#pragma`, `[SuppressMessage]`)
- Deleting a file outside `lode/`, or deleting a significant code block
- Changing a public API signature

## Guardrails

`.claude/hooks/block-raw-git` and `.claude/hooks/block-raw-commands` enforce the
deterministic script routing at execution time. The plugin
`.opencode/plugins/e128-guardrails/index.ts` runs the same scripts on
opencode, so both harnesses share one decision. The CI job named `harness`
fails when the plugin names a hook script that does not exist. See
[lode/infrastructure/opencode-guardrails.md](../../../lode/infrastructure/opencode-guardrails.md).