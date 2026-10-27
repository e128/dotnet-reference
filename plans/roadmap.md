# Roadmap
*Updated: 2026-10-27T16:59:48Z*

## Active

### Unsafe And Performance Analyzer Expansion
18 new E128 rules from two published guidance documents. 13 carry a code fix
provider. The rules are built and tested. The release is pending.
Plan: [unsafe-perf-analyzers](unsafe-perf-analyzers/unsafe-perf-analyzers-plan.md).

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
