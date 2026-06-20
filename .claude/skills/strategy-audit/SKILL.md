---
name: strategy-audit
description: >
  Strategic alignment audit for codebases. Applies Roger Martin's strategy framework directly
  to code — analyzing what the code reveals about its strategic choices, investment levels, and
  capability reinforcement. Produces a prioritized findings report and feeds actionable
  refactorings into dev-planning. Use for code review through a strategy lens, feature gap
  analysis, investment mismatch detection, or surfacing undocumented strategic choices.
  Triggers on: strategy audit, strategic audit, martinize, martinizing, roger martin,
  code as strategy, strategy review, investment mismatch, capability analysis,
  strategic alignment, where should I invest, code investment audit, feature gap analysis,
  over-engineered, dead code strategy, what's table stakes.
argument-hint: "<repo-path>"
allowed-tools: Read, Glob, Grep, Bash, Agent
---

# Martinizing

Reads the **code as strategy**: what do investment levels, architecture, and capability chains
reveal about the project's actual strategic choices? Roger Martin's "not stupid on its face" test
is the sharpest tool here. Unlike `apply all skills` (which checks code against technical
recommendations), this audit derives strategic intent from the code itself.

Finding category definitions, Phase 3 agent prompt template, and Phase 4 report format:
[references/scoring-rubric.md](references/scoring-rubric.md).

## Usage

```
/strategy-audit                    # Audit current working directory
/strategy-audit ./src              # Audit specific path
/strategy-audit /path/to/repo      # Audit any repo
/strategy-audit --resume           # Resume from last checkpoint in .claude/tmp/strategy-audit/state.md
```

One optional argument: the code path to audit. Defaults to current working directory.

**This skill does NOT read or modify documentation files.** It audits code only.

## Prerequisites

- **dev-planning** skill — structures the output into actionable implementation plans
- Strategy framework reference bundled at [references/five-elements-of-strategy.md](references/five-elements-of-strategy.md)

## Workflow

### Step R: Resume Check

Before doing anything else, check for an in-progress run:

Read `.claude/tmp/strategy-audit/state.md`. If the file exists, determine which phases are `DONE`, skip them, and load
referenced intermediate files:
- If Phase 2 is `DONE`, load strategic profile from `.claude/tmp/strategy-audit/strategic-profile.md`
- If Phase 3 is `DONE`, load merged findings from `.claude/tmp/strategy-audit/findings.md`

If `state.md` does not exist, start fresh: `mkdir -p .claude/tmp/strategy-audit`

Set the report output path:
```bash
REPORT_PATH="plans/strategy-audit-$(scripts/ts.sh | cut -dT -f1).md"
```

**Append after each phase completes:**
- Phase 1: `- Phase 1 (Discover): DONE — {N files, key language/framework}`
- Phase 2: `- Phase 2 (Strategy): DONE — {N differentiators -> strategic-profile.md}`; write strategic profile to `.claude/tmp/strategy-audit/strategic-profile.md`
- Phase 2a: `- Phase 2a (Classify): DONE — {N strategic, M imperative}`
- Phase 3: `- Phase 3 (Audit): DONE — {N findings across N agents -> findings.md}`; write merged agent findings to `.claude/tmp/strategy-audit/findings.md`
- Phase 4: `- Phase 4 (Synthesize): DONE — report written to $REPORT_PATH`

**Cleanup:** Delete `.claude/tmp/strategy-audit/` when Phase 5 completes.

---

### Phase 1: Discover the Codebase

Run these bash commands in parallel — no dependencies between them, no agent needed:

```bash
scripts/codebase-stats.sh --json
scripts/solution-inventory.sh --json
```

Synthesize a concise summary (under 20 lines) covering: Languages, Framework(s), key `src/` dirs and their purpose, entry points, test project names, file count by language, key namespaces.

**Do NOT read documentation files** (README, lode/, etc.).

### Phase 2: Read the Code as Strategy

Launch an `Explore` agent to answer:

1. **Where is investment concentrated?** — *revealed differentiators*
2. **Where is investment minimal?** — *revealed table stakes*
3. **What does the code NOT do?** — *revealed non-goals*
4. **What user action does the code enable?** — *desired customer action*
5. **Do capabilities form a reinforcing system?** — not just chain integrity: could a competitor copy one capability without weakening their others? If yes, it's not a true moat. Flag as `ARCHITECTURE` if differentiators are modular but not mutually reinforcing.
6. **Are there dead capabilities?** — defined/registered but never called
7. **What WTP dimensions does the code reveal?** — geography, segment, channel, product scope, and especially **value system stage**: what does the project own vs. delegate to external dependencies?
8. **Outsourcing test for major dependencies** — for each significant external dependency: (a) can an outside party do this better/cheaper in a way that improves the strategy? (b) could the dependency extract disproportionate value (hold-up risk)? (c) does delegating this create a future competitor? Dependency that fails any test is a `DEPENDENCY_LEVERAGE` finding.

**Output:** Strategic profile with: Revealed differentiators, Revealed table stakes, Revealed
non-goals, Desired customer action, Capability chain, Dead capabilities, WTP dimensions,
Dependency leverage risks.

### Phase 2a: Classify Capabilities

Apply the "not stupid on its face" test to each revealed differentiator to classify as
**Strategic differentiator** vs **Operating imperative**. See
[references/scoring-rubric.md](references/scoring-rubric.md) for the classification rubric.

**After individual classification**, run the "playing both sides" check: do any pair of
differentiators signal conflicting positions (cost leadership alongside premium differentiation)?
Flag as `STRATEGIC_CONFUSION` if found. See rubric for the signal table.

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

### Phase 5: Generate Implementation Plan

Invoke the **dev-planning** skill (do not produce ad-hoc task lists). Feature name derived from
the primary finding category (e.g., `strategy-audit-investment-alignment`,
`strategy-audit-chain-repair`). Feature description = concise summary of the strategic profile and
ranked Phase 4 findings, so dev-planning has full context.

Plan phases mirror the Phase 4 report structure:
1. Investment Mismatches
2. Dead Capabilities & Broken Chains
3. Robustness of Differentiators
4. Over-Engineering & Clarity

**Present the plan to the user. Do NOT begin implementation without approval.**

## Guidelines

- **Code only** — never read documentation files; derive the strategic profile from code alone
- **Never dump full files into main context** — Phase 3 agents run concurrently and report back
- **Dead means dead** — registered-but-never-called is a DEAD_CAPABILITY, not "implemented"
- **Over-engineering table stakes** is as much a misalignment as under-investing differentiators
- **Honest classification** — a "differentiator" that fails "not stupid on its face" is an operating imperative
- **Don't fix during audit** — produce the plan; let the user decide
- **AI/ML codebases** — apply Martin's AI Strategy Cascade ([references/cascade.md](references/cascade.md)). "Use AI" fails the "not stupid on its face" test; the strategic question is where AI augmentation creates advantage a competitor can't replicate with the same off-the-shelf tools.

## Troubleshooting

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

## User input

$ARGUMENTS
