# Read Before Edit

Read a file before you call Edit or Write on it. The one exception is a file
whose contents you already viewed through `Bash`.

**Re-read triggers (mandatory).** Each event below invalidates file contents
you read earlier. Re-read the file before you edit it:

- `format.sh` or `dotnet format` ran
- `check.sh` ran and produced format fixes
- A sub-agent wrote to the file
- Context compaction occurred

When a script finishes and you are in doubt, re-read before you edit.
