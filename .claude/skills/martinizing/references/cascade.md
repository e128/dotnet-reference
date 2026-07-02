# Strategy Choice Cascade — Full Reference

Detailed breakdown of each cascade question, the operating imperatives distinction, application
to product/tech strategy, common mistakes, and why the definition changed.
Loaded on demand from the main `five-elements-of-strategy` skill.

## The Strategy Choice Cascade — Five Questions

The definition tells you what strategy IS. The cascade is the companion tool for CREATING strategy.
Five interconnected questions, ordered from most abstract to most concrete:

### 1. What Is Our Winning Aspiration?

Define what winning means — specific, competition-focused, grounded in market reality.

- Not a vague mission statement
- Playing to win, not playing to participate
- P&G example: "Delivering market-leading, value-creating brands"

**Test:** Does this aspiration create urgency and force hard choices?

### 2. Where Will We Play?

Select the competitive arena deliberately:

| Dimension | What you're choosing |
|---|---|
| Geography | Which regions, countries, cities |
| Customers | Which segments, demographics, needs |
| Products | Which categories, offerings |
| Channels | Direct, retail, online, wholesale |
| Value chain | Design, build, distribute, service |

Choosing where NOT to play is as important as choosing where to play.

### 3. How Will We Win?

Define competitive advantage in the chosen arena. Two fundamental approaches:

- **Cost leadership** — comparable value, lower cost
- **Differentiation** — unique value worth a premium

**Test:** Can you complete "Customers choose us because ___" with something specific and defensible?

### 4. What Capabilities Must Be in Place?

A reinforcing system of capabilities — not a laundry list. The power is in how they combine.
Any single capability may be replicable; the interlocking system is not.

### 5. What Management Systems Are Required?

The most concrete level and the most neglected. Metrics, incentives, structure, processes,
talent systems, and review cadence that support the choices above.

**Test:** Do your management systems reward the behaviors your strategy requires?

### How the Cascade Works

- **Toggle, don't sequence** — iterate back and forth among the five boxes; it is not a one-way top-down exercise
- **Nested cascades** — in larger organizations, each unit/function has its own cascade nested within the broader one
- **Coherence** — all five answers must reinforce each other as a coherent whole

---

## Why the Definition Changed (Original → Revised)

| Original Element | What happened | Reason |
|---|---|---|
| Choices | **Kept** -- elaborated with "not stupid on its face" test | Still essential; counters "strategic plans" that are just initiative lists |
| Integrated set | **Kept** | Still essential; counters disconnected lists of choices |
| Positions the firm | **Removed** | Still valid, but already covered by Where-to-Play in the cascade -- redundant in the definition |
| Sustainable advantage | **Removed** | Still valid, but already covered by How-to-Win in the cascade -- redundant in the definition |
| Superior financial returns | **Removed** | Obvious/implicit -- if strategy produces poor returns, it isn't worth having. Also captured by "desired customer action" |
| Desired customer action | **Added** | Captures what strategy must ultimately accomplish -- the one thing the company cannot control |

---

## Operating Imperatives vs. Strategic Choices

**The "Not Stupid on Its Face" Test:**
A strategy choice is one for which the opposite of the choice is *not stupid on its face*.

- "Be customer-centric" **fails** this test — the opposite (ignoring customers) IS stupid on its face
- Such choices are **operating imperatives** — important, even critical, but not strategic
- Operating imperatives won't generate advantage because every competitor should and will choose them too
- A genuine strategic choice is one where a reasonable competitor might choose the opposite

---

## Applying to Product & Tech Strategy

| Cascade Question | Product/Tech Translation |
|---|---|
| Winning aspiration | What does success look like for this product? |
| Where to play | Which users, use cases, platforms, markets? |
| How to win | What is the unique value proposition? |
| Capabilities | What technical and team capabilities are needed? |
| Management systems | What metrics, processes, and rituals support execution? |

The "desired customer action" lens applies directly: what specific user behavior must your product
compel? Downloads, daily usage, purchases, renewals, referrals? Strategy is the set of integrated
choices that compel that action.

---

## AI Strategy Cascade

Martin applies the same five cascade questions to AI investment decisions (Copyright Roger L. Martin 2026). AI strategy is not a separate discipline — it is the Strategy Choice Cascade applied to AI augmentation choices:

| Cascade Question | AI-Specific Guidance |
|---|---|
| **Winning Aspiration** | Be a first-mover in uniquely valuable AI augmentation -- not "adopt AI everywhere" |
| **Where to Play** | Places to invest in AI augmentation where the enterprise can gain and maintain a competitive advantage -- not every process that could theoretically use AI |
| **How to Win** | The competitive method enabled by AI augmentation in the chosen arena |
| **Must-Have Capabilities** | AI augmentation of the workforce -- the specific human+AI capability combinations required |
| **Enabling Management Systems** | Trust-producing foundations + redesign of key enabling processes -- AI changes how work is done, which demands new management systems, not just new tools |

**Key insight:** The cascade's dashed feedback arrows apply here too — AI capabilities may reveal new where-to-play options, and management system constraints may limit which AI augmentations are viable. Toggle, don't sequence.

