# Token Efficiency

- Route every repeated command through a script. See
  [deterministic-scripts.md](deterministic-scripts.md).
- Never re-read a file you just wrote or edited. You know the contents. The
  re-read triggers in [read-before-edit.md](read-before-edit.md) override this
  rule and are mandatory.
- Never re-run a command to verify it, unless the outcome was uncertain.
- Never echo a large block of code or file content back, unless the user asks.
- Batch related edits into one operation.
- Use one tool call when one is enough.
- Never summarize what you just did, unless the result is ambiguous.
