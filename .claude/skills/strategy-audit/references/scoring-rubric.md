# Martinizing — Scoring Rubric & Finding Catalog

Detailed finding categories, the capability classification rubric, Phase 3 agent prompt template,
and the full Phase 4 report format. Loaded on demand from the main `martinizing` skill.

## Finding Categories

| Category | Definition | Priority |
|----------|-----------|----------|
| `INVESTMENT_MISMATCH` | Over-engineered table stakes, or under-invested differentiators | Highest |
| `DEAD_CAPABILITY` | Code that's defined/registered but never called from active pipelines | High |
| `CHAIN_BREAK` | Capability chain has a broken link — output of one subsystem doesn't reach the next | High |
| `ROBUSTNESS` | Error handling, silent failures, missing exception catches in differentiators | Medium |
| `OVER_ENGINEERED` | Unnecessary complexity in areas that don't create advantage | Medium |
| `ARCHITECTURE` | God class, unclear responsibilities, tight coupling | Medium |
| `CLARITY` | Dead code, redundant calls, confusing structure | Low |
| `DEPENDENCY_LEVERAGE` | Major dependency fails the outsourcing test — hold-up risk, future competitor risk, or mission-critical capability delegated to an outside party | High |
| `STRATEGIC_CONFUSION` | Differentiator set signals conflicting competitive positions (cost leadership and premium differentiation simultaneously) — pursuing both produces mediocrity on both dimensions | High |
| `BARNACLE` | Accumulated infrastructure, patterns, or conventions that now contradict or create drag on the HTW choice — may have been justified previously but now fights the current strategy | Medium |

## Phase 2a: Capability Classification Rubric

Apply the "not stupid on its face" test to each revealed differentiator:

| Capability | Opposite viable? | Classification |
|---|---|---|
| [capability] | Yes — a competitor could reasonably skip this | **Strategic differentiator** |
| [capability] | No — every serious competitor must do this | **Operating imperative** (quality threshold) |

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
2. **Dead capabilities** — Code that's defined/registered but never called from the active pipeline
3. **Capability chain integrity** — Does the output of one subsystem actually feed into the next?
4. **Robustness of differentiators** — Error handling, edge cases, test coverage on areas that matter most
5. **Over-engineering of non-differentiators** — Unnecessary complexity in non-differentiating areas
6. **Reinforcing system integrity** — Could a competitor copy any single capability in isolation without weakening the others? Capabilities that stand alone are individually vulnerable; capabilities that make adjacent ones stronger form a structural moat. If differentiators are modular and independent (could be individually extracted without loss), flag as `ARCHITECTURE`. Also check: do major external dependencies pass the outsourcing test? (better/cheaper outside? hold-up risk? future competitor risk?) Flag failures as `DEPENDENCY_LEVERAGE`.

Return findings as:
- Finding ID: S[N]
- Category: INVESTMENT_MISMATCH | DEAD_CAPABILITY | CHAIN_BREAK | ROBUSTNESS | OVER_ENGINEERED | ARCHITECTURE | CLARITY
- Severity: HIGH | MEDIUM | LOW
- Description: [what's wrong]
- File: [path:line]
- Evidence: [brief code snippet or method signature, max 3 lines]
- Suggested fix: [concrete action]
- Effort: small (<30 min) | medium (30 min - 2 hours) | large (2+ hours)

Do NOT return full file contents. Return only findings with file paths and line numbers.
```

## Phase 4: Full Report Format

```markdown
# Martinizing Report

**Codebase:** [repo-path]
**Audit Areas:** [N] agents covering [list]
**Total Findings:** [N] (by category breakdown)

## Strategic Profile (derived from code)

**Revealed differentiators:** [list]
**Operating imperatives:** [list]
**Table stakes:** [list]
**Revealed non-goals:** [list]
**Desired customer action:** [what the pipeline enables]
**Capability chain:** [how subsystems connect]

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

## Key Concepts from Roger Martin

**"Not stupid on its face" test:** A capability is only a strategic differentiator if a reasonable
competitor could choose the opposite. "Good error handling" fails this test (every tool should have
it). "CLI-only, no GUI" passes (many competitors choose to build a GUI).

**Operating imperatives vs. strategy:** Operating imperatives are important — even critical — but
they don't create advantage because every competitor should choose them. Quality thresholds in the
capabilities table are operating imperatives. Strategic investment flows to actual differentiators.

**"A dream is not a strategy":** Aspirations like "Be the best X" are dreams. Strategy embeds the
choices that enable the aspiration: "Be the go-to X for users who choose Y over Z."

**Reinforcing system:** Individual capabilities can be copied. The interlocking system cannot.
The audit checks whether capabilities actually chain together in code, not just in documentation.

**Desired customer action:** Strategy must compel the one thing you don't control — the customer.
The audit checks whether the code actually enables the user action the strategy claims to compel.

**Common strategy traps visible in code:** Investing equally in everything (no strategic
differentiation), building capabilities that aren't reachable from the main pipeline (dead
capabilities), over-engineering table stakes while under-investing differentiators, and copying
patterns from other projects without considering whether they serve this project's unique advantage.
