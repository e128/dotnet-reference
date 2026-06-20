---
name: tech-debt-audit
description: >
  Tech debt and architecture audit for .NET repos. Produces plans/TECH_DEBT_AUDIT.md with file-cited
  findings, severity, and effort estimates. Supports module scoping and focused-mode shortcuts
  (crap, dead-code, duplicates, suppressions, solid) for single-dimension audits. Does not auto-invoke.
  Triggers on: tech debt audit, debt audit, codebase health check, architecture review,
  code quality assessment, audit tech debt, tech debt scan, code health, crap analysis,
  crap score, dead code audit, find dead code, unused types, dup-scan, code duplication,
  duplicate code, find duplicates, DRY violations, review code suppressions,
  audit pragma warnings, pragma warning disable, suppression cleanup, code hygiene,
  SOLID violations, solid review.
argument-hint: "[scope path] [crap | dead-code | duplicates | suppressions | solid | all]"
---

# Tech Debt Audit

Everything from here through the `---` divider is the protocol Claude executes; the section after it is human documentation.

## Arguments

| Argument        | Required | Description                                                                  |
| --------------- | -------- | ---------------------------------------------------------------------------- |
| `[scope path]`  | No       | Limit audit to a specific directory (e.g., `src/Core`). Full repo if omitted |
| `[mode]`        | No       | Focused mode — run only one dimension (see table below). Full audit if omitted |

## Focused Modes (formerly code-health-audit)

When a mode argument is provided, skip Phase 1 orientation and Phase 2 full audit. Run only the specified dimension, produce findings in the standard output format, and write to `plans/TECH_DEBT_AUDIT.md`.

| Mode           | Dimension                        | What it finds                                              |
| -------------- | -------------------------------- | ---------------------------------------------------------- |
| `crap`         | §4 Test debt (CRAP scoring)      | High-risk methods (CRAP scoring)                           |
| `dead-code`    | §1 Architectural decay           | Unused public types + orphaned NuGet packages              |
| `duplicates`   | §1 Architectural decay           | Copy-pasted code blocks + repeated helper patterns         |
| `suppressions` | §8 Security hygiene              | `#pragma warning disable` — proposes fixes over suppression |
| `solid`        | §10 Code quality                 | SRP, OCP, LSP, ISP, DIP violations with refactoring suggestions |
| `all`          | All 16 dimensions                | Same as omitting the mode argument                         |

**CRAP formula:** `CRAP = CC² × (1 − cov/100)³ + CC` — source-level cyclomatic complexity, not IL-level. C#-specific counting rules: [references/crap-formula.md](references/crap-formula.md).

**Suppression policy:** Default stance is fix, don't suppress. Move recurring test suppressions to `tests/.editorconfig`. All suppressions require explicit user approval per CLAUDE.md. See [references/suppression-policy.md](references/suppression-policy.md).

---

## Operating principles

Find what's actually wrong, grounded in this specific repo — not generic best-practices pattern-matching, not surface-only, no sycophancy or "well-structured overall" filler. Cite `file:line` for every concrete finding; vague claims don't count. Read code before judging — a pattern that looks wrong in isolation may be load-bearing.

## Phase 1: Orient

1. Read the README, `Directory.Packages.props`, solution file(s), and any architecture docs in `lode/` or `docs/`.
2. Map the directory structure and identify the major projects / layers.
3. Run `scripts/branch.sh --json` to see recent activity direction.
4. Run the co-located scripts to gather metrics (all accept an optional scope path):
   ```bash
   .claude/skills/tech-debt-audit/scripts/tda-file-metrics.sh 6 20 [scope]
   .claude/skills/tech-debt-audit/scripts/tda-vcs-analysis.sh 6 30 [scope]
   ```
5. Identify entry points, hot paths, and cold corners from the script output.
6. Use `TaskCreate` to publish a plan so the user can see progress through the phases.

Write a 1–2 paragraph mental model of the architecture before proceeding. If your model contradicts the README, flag it — that itself is a finding.

## Phase 2: Audit across these dimensions

Use `rg`, `fd`, Roslyn MCP (when available), and `dotnet` CLI tools to find concrete examples. Cite `path/to/file:LINE` for every finding. If scope is provided, limit analysis to that directory.

