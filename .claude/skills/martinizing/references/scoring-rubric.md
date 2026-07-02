# Martinizing — Scoring Rubric & Finding Catalog

Detailed finding categories, the capability classification rubric, Phase 3 agent prompt template,
and the full Phase 4 report format. Loaded on demand from the main `martinizing` skill.

## Finding Categories

| Category | Definition | Priority |
|----------|-----------|----------|
| `INVESTMENT_MISMATCH` | Over-engineered table stakes, or under-invested differentiators | Highest |
| `DEAD_CAPABILITY` | Code that's defined/registered but never called from active pipelines | High |
| `CHAIN_BREAK` | Capability chain has a broken link -- output of one subsystem doesn't reach the next | High |
| `ROBUSTNESS` | Error handling, silent failures, missing exception catches in differentiators | Medium |
| `OVER_ENGINEERED` | Unnecessary complexity in areas that don't create advantage | Medium |
| `ARCHITECTURE` | God class, unclear responsibilities, tight coupling | Medium |
| `CLARITY` | Dead code, redundant calls, confusing structure | Low |
| `DEPENDENCY_LEVERAGE` | Major dependency fails the outsourcing test -- hold-up risk, future competitor risk, or mission-critical capability delegated to an outside party | High |
| `STRATEGIC_CONFUSION` | Differentiator set signals conflicting competitive positions (cost leadership and premium differentiation simultaneously) -- pursuing both produces mediocrity on both dimensions | High |
| `BARNACLE` | Accumulated infrastructure, patterns, or conventions that now contradict or create drag on the HTW choice -- may have been justified previously but now fights the current strategy | Medium |
| `KEYSTONE_GAP` | The keystone capability (the one others depend on) is the least-invested in the system -- structural vulnerability where the entire capability chain depends on an under-built foundation | Highest |
| `ASPIRATIONAL_STRATEGY` | A WTP/HTW combination exists in planning artifacts but the WWHTBT conditions are clearly unmet (near-zero implementation, missing foundational capabilities) -- the strategy is a bet that hasn't been placed yet | High |

## Phase 2a: Capability Classification Rubric

Apply the "not stupid on its face" test to each revealed differentiator:

| Capability | Opposite viable? | Classification |
|---|---|---|
| [capability] | Yes -- a competitor could reasonably skip this | **Strategic differentiator** |
| [capability] | No -- every serious competitor must do this | **Operating imperative** (quality threshold) |

This classification drives investment-level expectations in Phase 3:
- Differentiators should have deep, well-tested implementations
- Operating imperatives should be solid but not over-engineered
- Table stakes should be minimal and functional

### Playing Both Sides Check

After classifying all capabilities individually, review the aggregate profile for conflicting signals:

| Signal type | Examples in code | Competitive position implied |
|---|---|---|
| Cost leadership | Minimal dependencies, direct implementations, thin abstractions, no framework overhead | Low-cost position |
| Differentiation | Rich domain models, deep capability investment, premium abstractions, layered architecture | Value/premium position |

If the aggregate profile contains strong signals from **both** columns, flag `STRATEGIC_CONFUSION`.
The two positions require fundamentally different disciplines; simultaneous pursuit produces
mediocrity on both. Note: some projects legitimately pursue **cost-effective differentiation**
(differentiated value at disciplined cost) — this is not confusion. The flag is for cases where the
signals actively contradict each other (e.g., ultra-minimal core *and* elaborate abstraction
frameworks of equal investment).

## Phase 3: Agent Prompt Template

