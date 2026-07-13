---
name: review-orchestrator
description: >
  Orchestrates all code review agents to audit recent .NET changes. Accepts --commits N or --days N
  to define review scope, discovers agents dynamically, runs them in parallel, and produces a
  severity-grouped report with actionable recommendations.
  Triggers on: code review, review code, review changes, audit code, check code quality,
  clanker check, state bloat, grab bag review, check for flag bloat, mutation review,
  clanker discipline.
effort: high
---

# Review Orchestrator Skill

## Scope Parameters

The user's message must include a scope. Extract it from natural language; if none is provided, ask for one.

| Pattern in user message        | Effect                                            |
|--------------------------------|---------------------------------------------------|
| `days N` / `last N days`       | `scripts/diff.sh --json` (days-scoped discovery)  |
| `commits N` / `last N commits` | `scripts/diff.sh --json` (commit-range discovery) |
| `dry run` / `preview`          | Discovery only, no agents                         |
| `critical only` / `high+`      | Filter report by min severity                     |

## How It Works

### Phase 1: Discovery

1. **Parse arguments** from user input
2. **Gather change context** — run `scripts/diff.sh --json` for working-tree intelligence (staged/unstaged files, affected projects, test coverage hints, and commit-range changed files).
3. **Filter to .NET files**: `.cs`, `.csproj`, `.slnx`, `.props`
4. **Detect mechanical files** — run `scripts/internal/mechanical-diff.sh --json`; files classified `MECHANICAL` (every changed token is a pure namespace-prefix substitution) are excluded from deep agent review. Add a one-line note to the report: "N files excluded: namespace rename only." This prevents a single mechanical refactor from inflating the diff 10-20x and wasting agent budget.
5. **Generate unified diff** for the review window — this is the primary input for all agents
6. **Discover agents dynamically**:
   ```bash
   scripts/internal/review-agents.sh --json
   ```
   Parse the `agents` array — each entry has `name` and `path`. This replaces manual directory reading, description parsing, and keyword filtering.

### Phase 2: Execution

1. **Spawn agents in parallel** — pass each agent the unified diff and severity rules
2. **Diff-scoped constraint** — include in each agent prompt: "Review only the diff provided. Flag pre-existing concerns as 'adjacent concern' without investigating. Report ALL issues you find — including low-confidence and low-severity ones — and tag each with a confidence level and a severity. Do not pre-filter or suppress findings to avoid nitpicking; the orchestrator's severity-grouping stage and the downstream review-applier do the ranking and filtering."
3. **Diff delivery** — run `scripts/internal/cr-diff-deliver.sh <difffile>` per agent slice; it emits the mode (`inline` / `write <path>` / `split <paths…>`) by the 30KB/40KB thresholds, writing to `.claude/tmp/cr-<name>.diff` or splitting at file boundaries into sub-slices for separate agent instances. Never use `/tmp/`. Clean up `.claude/tmp/cr-*.diff` after completion.
4. **Collect outputs** and handle failures gracefully (timeout, error)

### Phase 3: Report Generation

1. **Parse agent findings** for severity markers
2. **Group by severity** (CRITICAL → HIGH → MEDIUM → LOW)
3. **Format for terminal** (colors, indentation, clickable paths)
4. **Add summary statistics** (files reviewed, issues by severity)
5. **Provide actionable next steps** based on highest severity. Additionally, check whether any finding pattern matches a `new` entry in `lode/dotnet/analyzer-candidates.md` (read file if not already loaded). If a match exists, append:
   > "Pattern '{pattern}' found in review findings — matches analyzer candidate (score N/5). Consider evaluating analyzer candidacy."
6. **Cross-reference with deeper audits** — if findings include `#pragma warning disable`, code duplication, or high-complexity methods, append:
   > "Deeper analysis available: run `/tech-debt-audit suppressions|duplicates|crap` for a focused audit."
7. **Save report** to `.claude/tmp/review-orchestrator-latest.md` (enables `review-applier` agent to batch-apply findings)

### Phase 4: Exit

1. **Offer batch fixes** — if CRITICAL or HIGH issues were found, prompt before invoking the `review-applier` agent. Skip this if `--dry-run` was passed or if only LOW issues were found.

   Print:
   > "CRITICAL/HIGH issues found — run review-applier to batch-apply fixes? (yes/no)"

   Wait for confirmation before proceeding. The review-applier modifies files — this is not a read-only operation.

2. **Spawn analyzer-review-miner in the background** — after each review:
   ```
   Agent(subagent_type="analyzer-review-miner", run_in_background=true,
         prompt="Code review just completed. Mine the last 3 days of git diffs and the
                 saved review report at .claude/tmp/review-orchestrator-latest.md for Roslyn
                 analyzer candidates. Update lode/dotnet/analyzer-candidates.md and
                 ask once before creating a pug plan for any candidate scoring >= 3.")
   ```
   Do not wait for the result. The agent runs silently and reports back when done.

3. **Suggest dev-planning for CRITICAL findings** — if the report contains any CRITICAL findings, suggest:
   > "CRITICAL findings detected — consider running `/dev-planning code-review-{date}` to create a tracked plan."
   
   Let the user decide whether to run dev-planning — do not auto-invoke it.
   Skip this suggestion if `--dry-run` was passed.

4. **Return exit code** based on highest severity:
   - **Exit 0**: Clean (no issues or LOW only)
   - **Exit 1**: MEDIUM or HIGH issues
   - **Exit 2**: CRITICAL issues

## Severity Classification

