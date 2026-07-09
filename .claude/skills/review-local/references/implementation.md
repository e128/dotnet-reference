# Code Review Implementation

Step-by-step implementation for the code-review skill orchestration.

## Steps

1. **Parse arguments** — extract `--commits N` / `--days N` (required), `--dry-run` (optional), `--min-severity LEVEL` (optional). Validate: N must be positive integer. If invalid, show usage and exit.

2. **Discover changed files**:
   ```bash
   scripts/diff.sh --json
   ```
   Filter to `.cs`, `.csproj`, `.props`, and solution-file extensions. Handle errors: no git, shallow clone, empty results.

3. **Detect and exclude mechanical commits** — namespace-rename-only commits (e.g., `ragtool.*` → `Harvest.*`/`Rag.*`) inflate the diff without semantic change. Detection: inspect `recent_commits[].message` for `(?i)rename namespace|namespace rename`. For detected mechanical commits, exclude those file hunks from the agent diff and add `Excluded: N files — mechanical namespace rename (commit abc1234)` to the report header.

4. **Generate unified diff for the review window**:
   ```bash
   scripts/diff.sh --json
   ```
   This diff is the **primary input** for all agents. Store once; reuse across all agent prompts (don't regenerate per agent). If the diff is very large, split by file.

   **Diff delivery strategy** (choose by per-agent slice size):
   - **≤30KB**: Pass the diff **inline in the agent prompt** (zero tool calls to read).
   - **30–40KB**: Write to `.claude/tmp/cr-<agent>.diff` — agents Read these directly.
   - **>40KB**: **Split into sub-slices** at file boundaries.
   - **Never use /tmp/** — subagents have Read blocked for `/tmp` paths.
   - Clean up `.claude/tmp/cr-*.diff` files after all agents complete.

   **Sub-slicing algorithm** (for per-agent slices >40KB):
   1. Parse the diff into per-file hunks (split on `^diff --git`)
   2. Greedily pack files into sub-slices, each ≤40KB, respecting file boundaries (never split a single file's diff across sub-slices)
   3. Name sub-slices sequentially: `cr-<agent>-1.diff`, `cr-<agent>-2.diff`, etc.
   4. Spawn one agent instance per sub-slice (e.g., `performance-1`, `performance-2`)
   5. Each sub-slice agent gets the same prompt, severity rules, and diff constraint
   6. In the report, **merge findings** from all sub-slices under the original agent name
   7. If a single file's diff alone exceeds 40KB, deliver it as-is in its own sub-slice — the agent will do its best with a large single-file diff

   **Why 40KB?** Sub-agents have limited tool call turns. Reading a >40KB diff via the Read tool consumes turns for pagination. Inline delivery avoids this for ≤30KB. The 30–40KB band works via a single Read call. Above 40KB, agents waste too many turns on I/O and never reach the analysis phase.

5. **Discover code review agents dynamically** — list files in `.claude/agents/` and read each agent's description. Filter by keyword:
   - **Include** if description contains: `code`, `review`, `check`, `fix`, `validate`, `compliance`, `security`, `quality`, `refactor`, `build`, `test`, `warning`, `diagnostic`, `concurrency`, `performance`
   - **Exclude** if description contains: `pipeline`, `sanitizer`, `lode`, `corpus`, `mhtml`, `markdown`, `web`, `fetch`, `download`

6. **Spawn agents in parallel** (Agent tool, one call per agent, `run_in_background: true`). Pass to each agent:
   - The **unified diff** (primary input — agents review this, not whole files)
   - List of changed file paths (for context)
   - Severity classification rules
   - The diff-scoping constraint (see below)
   - Request: format output as `[SEVERITY] file:line: message`

   Omit `model` for all agents (inherit from parent context). If a slice was sub-sliced (step 4), spawn one agent instance per sub-slice with suffix `-1`, `-2`, etc. Collect all TaskOutput results, then **merge sub-slice findings** under the original agent name.

   **Diff-scoping constraint** (include verbatim in every agent prompt):
   ```
   IMPORTANT: Review ONLY the diff provided below. Do not use Read or Grep
   to investigate files beyond these changes. If a changed line calls into
   existing code that looks suspicious, note it as "adjacent concern — not
   in diff" but do NOT spend tool calls reading that code. Your tool budget
   is limited; spend it analyzing the diff, not exploring the codebase.
   ```
   Without this constraint, agents read entire files to "understand context" and exhaust their tool budget before producing a report.

7. **Include project-specific compliance rules** in all compliance agent prompts. Populate this block with the house-style and convention rules that this repo's analyzers do not already enforce — for example:
   ```
   PROJECT-SPECIFIC COMPLIANCE RULES (example — replace with this repo's conventions):
   [HIGH] Missing [Trait("Category", "CI")] on any [Fact] or [Theory]
   [MEDIUM] ConfigureAwait() in test code (forbidden in test projects)
   [MEDIUM] "Arrange"/"Act"/"Assert" comments in test methods (house style)
   [MEDIUM] Assert.Contains/DoesNotContain without StringComparison.Ordinal
   [MEDIUM] Test method body exceeding 60 lines (MA0051)
   ```

8. **Apply build-validator tiebreaker** after collecting all agent results. If `build-validator` (build mode) reports **0 warnings and 0 errors**:
   - Downgrade any MEDIUM/LOW compliance findings to advisory: append "(advisory — build clean)"
   - Only escalate these findings if a human confirms them by checking the actual file
   - Rationale: diff-based agents can report false line numbers from diff context lines; the build catches real warnings

   If `build-validator` reports warnings/errors: those are ground truth regardless of other agents.

   **Test run note**: Use `scripts/test.sh` for targeted test runs (it handles filtering internally rather than relying on raw `dotnet test` filter flags). Treat known, pre-existing platform-specific failures (e.g. tests with hardcoded OS paths that only pass on one OS) as PASS.

9. **Parse agent outputs** — extract findings matching `[SEVERITY] file:line: message`. Default to MEDIUM if no marker. Group by severity (CRITICAL, HIGH, MEDIUM, LOW), sort within each by agent then file path.

10. **Build report** — header (scope, files, agents, stats), sections by severity (CRITICAL first), subgroup within each severity by agent source, file paths clickable for the terminal.

11. **Write plan files** (if HIGH+ findings exist and not `--dry-run`):
    - Slug from review scope: normalize keywords like `concurrency`, `security`, `code-quality`. Fallback: `general`.
    - Create `plans/review-{YYYY-MM-DD}-{slug}/` directory
    - Write `{slug}-plan.md` — findings summary table at top, then phase descriptions
    - Write `{slug}-tasks.md` — each finding as a self-contained checkbox with file:line, Fix:, Severity:, Source:
    - Write `{slug}-context.md` — raw agent output, diff summary, vectors run, dedup report
    - Stamp timestamps on the plan files with `scripts/ts.sh`

12. **Filter by --min-severity** (if provided) — remove findings below threshold.

13. **Add actionable next steps**:
    - CRITICAL: "Do not merge — fix critical issues first"
    - HIGH: "Fix high-priority issues before merge"
    - MEDIUM only: "Code is mergeable, but consider addressing medium issues"
    - LOW/None: "Code looks good!"

14. **Determine exit code** — 0: No issues OR only LOW; 1: MEDIUM or HIGH; 2: CRITICAL. Output report and exit.

15. **`--dry-run`** — show which files would be reviewed, which agents would run, expected severity types per agent. Exit 0.

---

## Component-Scoped Review (No Git Diff)

When reviewing a component without a git diff boundary (e.g., "review all sanitizers" or validating after a bulk fix), use this alternative to the git-diff-based flow:

1. **Enumerate source files** with `Glob` — e.g., `src/Harvest/Conversion/*Sanitizer.cs`
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

**Size guidance:** A typical Harvest sanitizer is ~80–120 lines (~3–4KB). 57 sanitizers ≈ 200KB — split into ~6 agents of 8–10 files each.
