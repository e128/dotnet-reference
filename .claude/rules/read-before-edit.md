# Read Before Edit

Always Read a file before using Edit or Write on it — **unless** you have already viewed its contents via `Bash`.

After `dotnet format`, linter hooks, or context compaction, re-Read files before editing — contents may have changed.

**Re-read triggers (mandatory):** Any of these events invalidates previously-read file contents:
- `format.sh` or `dotnet format` ran
- `check.sh` ran and produced format fixes
- Any sub-agent wrote to a file
- Context compaction occurred

When in doubt after any script completes: re-Read before Edit.
