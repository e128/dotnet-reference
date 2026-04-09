# Step 5: Performance Review

Launch an `Explore` agent (haiku) with the patterns from `step5-patterns.md`:

```
Read ${CLAUDE_SKILL_DIR}/steps/step5-patterns.md for grep patterns and checklist.
Read ${CLAUDE_SKILL_DIR}/conventions.md and ${CLAUDE_SKILL_DIR}/lessons/*.md for project-specific false positives and async conventions.

Analyze [scope] for performance issues:
1. Run the anti-pattern grep patterns against all .cs files in scope
2. For each match, read surrounding context to confirm it's a real issue (not a false positive)
3. Walk through the analysis checklist
4. Classify each finding by severity

Report findings with severity ratings and file:line references in this format:

| ID | Finding | Severity | Location | Impact | Recommendation |
|----|---------|----------|----------|--------|----------------|

Do NOT apply fixes — report only.
```

Map findings to the overhauler table with `P` prefix.
**Before presenting to user**, write findings table to `.claude/tmp/overhauler/findings-step5.md`.
Present. Wait for approval. Then Fix Cycle.
