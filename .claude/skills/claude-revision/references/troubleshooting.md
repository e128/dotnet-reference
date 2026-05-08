# Claude Revision — Troubleshooting

- **Web fetch fails or times out** — run with `--no-web` flag to skip Phase 1 and do a fast local-only run; web research is optional
- **Phase 2–4 grep returns sparse results** — the rg commands are the complete data source; if a pattern returns no matches, that field is absent across all files; note in the report and continue
- **First run (no revision log)** — normal; Phase 0 will note no prior context and proceed; the log will be created at Phase 6
- **Agent memory MEMORY.md files are untracked** — Phase 5 git check catches this; run `git add .claude/agent-memory/` to fix before any clone-based CI run
- **Phase 3 skill review flags reference skills for missing workflow steps** — reference skills (knowledge-injection only) are complete as-is; the guideline says do not flag them; re-check the classification
- **Revision log grows too large** — each entry is a short block; if the file exceeds 200 lines, archive older runs to `lode/infrastructure/claude-revision-log-archive.md`
- **`claude doctor` hangs or times out** — doctor spawns MCP servers for health checks, which can be slow. If it hangs beyond 60 seconds, kill it and note "doctor: timeout" in the report; continue with remaining phases. The MCP checks are nice-to-have, not blocking.
- **Description budget audit shows high total but no individual truncations** — the combined budget is dynamic (1% of context window). On smaller-context models or sessions with many tools loaded, the effective budget shrinks. Recommend trimming the longest descriptions even if individually under 1,536 chars.
- **Doctor reports MCP errors for servers you don't use** — doctor checks ALL configured MCP servers, including ones from `.mcp.json`. If a server is unused, consider removing it from config rather than suppressing the finding.