**"Not stupid on its face" applied to AI:** "Use AI" fails the test — every competitor will use AI. The strategic question is *where* AI augmentation creates advantage that competitors cannot easily replicate. An AI investment that any competitor could make with the same off-the-shelf tools is an operating imperative, not a differentiator.

---

## Martin's Later Teachings (2023–2026)

These dimensions extend the cascade with practical diagnostics Martin developed in his
PTW/PI essay series (260 weekly essays, Oct 2020–late 2025, Medium/Substack), *A New Way
to Think* (2022), "A Plan Is Not a Strategy" (HBR, 2025), and "Strategy and
Connoisseurship" (Substack, Dec 2025).

### A Plan Is Not a Strategy

Martin's hardest distinction. A plan involves allocating resources and specifying actions
*within the firm's control*. Strategy involves making choices about *what customers will
do* — and customers cannot be controlled. Therefore strategy is always a theory (a bet)
about the future: "You never know what the future holds for sure, so you have to make bets
based on a model." The model can be tested, the odds can be shortened, but certainty is
unavailable.

**Code audit implication:** When the codebase shows a list of funded initiatives (projects
in flight, features under construction) but no coherent cascade linking them to a customer
action — that is a plan, not a strategy. Flag the absence of cascade coherence, not the
individual initiatives.

### Comfort with Angst

Martin frames the psychological cost of strategy explicitly: it produces *angst* because
it requires committing to a bet that cannot be proven in advance. Managers conditioned to
be "right" gravitate toward planning because plans are controllable. Strategy requires
tolerating the discomfort of operating in what Aristotle called "the part of the world
that can be other than it is."

**Code audit implication:** When the code invests equally across every possible arena
without visible de-prioritization, this may signal organizational avoidance of the angst
that comes from choosing. The hardest strategic question is always: **what current
investment must receive less to fund the keystone capability?** If nothing has been
de-prioritized, no genuine strategic choice has been made.

### Where-to-Play Is the Foundational Choice

Martin's 2024–2025 writings consistently reinforce that WTP is where most strategies fail.
Companies with clear WTPs and matched HTWs can build tight capability systems; companies
with vague or over-broad WTPs cannot build anything distinctive because they are optimizing
for everywhere.

For technology and platform companies specifically, the WTP decision is compound:
- Which **layer of the stack** to own
- Which **customer type** to serve directly
- Which **adjacencies** to deliberately *not* enter

**Code audit implication:** When auditing a platform/infrastructure codebase, map
investment across layers and customer types. If investment is spread across many layers
without concentration, the WTP is likely too broad. Concentration in one layer with thin
integration stubs into adjacent layers signals a clearer WTP.

### The Can't/Won't Test for How-to-Win

Martin requires that the HTW create an advantage that passes one of two tests:
- **Can't test:** Competitors *cannot* replicate the advantage without significant cost or
  structural change
- **Won't test:** Competitors *won't* replicate the advantage because doing so would damage
  their own existing market position

If neither test passes, the claimed advantage is an operating imperative, not a differentiator.

**Code audit implication:** For each revealed differentiator, ask: could a competitor fork
this capability (or buy an equivalent) within a quarter? If yes, it is an operating
imperative regardless of how much was invested. The reinforcing *system* of capabilities
may still pass the can't/won't test even when individual capabilities do not.

### What Would Have to Be True (WWHTBT)

Martin's Strategic Choice Structuring Process uses this question as a logic separator:
instead of debating what *is* true about the market, teams articulate what *would have to
be true* for each strategic possibility to succeed. This separates logic from data, allows
parallel evaluation of competing possibilities, and converts debates from "who is right" to
"which conditions are we more confident in creating."

WWHTBT is the operational mechanism for making a bet — you are not claiming the future is
known; you are specifying the conditions your strategy requires and then stress-testing
whether those conditions are achievable.

