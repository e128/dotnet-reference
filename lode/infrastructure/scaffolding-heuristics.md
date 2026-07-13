# Scaffolding Heuristics
*Updated: 2026-07-13T17:50:43Z*

Reference definitions for the simplification-agent's 6 heuristics. The agent
prompt references this file rather than inlining the full definitions.

## Density Formula

```
density = (flagged_lines / meaningful_lines) x 100%
```

**Meaningful lines** = total minus blank lines, YAML frontmatter, code fence
delimiters, and pure markdown headers. A line matches at most one heuristic
(use highest-severity match).

## H1 -- PROCEDURAL_ENUM (HIGH)

Numbered micro-steps prescribing *how* rather than *what* -- sequences of 3+
atomic operations the model could infer, with no decision point or data
dependency between them.

**Not H1:** Output format specs, TDD RED/GREEN/Verify, steps with genuine
dependencies where step N+1 requires step N's result.

## H2 -- RETRIEVAL_ORDER (MEDIUM)

Explicit sequencing of file reads with no data dependency between them.

**Not H2:** "Load baseline before computing delta" (clear dependency),
orderings that affect output or decision logic.

## H3 -- INTERMEDIATE_VERIFY (HIGH)

A "present and wait" gate where the only valid user response is "yes, continue."
**Key test:** Could any plausible user response change the next step?

**Not H3:** Gates collecting real decisions (approve/reject, choose A/B),
gates before destructive operations (push, delete, PR creation).

## H4 -- AGGRESSIVE_LANGUAGE (LOW)

CRITICAL/MUST/NEVER/ALWAYS without condition-based rationale. Over-specifies
because the model once needed strong emphasis to comply.

**Not H4:** Emphasis with rationale ("NEVER edit -- irreversible action"),
genuine safety gates (force-push, schema migration, credentials).

## H5 -- STATE_NARRATION (MEDIUM)

Instructions to track or narrate intermediate state when only the final output
matters ("keep a running log", "track which files matched").

**Not H5:** Checkpointing to `.claude/tmp/`, audit trails, durable output.

## H6 -- EXPLICIT_CATCH (LOW)

Error-handling for operations that succeed deterministically ("if grep returns
no results, try rg instead").

**Not H6:** Network call handling, graceful handling of optional files that
legitimately may not exist.

## False Positive Discipline

When uncertain, do not flag. Require clear evidence:
- H1: >= 3 sequential micro-steps with no decision point
- H3: certainty that no user response changes the next step
- H4/H6: only clearly redundant instances
