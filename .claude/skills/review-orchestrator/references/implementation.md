# Code Review Implementation

Step-by-step implementation details for the code-review skill orchestration.

## Steps

1. **Parse arguments**:
   - Extract `--commits N` or `--days N` (required)
   - Extract `--dry-run` flag (optional)
   - Extract `--min-severity LEVEL` (optional)
   - Validate: N must be positive integer
   - If invalid: show usage and exit

2. **Discover changed files**:
   ```bash
   # For --commits N or --days N: structured output includes changed files, affected projects, stats
   scripts/diff.sh --json
   ```
   - Filter to: `.cs`, `.csproj`, `.slnx`, `.props` extensions
   - Handle errors: no git, shallow clone, empty results

3. **Detect and exclude mechanical commits**:

   A mechanical commit is one where all changed lines are pure namespace renames — every `+`/`-` pair differs only in a namespace prefix (e.g., `OldName.*` → `NewName.*`). These don't need code review — they're text substitution.

   Detection heuristic:
   ```bash
   # Use diff.sh --json output: inspect recent_commits[].message for namespace rename markers
   scripts/diff.sh --json | jq '.recent_commits[] | select(.message | test("(?i)rename namespace|namespace rename"))'
   ```

   For detected mechanical commits, generate a diff of *only those commits* and exclude those file hunks from the full diff passed to agents. Add to report header:
   ```
   Excluded: N files — mechanical namespace rename (commit abc1234)
   ```

   This prevents a single namespace rename from inflating a 30KB diff into a 750KB diff.

