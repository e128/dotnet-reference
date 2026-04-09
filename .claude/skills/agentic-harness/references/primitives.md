# Primitive Reference

Detailed descriptions of each of the 12 production primitives. Referenced from the main [SKILL.md](../SKILL.md).

---

### 1 — Tool Registry with Metadata-First Design

**Pattern:** Define capabilities as a data structure *before* writing implementation. The registry answers "what exists and what does it do" without executing anything.

**Claude Code implementation:** Two parallel registries — a command registry (207 user-facing entries) and a tool registry (184 model-facing entries). Each entry carries name, source hint, and responsibility description. Implementations load on demand.

**Why it matters:** Without a registry you cannot filter tools by context, cannot introspect without side effects, and every new tool forces orchestration changes.

**What good looks like:**
- A `list_tools()` function returns metadata for all capabilities without invoking them
- Runtime filtering is supported (by context, permission tier, mode)
- Each tool has: name, short description, risk classification
- Registry is written before any model prompt that selects a tool

---

### 2 — Permission System and Trust Tiers

**Pattern:** Not all tools carry the same risk. Classify risk, apply different approval tiers per class.

**Claude Code implementation:** Three trust tiers — built-in (always available, highest trust), plugin (medium trust, disable-on-command), user-defined skills (lowest trust by default). The bash tool alone has an 18-module security architecture: pre-approved command patterns, destructive command warnings, git-specific safety checks, sandbox termination.

**Why it matters:** If your agent can execute code, call APIs, send messages, or modify files without a permissions layer, you have a demo, not a product.

**What good looks like:**
- Pre-classification: read-only vs mutating vs destructive
- Pre-approved patterns for known-safe commands
- Destruction detection that flags deletes/overwrites before execution
- Domain-specific safety checks per tool category
- Permission logging: every decision (granted or denied) recorded with enough context to replay it

---

### 3 — Session Persistence That Survives Crashes

**Pattern:** An agent session is not just conversation history. It is a recoverable state that includes: conversation, usage metrics, permission decisions, and configuration. If any of those are missing on resume, the session behaves differently than the original.

**Claude Code implementation:** Sessions persisted as JSON files capturing session ID, messages, token usage (in/out), and full engine config. A query engine can be fully reconstructed: load → reconstruct transcript → restore counters → return functional agentic engine.

**Why it matters:** Agents crash. Connections drop. Users close tabs. Every interruption that isn't a resume is a restart. Every restart is a degraded experience.

**What good looks like:**
- A session state structure that captures: conversation, token counts, permissions granted, config, workflow position
- Persist after significant events, not just at shutdown
- A `resume_session()` function that reconstructs full agentic state, not just conversation history

---

### 4 — Workflow State vs Conversation State

**Pattern:** Resuming a conversation ≠ resuming a workflow. A chat transcript answers "what have we said?" A workflow state answers "what step are we in, what side effects have occurred, is this operation safe to retry, and what happens after restart?"

**Claude Code implementation:** Distinct from session persistence. Workflow state is a separate concern that persists beyond the agent session itself.

**Why it matters:** Almost every agentic framework conflates these two. Without workflow state, an agent can be fully reconstructed but still not know where it was in multi-step work — leading to duplicate writes, double-sent messages, or re-running expensive operations.

**What good looks like:**
- Long-running work modeled as explicit named states: `planned`, `awaiting_approval`, `executing`, `waiting_external`, `complete`
- Checkpoints persisted after every significant step (not just on success)
- State includes: what step, what side effects occurred, safe-to-retry flag
- Workflow state survives agent crashes and can bootstrap a fresh session mid-task

---

### 5 — Token Budget Tracking with Pre-Turn Checks

**Pattern:** Every turn calculates projected token usage before making an API call. If the projection exceeds budget, execution stops with a structured stop reason — before spending the tokens.

**Claude Code implementation:** Query engine configuration defines: max turns per conversation, max token budget, compaction threshold. Pre-turn projection; structured stop reason emitted on budget exhaustion.

**Why it matters:** Without budget tracking, you discover you've exceeded limits after the fact. Runaway loops are a common and expensive failure mode.

**What good looks like:**
- Hard limits: max input tokens, max output tokens, max turns
- Pre-turn check: project usage before each API call, stop early if budget exceeded
- Budget state is part of persisted session
- Compaction threshold: auto-compact before hitting hard limit

---

### 6 — Structured Streaming Events

**Pattern:** Streaming is not just for showing text. Every streaming event is an opportunity to communicate system state. Events should be typed and consumable by both humans and machines.

**Claude Code implementation:** Query engine emits typed events during stream: `message_start`, `command_match`, `tool_match`, and others. Crash events are emitted as the *last* message in the stream with a structured reason — a "black box" for failures.

**Why it matters:** Structured events let users (and orchestrators) understand what the model is doing and intervene. Crash events enable post-mortem analysis without re-running.

**What good looks like:**
- Defined event schema with types: thinking, tool invocation, result, error, budget warning, stop
- Crash events: last event in stream is a structured `{type: "crash", reason: "...", token_count: N}`
- Events are machine-readable, not just display text
- Consumers can subscribe to specific event types

