# Step 6: Concurrency Review

Launch an `Explore` agent (haiku) with the patterns from `step6-patterns.md`:

```
Read .claude/skills/dotnet-code-overhauler/steps/step6-patterns.md for grep patterns and checklist.
Read lode/coding-standards-async.md (if it exists) for project async/threading conventions.

Review [scope] for thread safety issues:
1. Run the anti-pattern grep patterns against all .cs files in scope
2. For each match, read surrounding context — trace the full access path before calling
   something a race. Confirm multiple threads actually reach it.
3. Check DI registration lifetimes for types in scope (Singleton = must be thread-safe,
   Scoped = safe unless parallelized, Transient = safe)
4. Walk through the analysis checklist
5. Classify each finding by severity

Report findings with severity ratings and file:line references in this format:

| ID | Finding | Severity | Location | Race Window / Evidence | Recommendation |
|----|---------|----------|----------|----------------------|----------------|

Do NOT apply fixes — report only.
```

Map findings to the overhauler table with `T` prefix.
**Before presenting to user**, write findings table to `.claude/tmp/overhauler/findings-step6.md`.
Present. Wait for approval. Then Fix Cycle.
