# Review Plan: days 2 Union Review

*Updated: 2026-11-14T00:42:34Z*

Target: commits newer than 2 days (base `be24a59`, head `b70fb1c`), 140 files, 16 commits.
Verdict: Resolved. All 8 High findings fixed on `fix/review-2026-11-14-general`.
`scripts/check.sh` green: shell, format, build, 2992 tests (2987 passed, 3 fixed at the
gate, 2 skipped). The 3 gate failures came from `HttpContentHeadersTypeName` resolving
`System.Net.Http.HttpContentHeaders` instead of `System.Net.Http.Headers.HttpContentHeaders`,
which left the ContentLength bound undetected. One-line fix in
`HttpBodyReadWithoutSizeBoundAnalyzer.cs`.
The Medium findings are advisory and stay unapplied.

## Findings

| # | Severity | File | Problem |
| - | -------- | ---- | ------- |
| 1 | High | `src/E128.Analyzers.CodeFixers/Reliability/GeneratedRegexNestedCodeFixProvider.cs:53` | Single-atom fix drops the outer `*`, so `(a+)*` becomes `(a+)` and stops matching the empty string. |
| 2 | High | `src/E128.Analyzers/Reliability/GeneratedRegexHelpers.cs:289` | Atom comparison does not expand character classes, so `(?:/[^\s:"']+)+` is exempted and stays exponential. |
| 3 | High | `src/E128.Analyzers/Reliability/HandRolledProcessCaptureAnalyzer.cs:156` | `FlagBinds` never reaches `LocalLinksToProcess`, so a start-info local set after creation never binds. |
| 4 | High | `src/E128.Analyzers/Performance/ReadOnlyMemoryStreamAnalyzer.cs:57-66,232-234` | Compression streams count as read-only consumers in `Compress` mode. |
| 5 | High | `src/E128.Analyzers/Security/HttpBodyReadWithoutSizeBoundAnalyzer.cs:142-157` | Bound detection matches identifier text, not a bounding call or an ordering. |
| 6 | High | `src/E128.Analyzers/README.md:3274-3286` | Documented `ContentLength` guard fails open on a chunked response. |
| 7 | High | `src/E128.Analyzers/Design/UnionSwitchDiscardArmAnalyzer.cs:124-147` | A `when`-guarded discard counts as a catch-all, and the fix produces a non-exhaustive switch. |
| 8 | High | `lode/dotnet/release-tracking.md:170` | The release-to-rule table ends at 3.0.0, but the head commit ships 3.1.0 with E128227. |

## Phases

### Phase 1: GeneratedRegex correctness

Fix findings 1 and 2. Restore language preservation in the nested-quantifier fix. Expand
class contents in the atom comparison, or record the limit in the rule contract. Update the
slash test and the README claim in the same change.

### Phase 2: E128223 process binding

Fix finding 3. Route a start-info local receiver through `LocalLinksToProcess`. Add tests for
the post-creation assignment and for `process.StartInfo = psi`.

### Phase 3: Performance rules

Fix finding 4. Require `CompressionMode.Decompress` for the compression wrapper types.

### Phase 4: E128227 size bound

Fix findings 5 and 6. Accept only a resolved `LoadIntoBufferAsync(long)` overload, or a
null-safe pre-read comparison. Replace the documented remediation and update the test that
locks the weak shape in.

### Phase 5: E128220 catch-all

Fix finding 7. Treat a guarded discard or var arm as not exhaustive. Add a test for
`_ when cond =>`.

### Phase 6: Release record

Fix finding 8. Add the 3.1.0 row with E128227 and bump the file timestamp.

## Medium Findings

Six findings were lowered to Medium by the defender pass:

- `ProcessLaunchScope.cs:19-32`: `this.p1` and `this.p2` share one root symbol.
- `GeneratedRegexNestedCodeFixProvider.cs:58-60`: `{1,}` rewrite leaves E128014 firing.
- `ReadOnlyMemoryStreamAnalyzer.cs:55,208-218`: a `Position` assignment counts as a read.
- `HttpBodyReadWithoutSizeBoundAnalyzer.cs:47,149-150`: `MaxResponseContentBufferSize` is trusted across the type.
- `tests/E128.Analyzers.Tests/*`: eight test classes bypass the shared runner.
- `ComparerSetConstructionAnalyzer.cs:62` and `ReadOnlyMemoryStreamAnalyzer.cs:102`: target-typed `new(...)` is missed.

Related: [tasks](general-tasks.md), [context](general-context.md).
