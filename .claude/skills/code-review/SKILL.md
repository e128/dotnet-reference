---
name: code-review
description: >
  Orchestrates all code review agents to audit recent .NET changes. Accepts --commits N or --days N
  to define review scope, discovers agents dynamically, runs them in parallel, and produces a
  severity-grouped report with actionable recommendations.
  Triggers on: code review, review code, review changes, audit code, check code quality,
  clanker check, state bloat, grab bag review, check for flag bloat, mutation review,
  clanker discipline.
effort: high
---

# Code Review Skill

Comprehensive code review using all available agents. Discovers changed .NET files from git history, dynamically identifies code review agents, runs them in parallel, and produces a consolidated report grouped by severity.

## Usage

```
# User says any of these (see keyword-shortcuts.md):
code review days 1
review last 7 days
review last 5 commits
code review commits 3

# Modifiers (append to any phrase above):
dry run              — preview which files and agents would run
critical only        — filter report to CRITICAL findings
high and above       — filter to HIGH+ findings
```

## Scope Parameters

The user's message must include a scope. Extract it from natural language:

| Pattern in user message          | Scope                                                   |
|----------------------------------|---------------------------------------------------------|
| `days N` / `last N days`         | `scripts/diff.sh --json` (days-scoped discovery) |
| `commits N` / `last N commits`   | `scripts/diff.sh --json` (commit-range discovery) |
| `dry run` / `preview`            | Discovery only, no agents                               |
| `critical only` / `high+`        | Filter report by min severity                           |

If no scope is provided, ask the user for one.

## How It Works

### Phase 1: Discovery

1. **Parse arguments** from user input
2. **Gather change context** — run `scripts/diff.sh --json` for working-tree intelligence (staged/unstaged files, affected projects, test coverage hints, and commit-range changed files).
3. **Filter to .NET files**: `.cs`, `.csproj`, `.slnx`, `.props`
4. **Detect mechanical commits** — if all changes in the diff are pure namespace renames (every `+`/`-` line is identical except for a namespace prefix substitution), mark those files as `MECHANICAL` and exclude them from deep agent review. Add a one-line note to the report: "N files excluded: namespace rename only." This prevents a single mechanical refactor from inflating the diff 10-20x and wasting agent budget.
5. **Generate unified diff** for the review window — this is the primary input for all agents
6. **Discover agents dynamically**:
   - Read `.claude/agents/` directory
   - Parse agent `.md` files for descriptions
   - Filter to code-review-relevant agents using keywords
   - Exclude non-code-review agents (pipeline, lode, corpus tools)

### Phase 2: Execution

1. **Spawn agents in parallel** — pass each agent the unified diff and severity rules
2. **Diff-scoped constraint** — include in every agent prompt: "Review ONLY the diff provided. Flag pre-existing concerns as 'adjacent concern' without investigating."
3. **Diff delivery by size**: ≤30KB inline in prompt; 30–40KB write to `.claude/tmp/cr-<agent>.diff`; >40KB split at file boundaries into sub-slices with separate agent instances. Never use `/tmp/`. Clean up `.claude/tmp/cr-*.diff` after completion.
4. **Collect outputs** and handle failures gracefully (timeout, error)

### Phase 3: Report Generation

1. **Parse agent findings** for severity markers
2. **Group by severity** (CRITICAL → HIGH → MEDIUM → LOW)
3. **Format for terminal** (colors, indentation, clickable paths)
4. **Add summary statistics** (files reviewed, issues by severity)
5. **Provide actionable next steps** based on highest severity. Additionally, check whether any finding pattern matches a `new` entry in `lode/dotnet/analyzer-candidates.md` (read file if not already loaded). If a match exists, append:
   > "Pattern '{pattern}' found in review findings — matches analyzer candidate (score N/5). Consider evaluating E128 analyzer candidacy."
6. **Cross-reference with deeper audits** — if findings include `#pragma warning disable`, code duplication, or high-complexity methods, append:
   > "Deeper analysis available: run `/code-health-audit suppressions|duplicates|crap` for a full audit."
7. **Save report** to `.claude/tmp/code-review-latest.md` (enables `review-applier` agent to batch-apply findings)

### Phase 4: Exit

1. **Offer batch fixes** — if CRITICAL or HIGH issues were found, prompt before invoking the `review-applier` agent. Skip this if `--dry-run` was passed or if only LOW issues were found.

   Print:
   > "CRITICAL/HIGH issues found — run review-applier to batch-apply fixes? (yes/no)"

   Wait for confirmation before proceeding. The review-applier modifies files — this is not a read-only operation.

2. **Spawn analyzer-review-miner in the background** — always, unconditionally, after every review:
   ```
   Agent(subagent_type="analyzer-review-miner", run_in_background=true,
         prompt="Code review just completed. Mine the last 3 days of git diffs and the
                 saved review report at .claude/tmp/code-review-latest.md for Roslyn
                 analyzer candidates. Update lode/dotnet/analyzer-candidates.md and
                 ask once before creating a pug plan for any candidate scoring >= 3.")
   ```
   Do not wait for the result. The agent runs silently and reports back when done.

3. **Auto-invoke dev-planning when CRITICAL findings are present** — if the report contains any CRITICAL findings (regardless of whether review-applier ran), invoke the `dev-planning` skill with:
   - Feature name: `code-review-{YYYY-MM-DD}` (derive date from `scripts/ts.sh`)
   - Feature description: paste the full findings list (all CRITICAL + HIGH items), scoped files, and the exit code from step 4 below as context
   
   This is automatic — no user confirmation needed. CRITICAL findings always warrant a tracked plan.
   Skip this step if `--dry-run` was passed.

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

