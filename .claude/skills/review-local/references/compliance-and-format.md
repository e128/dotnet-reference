# Project Compliance Rules & Report Format

## Project-Specific Compliance Rules

Include this repo's house-style rules verbatim in the compliance agent prompts (in addition to the agent's built-in checks). Populate the block with conventions that the repo's analyzers do not already enforce. Example:

```
PROJECT-SPECIFIC RULES (example — replace with this repo's conventions):

Test code (rules NOT caught by analyzers — analyzer-enforced rules omitted):
  [MEDIUM] "Arrange"/"Act"/"Assert" comments in tests — house style forbids these.
  [MEDIUM] ConfigureAwait() in test code (forbidden in test projects).
```

## Build Validator as Tiebreaker

The `build-validator` agent (in build mode) is the **authoritative source** for warnings and errors. If it reports 0 warnings:

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
[build-validator (build mode)]
  • src/MyProject/Security/AuthHelper.cs:45
    Hardcoded credentials detected — store in secure configuration
[build-validator (build mode)]
  • Build failed: 2 errors, 3 test failures

═══════════════════════════════════════════════════════
  ❌ ACTION REQUIRED — do not merge until resolved
═══════════════════════════════════════════════════════
Exit Code: 2 (CRITICAL issues found)
```

HIGH/MEDIUM/LOW sections follow the same pattern, grouped by agent within each severity.
