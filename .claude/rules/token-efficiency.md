# Token Efficiency

- Use the routing table in [Deterministic Scripts](deterministic-scripts.md) instead of rebuilding a command sequence.
- Reuse successful check results until their inputs change. Run affected checks after the final edit.
- Do not reread a file immediately after writing it. The triggers in [Read Before Edit](read-before-edit.md) still apply.
- Batch related edits. Avoid overlapping subagent work.
- Do not echo large file or code blocks unless requested.
- Report results and blockers. Skip tool-by-tool recaps.
