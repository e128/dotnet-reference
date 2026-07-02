# Martinizing — Troubleshooting

**Phase 2 agent returns a generic profile that doesn't reflect actual code** — the agent read
documentation instead of code. Restart Phase 2 with explicit instruction: "Read only `.cs`, `.py`,
or source files. Do not read README, ARCHITECTURE, or any lode/ files."

**Phase 3 agents return too many LOW/CLARITY findings** — instruct agents to return only HIGH and
MEDIUM severity findings in the first pass. Run a second pass for CLARITY if needed.

**Phase 2 can't identify the desired customer action** — the pipeline may not have a clear entry
point. Ask Phase 3 Agent 4 to specifically trace: what does a user invoke first, and what is the
last thing the code produces? This chain defines the desired action.

**"Not stupid on its face" test is ambiguous for a capability** — classify it as an operating
imperative (conservative default) and note the ambiguity in the finding. The user can reconsider.
