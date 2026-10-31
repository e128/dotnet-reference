# Code Review Error Handling

## Failure Paths

| Condition                  | Behavior                                                     | Exit code |
| -------------------------- | ------------------------------------------------------------ | --------- |
| Not a git repository       | State the condition and stop.                                | 0         |
| Shallow clone              | Warn the user, then review the available history.            | unchanged |
| No commits in the range    | State the condition and stop.                                | 0         |
| Empty diff or no matching file | State the condition and stop.                            | 0         |
| No reviewers available     | Warn the user. A vendored reviewer is always present, so this means a setup defect. | 1 |
| Reviewer timeout           | Record "Agent timed out" in Reviewer Notes.                  | unchanged |
| Reviewer error             | Record "Agent failed: <error>" in Reviewer Notes.            | unchanged |
| MCP server unavailable     | Record "Roslyn navigator: MCP server not running, skipped".  | unchanged |

## Rules

- Reviewers run from a fixed roster. Never discover reviewers from `.claude/agents/`.
- Group the report by severity, not by reviewer.
- Exit codes let CI block a merge on a Critical finding.

## Known Exceptions

See [known-exceptions.md](../../review-apply/references/known-exceptions.md) for the legitimate patterns that a reviewer must not flag.
