# Analyzer Plan Tasks: Proof-Test Sweep

*Updated: 2026-11-04T13:45:17Z*

Phases match [the plan](analyzers-2026-11-04-plan.md). Run the phase gate before
you tick the last box of a phase.

## Phase 1: Semantic Gaps

### E128154 Singleton depends on a scoped service

- [x] `src/E128.Analyzers/Reliability/CaptiveDependencyAnalyzer.cs` Report a singleton constructor that takes an `AddScoped` type. Severity: Warning. Source: analyzer-candidates Tier 1.
- [x] `tests/E128.Analyzers.Tests/CaptiveDependencyAnalyzerTests.cs` Positive case, scope-factory skip case, singleton-to-singleton silent case.
- [x] README row and `AnalyzerReleases.Unshipped.md` entry.

### E128155 Collection mutated during its own foreach

- [x] `src/E128.Analyzers/Reliability/CollectionMutationInEnumerationAnalyzer.cs` Report a mutation call or indexer set on the loop source. Severity: Warning. Source: analyzer-candidates Tier 1.
- [x] `src/E128.Analyzers.CodeFixers/Reliability/CollectionMutationInEnumerationCodeFixProvider.cs` Insert `ToArray()` on the loop source.
- [x] `tests/E128.Analyzers.Tests/CollectionMutationInEnumerationAnalyzerTests.cs` Mutate, indexer set, field source, immutable silent case, lambda silent case, materialized source, different collection.
- [x] README row and `AnalyzerReleases.Unshipped.md` entry.

### E128156 HttpClient certificate validation returns true

- [x] `src/E128.Analyzers/Security/HttpClientCertificateBypassAnalyzer.cs` Report a `HttpClientHandler` callback or validator that accepts every certificate. Severity: Error. Source: analyzer-candidates Tier 1.
- [x] `tests/E128.Analyzers.Tests/HttpClientCertificateBypassAnalyzerTests.cs` Lambda, block body, dangerous validator, anonymous method, parenthesized `true`, method group, conditional check silent case.
- [x] README row and `AnalyzerReleases.Unshipped.md` entry.

### E128157 Fixed-time comparison of a secret-derived value

- [x] `src/E128.Analyzers/Security/TimingSafeComparisonAnalyzer.cs` Report `==` or `SequenceEqual` on a value from a MAC, tag, or signature producer. Severity: Warning. Source: analyzer-candidates Tier 1.
- [x] `src/E128.Analyzers.CodeFixers/Security/TimingSafeComparisonCodeFixProvider.cs` Replace the comparison with `CryptographicOperations.FixedTimeEquals`.
- [x] `tests/E128.Analyzers.Tests/TimingSafeComparisonAnalyzerTests.cs` Span equality and inequality, byte-array silent case, `SequenceEqual`, HMAC producer through `AsSpan`, lowercase tag name, `FixedTimeEquals` silent case, content-hash silent case.
- [x] README row and `AnalyzerReleases.Unshipped.md` entry.

### E128158 Async iterator missing EnumeratorCancellation

- [x] Dropped. The C# compiler reports CS8425 on this shape, so a rule would
  repeat a build failure. E128158 stays unused and no file is created.

### Phase 1 gate

- [x] `scripts/test.sh CaptiveDependencyAnalyzerTests` and the other four classes.
- [x] `scripts/readme-table-diff.sh --analyzer` reports no drift.
- [x] `scripts/internal/analyzer-release-check.sh` passes.
- [x] `scripts/check.sh` passes.
- [x] `lode/dotnet/analyzers.md` and `lode/dotnet/analyzer-candidates.md` updated.

## Phase 2: Lifecycle And Allocation Gaps

### E128159 TaskCompletionSource without RunContinuationsAsynchronously