---

### 7 — System Event Logging

**Pattern:** Separate from streaming events and conversation transcript. A machine-readable log of *what the agent did*, not what it said. This is the source of truth for audits and post-mortem analysis.

**Claude Code implementation:** Separate history log capturing: what context was loaded, registry initialization details, routing decisions, execution counts, permission grants/denials. Each event has a category and structured details.

**Why it matters:** When something goes wrong in an enterprise deployment, you need to prove what happened — not reconstruct it from chat messages.

**What good looks like:**
- Separate log file/store from conversation transcript
- Each event: timestamp, category, structured details (not free text)
- Events cover: context loading, tool invocations (with args), permission decisions, errors, token usage per turn
- Log is queryable (can filter by category, time range, session ID)

---

### 8 — Two-Level Verification

**Pattern:** Verification operates at two levels. Level 1: the agent verifies its own output. Level 2: changes to the harness are verified against known-good invariants.

**Claude Code implementation:**
- Level 1: Explicit verification step in the harness after tool execution. Claude Code makes this a first-class harness primitive, not an afterthought in the prompt.
- Level 2: Verification tests for harness changes — e.g., "destructive tools always require approval," "when tokens run out, the model gracefully stops." These are named, logged guardrails that run against the harness itself.

**Why it matters:** Level 1 is well-understood. Level 2 is almost universally missing — without it, refactoring the harness silently breaks safety guarantees.

**What good looks like:**
- Level 1: `verify_output()` step built into the harness, runs after every significant tool execution
- Level 2: Named invariant tests — checklist of properties the harness must always satisfy
- Level 2 tests run before and after any harness change
- Invariants include: destructive tool gate, budget stop behavior, permission logging completeness

---

### 9 — Tool Pool Assembly

**Pattern:** A general-purpose agent with a large tool set does not load all tools on every run. It assembles a session-specific tool pool based on mode flags, permission context, and deny lists.

**Claude Code implementation:** 184 tools in the registry; a session-specific subset is assembled at run start. Not all tools are available in every mode. The model can read the tool pool efficiently and select from it.

**Why it matters:** Giving an LLM every available tool on every run is expensive, confusing, and unsafe. Dynamic assembly enables specialization without hard-coding separate agent variants.

**What good looks like:**
- `assemble_tool_pool(mode, permissions, deny_list)` — returns a filtered, ordered list of tools
- Tools in the pool carry full metadata (name, description, risk tier)
- Pool assembly is logged as a system event
- The model is given the assembled pool, not the full registry

---

### 10 — Transcript Compaction

**Pattern:** Conversation history is a token-expensive resource. Long-running agents automatically compact it after a configurable number of turns, keeping recent context and discarding older turns. The transcript store tracks whether compaction has been persisted.

**Claude Code implementation:** Auto-compaction after a configurable turn threshold. Keeps recent entries, discards older ones. Tracks persistence state to avoid data loss during compaction.

**Why it matters:** Without compaction, long-running agents balloon cost and eventually hit context limits. The initial instruction that started the workflow is especially important to preserve.

**What good looks like:**
- Configurable compaction threshold (turns or token count)
- Compaction policy: always preserve system/instruction turns; compact middle conversation turns
- Persistence flag: know whether the compacted transcript has been written to durable storage
- Compaction is logged as a system event with before/after token counts

---

### 11 — Permission Audit Trail

**Pattern:** Permissions are not a boolean gate. They are first-class queryable state objects. The audit trail is designed to answer "what was permitted, when, and why" for any past run.

**Claude Code implementation:** Three separate permission handlers for different contexts — interactive (human in the loop), coordinator (orchestrator in multi-agent), swarm worker (autonomous, managed by orchestrator). Each handler has different approval logic and audit requirements.

**Why it matters:** Enterprise deployments require the ability to audit, replay, and challenge permission decisions. A boolean flag cannot support this.

**What good looks like:**
- Permission state is a record: `{tool, action, granted, context, timestamp, handler_type}`
- Permissions are queryable by session, tool, and time range
- Three handler types implemented if multi-agent: interactive, coordinator, worker
- Denied permissions are logged with the same detail as granted ones

---

### 12 — Agent Type System

**Pattern:** Agent roles are sharply typed with defined prompts, allowed tool sets, and behavioral constraints. You manage a population of typed agents, not a pool of identical clones.

**Claude Code implementation:** Six built-in agent types: `explore` (cannot edit files), `plan` (does not execute code), `verify`, `guide`, `general_purpose`, `status_line_setup`. Each type has its own system prompt, allowed tools list, and behavioral constraints baked in.

**Why it matters:** Spawning agents randomly without role constraints produces unpredictable behavior and makes multi-agent systems unmanageable. Typed roles make the agent population observable and auditable.

**What good looks like:**
- Defined type registry: each type has a name, purpose, system prompt override, and allowed tools list
- Tool allow-list is enforced at runtime, not just in the prompt
- Types are conservative by default (an explore agent *cannot* write, not just *shouldn't*)
- Multi-agent orchestrators select from the type registry, not from ad-hoc instructions
