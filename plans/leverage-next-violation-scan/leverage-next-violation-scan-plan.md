# Plan: leverage-next-violation-scan
*Created: 2026-04-11T14:33:35Z*

## Analysis Axis
Addition

## Score Breakdown

| Dimension    | Score | Rationale                                                               |
| ------------ | ----- | ----------------------------------------------------------------------- |
| Novelty      | 3/3   | No existing script scans .claude/ for anti-pattern rule violations      |
| Compound     | 2/3   | Feeds session-health, weekly-learner, and CI pre-commit hooks           |
| User Impact  | 2/3   | Surfaces drift that currently goes unnoticed across sessions             |
| Automation   | 3/3   | Fully automatable; closes the loop on .claude/rules/dotnet-anti-patterns |
| **Total**    | **10/12** |                                                                     |

## Finding

`scripts/violation-scan.sh` is explicitly listed in `plans/roadmap.md` under Deferred but has never been built. A parallel rule file (`.claude/rules/dotnet-anti-patterns.md`) documents four specific anti-patterns Claude must never generate. However, there is no automated gate that checks whether those anti-patterns appear in `.claude/` skill and agent files — or in C# source files more broadly.

The gap means a skill or agent can accidentally describe, use, or recommend a banned pattern (`DateTime.Now`, `async void`, `new HttpClient()`) and no script will catch it. The `weekly-learner` and `token-optimizer` agents both do manual, ad-hoc scanning today; a dedicated script eliminates that manual labor.

## Why Highest-Leverage

The roadmap already acknowledged this as valuable enough to track. The anti-patterns rule file creates an implicit contract that nothing enforces. Every session that generates code without this guard risks producing un-caught violations. Automation score 3/3 because: the script can run in pre-commit hooks, in CI, and be invoked by `check.sh`.

## Success Criteria

- `scripts/violation-scan.sh` exists, passes `bash -n` and `shellcheck`
- Appears in `scripts/help.sh` output
- Detects at least the four anti-patterns from `.claude/rules/dotnet-anti-patterns.md` across `src/**/*.cs`
- Optionally scans `.claude/` files for references to banned patterns in agent/skill prose
- Exits non-zero when violations found; `--json` flag for machine-readable output
- Returns zero violations on clean codebase

## Phased Implementation

### Baseline
- Confirm `.claude/rules/dotnet-anti-patterns.md` lists the canonical four patterns
- Run `rg "DateTime\.Now|new HttpClient|async void|\.GetAwaiter\(\)\.GetResult\(\)" src/` to establish current violation count (expected: 0)
- Note: `session-health.sh` already checks build/format errors but not code anti-patterns

### Implement
1. Write `scripts/violation-scan.sh` using ripgrep patterns for each anti-pattern
2. Support `--json` output flag (consistent with other scripts)
3. Support `--claude` flag to also scan `.claude/skills/` and `.claude/agents/` prose
4. Add entry to `scripts/help.sh`
5. Add keyword shortcut to `.claude/rules/keyword-shortcuts.md`: `scan violations` / `check anti-patterns`

### Verify
- `bash -n scripts/violation-scan.sh`
- `shellcheck scripts/violation-scan.sh`
- `scripts/help.sh | grep violation-scan`
- `scripts/violation-scan.sh` (exit 0 on clean codebase)
- `scripts/violation-scan.sh --json | jq .`
