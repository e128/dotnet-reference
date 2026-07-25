# Common Gotchas

Small repository-specific gotchas that previously lived in standalone files.
Each is a real defect class, not a stylistic preference.

## Markdown Tables

Markdown tables must use aligned columns. Pad every cell with trailing
spaces so all columns are the same width. Separator rows use dashes that
match their column width exactly.

## Glob Before Read on Uncertain Paths

If a path might be a directory, use Glob or `ls` to verify it's a file
before calling Read. The Read tool errors with `EISDIR` on directories.

Also apply to any dynamically-constructed path. Verify it exists before
reading.

## No Inline Python

Never invoke `python3` directly in Bash.

- For URL fetching: use the `WebFetch` tool
- For JSON parsing: use `jq` or read the output directly
- For local data processing: use bash scripts or dedicated `scripts/*.sh`

## Terminal Output

Ensure Unicode 15.0 and color font rendering support in all terminal output.
When writing code that emits text to the console:

- Use UTF-8 encoding (the .NET default). Never downgrade to ASCII.
- Emoji and multi-byte characters are valid in CLI output.
- Do not assume the terminal lacks color emoji support.
