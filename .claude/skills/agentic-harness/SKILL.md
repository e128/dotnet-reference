---
name: agentic-harness
description: Evaluate or design an agent harness against the 12 production primitives distilled from the Claude Code architecture leak. Two modes: evaluate an existing codebase for gaps, or design a new harness from scratch. Biases toward lean, solo-maintainable, single-agent architecture. Use when assessing agent reliability, security, or durability.
argument-hint: "[eval <path-to-harness>] | [design <agent-description>]"
allowed-tools: Read, Glob, Grep, Edit, Write
---

# Agentic Harness

Evaluate or design an agent harness against **12 production primitives** derived from the Claude Code architecture. Agents are 80% plumbing. This skill surfaces the plumbing gaps.

Source material: [Nate B Jones — Your Agent Has 12 Blind Spots](https://natesnewsletter.substack.com/p/your-agent-has-12-blind-spots-you) and the companion YouTube breakdown.

## Usage

```
/agentic-harness eval <path>          # Evaluate existing harness at <path>
/agentic-harness design <description> # Design a new harness for <description>
```

If no mode or argument is given, ask the user which mode they want and what target/description to use.

---

## The 12 Primitives

These are organized into two tiers. Implement Tier 1 before touching Tier 2.

### Tier 1 — Day One Basics (Primitives 1–8)

| # | Primitive                           | Core Question                                                     |
|---|-------------------------------------|-------------------------------------------------------------------|
| 1 | Tool Registry (metadata-first)      | Can you list all capabilities without executing anything?         |
| 2 | Permission System & Trust Tiers     | Are tools classified by risk, with different approval paths?      |
| 3 | Session Persistence (crash-safe)    | Does a crash lose work, or can the session be fully reconstructed?|
| 4 | Workflow State vs Conversation State| Do you track *what step you're in*, separate from chat history?   |
| 5 | Token Budget Tracking               | Are there hard limits and pre-turn checks before each API call?   |
| 6 | Structured Streaming Events         | Does the agent emit typed events consumers can act on?            |
| 7 | System Event Logging                | Is there a machine-readable log of *what was done* (not said)?    |
| 8 | Two-Level Verification              | Does the agent verify its output AND verify harness changes?      |

### Tier 2 — Operational Maturity (Primitives 9–12)

| #  | Primitive                    | Core Question                                                      |
|----|------------------------------|--------------------------------------------------------------------|
| 9  | Tool Pool Assembly           | Are session-specific tool subsets assembled dynamically?           |
| 10 | Transcript Compaction        | Is long-context automatically compacted with configurable policy?  |
| 11 | Permission Audit Trail       | Are permissions queryable first-class state, not just yes/no booleans? |
| 12 | Agent Type System            | Are agent roles sharply typed with constrained tool sets and behavior? |

---

## Primitive Reference

See [references/primitives.md](references/primitives.md) for the full detailed description of each primitive, including Claude Code implementation details, "why it matters" rationale, and "what good looks like" criteria.

---

## Modes

### Design Mode — `/agentic-harness design <description>`

Use when building a new agent harness from scratch.

**Workflow:**

1. **Gather context.** Ask: What is this agent's primary job? (code assistant, workflow orchestrator, data pipeline, chat assistant, etc.) What actions can it take in the world? Who are its users? Is there a human in the loop?

2. **Identify minimum useful primitive set.** Based on the description, identify which Tier 1 primitives are non-negotiable vs deferrable. Use this heuristic:
   - Any agent that executes code or modifies files → Primitives 1, 2, 3, 4, 5 are all non-negotiable
   - Any agent running more than 10 turns → add Primitive 10 (compaction)
   - Any agent with multiple users or enterprise context → add Primitives 7, 11
   - Multi-agent systems → add Primitives 9, 12

3. **Sequence implementation into phases.** Phase 1: Primitives 1–2. Phase 2: Primitives 3–5. Phase 3: Primitives 6–8. Phase 4 (only if needed): Primitives 9–12.

4. **Define verification criteria for each phase.** Concrete tests that confirm each phase is complete before moving on.

5. **Present the harness design.** Format:

```
## Agent Harness Design: [agent description]

**Complexity bias:** solo / small-team / enterprise (with rationale)
**Recommended starting point:** single-agent / multi-agent (with rationale)

### Phase 1 — Foundation
Primitives: [list]
Why: [rationale]
Verification: [concrete tests]

### Phase 2 — Durability
...

### Deferred (not needed now)
Primitives: [list] — [one-line reason per item]

### Invariants to test before going to production
- [list of named invariants from Primitive 8 Level 2]
```

**Design bias rules:**
- Default to single-agent. Only recommend multi-agent if the task description clearly requires parallel execution or role separation.
- Default to Tier 1 only. Only include Tier 2 if the agent description clearly warrants it (high turn count, enterprise, multi-agent).
- Flag premature complexity: if the description implies Tier 2 before Tier 1 is solid, say so explicitly.

---

### Eval Mode — `/agentic-harness eval <path>`

Use when evaluating an existing agent harness.

**Workflow:**

1. **Discover the harness.** Read the target path using Glob and Read. Look for:
   - System prompt / agent instructions (CLAUDE.md, system.md, prompts/, instructions/)
   - Tool definitions (tool files, function schemas, tool registries)
   - Session management code (session files, state files, persistence logic)
   - Configuration (harness config, token limits, permission settings)
   - Logging/event code

2. **Score each primitive.** For each of the 12 primitives, assign:
   - `present` — clear evidence this primitive is implemented
   - `partial` — some evidence, but gaps exist
   - `missing` — no evidence found
   - `n/a` — not applicable given the agent type (explain why)

3. **Generate findings.** For each `partial` or `missing` primitive, produce a finding:
   ```
   [P-N] Primitive name — MISSING/PARTIAL
   Evidence: [what was found, or "none"]
   Gap: [what is absent]
   Risk: HIGH/MEDIUM/LOW
   Fix: [concrete, minimal recommendation]
   ```

4. **Present report.** Format:

```
## Agentic Harness Evaluation: [path]

**Primitives present:** N/12
**Critical gaps (HIGH risk):** N
**Improvement opportunities (MEDIUM):** N
**Nice-to-haves (LOW):** N

### Tier 1 Scorecard
| # | Primitive                      | Status  | Risk   |
|---|--------------------------------|---------|--------|
| 1 | Tool Registry                  | present |        |
| 2 | Permission System              | missing | HIGH   |
...

### Tier 2 Scorecard
...

### Findings — HIGH

[findings ordered by tier then risk]

### Findings — MEDIUM

...

### Findings — LOW

...

### Upgrade Path

Phase 1 (do this week): [highest-risk gaps]
Phase 2 (do this month): [medium-risk gaps]
Phase 3 (optional): [low-risk / Tier 2 gaps if warranted]

### Invariants Missing
These named invariants from Primitive 8 Level 2 were not found:
- [list]
```

**Eval bias rules:**
- Only report gaps you can point to with evidence (or absence of evidence). No speculation.
- Do not recommend Tier 2 primitives if Tier 1 has HIGH-risk gaps — fix the foundation first.
- Do not recommend multi-agent patterns unless the existing harness is clearly single-agent and the description suggests parallel work.

---

## Guidelines

- **Lean over complex.** The most common failure mode is overengineering. Premature multi-agent coordination before sessions can survive crashes is where most projects die.
- **Plumbing first, model second.** Agents are 80% infrastructure. Chase the boring stuff.
- **Failures are first-class.** Good harness design assumes crashes and plans for them at every layer.
- **Verify the harness, not just the output.** Primitive 8 Level 2 is the most-skipped primitive. Name your invariants and test them.
- **Phase your work.** Tier 1 before Tier 2. Foundation before operational maturity.

## User input

$ARGUMENTS
