# Repository Gotchas

## File Tools

Use `rg` for content search, `fd` for file discovery, and `sd` for replacements.
Before a bulk replacement, preview it with `sd -p`.
When a path might be a directory, verify it is a file before reading it.
Apply the same check to a dynamically constructed path.

## Case-Sensitive Paths

macOS can be case-insensitive. Linux CI uses a case-sensitive filesystem.
Use exact filesystem casing in paths, namespaces, solution folders, and project references.
Use `git mv` for a rename. After a rename, search for stale references in code and documentation.

## Bash Interpreter

Run repository scripts by their own path.
`scripts/lib.sh` re-executes under Homebrew Bash when macOS Bash lacks the required features.
Do not force `/bin/bash` or a login shell that selects the system Bash first.
Shell requirements live in `lode/infrastructure/shell-authoring.md`.

## Local Data

Do not run inline Python.
Fetch URLs with a fetch tool, parse JSON with `jq`, and process local data through a repository script.

## Markdown Tables

Align every column. Pad cells to a shared width and match each separator to its column width.

## Terminal Output

Keep UTF-8 and Unicode 15.0 support. Do not downgrade output to ASCII.
Emoji and multibyte characters are valid CLI output.

## Working Files

Use the harness scratchpad for session-only files when one is available.
Use `.claude/tmp/` for repository-local probes and `lode/tmp/` for Lode scraps. Do not use `/tmp`.
In repository prose, use `~` or a repository-relative path instead of an absolute user profile path.