**Code audit implication:** For each WTP/HTW combination revealed in the code, articulate
the WWHTBT conditions. If a condition is clearly false today (e.g., "this requires a
capability that has near-zero implementation"), the strategy is aspirational. If a condition
is true today but fragile, the strategy is at risk. This turns the audit from a
code-quality exercise into a strategic viability exercise.

### Strategy and Execution Are Not Separable

Martin's formulation: "There is no 'execution' — there are only strategy choices made at
various levels of the organization." What is called execution is actually a nested set of
lower-level strategy choices, each of which must pass the same cascade logic. The cascade
runs at the corporate level, the business unit level, the functional level, and the team
level — each nested inside the level above like Russian dolls.

**Code audit implication:** When a codebase has a clear top-level cascade but the
implementation contradicts it (e.g., the strategy says "differentiate on X" but the code
under-invests in X), this is not an "execution gap" — it is a *strategy incoherence* at
the team/function level. Flag as `INVESTMENT_MISMATCH` or `STRATEGIC_CONFUSION` depending
on whether the contradiction is one of degree or direction.

### Integrative Thinking / The Opposable Mind

From *The Opposable Mind* (2007, still heavily cited in 2022–2026 work): integrative
thinking is the ability to hold two conflicting models in tension simultaneously, and
generate a third model superior to either. Most managers default to choosing one horn of a
dilemma. Great strategists synthesize.

In strategy generation, when two possibilities create genuine tension, integrative thinking
produces a new WTP/HTW combination that captures benefits of both without their individual
costs.

**Code audit implication:** When a codebase shows two architectural approaches coexisting
(e.g., two policy engines, two approval systems), this may be a failure to choose — or it
may be mid-transition. Check git history: if both systems are actively invested in with no
convergence plan, flag as `STRATEGIC_CONFUSION`. If one is being deprecated in favor of
the other, the integrative thinking test applies: is the replacement a synthesis that
captures the strengths of both, or is it simply one system winning over the other?

### Customers Are the Only Constraint

Martin's cascade is customer-terminal: every choice in all five boxes is ultimately tested
against "does this compel desired customer action?" He distinguishes firms that orient
toward competitors (reacting to moves, benchmarking) from firms that orient toward
customers (asking what would make customers choose them). Competitor orientation produces
mimicry; customer orientation is the source of genuine differentiation.

**Code audit implication:** When revealed differentiators map more closely to competitive
parity features ("they have X, so we built X") than to customer-action-compelling
capabilities, flag as potential mimicry. The presence of "competitive feature" in commit
messages or PRD docs is a signal of competitor orientation.

### Strategy as Connoisseurship (Dec 2025)

In "Strategy and Connoisseurship" (Substack, Dec 2025), Martin argues that after decades
of applying the cascade, the next level is qualitative discrimination: the ability to judge
that one strategy is *subtly* superior to another even when both pass the basic tests —
the way a sommelier distinguishes wines that both score well. This requires pattern
recognition accumulated across many strategies, not just framework application.

**Code audit implication:** This is why martinizing audits should persist strategic profiles
to `lode/strategy/` — pattern recognition across many audits reveals which capability
configurations consistently produce advantage. A single audit applies the framework; a
library of audits develops connoisseurship.

### Management Systems Build Capabilities (Not Efficiency)

Martin finds the management systems box the least understood. Most organizations design
management systems for efficiency, not for building the specific capabilities the strategy
requires. The management system must close the loop: if it is not measurably building the
required capability, the strategy will not hold.

The canonical negative example: Sears' store-level P&L structures penalized behaviors the
online strategy required — employees were measured on store metrics while being asked to
support an omnichannel strategy.

**Code audit implication:** The `BARNACLE` finding category captures this directly. CI
pipelines, test infrastructure, code review processes, and deploy gates are management
systems. When they reward behaviors that contradict the HTW choice (e.g., gate on code
coverage in table-stakes modules while differentiating modules have no test requirements),
that is a management system misalignment.

---

## Common Mistakes

| Mistake | Why it fails |
|---|---|
| Defining strategy as a plan | Plans list activities; strategy makes choices |
| Confusing operating imperatives with strategy | "Be customer-centric" fails the "not stupid on its face" test |
| Trying to play everywhere | Refusing to choose is itself a (bad) choice |
| Ignoring the customer action question | Strategy that doesn't compel customers to act is just internal activity |
| Copying best practices | Best practices are table stakes (operating imperatives), not advantage |
| Skipping management systems | The #1 implementation failure |
| Treating the cascade as one-way | Must iterate; lower-level realities inform upper-level choices |
| Action bias over thinking | Strategy requires disciplined thinking before action |
| Substituting initiatives for HTW | A list of projects ("invest in digital") is not a competitive logic |
| Avoiding angst by not choosing | Equal investment everywhere signals absence of strategy |
| Treating execution as separate | "Execution gap" usually means strategy incoherence at a lower level |
| Competitor orientation | Building what competitors have produces mimicry, not differentiation |

---

## Sources

- Roger Martin, "Revisiting My Definition of Strategy" (Medium/Substack, Oct 2025) — primary source
- Roger Martin, *A New Way to Think* (Harvard Business Review Press, 2022) — strategy/execution inseparability, integrative thinking
- Roger Martin, "A Plan Is Not a Strategy" (HBR, May 2025) — plan vs. strategy distinction, comfort with angst
- Roger Martin, "Strategy and Connoisseurship" (Substack, Dec 2025) — qualitative discrimination across strategies
- Roger Martin, *The Opposable Mind* (Harvard Business Press, 2007) — integrative thinking framework
- Roger Martin, PTW/PI essay series (260 weekly essays, Oct 2020–late 2025, Medium → Substack) — WWHTBT, customer orientation, WTP primacy
- https://rogerlmartin.com/thought-pillars/strategy (scraped: 2026-02-13)
- https://www.frontierstrategypartners.com/fsp-blog/cascade-of-choices-understanding-the-five-essential-questions-of-strategy (scraped: 2026-02-13)
- https://www.mindtools.com/a6re8qh/lafley-and-martins-five-step-strategy-model/ (scraped: 2026-02-13)
- https://fs.blog/playing-to-win-how-strategy-really-works/ (scraped: 2026-02-13)
- https://www.lennysnewsletter.com/p/the-ultimate-guide-to-strategy-roger-martin (Lenny's Newsletter interview)