```
Very thorough exploration. I'm auditing [repo-path] through a strategic investment lens.

Phase 2 identified these as the project's revealed strategic profile:

Revealed differentiators (deep investment expected):
[list from Phase 2]

Revealed table stakes (minimal investment expected):
[list from Phase 2]

Revealed non-goals (should be absent):
[list from Phase 2]

Capability chain:
[from Phase 2]

Your audit area: [specific area]

For each area you examine, assess:

1. **Investment level match** — Does the depth of implementation match its strategic classification?
   - Differentiators should have deep, well-tested, sophisticated implementations
   - Operating imperatives should be solid but not over-engineered
   - Table stakes should be minimal and functional
   - If something classified as table stakes has more investment than a differentiator, flag it
2. **Dead capabilities** — Code that's defined/registered but never called from the active pipeline. The Phase 2-pre step has already run `mcp__cwm-roslyn-navigator__find_dead_code` — cross-reference its symbol list when classifying dead capability findings rather than re-discovering them by reading files.
3. **Capability chain integrity** — Does the output of one subsystem actually feed into the next? Use `mcp__cwm-roslyn-navigator__find_references` on the key output types/methods from each differentiator to verify call chains in code — don't rely on file-reading to infer data flow.
4. **Robustness of differentiators** — Error handling, edge cases, test coverage on areas that matter most
5. **Over-engineering of non-differentiators** — Unnecessary complexity in non-differentiating areas
6. **Reinforcing system integrity** — Could a competitor copy any single capability in isolation without weakening the others? Capabilities that stand alone are individually vulnerable; capabilities that make adjacent ones stronger form a structural moat. If differentiators are modular and independent (could be individually extracted without loss), flag as `ARCHITECTURE`. Also check: do major external dependencies pass the outsourcing test? (better/cheaper outside? hold-up risk? future competitor risk?) Flag failures as `DEPENDENCY_LEVERAGE`.

Return findings as:
- Finding ID: S[N]
- Category: INVESTMENT_MISMATCH | DEAD_CAPABILITY | CHAIN_BREAK | ROBUSTNESS | OVER_ENGINEERED | ARCHITECTURE | CLARITY | STRATEGIC_CONFUSION | DEPENDENCY_LEVERAGE | BARNACLE | KEYSTONE_GAP | ASPIRATIONAL_STRATEGY
- Severity: HIGH | MEDIUM | LOW
- Description: [what's wrong]
- File: [path:line]
- Evidence: [brief code snippet or method signature, max 3 lines]
- Suggested fix: [concrete action]
- Effort: small (<30 min) | medium (30 min - 2 hours) | large (2+ hours)

**Category guidance for the three additional categories:**
- `STRATEGIC_CONFUSION` — use when the aggregate capability profile signals conflicting competitive positions simultaneously (cost-leader signals AND premium-differentiation signals of equal investment). Not for projects doing cost-effective differentiation — only when signals actively contradict each other.
- `DEPENDENCY_LEVERAGE` — use when a major external dependency fails the outsourcing test: (a) could an outside party do it better/cheaper in a way that improves the strategy? (b) does the dependency hold-up risk or extract disproportionate value? (c) does delegating this create a future competitor?
- `BARNACLE` — use when accumulated infrastructure, patterns, or conventions now contradict or create drag on the current strategic choices. May have been justified in an earlier strategy; now fights the current direction. The Sears example: store-level P&L structures that penalized online-strategy behaviors.
- `KEYSTONE_GAP` — use when the capability other capabilities depend on most is the least-invested. The keystone is the structural foundation of the capability system; if it is under-built, no amount of investment in dependent capabilities will produce the HTW.
- `ASPIRATIONAL_STRATEGY` — use when a WTP/HTW combination is visible in planning artifacts, PDDs, or architectural docs, but the WWHTBT conditions for it are clearly unmet in the code. Near-zero implementation of a claimed strategic direction is not an execution gap — it is a bet that has not been placed. Name it honestly.

Do NOT return full file contents. Return only findings with file paths and line numbers.
```

## Phase 4: Full Report Format

