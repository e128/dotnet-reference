# Roadmap
*Updated: 2026-10-31T19:55:01Z*

## Backlog

### `--filter-class` MTP Root Cause Investigation
The 0-tests-returned behavior of `--filter-class` when called through `test.sh` is unresolved. Needs dedicated investigation — xUnit v3 MTP runner may not support the flag the same way.

### E128062 Older TFM Reference Assembly Handling
Future analyzers that detect APIs only available on newer runtimes should pre-scan for banned TFMs in the test host setup rather than discovering the gap during test verification. Requires test infrastructure changes.

## Deferred

### smart.sh — Natural Language Router
Natural language command dispatcher. Accepts plain English and routes to the right script.
