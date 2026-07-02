---
name: martinizing
description: >
  Roger Martin strategic alignment audit for code investment decisions.
when_to_use: >
  martinize, martinizing, strategic audit, roger martin, code as strategy,
  strategy review, investment mismatch, capability analysis, strategic alignment,
  where should I invest, feature gap analysis, what's table stakes.
argument-hint: "<repo-path>"
allowed-tools: Read, Glob, Grep, Bash, Agent
---

# Martinizing

Reads the **code as strategy** — interprets investment levels, architecture, and capability chains
as a set of revealed strategic bets, surfacing choices, investment mismatches, and capability gaps
through Roger Martin's lens. Unlike a technical-recommendations audit, it never checks code against
per-skill rules.

For the full finding category definitions, Phase 3 agent prompt template, and Phase 4 report
format, see [references/scoring-rubric.md](references/scoring-rubric.md).

## Usage

```
/martinizing                    # Audit current working directory
/martinizing ./src              # Audit specific path
/martinizing /path/to/repo      # Audit any repo
/martinizing --resume           # Resume from last checkpoint in .claude/tmp/martinizing/state.md
```

One optional argument: the code path to audit. Defaults to current working directory.

**This skill audits code only — it does not read or modify documentation files.**

## Prerequisites

- **dev-planning** skill — structures the output into actionable implementation plans
- Strategy framework reference bundled at [references/five-elements-of-strategy.md](references/five-elements-of-strategy.md)
- **lode/strategy/** — strategic profiles are written here after Phase 4.5. Directory is created on first run. Files follow the naming pattern `{YYYY-MM-DD}-{repo-slug}.md`. These are permanent project memory — not deleted with `.claude/tmp/martinizing/`.

## Workflow

### Step R: Resume Check

Before doing anything else, check for an in-progress run:

```bash
[ -f .claude/tmp/martinizing/state.md ] && cat .claude/tmp/martinizing/state.md
```

If `state.md` exists, read it to determine which phases are `DONE`, skip them, and load
referenced intermediate files:
- If Phase 2 is `DONE`, load strategic profile from `.claude/tmp/martinizing/strategic-profile.md`
- If Phase 3 is `DONE`, load merged findings from `.claude/tmp/martinizing/findings.md`

If `state.md` does not exist, start fresh: `mkdir -p .claude/tmp/martinizing`

Set the report output path:
```bash
REPORT_PATH="plans/martinizing-$(scripts/ts.sh | cut -dT -f1).md"
```

**Append after each phase completes:**
- Phase 1: `- Phase 1 (Discover): DONE — {N files, key language/framework}`
- Phase 2: `- Phase 2 (Strategy): DONE — {N differentiators → strategic-profile.md}`; write strategic profile to `.claude/tmp/martinizing/strategic-profile.md`
- Phase 2a: `- Phase 2a (Classify): DONE — {N strategic, M imperative}`
- Phase 3: `- Phase 3 (Audit): DONE — {N findings across N agents → findings.md}`; write merged agent findings to `.claude/tmp/martinizing/findings.md`
- Phase 4: `- Phase 4 (Synthesize): DONE — report written to $REPORT_PATH`
- Phase 4.5: `- Phase 4.5 (Lode): DONE — strategic profile written to lode/strategy/{date}-{repo-slug}.md`

**Cleanup:** Delete `.claude/tmp/martinizing/` when Phase 5 completes. Lode output at `lode/strategy/` is **not** deleted — it is permanent project memory.

---

### Phase 1: Discover the Codebase

Run bash directly — file counts by language (`fd -e cs src/ | wc -l`), key modules (`ls src/`), project structure (`fd -e csproj src/ --max-depth 2`).

Synthesize under 20 lines: Languages, Frameworks, key `src/` dirs, entry points, test projects, namespaces. **Don't read documentation files.**

### Phase 2: Read the Code as Strategy (Choice Cascade)

**Step 2-pre: Dead capability scan (Roslyn MCP)**

Before launching the Explore agent, run `mcp__cwm-roslyn-navigator__find_dead_code` scoped to `<repo-path>/src/` to get a list of public types/members with zero call sites. Record as `DEAD_CAPABILITY` candidates. Pass the symbol list into the Explore agent as Q4 context. If the Roslyn navigator MCP is unavailable, fall back to `scripts/find.sh --callers` spot-checks on suspect public types.

> Scoping: point at the audited `src/` path, not the solution root — test helpers produce false positives.

Launch an `Explore` agent to answer the **Strategy Choice Cascade** — five questions, each constraining the next. Full framework detail (per-question tests, code-evidence mapping, AI Strategy Cascade): [references/cascade.md](references/cascade.md). Brief the agent on all five; the code-evidence-to-question mapping is in the rubric's § Audit Framework table.

- **Q1. Winning Aspiration** — what does the code enable users to *achieve* (desired customer action)?
- **Q2. Where to Play** — what arena does the code choose, and what does it explicitly NOT do? (angst test → flag `STRATEGIC_CONFUSION` if nothing de-prioritized)
- **Q3. How to Win** — where is investment concentrated (differentiators) vs. minimal (table stakes)? Apply the can't/won't test.
- **Q4. Capabilities** — do they form a reinforcing system? Cross-ref the 2-pre dead-capability scan; identify the keystone; run the outsourcing test (→ `DEPENDENCY_LEVERAGE`) and WWHTBT.
- **Q5. Management Systems** — does CI/test/build infrastructure support or contradict the HTW choice (→ `BARNACLE`)? Is there portfolio governance?

**Output:** Strategic profile structured as the 5 Cascade answers, plus: dead capabilities list, WTP dimensions, dependency leverage risks, WWHTBT conditions for the primary WTP/HTW, keystone capability identification.

**Cascade coherence check** (after populating all 5): verify each level coheres with the one above. Flag `STRATEGIC_CONFUSION` when adjacent levels contradict. See [references/scoring-rubric.md](references/scoring-rubric.md) § Cascading Coherence Test.

**Plan vs. Strategy check**: Does the cascade describe a coherent bet on customer action, or is it a list of funded initiatives with no unifying competitive logic? If the latter, note in the strategic profile that the codebase reveals a *plan*, not a *strategy*, per Martin's "A Plan Is Not a Strategy" distinction.

### Phase 2a: Classify Capabilities

Apply the "not stupid on its face" test to each revealed differentiator to classify as
**Strategic differentiator** vs **Operating imperative**. See
[references/scoring-rubric.md](references/scoring-rubric.md) for the classification rubric.

**After individual classification**, run the "playing both sides" check: do any pair of
differentiators signal conflicting competitive positions (cost leadership signals alongside
premium differentiation signals)? Flag as `STRATEGIC_CONFUSION` if found — pursuing both
simultaneously produces mediocrity on both dimensions. See rubric for the signal table.

### Phase 3: Strategic Code Audit

Launch **parallel `Explore` agents** (max 6, one per architectural concern):

- **Agent 1:** Core differentiator #1 — primary value-generating pipeline
- **Agent 2:** Core differentiator #2 — second major capability area
- **Agent 3:** Core differentiator #3 (or combine with #2)
- **Agent 4:** Public surface (CLI, API, UI) + dependency wiring
- **Agent 5:** Storage/persistence layer + investment level check
- **Agent 6:** External integrations + third-party service handling + error boundaries

For the full agent prompt template, see [references/scoring-rubric.md](references/scoring-rubric.md).

### Phase 4: Synthesize Findings

Combine all agent results into a single prioritized report organized by:
1. Investment Mismatches — Fix First
2. Dead Capabilities & Broken Chains — Fix Next
3. Robustness of Differentiators — Strengthen
4. Over-Engineering & Clarity — Simplify

Present the strategic profile first so the user sees how the code was interpreted.
Full report format in [references/scoring-rubric.md](references/scoring-rubric.md).

Write the full report to `$REPORT_PATH`.

### Phase 4.5: Persist Strategic Profile to Lode

Write the strategic profile to `lode/strategy/` using the template in [references/lode-template.md](references/lode-template.md). Runs after Phase 4 report, before Phase 5.

### Phase 5: Generate Implementation Plan

Invoke the **dev-planning** skill with a feature name derived from the primary finding category
(e.g., `martinizing-investment-alignment` or `martinizing-chain-repair`). Provide as the
feature description a concise summary of the strategic profile and ranked findings from Phase 4
so dev-planning can research the codebase with full context.

The plan phases should mirror the Phase 4 report structure:
1. Investment Mismatches
2. Dead Capabilities & Broken Chains
3. Robustness of Differentiators
4. Over-Engineering & Clarity

**Present the plan to the user. Begin implementation only with approval.**

## Guidelines

Skill-specific workflow rules (the Martin strategy theory behind these — angst, WWHTBT, plan-vs-strategy, AI cascade, customer orientation — lives in [references/cascade.md](references/cascade.md)):

- **Code only** — don't read documentation files; derive the strategic profile from code alone
- **Parallel everything** — Phase 3 agents run concurrently; never dump full files into main context
- **Dead means dead** — a registered-but-never-called capability is a `DEAD_CAPABILITY`, not "implemented"
- **Don't fix during audit** — produce the plan; let the user decide what to implement
- **Use dev-planning skill** — Phase 5 invokes the dev-planning skill; do not produce ad-hoc task lists

## Troubleshooting

See [references/troubleshooting.md](references/troubleshooting.md).
