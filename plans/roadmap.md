# Roadmap
*Updated: 2026-05-17T00:00:00Z*

## Active

### Pugworks Integration: E128083–E128085
NuGet version bump, `.globalconfig` severity entries, and violation fixes for ~4 E128085 instances in `NeoOutputWriter.cs`, `NeoCommand.cs`, `CommitReportSummary.cs`.

## Backlog

### `--filter-class` MTP Root Cause Investigation
The 0-tests-returned behavior of `--filter-class` when called through `test.sh` is unresolved. Needs dedicated investigation — xUnit v3 MTP runner may not support the flag the same way.

### E128062 Older TFM Reference Assembly Handling
Future analyzers that detect APIs only available on newer runtimes should pre-scan for banned TFMs in the test host setup rather than discovering the gap during test verification. Requires test infrastructure changes.

## Deferred

### smart.sh — Natural Language Router
Natural language command dispatcher. Accepts plain English and routes to the right script.

### session.sh — Transcript Analysis
Session transcript analysis: tool-counts, errors, topics, stats from Claude Code JSONL transcripts.

### violation-scan.sh — Anti-Pattern Scanner
Session anti-pattern scan: detects bash anti-patterns and raw command usage in `.claude/` files.
