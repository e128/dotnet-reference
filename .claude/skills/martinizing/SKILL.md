---
name: martinizing
description: >
  Strategic alignment audit for codebases. Applies Roger Martin's strategy framework directly
  to code — analyzing what the code reveals about its strategic choices, investment levels, and
  capability reinforcement. Produces a prioritized findings report and feeds actionable
  refactorings into dev-planning. Use for code review through a strategy lens, feature gap
  analysis, investment mismatch detection, or surfacing undocumented strategic choices.
  Triggers on: martinize, martinizing, strategic audit, roger martin, code as strategy,
  strategy review, investment mismatch, capability analysis, strategic alignment,
  where should I invest, code investment audit, feature gap analysis, over-engineered,
  dead code strategy, what's table stakes.
argument-hint: "<repo-path>"
allowed-tools: Read, Glob, Grep, Bash, Agent
---

# Martinizing

Audits a codebase through Roger Martin's strategy lens — analyzing the code itself to surface
strategic choices, investment mismatches, and capability gaps. The name is a play on "Martin" —
as in Roger L. Martin, whose "not stupid on its face" test is the sharpest tool in this workflow.

For the full finding category definitions, Phase 3 agent prompt template, and Phase 4 report
format, see [references/scoring-rubric.md](references/scoring-rubric.md).

**What this skill does differently from `apply all skills`:**
- `apply all skills` checks code against each skill's technical recommendations
- `/martinizing` reads the **code as strategy** — what do investment levels, architecture, and capability chains reveal about the project's actual strategic choices?

## Usage

```
/martinizing                    # Audit current working directory
/martinizing ./src              # Audit specific path
/martinizing /path/to/repo      # Audit any repo
/martinizing --resume           # Resume from last checkpoint in .claude/tmp/martinizing/state.md
```

One optional argument: the code path to audit. Defaults to current working directory.

**This skill does NOT read or modify documentation files.** It audits code only.

## Prerequisites

- **dev-planning** skill — structures the output into actionable implementation plans
- Strategy framework reference bundled at [references/five-elements-of-strategy.md](references/five-elements-of-strategy.md)

## Workflow

### Step R: Resume Check

Before doing anything else, check for an in-progress run:

```bash
cat .claude/tmp/martinizing/state.md 2>/dev/null
```

If `state.md` exists, read it to determine which phases are `DONE`, skip them, and load
referenced intermediate files:
- If Phase 2 is `DONE`, load strategic profile from `.claude/tmp/martinizing/strategic-profile.md`
- If Phase 3 is `DONE`, load merged findings from `.claude/tmp/martinizing/findings.md`

If `state.md` does not exist, start fresh: `mkdir -p .claude/tmp/martinizing`

Set the report output path:
```bash
REPORT_PATH="plans/martinizing-$(date -u +%Y-%m-%d).md"
```

**Append after each phase completes:**
- Phase 1: `- Phase 1 (Discover): DONE — {N files, key language/framework}`
- Phase 2: `- Phase 2 (Strategy): DONE — {N differentiators -> strategic-profile.md}`; write strategic profile to `.claude/tmp/martinizing/strategic-profile.md`
- Phase 2a: `- Phase 2a (Classify): DONE — {N strategic, M imperative}`
- Phase 3: `- Phase 3 (Audit): DONE — {N findings across N agents -> findings.md}`; write merged agent findings to `.claude/tmp/martinizing/findings.md`
- Phase 4: `- Phase 4 (Synthesize): DONE — report written to $REPORT_PATH`

**Cleanup:** Delete `.claude/tmp/martinizing/` when Phase 5 completes.

---

### Phase 1: Discover the Codebase

Run these bash commands directly — no agent needed for discovery:

```bash
# File counts by language
fd -e cs src/ | wc -l          # C# source files
fd -e cs tests/ | wc -l        # C# test files
fd -e sh scripts/ | wc -l      # Shell scripts
# Key modules (top-level src dirs)
ls <repo-path>/src/
# Entry points and project structure
fd -e csproj src/ --max-depth 2
```

If `fd` is unavailable, fall back to: `find src/ -name "*.cs" | wc -l`

Synthesize a concise summary (under 20 lines) covering: Languages, Framework(s), key `src/` dirs and their purpose, entry points, test project names, file count by language, key namespaces.

**Do NOT read documentation files** (README, lode/, etc.).

### Phase 2: Read the Code as Strategy

Launch an `Explore` agent (`model: sonnet`) to answer:

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
differentiators signal conflicting competitive positions (cost leadership signals alongside
premium differentiation signals)? Flag as `STRATEGIC_CONFUSION` if found — pursuing both
simultaneously produces mediocrity on both dimensions. See rubric for the signal table.

### Phase 3: Strategic Code Audit

Launch **parallel `Explore` agents** (`model: sonnet`, max 6, one per architectural concern):

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

Invoke the **dev-planning** skill with a feature name derived from the primary finding category
(e.g., `martinizing-investment-alignment` or `martinizing-chain-repair`). Provide as the
feature description a concise summary of the strategic profile and ranked findings from Phase 4
so dev-planning can research the codebase with full context.

The plan phases should mirror the Phase 4 report structure:
1. Investment Mismatches
2. Dead Capabilities & Broken Chains
3. Robustness of Differentiators
4. Over-Engineering & Clarity

**Present the plan to the user. Do NOT begin implementation without approval.**

## Guidelines

- **Code only** — do NOT read documentation files; derive the strategic profile from code alone
- **Parallel everything** — Phase 3 agents run concurrently; never dump full files into main context
- **Dead means dead** — a registered-but-never-called capability is a DEAD_CAPABILITY, not "implemented"
- **Investment level matters** — over-engineering table stakes is as much a misalignment as under-investing differentiators
- **Honest classification** — if a "differentiator" fails the "not stupid on its face" test, reclassify it as an operating imperative
- **Don't fix during audit** — produce the plan; let the user decide what to implement
- **Use dev-planning skill** — Phase 5 invokes `/dev-planning`; do not produce ad-hoc task lists
- **AI Strategy Cascade** — when auditing codebases with AI/ML components, apply Martin's AI-specific cascade (see [references/cascade.md](references/cascade.md) section on AI Strategy Cascade). "Use AI" fails the "not stupid on its face" test — every competitor will. The strategic question is where AI augmentation creates advantage a competitor cannot easily replicate with the same off-the-shelf tools.

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
