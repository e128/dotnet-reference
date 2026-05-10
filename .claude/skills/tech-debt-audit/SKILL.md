---
name: tech-debt-audit
description: >
  Tech debt and architecture audit for .NET repos. Produces plans/TECH_DEBT_AUDIT.md with file-cited
  findings, severity, and effort estimates. Supports module scoping. Does not auto-invoke.
when_to_use: >
  tech debt audit, debt audit, codebase health check, architecture review,
  code quality assessment, audit tech debt, tech debt scan.
argument-hint: "[scope path] -- e.g., src/Core to audit only that module"
---

# Tech Debt Audit

A Claude Code skill that conducts a deliberate, opinionated audit of a .NET codebase and produces `plans/TECH_DEBT_AUDIT.md` with cited findings.

When invoked via `/tech-debt-audit [scope]`, follow the protocol below. Everything from here through the `---` divider is the protocol Claude executes. The section after the divider is documentation for humans.

## Arguments

| Argument        | Required | Description                                                                  |
| --------------- | -------- | ---------------------------------------------------------------------------- |
| `[scope path]`  | No       | Limit audit to a specific directory (e.g., `src/Core`). Full repo if omitted |

---

## Operating principles

Find what's actually wrong. Not diplomatic. Not surface-only. Don't pattern-match to generic best practices without grounding in this specific repo. No sycophancy. No "overall the codebase is well-structured" filler.

Cite `file:line` for every concrete finding. Vague claims like "the code generally..." don't count. Read code before judging it — a pattern that looks wrong in isolation may be load-bearing.

## Phase 1: Orient

Do not skip this. Forming opinions before understanding the system produces bad audits.

1. Read the README, `Directory.Packages.props`, solution file(s), and any architecture docs in `lode/` or `docs/`.
2. Map the directory structure and identify the major projects / layers.
3. Run `git log --oneline -200` and `git log --stat --since="6 months ago"` to see what's actually changing and where churn concentrates.
4. Identify entry points, hot paths, and cold corners.
5. List the top 20 largest `.cs` files by line count, and the 20 files most frequently modified in the last 6 months. The intersection is where debt usually hides.
6. Run VCS-based analysis for behavioral dimensions — hotspot candidates, author concentration, co-change pairs, SATD age attribution. See "VCS-based analysis" in the tooling section. This data feeds dimensions 11–13 and enriches dimension 10.
7. Use `TaskCreate` to publish a plan so the user can see progress through the phases.

Write a 1–2 paragraph mental model of the architecture before proceeding. If your model contradicts the README, flag it — that itself is a finding.

## Phase 2: Audit across these dimensions

Use `rg`, `fd`, Roslyn MCP (when available), and `dotnet` CLI tools to find concrete examples. Cite `path/to/file:LINE` for every finding. If scope is provided, limit analysis to that directory.

### Core Dimensions

1. **Architectural decay** — circular deps, layering violations, god files (>500 LOC) and god functions, duplicated logic across 3+ sites where an abstraction should exist, abstractions that exist but nobody uses, dead code (unused exports, unreachable branches, stale commented-out blocks).

2. **Consistency rot** — multiple ways of doing the same thing (HTTP clients, error handling, logging, config loading, validation, date handling, serialization). Naming drift. Folder structure that no longer reflects what the code actually does.

3. **Type & contract debt** — `object` / `dynamic` as type erasure. Untyped API boundaries. Missing schema validation at trust boundaries. Misuse of `string` where a strongly-typed value should exist.

4. **Test debt** — run coverage if available; identify gaps on critical paths. Tests that assert implementation rather than behavior. Skipped or flaky tests. High-churn files with no tests. Reflection usage in tests instead of `InternalsVisibleTo`.

5. **Dependency & config debt** — `dotnet list package --vulnerable --include-transitive`. Unused deps. Duplicate deps doing the same job. CPM hygiene: missing `<clear />`, missing `PackageSourceMapping`, unpinned transitive versions. Env var sprawl (referenced but not documented).

6. **Performance & resource hygiene** — N+1 queries, sync-over-async (`.Result`, `.GetAwaiter().GetResult()`), `async void` (non-event-handler), `new HttpClient()` instead of `IHttpClientFactory`, blocking I/O on hot paths, uncleaned handles, unnecessary serialization, missing `Span<T>`/`Memory<T>` opportunities.

7. **Error handling & observability** — swallowed exceptions, blanket catches, errors logged but not handled, inconsistent error shapes across modules, missing structured logs on critical paths. Structural observability gaps: traces without span correlation, logs with no correlation ID, metrics with unbounded label cardinality.

8. **Security hygiene** — hardcoded secrets, string-concat SQL, missing input validation at trust boundaries, permissive auth or CORS, weak crypto, `DateTime.Now`/`DateTime.UtcNow` instead of injected `TimeProvider`.

9. **Documentation drift** — README claims that don't match reality, comments that contradict adjacent code, public APIs without XML doc comments.

