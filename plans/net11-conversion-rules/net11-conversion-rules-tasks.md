# .NET 11 Conversion Rules: Tasks
*Updated: 2026-11-13T12:24:18Z*

## Phase 0: Evidence And Open Questions

- [x] Confirm the `closed` with an abstract member compiles.
- [x] Confirm the `Process` capture API surface.
- [x] Confirm the `StringStream` and `ReadOnlyMemoryStream` constructors.

## Phase 1: E128223 Hand-Rolled Process Capture

- [x] Write the analyzer tests.
- [x] Write the analyzer.
- [x] Add the README row and section.
- [x] Add the `AnalyzerReleases.Unshipped.md` row.
- [x] Convert the repository's own capture site in `PodmanSmokeTests`.

## Phase 2: E128224 Read-Only MemoryStream

- [x] Write the analyzer tests.
- [x] Write the analyzer. Diagnostic only, because a write through the stream would break.
- [x] Add the README row and section.
- [x] Add the release row.

## Phase 3: E128225 Abstract Base With Members

- [x] Write the analyzer and code fix tests.
- [x] Write the analyzer and code fix. Shared analysis lives in `ClosedHierarchy`.
- [x] Add the README row and section.
- [x] Add the release row.

## Phase 4: E128226 Comparer Set

- [x] Write the analyzer tests.
- [x] Write the analyzer. Diagnostic only, because the rewrite moves the loop.
- [x] Add the README row and section.
- [x] Add the release row.

## Phase 5: E128220 Extension

- [x] Write the closed-hierarchy tests.
- [x] Extend the analyzer.
- [x] Update the README section.
- [x] Add the Changed Rules row.

## Phase 6: Retire Or Re-Read

- [x] Decide E128160: keep until the consumer migration lands.
- [x] Decide E128192: keep, re-read after the stream migration.
- [x] Decide E128005: keep, no change.
- [x] Decide E128049: keep, no change.

## Phase 7: Documentation And Release

- [x] Update the lode register, catalog, and sweep file.
- [x] Add `rule-details-conversion.md` and the lode-map row.
- [x] Remove the roadmap item.
- [x] Run `scripts/check.sh`.
