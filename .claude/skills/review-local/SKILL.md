---
name: review-local
description: >
  Orchestrated code review of git diff using parallel agents. Standard mode (diff-scoped)
  or full mode (adds Roslyn semantic analysis). Produces severity-grouped plan.
when_to_use: "dispatched by review router"
argument-hint: "[--full] [days N | commits N] [--perf] [--test-quality] [dry run] [critical only]"
user-invocable: true
---

# Review — Local Mode

Comprehensive code review using orchestrated agents. Discovers changed .NET files from git history, dynamically identifies code review agents, runs them in parallel, and produces a consolidated report grouped by severity.

**Standard mode (default):** Day-to-day review, fast turnaround, diff-scoped agent review only.
**Full mode (`--full`):** Pre-ship review, maximum coverage — adds solution-wide Roslyn semantic analysis (antipatterns + circular dependencies).

## Usage

```
code review days 1            # Standard
review --full last 3 days     # Full 3-vector
dry run                       # Preview only
critical only                 # Filter to CRITICAL
--perf                        # Spawn a performance-focused review agent
--test-quality                # Load enriched test rubric
```

## Scope Parameters

| Pattern in user message          | Scope                                           |
|----------------------------------|-------------------------------------------------|
| `days N` / `last N days`         | `scripts/diff.sh --json` (days-scoped)          |
| `commits N` / `last N commits`   | `scripts/diff.sh --json` (commit-range)         |
| `dry run` / `preview`            | Discovery only, no agents                       |
| `critical only` / `high+`        | Filter report by min severity                   |
| `--perf`                         | Spawn a performance-focused review agent on diff scope |
| `--test-quality`                 | Inject enriched Test Quality Rubric              |

If no scope is provided, ask for one.

## --perf and --test-quality Flags

**--perf:** Spawn a performance-focused review agent on diff scope in addition to normal review agents, using the anti-pattern catalog in [references/perf-patterns.md](references/perf-patterns.md). Findings merged into report.

**--test-quality:** Load enriched Test Quality Rubric from [references/review-rubrics.md](references/review-rubrics.md) and inject into agent prompts for `tests/**/*.cs` files.

## How It Works

### Phase 1: Discovery

Run in parallel — no ordering dependency:
- Parse arguments from user input
- Gather change context: `scripts/diff.sh --json`
- Discover agents dynamically: read `.claude/agents/`; filter to code-review-relevant agents (include: code, review, check, fix, validate, compliance, security, quality, refactor, build, test, warning, diagnostic, concurrency, performance — exclude: pipeline, sanitizer, lode, corpus, mhtml, markdown, web, fetch, download)

From diff: filter to .NET files; detect mechanical commits (namespace rename → `MECHANICAL`, exclude); generate unified diff. Diff delivery by size: <=30KB inline; 30-40KB write to `.claude/tmp/cr-<agent>.diff`; >40KB split at file boundaries. Clean up `.claude/tmp/cr-*.diff` after completion.

### Phase 2: Execution

**Vector 1: Code-Review Agents (always runs)**
Spawn agents in parallel — pass each the unified diff and severity rules. Include: "Review only the diff provided. Flag pre-existing concerns as 'adjacent concern' without investigating." Handle timeouts/errors gracefully.

**Vector 2: Roslyn Navigator (--full only)**
Run MCP calls in main context (session-only):
1. `mcp__cwm-roslyn-navigator__detect_antipatterns` with the solution file
2. `mcp__cwm-roslyn-navigator__detect_circular_dependencies` with the solution file

Severity mapping: Circular (cross-project) → HIGH; Circular (namespace) → MEDIUM; Antipatterns → MEDIUM; Generated code → SKIP; Informational → LOW.

Graceful degradation: if MCP unavailable, note "Roslyn navigator: MCP server not running — skipped".

### Phase 3: Plan Generation

See [references/implementation.md](references/implementation.md) for full orchestration details: dedup algorithm, severity grouping, plan file format (plan.md/tasks.md/context.md), report template, and analyzer-candidate mining integration.

### Phase 4: Exit

1. Offer to execute the plan if CRITICAL or HIGH issues
2. Spawn the `analyzer-review-miner` agent in background to mine findings for new analyzer candidates
3. Return exit code: 0 (clean/LOW), 1 (MEDIUM/HIGH), 2 (CRITICAL)

## Severity Classification

| Severity     | Description              | Examples                                          |
|--------------|--------------------------|---------------------------------------------------|
| **CRITICAL** | Blocks shipping          | Build failures, security vulns, race conditions   |
| **HIGH**     | Must fix before merge    | Analyzer errors, concurrency, perf regressions    |
| **MEDIUM**   | Should fix               | Code smells, refactoring, missing XML docs        |
| **LOW**      | Nice to have             | Style preferences, minor optimizations            |

## Review Rubrics

Seven rubric checklists in [references/review-rubrics.md](references/review-rubrics.md). Inject relevant sections into agent prompts based on diff content. Eighth rubric for multi-file consistency in [references/cross-file-consistency.md](references/cross-file-consistency.md).

## References

- [references/compliance-and-format.md](references/compliance-and-format.md)
- [references/review-rubrics.md](references/review-rubrics.md)
- [references/cross-file-consistency.md](references/cross-file-consistency.md)
- [references/error-handling.md](references/error-handling.md)
- [references/implementation.md](references/implementation.md)
- [references/clanker-patterns.md](references/clanker-patterns.md)
- [references/perf-patterns.md](references/perf-patterns.md)
