# Code Review Self-Improvement

This skill must get better with every use. After completing any code review:

1. **Capture new review categories** — If a review uncovered a class of issue not covered by existing agents, add it to the Notes section of SKILL.md with the agent best suited to catch it, or propose a new agent in `.claude/agents/`.
2. **Refine agent prompts** — If an agent produced low-quality findings (too many false positives, missed obvious issues, or redundant with another agent), update the agent's prompt in `.claude/agents/<name>.md` directly.
3. **Update severity calibration** — If findings were consistently over- or under-classified, adjust the severity mapping guidance in SKILL.md.
4. **Record codebase-specific exceptions** — If legitimate patterns in this codebase are flagged as issues, add them to the review's known-exceptions reference file, grouped by category.
5. **Refine Roslyn severity mapping** (--full only) — Update Vector 3 severity mapping if Roslyn findings are consistently over/under-classified.
6. **Update security review prompt** (--full only) — Refine Vector 2 template if security reviewer produces low-signal findings.
7. **Record deduplication misses** — Improve Phase 3 dedup if same issue appears from multiple vectors.

The goal: each review should produce higher signal-to-noise findings because previous reviews refined the agents and severity mappings.
