# Auto-Approvals

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

These actions still require explicit approval:

- Any analyzer suppression (`#pragma`, `[SuppressMessage]`)
- Deleting a file outside `lode/`, or deleting a significant code block
- Changing a public API signature
- Any git push, PR creation, or other external-facing action
- Modifying `.claude/settings.json`