10. **Code quality & maintainability** — cognitive complexity hotspots (penalizes nesting depth, not just branch count), deep nesting, magic numbers/strings, inconsistent error types. SATD analysis: not just TODO/FIXME count, but age distribution via `git log -S "TODO"`, author attribution, and category (design compromise vs missing test vs known bug).

11. **Hotspot density** — files with both high complexity AND high change frequency. A complex file untouched for two years is low risk; the same complexity in a file with 3+ commits per sprint is a productivity tax paid constantly. Cross-reference Phase 1 churn data with complexity scores.

    **Detection:**
    ```bash
    git log --since="6 months ago" --name-only --pretty=format: | sort | uniq -c | sort -rn | head -30
    ```

12. **Temporal / change coupling** — files always changed together across commits that have no explicit reference relationship. High co-change frequency between unrelated modules reveals hidden architectural entanglement.

    **Detection:**
    ```bash
    git log --since="6 months ago" --name-only --pretty=format:"---" | awk '/^---$/{if(NR>1) for(i in files) for(j in files) if(i<j) print files[i], files[j]; delete files; next} NF{files[$0]=$0}'
    ```

13. **Knowledge concentration (bus factor)** — files or subsystems where a single developer authored 80%+ of recent commits. Flag files where the primary author has left the team or project.

    **Detection:**
    ```bash
    git log --since="12 months ago" --format="%ae" -- <file> | sort | uniq -c | sort -rn
    ```

14. **Package / assembly coupling metrics** — Robert C. Martin's package metrics: Afferent Coupling (Ca), Efferent Coupling (Ce), Instability (I = Ce/(Ca+Ce)), Abstractness (A = abstract types / total types), Distance from Main Sequence (D = |A + I − 1|). Flag assemblies in the Zone of Pain (I≈0, A≈0) and Zone of Uselessness (I≈1, A≈1). Threshold: D > 0.7.

    **Detection:** Compute from project reference graph. Roslyn MCP `get_project_graph` + `get_public_api` can approximate it.

15. **Service contract & API drift** — drift between published API contracts (OpenAPI specs, protobuf schemas, NuGet package public APIs) and actual runtime behavior. Missing `PublicApiAnalyzers`. Undocumented breaking changes. Missing consumer-driven contract tests.

16. **Fitness function coverage** — architectural properties verified by CI, not just unit tests. Layer violation tests, namespace rules, assembly size budgets, circular dependency prevention. Flag constraints that exist only in documentation with no CI enforcement.

    **Detection:**
    ```bash
    rg "NetArchTest|ArchUnitNET|ArchTest" . -l
    rg "Architecture|LayerViolation|FitnessFunction" tests/ -l
    ```

### .NET-Specific Dimensions (conditional)

See [references/dotnet-dimensions.md](references/dotnet-dimensions.md) for full dimension tables with severity mappings. These are evaluated only when the relevant technology is detected:

- **AOT & Trimming** — when `PublishAot=true` or `IsAotCompatible=true` present
- **Blazor WASM Health** — when `Microsoft.NET.Sdk.BlazorWebAssembly` SDK present
- **Data / Schema Debt** — when EF Core referenced
- **Cloud / Container Readiness** — when Dockerfiles or container config present
- **Analyzer Configuration** — `.editorconfig` vs `.globalconfig` drift, suppression sprawl

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

- Cite `file:line` for every concrete finding.
- If unsure whether something is debt or intentional, ask in the open questions section — don't assert.
- Don't recommend rewrites. Recommend specific, scoped changes.
- Don't pad. If a category has nothing material, write "Nothing material" and move on.
- No sycophancy. Tell the user what's broken.

## .NET Tooling

```bash
# Package vulnerabilities and staleness
dotnet list package --vulnerable --include-transitive
dotnet list package --outdated --include-transitive

# Dead code detection (if Roslyn MCP available)
mcp__cwm-roslyn-navigator__find_dead_code scope=solution

# Circular dependencies
mcp__cwm-roslyn-navigator__detect_circular_dependencies scope=projects

# Project dependency graph (for package coupling metrics D14)
mcp__cwm-roslyn-navigator__get_project_graph

# Build warnings (use repo scripts when available, else raw dotnet build)
scripts/build.sh --warnings 2>/dev/null || dotnet build --no-incremental 2>&1 | grep -i warning
```

## VCS-based analysis

Run these in Phase 1 to feed dimensions 11-13.

```bash
# Hotspot candidates: change frequency per file (last 6 months)
git log --since="6 months ago" --name-only --pretty=format: | sort | uniq -c | sort -rn | head -40

# Author concentration per high-churn file
git log --since="12 months ago" --format="%ae" -- <file> | sort | uniq -c | sort -rn

# SATD age attribution (oldest unresolved TODOs)
git log -S "TODO" --format="%H %ad %ae" --date=short -- "*.cs"

# Co-change pairs (temporal coupling)
git log --since="6 months ago" --name-only --pretty=format:"---" -- "*.cs" | awk '/^---$/{if(NR>1) for(i in files) for(j in files) if(i<j) print files[i], files[j]; delete files; next} NF{files[$0]=$0}' | sort | uniq -c | sort -rn | head -20
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