4. **Generate unified diff for the review window**:
   ```bash
   # diff.sh --json returns staged/unstaged stats, recent commits, affected projects,
   # and test coverage hints in one structured call. For commit-range or days-scoped diffs,
   # pass flags as supported (see diff.sh --help for current flag set).
   scripts/diff.sh --json
   ```
   - This diff is the **primary input** for all agents — it scopes review to only what changed
   - If the diff is very large (>50KB), split by file and assign relevant subsets to each agent
   - Store diff output for reuse across agent prompts (don't regenerate per agent)

   **Diff delivery strategy** (choose based on per-agent slice size):
   - **≤30KB**: Pass the diff **inline in the agent prompt** (zero tool calls to read).
   - **30–40KB**: Write to `.claude/tmp/cr-<agent>.diff` — agents Read these directly.
   - **>40KB**: **Split into sub-slices** at file boundaries (see Sub-Slicing below).
   - **Never use /tmp/** — subagents have Read blocked for /tmp paths.
   - Clean up `.claude/tmp/cr-*.diff` files after all agents complete.

   **Sub-slicing algorithm** (for per-agent slices >40KB):
   1. Parse the diff into per-file hunks (split on `^diff --git`)
   2. Greedily pack files into sub-slices, each ≤40KB, respecting file boundaries
      (never split a single file's diff across sub-slices)
   3. Name sub-slices sequentially: `cr-<agent>-1.diff`, `cr-<agent>-2.diff`, etc.
   4. Spawn one agent instance per sub-slice (e.g., `performance-1`, `performance-2`)
   5. Each sub-slice agent gets the same prompt, severity rules, and diff constraint
   6. In the report, **merge findings** from all sub-slices under the original agent name
   7. If a single file's diff alone exceeds 40KB, deliver it as-is in its own sub-slice
      — the agent will do its best with a large single-file diff

   **Why 40KB?** Agents on Sonnet have ~15 tool call turns. Reading a >40KB diff via
   the Read tool consumes turns for pagination. Inline delivery avoids this entirely
   for ≤30KB. The 30–40KB band works via a single Read call. Above 40KB, agents
   waste too many turns on I/O and never reach the analysis phase.

4. **Discover code review agents dynamically**:
   - List files in `.claude/agents/`
   - Read agent `.md` files to get descriptions
   - Filter using keyword matching:
     - **Include** if description contains: `code`, `review`, `check`, `fix`, `validate`, `compliance`, `security`, `quality`, `refactor`, `build`, `test`, `warning`, `diagnostic`, `concurrency`, `performance`
     - **Exclude** if description contains: `pipeline`, `sanitizer`, `lode`, `corpus`, `mhtml`, `markdown`, `web`, `fetch`, `download`
   - Result: list of agent names to invoke

5. **If --dry-run**:
   - Show which files would be reviewed
   - Show which agents would run
   - Show expected severity types per agent
   - Exit with code 0

6. **Spawn agents in parallel**:
   - Use Agent tool with one call per agent, `run_in_background: true`
   - Pass to each agent:
     - The **unified diff** (primary input — agents review this, not whole files)
     - List of changed file paths (for context)
     - Severity classification rules
     - The diff-scoping constraint (see below)
     - Request: format output as `[SEVERITY] file:line: message`
   - Use `model: "sonnet"` for all agents (except build-validator uses haiku)
   - **If a slice was sub-sliced** (step 3), spawn one agent instance per sub-slice
     with suffix `-1`, `-2`, etc. (e.g., `dotnet-performance-analyst-1`)
   - Collect all TaskOutput results
   - **Merge sub-slice findings**: combine findings from all sub-slice agents under
     the original agent name in the report (step 8)

   **Diff-scoping constraint** (include verbatim in every agent prompt):
   ```
   IMPORTANT: Review ONLY the diff provided below. Do not use Read or Grep
   to investigate files beyond these changes. If a changed line calls into
   existing code that looks suspicious, note it as "adjacent concern — not
   in diff" but do NOT spend tool calls reading that code. Your tool budget
   is limited; spend it analyzing the diff, not exploring the codebase.
   ```

   **Why this matters**: Without the diff constraint, agents read entire files
   to "understand context" and exhaust their tool budget before producing a
   report. The diff gives them exactly the context they need — changed lines
   plus surrounding context from git's unified format.

7. **Include project-specific compliance rules** in all compliance agent prompts:

   ```
   E128 PROJECT RULES:
   [HIGH] Missing [Trait("Category", "CI")] on any [Fact] or [Theory]
   [HIGH] DiagnosticDescriptor without matching AnalyzerReleases entry
   [HIGH] CodeFixProvider without corresponding Analyzer tests
   [MEDIUM] "Arrange"/"Act"/"Assert" comments in test methods (house style)
   [MEDIUM] Assert.Contains/DoesNotContain without StringComparison.Ordinal
   [MEDIUM] Test method body exceeding 60 lines (MA0051)
   ```

8. **Apply build-validator tiebreaker** after collecting all agent results:

   If `build-validator` reports **0 warnings and 0 errors**:
   - Downgrade any MEDIUM/LOW compliance findings to advisory: append "(advisory — build clean)"
   - Only escalate these findings if a human confirms them by checking the actual file
   - Rationale: diff-based agents can report false line numbers from diff context lines; the build catches real warnings

   If `build-validator` reports warnings/errors: those are ground truth regardless of other agents.

   **xUnit v3 test filter note**: This project uses xUnit v3 with MTP which does NOT support `--filter`
   or `--filter-trait` via raw `dotnet test`. Use `scripts/test.sh --all --json` instead
   (it handles the MTP filter internally).

9. **Parse agent outputs**:
   - Extract findings: `[SEVERITY] file:line: message`
   - If no severity marker: default to MEDIUM
   - Group findings by severity: CRITICAL, HIGH, MEDIUM, LOW
   - Sort within each severity by agent, then by file path

8. **Build report**:
   - Header: scope, files reviewed, agents run, summary stats
   - Sections by severity (CRITICAL first, LOW last)
   - Within each severity: subgroup by agent source
   - Format with colors/bold for terminal
   - File paths should be clickable if terminal supports it

9. **Filter by --min-severity** (if provided):
   - Remove findings below threshold
   - Example: `--min-severity HIGH` removes MEDIUM and LOW

10. **Add actionable next steps**:
    - If CRITICAL: "Do not merge — fix critical issues first"
    - If HIGH: "Fix high-priority issues before merge"
    - If MEDIUM only: "Code is mergeable, but consider addressing medium issues"
    - If LOW/None: "Code looks good!"

11. **Determine exit code**:
    - Exit 0: No issues OR only LOW severity
    - Exit 1: MEDIUM or HIGH severity found
    - Exit 2: CRITICAL severity found

12. **Output report and exit**

---

## Component-Scoped Review (No Git Diff)

When reviewing a component without a git diff boundary (e.g., "review all sanitizers" or validating after a bulk fix), use this alternative to the git-diff-based flow:

1. **Enumerate source files** with `Glob` — e.g., `src/MyProject/Services/*.cs`
2. **Measure total size** (rough estimate: count files × average file size)
3. **Group into ≤40KB agent slices** at file boundaries — assign a specific file list to each agent
4. **Pass explicit file paths** to each agent with this instruction:
   ```
   Read the following files and review them for [concern].
   Files to review: [list of paths]
   Do not read files outside this list.
   ```
5. **Pre-grep patterns** for known search patterns (e.g., `[GeneratedRegex]`, `ConfigureAwait`, `Assert.Contains`) before spawning agents — use `Grep` in the orchestrator and pass findings inline to the relevant agent. This avoids each agent spending turns on the same grep.
6. **Merge findings** the same as the diff-based flow — group by severity, apply tiebreakers.

**When to use:** After bulk fixes (verify all instances fixed), for component health checks not tied to a git range, or when `git diff` is empty but a review is still needed.

**Size guidance:** A typical service class is ~80–120 lines (~3–4KB). 50 files ≈ 200KB — split into ~6 agents of 8–10 files each.
