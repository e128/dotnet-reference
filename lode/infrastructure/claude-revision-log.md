# Claude Revision Log
*Updated: 2026-04-17T13:51:12Z*

Persistent memory for `/claude-revision`. Each run appends one entry.
Read at Phase 0 to recover last-known state and deferred items.

## Runs

### 2026-04-17 (Run 4)
- Agents: 16 | Skills: 17 | Memory files: 1
- Web guidance: v2.1.112 (up from v2.1.101); `xhigh` effort level (Opus 4.7); `PreCompact` hook (v2.1.105); Agent SDK rename; `budget_tokens` removed on Opus 4.7; skill description budget 1,536 chars
- HIGH: 0 | MEDIUM: 1 | LOW: 6
- Actions taken:
  - W1: Added `effort: high` to dotnet-overhaul, code-review, solution-audit, claude-revision SKILL.md files
  - W4/W6: Updated claude-code-upstream.md — version 2.1.112, xhigh effort field, description truncation 1,536 chars, API/SDK notes section, sources dated 2026-04-17
  - W5: Added PreCompact hook note to harness-eval Guidelines (Primitive 10 implementation in Claude Code)
  - W6: Updated claude-code-maintenance.md — added `jb` prerequisite; noted DockerSmokeTests graceful daemon-unavailable skip
  - W2/W3: No file changes needed — vars (`${CLAUDE_SKILL_DIR}`, `${CLAUDE_SESSION_ID}`) and `skills:` agent field already documented in upstream doc
- Deferred:
  - A1 (MEDIUM): 4 agents missing explicit `model:` — `fill-test-gaps`, `review-applier`, `sme-researcher`, `tdd-loop-optimizer`. Intentionally left to inherit session model per Run 2 (opus use case). Confirm intent and add explicit model next run.
- Notes: Session also included jb cleanupcode integration (format.sh), editorconfig updates (primary constructors off, collection expressions = never), and Docker test graceful skip (Assert.Skip when daemon unavailable). All 553 tests passing.

### 2026-04-12 (Run 3)
- Agents: 16 | Skills: 17 | Memory files: 1
- Web guidance: v2.1.101 (up from v2.1.98); sub-agent MCP inheritance fix, worktree Read/Edit fix, SDK renamed to "Claude Agent SDK", /ultraplan + /team-onboarding added
- HIGH: 1 | MEDIUM: 1 | LOW: 0
- Actions taken:
  - S1: Extracted solution-audit Phase 1 parse steps to references/parse-steps.md, compressed Phase 4 report template (314->227 lines)
  - A1: Trimmed weekly-learner: compressed triggers, Phase 2.4, Phase 7 (263->242 lines)
  - L1: Updated claude-code-upstream.md version baseline to v2.1.101
- Deferred: none
- Notes: Consolidation commit (3063fc0) since Run 2 merged 3 agents (lode-capture→lode-sync, skill-loop-optimizer→skill-self-updater, token-optimizer→weekly-learner) and deleted 3 (appsettings-drift, commit-preflight, confirmation-reducer). Clean state — all agents have explicit model/maxTurns/tools/memory.

### 2026-04-10 (Run 2)
- Agents: 23 | Skills: 15 | Memory files: 0
- Web guidance: no changes since Run 1 (v2.1.98, all 5 sources checked, lode upstream doc current)
- HIGH: 1 | MEDIUM: 1 | LOW: 8
- Actions taken:
  - S1: Trimmed bash-patterns SKILL.md 254->247 (removed Sources section; URLs are common knowledge)
  - A1: Model optimization — 18 agents assigned explicit models (2 haiku, 16 sonnet); 4 code-writing agents remain on opus (fill-test-gaps, review-applier, tdd-loop-optimizer, sme-researcher)
  - R1: Moved plan-context.sh to scripts/internal/ (called by context.sh, not user-facing)
  - R2: Moved plan-path.sh to scripts/internal/ (called by assert.sh, not user-facing)
  - R3: Moved lode.sh to scripts/internal/ (legacy Claude CLI wrapper; redundant with CLAUDE.md Lode section)
  - R4: Added keyword shortcut for loop.sh (poll until / wait for build / loop until)
  - R5: Updated scripts/README.md for all script relocations
  - R6: Updated scripts/context.sh caller reference for plan-context.sh move
  - A2: leverage-advisor maxTurns: 50 reviewed — justified (3 analyses + 5 plans)
- Deferred: none — all prior deferred items resolved
- Resolved from prior run:
  - R7 lode.sh → moved to internal (confirmed redundant)
  - R8 loop.sh → added keyword shortcut (distinct from /loop skill)
  - R9 plan-context.sh → moved to internal (confirmed: internal dependency of context.sh)
  - R10 plan-path.sh → moved to internal (confirmed: internal dependency of assert.sh)
  - A18-A23 model optimization → completed (18 agents assigned explicit models)

### 2026-04-10 (Run 1)
- Agents: 23 | Skills: 15 | Memory files: 0
- Web guidance: first baseline captured (v2.1.91-2.1.98); WORKFLOW.md not in official docs; default effort now high; disallowedTools field exists; skill paths field available
- HIGH: 2 | MEDIUM: 14 | LOW: 9
- Actions taken:
  - S1: Renamed tool-learner/WORKFLOW.md to SKILL.md (now registered)
  - A1-A5: Trimmed 5 agents to under 250 lines (weekly-learner 438->248, simplification-agent 312->248, analyzer-review-miner 296->175, leverage-advisor 270->189, token-optimizer 258->167)
  - A2: Confirmed simplification-agent missing Edit is by design (read-only agent)
  - S2-S3: Trimmed claude-revision 296->242, dotnet-overhaul 266->248
  - R1-R11: Added 7 keyword shortcuts for unreferenced scripts (bench, coverage-areas, docker, gh-actions-update, lint-yaml, lode-summary, update)
- Deferred:
  - R7 lode.sh (external Claude wrapper — may be obsolete, needs investigation)
  - R8 loop.sh (overlaps /loop skill — needs overlap verification)
  - R9 plan-context.sh (overlaps context.sh — needs overlap verification)
  - R10 plan-path.sh (internal helper for plan-context — LOW)
  - A18-A23 model optimization (4 agents could use model: sonnet — LOW priority)
