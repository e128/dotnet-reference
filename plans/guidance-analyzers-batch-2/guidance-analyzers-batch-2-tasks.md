# Guidance Analyzers Batch 2 Tasks

*Updated: 2026-10-31T23:05:00Z*

## Phase 1: Rules With A Code Fix

- [x] E128131 `Encoding.UTF8.GetString` feeding `Parse` or `TryParse`.
- [x] E128132 Culture comparer in a string dictionary or set.
- [x] E128133 `new string(span)` key into a dictionary or set.
- [x] E128134 Manual bit-count loop.
- [x] E128135 Manual sum loop over a float or double span.
- [x] E128061 RVA span fix for a byte literal field.

## Phase 2: Rules Without A Code Fix

- [x] E128136 Pointer and length method pair.
- [x] E128137 Nonpublic BCL member through reflection.
- [x] E128138 Raw IL through `ILGenerator`.

## Phase 3: Bookkeeping

- [x] Audit the release files against the source. Confirm every
      `DiagnosticId` constant appears exactly once across `Shipped` and
      `Unshipped`, no deleted rule stays listed, and the newest release
      section matches the shipped package. Keep `Unshipped.md`: RS2000 fails
      the build for a rule ID that is in neither file.
- [x] Add the rule rows to `AnalyzerReleases.Unshipped.md`.
- [x] Add the README rows and the per-rule sections.
- [x] Add two `PublicAPI.Unshipped.txt` lines per new type.
- [x] Update the lode rule map and the unsafe coverage map.
- [x] Run `scripts/test.sh` for each new test class.
- [x] Run `scripts/check.sh`.

## Phase 4: Delivery

- [ ] Commit on a `feature/` branch.
- [ ] Open one pull request.