### Core Dimensions

1. **Architectural decay** — circular deps, layering violations, god files (>500 LOC) and god functions, logic duplicated across 3+ sites, abstractions nobody uses, dead code.

2. **Consistency rot** — multiple ways of doing the same thing (HTTP clients, error handling, logging, config loading, validation, date handling, serialization). Naming drift. Folder structure that no longer reflects the code.

3. **Type & contract debt** — `object`/`dynamic` as type erasure, untyped API boundaries, missing schema validation at trust boundaries, `string` where a strong type belongs.

4. **Test debt** — coverage gaps on critical paths, tests asserting implementation not behavior, skipped/flaky tests, high-churn untested files, reflection in tests instead of `InternalsVisibleTo`.

5. **Dependency & config debt** — run `tda-nuget-health.sh` for vulnerable/outdated/deprecated in parallel. Severity: deprecated = **CRITICAL** (any gap), major behind = HIGH, minor = MEDIUM, patch = informational. Also: unused/duplicate deps, CPM hygiene (missing `<clear />`, `PackageSourceMapping`, unpinned transitive versions), env var sprawl.

6. **Performance & resource hygiene** — N+1 queries, sync-over-async (`.Result`, `.GetAwaiter().GetResult()`), `async void` (non-handler), `new HttpClient()` over `IHttpClientFactory`, blocking I/O on hot paths, uncleaned handles, missing `Span<T>`/`Memory<T>` opportunities.

7. **Error handling & observability** — swallowed exceptions, blanket catches, log-but-don't-handle, inconsistent error shapes, missing structured logs on critical paths. Structural gaps: traces without span correlation, logs with no correlation ID, metrics with unbounded label cardinality.

8. **Security hygiene** — hardcoded secrets, string-concat SQL, missing input validation at trust boundaries, permissive auth/CORS, weak crypto, `DateTime.Now`/`UtcNow` over injected `TimeProvider`. FIPS violations (see dotnet-dimensions.md).

9. **Documentation drift** — README claims that don't match reality, comments contradicting adjacent code, public APIs without XML doc comments.

10. **Code quality & maintainability** — cognitive complexity hotspots (penalizes nesting depth, not just branch count), deep nesting, magic numbers/strings, inconsistent error types. SATD: age distribution via `git log -S "TODO"`, author attribution, category (design compromise vs missing test vs known bug) — not just a TODO/FIXME count.

11. **Hotspot density** — files with both high complexity AND high change frequency. Cross-reference `tda-file-metrics.sh` intersection output with complexity scores.

12. **Temporal / change coupling** — files always changed together across commits with no explicit reference relationship. Use `tda-vcs-analysis.sh` co-change pairs output.

13. **Knowledge concentration (bus factor)** — files where a single developer authored 80%+ of recent commits. Use `tda-vcs-analysis.sh` author concentration output.

14. **Package / assembly coupling metrics** — Robert C. Martin's package metrics: Afferent Coupling (Ca), Efferent Coupling (Ce), Instability (I = Ce/(Ca+Ce)), Abstractness (A = abstract types / total types), Distance from Main Sequence (D = |A + I − 1|). Flag assemblies in the Zone of Pain (I≈0, A≈0) and Zone of Uselessness (I≈1, A≈1). Threshold: D > 0.7. Compute from Roslyn MCP `get_project_graph` + `get_public_api`.

15. **Service contract & API drift** — drift between published API contracts and actual runtime behavior. Missing `PublicApiAnalyzers`. Undocumented breaking changes. Use `tda-detect-dimensions.sh` → `SERVICE_CONTRACT` output.

16. **Fitness function coverage** — architectural properties verified by CI, not just unit tests. Use `tda-detect-dimensions.sh` → `Fitness Function Coverage` output.

### .NET-Specific Dimensions (conditional)

Run the detection script first to determine which conditional dimensions apply:
```bash
.claude/skills/tech-debt-audit/scripts/tda-detect-dimensions.sh [scope]
```

Only evaluate dimensions the script reports as `ACTIVE`. See [references/dotnet-dimensions.md](references/dotnet-dimensions.md) for severity tables and what to flag for each active dimension.

## Phase 3: Deliverable