```markdown
# Martinizing Report

**Codebase:** [repo-path]
**Audit Areas:** [N] agents covering [list]
**Total Findings:** [N] (by category breakdown)

## Strategic Profile — Choice Cascade (derived from code)

**Q1. Winning Aspiration:** [what the code enables users to achieve]
**Q2. Where to Play:** [arena — segments, channels, value chain stage, non-goals]
**Q3. How to Win:** [differentiators vs. table stakes vs. operating imperatives]
**Q4. Capabilities:** [reinforcing system — chain integrity, dead capabilities, dependencies]
**Q5. Management Systems:** [CI/test/build/metrics — support or contradict HTW? portfolio governance present or absent?]
**Keystone Capability:** [which capability do the others depend on? is it the most- or least-invested?]
**WWHTBT Conditions:** [for the primary WTP/HTW — what must be true for this bet to pay off? which conditions are met, which are not?]
**Cascade coherence:** [do adjacent levels reinforce each other? contradictions? is this a strategy or a plan?]

## Investment Mismatches — Fix First

Areas where code investment doesn't match strategic importance.

| ID | Finding | Category | File | Effort |
|----|---------|----------|------|--------|
| S1 | [description] | INVESTMENT_MISMATCH | [path:line] | medium |

## Dead Capabilities & Broken Chains — Fix Next

Code that's defined but unreachable, or broken links in the capability chain.

| ID | Finding | Category | File | Effort |
|----|---------|----------|------|--------|
| S2 | [description] | DEAD_CAPABILITY | [path:line] | small |

## Robustness of Differentiators — Strengthen

Error handling, edge cases, test gaps in the areas that matter most.

| ID | Finding | Category | File | Effort |
|----|---------|----------|------|--------|

## Over-Engineering & Clarity — Simplify

Unnecessary complexity in non-differentiating areas.

| ID | Finding | Category | File | Effort |
|----|---------|----------|------|--------|

## Management Systems — Nervous System

Does CI, testing infrastructure, build conventions, and deploy patterns support or contradict the
HTW choice? Accumulated infrastructure that fights the strategy is the "barnacle problem."

| ID | Finding | Category | File | Effort |
|----|---------|----------|------|--------|
| (BARNACLE findings here) | | | | |

_Diagnostic: Does the build/test/deploy infrastructure punish the behaviors the strategy rewards?
The Sears pattern: store-level P&L structures that penalized online-strategy behaviors._

## What the Code Gets Right

- [capability]: [evidence of good strategic alignment]

## Priority Matrix

| ID | Finding | Severity | Effort | Category |
|----|---------|----------|--------|----------|
```

## Strategy Choice Cascade — Audit Framework

The cascade is the primary structuring framework for this audit. Each question maps to code evidence:

| # | Cascade Question | Code Evidence | Phase 2 Mapping |
|---|---|---|---|
| 1 | **Winning Aspiration** -- purpose, guiding aspirations | Entry points, CLI verbs, pipeline output -- what the code enables users to *achieve* | → Desired customer action |
| 2 | **Where to Play** -- geographies, segments, channels, product categories, value chain stages | WTP dimensions, dependency choices, what the code explicitly does NOT do | → WTP dimensions, Non-goals |
| 3 | **How to Win** -- value proposition, competitive advantage | Investment concentration, unique implementations, revealed differentiators vs table stakes | → Differentiators, Table stakes |
| 4 | **Capabilities** -- reinforcing activities, specific configuration | Capability chain integrity, dead capabilities, reinforcing system test, dependency leverage | → Chain, Dead caps, Dependencies |
| 5 | **Management Systems** -- systems, structures, measures | CI/CD, test infrastructure, build conventions, metrics, conventions that support or contradict HTW | → Barnacle analysis |

### Cascading Coherence Test

Each choice must be **coherent with and constrained by** the choice above it. After Phase 2 populates all 5 levels, verify:

- Does **WTP** (Q2) narrow logically from the **aspiration** (Q1)?
- Does **HTW** (Q3) define advantage *in the chosen arena* (Q2), not generically?
- Do **capabilities** (Q4) form a reinforcing system that *enables* the HTW choice (Q3)?
- Do **management systems** (Q5) *reward the behaviors* the strategy requires (Q3+Q4)?

Flag `STRATEGIC_CONFUSION` when answers at adjacent levels contradict each other.

**Toggle, don't sequence** — the dashed feedback arrows in the cascade mean lower-level realities inform upper choices. A capability gap (Q4) may force revision of where to play (Q2).

### Key Tests

- **"Not stupid on its face"** — a capability is strategic only if a reasonable competitor could choose the opposite
- **Operating imperatives** — important but not advantage-creating; every competitor should choose them
- **Reinforcing system** — individual capabilities can be copied; the interlocking system cannot
- **Desired customer action** — strategy must compel the one thing you don't control: the customer
