# Context: leverage-next-violation-scan
*Created: 2026-04-11T14:33:35Z*

## Axis: Addition

## Evidence

| Evidence                                     | Location                                                 |
| -------------------------------------------- | -------------------------------------------------------- |
| Deferred roadmap entry for violation-scan.sh | `plans/roadmap.md` — Deferred section                   |
| Anti-pattern rule file (4 specific patterns) | `.claude/rules/dotnet-anti-patterns.md`                  |
| No existing scan script in catalog           | `scripts/help.sh` — no violation-scan entry              |
| Weekly-learner does ad-hoc manual scanning   | `.claude/agents/weekly-learner.md` — Phase 2.5 reference |
| session-health.sh only tracks build/format   | `scripts/session-health.sh` — confirmed via read         |
| Keyword shortcut table has no scan entry     | `.claude/rules/keyword-shortcuts.md`                     |

### Anti-patterns to detect (from `.claude/rules/dotnet-anti-patterns.md`)

1. `DateTime.Now` / `DateTime.UtcNow` directly (inject TimeProvider instead)
2. `new HttpClient()` (use IHttpClientFactory)
3. `async void` methods (non-event-handler)
4. `.Result` / `.GetAwaiter().GetResult()` (sync-over-async)

### Current violation count (baseline)

Running `rg` against `src/` would establish baseline — expected 0 since this is a reference repo demonstrating best practices.

## Why This Beats the Runner-Up

Runner-up was `session.sh` (JSONL transcript stats, score 7). That addresses a different pain point — session retrospective analysis. `violation-scan.sh` wins on Automation (3 vs 2) because it is a gate that can live in `check.sh`, pre-commit hooks, and CI — making it fully continuous rather than triggered-only. It also has higher Novelty (3 vs 3, tie broken on Compound: 2 vs 1).

## Runners-Up Table

| Candidate              | Novelty | Compound | Impact | Automation | Total | Reason runner-up                       |
| ---------------------- | ------- | -------- | ------ | ---------- | ----- | -------------------------------------- |
| violation-scan.sh      | 3       | 2        | 2      | 3          | 10    | WINNER                                 |
| session.sh             | 3       | 1        | 1      | 2          | 7     | Lower compound, limited to retro view  |
| analyzer-catalog.sh    | 2       | 2        | 1      | 2          | 7     | Requires external data source          |
| watchdog.sh            | 2       | 1        | 2      | 2          | 7     | Three-way tie, lower novelty           |
| smart.sh (NL router)   | 2       | 1        | 1      | 1          | 5     | Below threshold; roadmap already skips |