Write to `plans/TECH_DEBT_AUDIT.md` with this structure:

- **Executive summary** — max 10 bullets, ranked by severity (CRITICAL → HIGH → MEDIUM → LOW).
- **Architectural mental model** — your understanding of the system as it actually is.
- **Findings table** — columns: `ID | Category | File:Line | Severity | Effort | Description | Recommendation`. Aim for 30–80 findings; padding past that is noise.
- **Top 5 "if you fix nothing else, fix these"** — with concrete diff sketches or refactor outlines, not vague advice.
- **Quick wins** — Low effort × Medium+ severity, as a checklist.
- **Things that look bad but are actually fine** — calls you considered flagging and chose not to, with reasoning. **This section is required.** If it's empty, you didn't look hard enough.
- **Open questions for the maintainer** — things you couldn't tell were debt vs. intentional.

## Rules

- If unsure whether something is debt or intentional, ask in open questions — don't assert.
- Recommend specific, scoped changes, not rewrites.
- If a category has nothing material, write "Nothing material" and move on.

## Tooling

### Co-located scripts (run these instead of inline bash)

| Script                     | Purpose                                    | Args                     |
| -------------------------- | ------------------------------------------ | ------------------------ |
| `scripts/tda-vcs-analysis.sh`    | Hotspots, co-change, SATD, author concentration | `[months] [top_n] [scope]` |
| `scripts/tda-file-metrics.sh`    | Largest files, churn, intersection         | `[months] [top_n] [scope]` |
| `scripts/tda-detect-dimensions.sh` | Conditional dimension detection          | `[scope]`                |
| `scripts/tda-nuget-health.sh`    | Vulnerable + outdated + deprecated (parallel) | (none)               |

All paths are relative to `.claude/skills/tech-debt-audit/`.

### Roslyn MCP (when available)

```
mcp__cwm-roslyn-navigator__find_dead_code scope=solution
mcp__cwm-roslyn-navigator__detect_circular_dependencies scope=projects
mcp__cwm-roslyn-navigator__get_project_graph
```

### Build warnings

```bash
scripts/build.sh --warnings
```

If a tool isn't installed, note it in the audit and move on rather than blocking.

## Large repos: spawn subagents

If the repo is >50k LOC or has >5 top-level projects, dispatch subagents (Agent tool) in parallel — one per module — and synthesize their reports. Serial reading on a large repo eats the context window before findings can be written.

Each subagent gets: scope (one module), the dimensions list above, the citation requirement, and a 200-finding cap. The main agent merges, dedupes, and ranks.

## Repeat-run mode

If `plans/TECH_DEBT_AUDIT.md` already exists:

1. **Read the existing audit** — extract all previous findings with their IDs and severities
2. **Re-evaluate each finding** — check if the cited `file:line` still exists and the issue persists
3. **Mark findings**:
   - `RESOLVED` — the file/line no longer exists OR the code has been fixed
   - `UNCHANGED` — the finding still applies at the same location
   - `MOVED` — the same issue exists but moved to a different line (update the citation)
   - `NEW` — tag all new findings discovered this run
4. **Update timestamps** — change the `Generated:` date header
5. **Preserve IDs** — keep the same F001, F002... IDs for unchanged findings; assign new sequential IDs for NEW findings
6. **Recalculate summary** — update the severity counts in executive summary

This turns the audit into a living document tracked over time.

---

**Human guide:** [references/human-guide.md](references/human-guide.md) — installation, usage, philosophy, adaptation notes, limitations.

## Self-Improvement

At the end of every run, build a feedback payload and spawn `skill-self-updater` only if the payload is non-empty.

### Payload structure

```markdown
## Self-Improvement Report: tech-debt-audit
*Run: {one-line description of what was done}*

### Errors Encountered
- {error type}: {root cause} — {what triggered it}

### User Corrections / Redirects
- {what the user corrected} — {what assumption in SKILL.md was wrong}

### Undocumented Edge Cases
- {input pattern or code state not covered by SKILL.md} — {how it was handled} — {suggested addition}
```

**Spawn condition**: ≥1 entry in any section → spawn `skill-self-updater` with this block as the prompt argument.
**Skip condition**: All sections empty (clean run) → do not spawn.
