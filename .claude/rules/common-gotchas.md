# Common Gotchas

Small repository-specific gotchas. Each one is a real defect class, not a
style preference.

## Markdown Tables

Align every markdown table column. Pad each cell with trailing spaces so all
columns share one width. Match each separator row's dashes to its column width.

## Glob Before Read on an Uncertain Path

When a path might be a directory, verify it is a file first. Use Glob or `ls`.
The Read tool fails with `EISDIR` on a directory.

Apply the same check to any dynamically-constructed path.

## No Inline Python

Never invoke `python3` from Bash.

- To fetch a URL, use the fetch tool.
- To parse JSON, use `jq` or read the output directly.
- To process local data, use a bash script or a `scripts/*.sh` entry.

## Terminal Output

Emit UTF-8 with Unicode 15.0 and color font support. The .NET default is
UTF-8. Never downgrade to ASCII. Emoji and multi-byte characters are valid in
CLI output. Do not assume the terminal lacks color emoji support.

## Working Files

Write scratch files to `.claude/tmp/`. Never write to `/tmp`. Lode scraps go
in `lode/tmp/`.

Never write an absolute user profile path. Use `~` or a repo-relative path.
