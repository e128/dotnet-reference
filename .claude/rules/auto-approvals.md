# Auto-Approvals

The following changes never require user confirmation — apply them silently:

- Removing unused `using` statements (IDE0005)
- `dotnet format` whitespace and formatting fixes
- Lode timestamp updates
- Adding missing file-scoped namespace declarations
- Sorting `using` directives
- Adding `[Trait("Category", "CI")]` to test methods missing it
- Running read-only shell commands: `git diff`, `git log`, `git status`, `ls`, `wc`
- All `scripts/*.sh` and `scripts/internal/*.sh` invocations
- Spawning read-only agents
- Writing to `.claude/tmp/`

The following still require explicit approval:

- Any analyzer suppression (`#pragma`, `[SuppressMessage]`)
- Deleting files or significant code blocks
- Changing public API signatures
- Any git push, PR creation, or external-facing action
- Modifying `.claude/settings.json`
