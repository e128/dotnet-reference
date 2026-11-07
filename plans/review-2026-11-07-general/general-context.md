# Mr. Sparkle Context: performance analyzers
*Updated: 2026-11-07T17:38:37Z*

## Target

- Kind: working tree against base `ed9e5e31`.
- Branch: `feature/performance-analyzers`.
- Files: 49 (11 tracked modified, 38 new).
- Tracked diff: 550 insertions, 17 deletions.
- Commits on the branch: 0.
- Plan found for the branch: none.
- Shallow clone: false.

## Reviewers run

| Reviewer | Instances | Result |
| -------- | --------- | ------ |
| `security-reviewer` (vendored) | 5 slices | 1 Medium, 6 Low |
| `code-reviewer` (vendored) | 5 slices | 1 High, 13 Medium, 19 Low |
| `adversarial-review` | 3 attackers | 6 security, 20 functionality, 18 quality |
| `mr-sparkle-dotnet` | 1 | Build clean, no suppression, 3 pre-existing self-hits |
| `code-review` skill | 0 | Not available in this harness |
| `ponytail:ponytail-review` | 0 | Plugin not installed |
| `interfaces:better-interface` | 0 | Not routed, no UI files |

## Diff delivery

The 184KB diff exceeded the 40KB inline limit, so
`scripts/internal/cr-diff-deliver.sh` split it into five slices at file
boundaries. Each reviewer instance read one slice. Findings were merged under
the reviewer name. The slice files were deleted after the run.

## Tool vectors

- `scripts/diagnostics.sh --group`: zero diagnostics. Build clean, so an
  unconfirmed Medium or Low compliance finding is advisory.
- `scripts/violation-scan.sh`: three hits, all pre-existing analyzer source that
  detects its own pattern. Not in the diff.
- `scripts/suppression-scan.sh --json`: no suppression in a changed file.
- `scripts/analyzer-stats.sh --json`: rule inventory read.
- `scripts/internal/mechanical-diff.sh --json`: 0 mechanical, 32 substantive.
- Roslyn navigator: MCP server not running, skipped.

## Dedup report

- `SortedSet` and `SortedDictionary` invalid fix: 3 reviewers, one entry.
- `LoopContext` duplication and deconstruction gap: 2 reviewers, one entry.
- `HasConstantBound` over-broad: 3 reviewers, one entry.
- E128208 indexer write: 3 reviewers, one entry.
- E128214 field initializer: 3 reviewers, one entry.
- E128215 initializer `Capacity`: 2 reviewers, one entry.
- E128217 recursion: 2 reviewers, one entry.
- E128211 cancellation-aware condition: 2 reviewers, one entry.
- Lode map wording: 2 reviewers, one entry.

## Defender

Three Critical and High findings were sent to the defender. All three were
confirmed. One was downgraded from High to Medium: the E128210 constant-pattern
gap is a missed warning, not incorrect emitted code.

- Confirmed High: 2.
- Confirmed Medium after downgrade: 1.
- Rejected: 0.

## Applied fixes

The user approved autofixing. All confirmed High findings and the clear Medium
bugs were fixed in the working tree, with a regression test per behaviour.
`scripts/check.sh` passes shell, format, build, and test.

## Deferred

- Merging the two private `IsInsideLoopBody` copies into `LoopContext`. The new
  helper now matches their `CommonForEachStatementSyntax` handling, but the
  merge touches two older analyzers.
- Folding E128203 and E128204 into one detector. The two classes differ only in
  the descriptor and the method set.
- Requiring a `CancellationToken` in scope for E128211, the E128038 shape. That
  narrows a documented rule, so it needs a product decision.
- Aligned struct size. The message now says "at least N bytes", which makes the
  estimate honest without an alignment model.
