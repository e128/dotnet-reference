# Code Review Error Handling & Notes

## Error Handling

- **No git**: Show error, suggest running in git repository
- **Shallow clone**: Warn user, attempt to work with available history
- **No commits in range**: Show message, exit 0
- **No .NET files changed**: Show message, exit 0
- **No agents found**: Warn user, check `.claude/agents/` directory
- **Agent timeout**: Include in report as "Agent timed out"
- **Agent error**: Include in report as "Agent failed: [error]"

## Notes

- Agents are discovered **dynamically** every run — no hardcoded list
- Report is grouped by **severity**, not by agent
- Exit codes enable CI integration (block merge on CRITICAL findings)

### Known Exceptions (codebase-specific)

See [known-exceptions.md](known-exceptions.md) for the full list of legitimate patterns that should not be flagged. Includes test conventions, threat model exceptions, sanitizer TextContent/DOM rules, and severity calibration rules.