**Include agents with keywords:** `code`, `review`, `check`, `fix`, `validate`, `compliance`, `security`, `quality`, `refactor`, `build`, `test`, `warning`, `diagnostic`, `concurrency`, `performance`

**Exclude agents with keywords:** `pipeline`, `lode`, `corpus`, `mhtml`, `markdown`, `web`, `fetch`, `download`

**Example agents (not hardcoded — discovered dynamically):**
- `refactoring-specialist` — code quality
- `build-validator` — build + test verification

New agents added to `.claude/agents/` are automatically discovered if relevant.

## Project-Specific Compliance Rules

Include these rules in compliance agent prompts (in addition to the agent's built-in checks):

```
E128-SPECIFIC RULES (include in every compliance agent prompt):

Test code (rules NOT caught by analyzers — analyzer-enforced rules omitted):
  [MEDIUM] "Arrange"/"Act"/"Assert" comments in tests — house style forbids these.

Analyzer code:
  [HIGH] DiagnosticDescriptor without matching AnalyzerReleases entry — every new
         rule must appear in AnalyzerReleases.Unshipped.md.
  [HIGH] CodeFixProvider without corresponding Analyzer tests — both analyzer and
         code fix must have test coverage.
```

## Build Validator as Tiebreaker

The `build-validator` is the **authoritative source** for warnings and errors. If it reports 0 warnings:

- MEDIUM/LOW compliance findings from diff-based agents are **advisory only** — flag them with "(needs verification — build reports 0 warnings)"
- The build cannot lie; diff-based agents can miscalculate line numbers from diff context

This matters most when compliance agents report violations at line numbers that don't exist in the actual file (a known diff-parsing artifact). Always cross-reference with the build result before escalating compliance findings.

## Example Report Format

```
═══════════════════════════════════════════════════════
  CODE REVIEW REPORT
═══════════════════════════════════════════════════════
Scope: Last 5 commits | Files: 12 | Agents: 6
Issues Found: 3 CRITICAL, 7 HIGH, 12 MEDIUM, 5 LOW

───────────────────────────────────────────────────────
  ❌ CRITICAL ISSUES (3)
───────────────────────────────────────────────────────
[build-validator]
  • src/MyProject/Security/AuthHelper.cs:45
    Hardcoded credentials detected — store in secure configuration
[build-validator]
  • Build failed: 2 errors, 3 test failures

═══════════════════════════════════════════════════════
  ❌ ACTION REQUIRED — do not merge until resolved
═══════════════════════════════════════════════════════
Exit Code: 2 (CRITICAL issues found)
```

HIGH/MEDIUM/LOW sections follow the same pattern, grouped by agent within each severity.

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

**GitHub**: `gh pr comment <number> --body "$(cat .claude/tmp/code-review-latest.md)"` — use `@author-login` for mentions, ATX headers (`##`).

**ADO**: Use `mcp__azure-devops__repo_create_pull_request_thread` with project parameter. Use setext headers, escape `<`/`>` as `&lt;`/`&gt;`, use `@[Display Name]` for mentions. Note: `repo_get_pull_request_by_id` often fails — use `repo_list_pull_requests_by_repo_or_project` as fallback.

## Error Handling

- **No git**: Show error, suggest running in git repository
- **Shallow clone**: Warn user, attempt to work with available history
- **No commits in range**: Show message, exit 0
- **No .NET files changed**: Show message, exit 0
- **No agents found**: Warn user, check `.claude/agents/` directory
- **Agent timeout**: Include in report as "Agent timed out"
- **Agent error**: Include in report as "Agent failed: [error]"

## Notes

- Agents are discovered **dynamically** every run — no hardcoded list
- Report is grouped by **severity**, not by agent
- Exit codes enable CI integration (block merge on CRITICAL findings)

### Known Exceptions (codebase-specific)

See [references/known-exceptions.md](references/known-exceptions.md) for the full list of legitimate patterns that should not be flagged. Includes test conventions, threat model exceptions, sanitizer TextContent/DOM rules, and severity calibration rules.

## Self-Improvement (Mandatory)

This skill must get better with every use. After completing any code review:

1. **Capture new review categories** — If a review uncovered a class of issue not covered by existing agents, add it to the Notes section of this WORKFLOW.md with the agent best suited to catch it, or propose a new agent in `.claude/agents/`.
2. **Refine agent prompts** — If an agent produced low-quality findings (too many false positives, missed obvious issues, or redundant with another agent), update the agent's prompt in `.claude/agents/<name>.md` directly.
3. **Update severity calibration** — If findings were consistently over- or under-classified, adjust the severity mapping guidance in this WORKFLOW.md.
4. **Record codebase-specific exceptions** — If legitimate patterns in this codebase are flagged as issues (e.g., intentional `ConfigureAwait(false)` usage, acceptable suppression patterns), add them to [references/known-exceptions.md](references/known-exceptions.md), grouped by category.

The goal: each review should produce higher signal-to-noise findings because previous reviews refined the agents and severity mappings.

## Troubleshooting

- **No scope in user message** — ask for one (e.g., "How many days or commits should I review?")
- **No .NET files changed in range** — this is a clean result, not an error; report "No .NET files changed" and exit 0
- **No agents discovered** — check that `.claude/agents/` exists and contains `.md` files; agents are filtered by keyword, so a new agent may be excluded if its description lacks code-review keywords
- **Agent times out** — include in report as "Agent timed out"; do not retry; other agents' results are still valid
- **Report is too large** — use `--min-severity HIGH` or `--min-severity CRITICAL` to filter; the full report is available without the flag
