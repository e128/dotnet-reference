# Analyzer Plan: Proof-Test Sweep Candidates, 2026-11-04

*Updated: 2026-11-04T13:45:17Z*

Source: review of the e128.info proof-test page and its six linked sources. The
sweep covered the .NET code-analysis index, CERT C, CERT Java, the CWE Top 25,
the NASA Power of Ten, and the Clang Static Analyzer check list. Candidate rows
and coverage verdicts live in
[lode/dotnet/analyzer-candidates.md](../../lode/dotnet/analyzer-candidates.md).

Nine Tier 1 rules have no current owner. Seventeen Tier 2 rules carry lower value
or higher noise. Tier 1 ships first.

New IDs start at E128154. E128095 and E128130 stay unused. Confirm the next free
ID with `scripts/analyzer-context.sh` before each phase.

## Success Criteria

- Each rule reports on its violation snippet and stays silent on its negative cases.
- The README rule table, `AnalyzerReleases.Unshipped.md`, and the test class agree.
- `scripts/check.sh` passes at the end of each phase.
- The matching lode file changes in the same commit.

## Phase 1: Semantic Gaps

These rules read symbols, constructor signatures, or whole-compilation state.

| #   | Rule                                                         | Analyzer                                                 | Fix                      |
| --- | ------------------------------------------------------------ | -------------------------------------------------------- | ------------------------ |
| 1   | E128154 Singleton service depends on a scoped service        | `Reliability/CaptiveDependencyAnalyzer.cs`               | None                     |
| 2   | E128155 Collection mutated during its own `foreach`          | `Reliability/CollectionMutationInEnumerationAnalyzer.cs` | Append `ToArray()`       |
| 3   | E128156 HttpClient certificate validation returns `true`     | `Security/HttpClientCertificateBypassAnalyzer.cs`        | None                     |
| 4   | E128157 Fixed-time comparison of a secret-derived value      | `Security/TimingSafeComparisonAnalyzer.cs`               | `FixedTimeEquals`        |
| 5   | E128158 Async iterator missing `[EnumeratorCancellation]`    | Dropped: the compiler reports CS8425                     | None                     |

Phase 1 notes:

- Rule 1 reuses the compilation-end registration pattern in
  `Reliability/UnregisteredServiceResolveAnalyzer.cs`. Skip `IServiceProvider`,
  `IServiceScopeFactory`, and `IServiceScope` parameters.
- Rule 2 reads a local, a field, or a parameter as the loop source. An immutable,
  frozen, or concurrent collection, a `string`, and a materialized call stay
  silent.
- Rule 3 targets `HttpClientHandler` only. CA5359 owns the
  `ServicePointManager` callback.
- Rule 4 scopes to a name ending `Mac`, `Hmac`, `Tag`, or `Signature`, a name
  segment with one of those words, and a value an `HMAC` type produced. A plain
  content hash stays silent.
- Rule 5 is dropped. The compiler reports CS8425 on the same shape, so a rule
  would repeat a build failure. E128158 stays unused.

## Phase 2: Lifecycle And Allocation Gaps

| #   | Rule                                                                    | Analyzer                                                     | Fix                         |
| --- | ----------------------------------------------------------------------- | ------------------------------------------------------------ | --------------------------- |
| 6   | E128159 `TaskCompletionSource` without `RunContinuationsAsynchronously` | `Reliability/TaskCompletionSourceOptionsAnalyzer.cs`         | Add the flag                |
| 7   | E128160 Redirected process output never read before `WaitForExit`       | `Reliability/ProcessOutputDrainAnalyzer.cs`                  | None                        |
| 8   | E128161 `Path.Combine` with a rooted or parent segment                  | `Reliability/PathCombineSegmentAnalyzer.cs`                  | `Path.Join`, rooted only    |
| 9   | E128162 Local used after an explicit `Dispose`                          | `Reliability/UseAfterDisposeAnalyzer.cs`                     | None                        |
| 10  | E128163 `ImmutableArray<T>` field left at its default                   | `Reliability/ImmutableArrayDefaultAnalyzer.cs`               | `ImmutableArray<T>.Empty`   |

Phase 2 notes:

- Rule 7 runs per method body by design. A drain in a helper method stays silent.
- Rule 8 reports constant segments only, because a rooted override can be
  deliberate. The message asks for confirmation.
- Rule 9 anchors on an explicit `Dispose()` call in the same block. It does not
  model scope exit.

## Phase 3: Tier 2 Batch

Seventeen rules wait for a real defect in review. Order them by the next defect
found in this repository. See the Tier 2 table in
[analyzer-candidates.md](../../lode/dotnet/analyzer-candidates.md).

## Gates For Each Phase

```bash
scripts/test.sh <AnalyzerClassName>
scripts/readme-table-diff.sh --analyzer
scripts/internal/analyzer-release-check.sh
scripts/check.sh
```

## See Also

- [context](analyzers-2026-11-04-context.md)
- [tasks](analyzers-2026-11-04-tasks.md)
- [analyzer checklist](../../lode/dotnet/analyzers/new-analyzer-checklist.md)