| Severity | Description | Examples |
|----------|-------------|----------|
| **CRITICAL** | Blocks shipping | Build failures, security vulnerabilities, race conditions, data loss risks |
| **HIGH** | Must fix before merge | Analyzer errors, IDE errors, concurrency issues, performance regressions >20% |
| **MEDIUM** | Should fix | Code smells, refactoring opportunities, missing XML docs, analyzer warnings |
| **LOW** | Nice to have | Style preferences, minor optimizations, informational messages |

## Agent Discovery Algorithm

`scripts/internal/review-agents.sh --json` (Phase 1) applies these filters — new relevant agents in `.claude/agents/` are picked up automatically, none are hardcoded:

- **Include keywords:** `code`, `review`, `check`, `fix`, `validate`, `compliance`, `security`, `quality`, `refactor`, `build`, `test`, `warning`, `diagnostic`, `concurrency`, `performance`
- **Exclude keywords:** `pipeline`, `lode`, `corpus`, `mhtml`, `markdown`, `web`, `fetch`, `download`

## Project-Specific Compliance Rules

Include these rules in compliance agent prompts (in addition to the agent's built-in checks):

```
PROJECT-SPECIFIC RULES (include in every compliance agent prompt):

Test code (rules NOT caught by analyzers — analyzer-enforced rules omitted):
  [MEDIUM] "Arrange"/"Act"/"Assert" comments in tests — house style forbids these.

Analyzer code:
  [HIGH] DiagnosticDescriptor without matching AnalyzerReleases entry — every new
         rule must appear in AnalyzerReleases.Unshipped.md.
  [HIGH] CodeFixProvider without corresponding Analyzer tests — both analyzer and
         code fix must have test coverage.
```

## Build Validator as Tiebreaker

The `build-validator` is the **authoritative source** for warnings/errors. If it reports 0 warnings, treat MEDIUM/LOW compliance findings from diff-based agents as **advisory only** — flag them "(needs verification — build reports 0 warnings)". Diff-based agents can miscalculate line numbers from diff context (sometimes citing lines that don't exist in the file), so cross-reference with the build before escalating.

## Example Report Format

```
═══ CODE REVIEW REPORT ═══
Scope: Last 5 commits | Files: 12 | Agents: 6
Issues Found: 3 CRITICAL, 7 HIGH, 12 MEDIUM, 5 LOW

─── ❌ CRITICAL ISSUES (3) ───
[build-validator]
  • src/MyProject/Security/AuthHelper.cs:45
    Hardcoded credentials detected — store in secure configuration
[build-validator]
  • Build failed: 2 errors, 3 test failures

═══ ❌ ACTION REQUIRED — do not merge until resolved ═══
Exit Code: 2 (CRITICAL issues found)
```

HIGH/MEDIUM/LOW sections follow the same pattern, grouped by agent within each severity. Use terminal colors and clickable paths.

## Review Rubrics

Six rubric checklists (Security, SOLID, Pike's Rules, Design Priority Order, Test Quality, Code Reduction) are in [references/review-rubrics.md](references/review-rubrics.md). Read that file and inject the relevant sections into agent prompts based on diff content:

- **Security Checklist** — when diff touches controllers, command handlers, or business logic
- **SOLID Design Review** — when diff introduces new classes, interfaces, or modifies class structure
- **Pike's Rules Review** — every review (catches over-engineering SOLID misses)
- **Design Priority Order** — when diff touches .NET classes, business logic, or data processing
- **Test Quality Rubric** — when diff touches test files
- **Code Reduction Review** — every review (always include)

## Implementation

You are a skill that orchestrates code review agents. See [references/implementation.md](references/implementation.md) for the complete step-by-step implementation (argument parsing, file discovery, agent spawning, output parsing, report building, and exit codes).

## Posting Reviews to PRs

After generating the report, you can post it to a PR:

**GitHub**: `gh pr comment <number> --body "$(cat .claude/tmp/review-orchestrator-latest.md)"` — use `@author-login` for mentions, ATX headers (`##`).

**ADO**: Use `mcp__azure-devops__repo_create_pull_request_thread` with project parameter. Use setext headers, escape `<`/`>` as `&lt;`/`&gt;`, use `@[Display Name]` for mentions. Note: `repo_get_pull_request_by_id` often fails — use `repo_list_pull_requests_by_repo_or_project` as fallback.

## Error Handling

- **No scope in user message** — ask (e.g., "How many days or commits should I review?")
- **No git / shallow clone** — error or warn; work with available history
- **No commits or no .NET files in range** — clean result, not an error; report and exit 0
- **No agents found** — check `.claude/agents/` exists with `.md` files; a new agent is excluded if its description lacks code-review keywords
- **Agent timeout / error** — include in report ("Agent timed out" / "Agent failed: [error]"); do not retry, other agents' results stay valid
- **Report too large** — use `--min-severity HIGH|CRITICAL` to filter

## Known Exceptions (codebase-specific)

See [references/known-exceptions.md](references/known-exceptions.md) for legitimate patterns that should not be flagged: test conventions, threat model exceptions, sanitizer TextContent/DOM rules, and severity calibration rules.

## Self-Improvement

After a review, fold learnings back in:

- **New review categories** → add to a relevant agent prompt or propose a new agent in `.claude/agents/`.
- **Low-quality agent findings** (false positives, misses, redundancy) → edit `.claude/agents/<name>.md`.
- **Mis-calibrated severities** → adjust the severity mapping above.
- **Wrongly-flagged legitimate patterns** → add to [references/known-exceptions.md](references/known-exceptions.md), grouped by category.