- [x] `src/E128.Analyzers/Reliability/TaskCompletionSourceOptionsAnalyzer.cs` Report a source built without the flag. Severity: Warning. Source: analyzer-candidates Tier 1.
- [x] `src/E128.Analyzers.CodeFixers/Reliability/TaskCompletionSourceOptionsCodeFixProvider.cs` Add the flag.
- [x] `tests/E128.Analyzers.Tests/TaskCompletionSourceOptionsAnalyzerTests.cs` Constructor form, options form, flag present silent case.
- [x] README row and `AnalyzerReleases.Unshipped.md` entry.

### E128160 Redirected process output never read

- [x] `src/E128.Analyzers/Reliability/ProcessOutputDrainAnalyzer.cs` Report a redirect flag with a wait and no drain in the same body. Severity: Warning. Source: analyzer-candidates Tier 1.
- [x] `tests/E128.Analyzers.Tests/ProcessOutputDrainAnalyzerTests.cs` Stdout and stderr redirect, read before the wait, event-handler drain, read after the wait, read on another process, no redirect silent case.
- [x] README row and `AnalyzerReleases.Unshipped.md` entry.

### E128161 Path.Combine with a rooted or parent segment

- [x] `src/E128.Analyzers/Reliability/PathCombineSegmentAnalyzer.cs` Report a constant segment that is rooted or climbs to a parent directory. Severity: Warning. Source: analyzer-candidates Tier 1.
- [x] `src/E128.Analyzers.CodeFixers/Reliability/PathCombineSegmentCodeFixProvider.cs` Replace the call with `Path.Join` for a rooted segment only.
- [x] `tests/E128.Analyzers.Tests/PathCombineSegmentAnalyzerTests.cs` Rooted segment, drive letter, parent segment, bare parent silent case, relative segment silent case, non-`Path` combine silent case.
- [x] README row and `AnalyzerReleases.Unshipped.md` entry.

### E128162 Local used after an explicit Dispose

- [x] `src/E128.Analyzers/Reliability/UseAfterDisposeAnalyzer.cs` Report a read of a local after a `Dispose()` call in the same block. Severity: Warning. Source: analyzer-candidates Tier 1.
- [x] `tests/E128.Analyzers.Tests/UseAfterDisposeAnalyzerTests.cs` Read after dispose, re-assignment, `using` block silent case.
- [x] README row and `AnalyzerReleases.Unshipped.md` entry.

### E128163 ImmutableArray field left at its default

- [x] `src/E128.Analyzers/Reliability/ImmutableArrayDefaultAnalyzer.cs` Report a field with no initializer and no constructor assignment. Severity: Warning. Source: analyzer-candidates Tier 1.
- [x] `src/E128.Analyzers.CodeFixers/Reliability/ImmutableArrayDefaultCodeFixProvider.cs` Initialize to `ImmutableArray<T>.Empty`.
- [x] `tests/E128.Analyzers.Tests/ImmutableArrayDefaultAnalyzerTests.cs` Uninitialized field, initialized field, constructor assignment, property silent case, other field type silent case.
- [x] README row and `AnalyzerReleases.Unshipped.md` entry.

### Phase 2 gate

- [x] All five test classes pass.
- [x] `scripts/readme-table-diff.sh --analyzer` reports no drift.
- [x] `scripts/internal/analyzer-release-check.sh` passes.
- [x] `scripts/check.sh` passes.
- [x] Lode files updated.

## Phase 3: Tier 2 Batch

This phase carries no checklist. The seventeen Tier 2 rows are a standing
backlog, not a deliverable of this plan. They stay in the Tier 2 table of
`lode/dotnet/analyzer-candidates.md`. A future plan starts when a real defect
in review matches one of those rows.

## Plan Close

- [x] Tier 2 rows stay in `lode/dotnet/analyzer-candidates.md`. The table is
  already the backlog, so no row needs to fold back.
- [ ] `scripts/internal/plan-close.sh --plan analyzers-2026-11-04 --dry-run`, then run it without the flag.

## See Also

- [plan](analyzers-2026-11-04-plan.md)
- [context](analyzers-2026-11-04-context.md)
