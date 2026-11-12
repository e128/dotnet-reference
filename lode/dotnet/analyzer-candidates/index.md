# Analyzer Candidates: Open Register
*Updated: 2026-11-12T02:18:06Z*

One register for every open analyzer candidate, whichever repository noticed it.
A candidate enters at status `new`, carries a four-dimension score, and leaves
only when a rule ships or a session closes the decision as `skipped`. The
settled side, including every shipped rule and every rejected idea, lives in
[catalog.md](catalog.md).

Writers: `/analyzer` scout and discover, `scripts/pug-heuristic.nu` in the
pugworks sibling checkout, and any session in this repository.

## Schema

Each row: `Pattern | Source | Score | Status | Notes`.

| Status    | Meaning                                                      |
| --------- | ------------------------------------------------------------ |
| new       | discovered and scored, not yet planned                       |
| planned   | owned by an active plan                                      |
| skipped   | evaluated and declined, with the reason in a section below   |
| candidate | Tier 2 shape that clears the bar and waits for a real defect |

The four dimensions are frequency (0-2), coverage gap (0-1), expressibility
(0-1), and value (0-1). A total of 3 or more is RECOMMEND. The rubric lives in
[scoring-rubric.md](../../infrastructure/scoring-rubric.md).

## Open Candidates

| Pattern | Source | Score | Status | Notes |
| ------- | ------ | ----- | ------ | ----- |
| Unguarded `Path.Combine(root.FullName, name)` where `name` is not a literal | analyzer discover --from-commits 2026-11-10 (fix 1ece23206) | 5/5 | new | No rule covers path containment. Detail below. |
| Body read into memory (`ReadAsStringAsync`, `ReadToEndAsync`) with no size bound | analyzer discover --from-commits 2026-11-10 (fix 1ece23206) | 3.5/5 | new | 16 read sites in pugworks `src/`, 1 bound (as of 2026-11-10). Detail below. |
| `foreach` over `JsonElement.EnumerateArray()` or `EnumerateObject()` without disposing the enumerator | analyzer discover --from-commits 2026-11-10 (fix 2381b8535) | 3.5/5 | new | 3 unguarded sites remain in one pugworks test file (as of 2026-11-10). Detail below. |

## Unguarded Path Join Under a Directory Root

**Pattern**: `Path.Combine(<dir>.FullName, <leaf>)`, or a `FileInfo` built from
it, where the leaf is not a compile-time literal and no guard in the same method
confines the result to `<dir>`.

**Before**: `var file = new FileInfo(Path.Combine(imagesDir.FullName, fileName));`
then read, judge, or delete it.

**After**: resolve the joined path and check containment, throwing on a miss.
`Path.GetFileName` is not the fix, because it flattens a multi-segment leaf and
retargets the read.

**Why not covered**: no E128 rule. CA3003 and CA3006 are taint rules and neither
is enabled. E128001 covers typed path parameters, not containment.

**Scope**: skip a literal leaf, and skip a call already followed by a
`FilePathContainment.IsInside` check or a domain validator such as
`CorpusScanner.ValidateDomainPath`.

**Evidence**: commit 1ece23206 confines the caller's image name in
`DashboardImagesService` after a security review. 4 of the 59 root-joined sites
in pugworks `src/` carry a guard (as of 2026-11-10).

**Source**: `analyzer discover --from-commits 2026-11-10`

## Body Read Without a Size Bound

**Pattern**: `ReadAsStringAsync`, `ReadAsByteArrayAsync`, or `ReadToEndAsync`
over a remote or persisted body with no `ContentLength` check, streamed cap, or
`MaxResponseContentBufferSize` ahead of it.

**Why not covered**: no rule. The FIPS set covers crypto, not allocation.

**Scope**: the bound is a runtime judgment, so flag only a read whose source is
a response or a file opened from a caller path.

**Evidence**: commit 1ece23206 restores the lost persisted-response bound in
`OllamaVisionClassifier`. 16 read sites in pugworks `src/`, 1 with a bound (as
of 2026-11-10).

**Source**: `analyzer discover --from-commits 2026-11-10`

## JSON Enumerator Not Disposed

**Pattern**: `foreach (var x in element.EnumerateArray())` or
`EnumerateObject()` with no `using` declaration over the enumerator.

**Before**: `foreach (var cell in cells.EnumerateArray())`

**After**: `using var cellEnumerator = cells.EnumerateArray();` then
`foreach (var cell in cellEnumerator)`

**Why not covered**: CA2000 does not fire on a disposable struct. E128041 owns
the enclosing `JsonDocument` lifetime, not the enumerator.

**Scope**: the enumerator rents pooled metadata from the owning document. No
defect is observed, so the value is potential only.

**Evidence**: commit 2381b8535 converted 3 sites. 3 unguarded sites remain in
pugworks `tests/Pug.Core.Tests/ThemeJsonAuditTests.cs` (as of 2026-11-10).

**Source**: `analyzer discover --from-commits 2026-11-10`

## Tier 2 Candidates

These clear the same bar and wait for a real defect in review.

| Rule                                                               | Check                                                          | Noise  |
| ------------------------------------------------------------------ | -------------------------------------------------------------- | ------ |
| Array compared by reference                                        | `==` or `object.Equals` on two arrays, except byte arrays      | Low    |
| Discarded `Stream.Read` count                                      | read used as a statement, buffer read later                    | Low    |
| Blocking call inside `lock`                                        | sync `Thread.Sleep`, `File`, `Stream`, or `DbCommand` call     | Low    |
| `CollectionsMarshal.AsSpan` invalidated by a list mutation         | span read after a mutating call on the list                    | Low    |
| `AsReadOnly()` in a property getter                                | wrapper allocated on every access                              | Low    |
| `Array.Resize` or `Concat` in a loop                               | call reassigned to the same receiver inside the loop           | Low    |
| CORS wildcard with credentials                                     | one chain holds `AllowAnyOrigin` and `AllowCredentials`        | Low    |
| Antiforgery missing on a state-changing action                     | verb attribute with no antiforgery attribute in scope          | Medium |
| Token validation flag set to constant `false`                      | six `TokenValidationParameters` flags                          | Low    |
| `readonly` field of a mutable struct                               | defensive copy on every member call                            | Low    |
| Floating-point loop counter                                        | `for` loop steps a `float` or `double` local                   | Low    |
| Enum cast outside the member set                                   | constant integral to enum conversion                           | Low    |
| Guard after the division                                           | divisor compared with zero after the division                  | Low    |
| `[Flags]` member value that is not a power of two                  | constant collides with a lower bit                             | Low    |
| Dead store to a private field                                      | assignment killed on every path before a read                  | Medium |
| Loop with no exit path                                             | `while (true)` with no exit call                               | Medium |

## Skipped Decisions

A skipped row keeps `/analyzer` from reporting the same idea twice.

## Inline `new Option<T>(...)` Inside `Command.Create()`

**Pattern**: `System.CommandLine` `Option<T>` instances created inline inside
the `Create()` method body. A static readonly field at the type level is
reusable across unit tests and follows the PascalCase convention for static
members.

**Why skipped**: style only, score 2/5. A Roslyn fix is possible, but the
analyzer would report a convention that most projects follow.

**Evidence**: 1 site in pugworks `src/TravelSearch/Commands/HotelCommand.cs`
refactored in commit 792196a68 (2026-05-26).

**Source**: `analyzer-scout 2026-07-16` (code-review-2026-05-26)

## HttpClient Per-Request Instantiation

**Pattern**: `new HttpClient()` inside a method that runs many times, instead of
`IHttpClientFactory`, a singleton `HttpClient`, or an `AddHttpClient`
registration.

**Why skipped**: E128004 already flags the zero-arg form. No real handler-arg
evidence exists in the pugworks repo. The one candidate site,
`PugFetcher.ExecuteSessionFetchAsync`, is a sanctioned custom-handler exception.

**Evidence**: zero occurrences in pugworks. Score 4 (frequency 2, coverage gap
1, expressibility 1, value 1).

**Source**: `analyzer discover 2026-08-11`

## Two Boolean Fields Encoding One State

**Pattern**: two `bool` members the caller must keep in sync, such as
`IsComplete` with `IsSuccess`, or a flag beside a nullable snapshot.

**Why skipped**: E128218 already reports the implicit state machine shape at
`error`. Commit 3dd5c99fd and commit 7d7de220d are findings from that rule, so a
second rule would double-report.

**Source**: `analyzer-scout 2026-11-10` (session repeat)

## Disposable Local From a Factory Call

**Pattern**: `var x = await factory.GetConnectionAsync(ct)` with no `using` or
`await using`.

**Why skipped**: IDISP001 owns the shape. Commit 2381b8535 fixed 6 sites from a
review, and 11 suppression sites declare ownership transfer. The remaining work
is the ownership declaration, not a rule.

**Source**: `analyzer-scout 2026-11-10` (fix 2381b8535)

## Working The Register

1. Read this file and [catalog.md](catalog.md) before you scout, so a settled
   decision is not suggested again.
2. Check the installed rule sets and the C# compiler before you score a
   candidate. A shape that already reports as a build error is not a candidate.
3. Append the row, then add a detail section when the score is 3 or more.
4. Take the next free rule ID from `AnalyzerReleases.Shipped.md` and
   `AnalyzerReleases.Unshipped.md` in `src/E128.Analyzers`.
5. Move the row to [catalog.md](catalog.md) when the rule ships, and delete the
   detail section here.

## Related

- [catalog.md](catalog.md): settled rules, rejected ideas, and the ID map
- [async-sweep.md](async-sweep.md): closed async fire-and-forget sweep
- [external-sweep.md](external-sweep.md): closed 2026-11-05 external rule-set sweep
- [performance-sweep.md](performance-sweep.md): closed twenty-practice performance sweep

## Captured

A capture records a correction before anyone scores it. The same shape counts
up, and the third capture opens a roadmap item. A C# capture lands here. A
Nushell or Markdown capture stays in the pugworks ledger.

### Captured: Confine a joined path by checking the resolved result, never by flattening the leaf with Path.GetFileName, which silently retargets a multi-segment leaf (1/3): score pending
> **Key**: `confine-a-joined-path-by-checking-the-resolved-result-never-b`
> **Class**: `correctness`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: Confine a joined path by checking the resolved result, never by flattening the leaf with Path.GetFileName, which silently retargets a multi-segment leaf
> **Why**: A caller that passes a relative subpath such as images/a.png needs that subpath preserved. GetFileName turns it into a.png and the read or delete lands on a different file while the guard reports clean.
> **Source**: `correction 2026-11-11T14:26:32Z`, occurrence 1 of 3

### Captured: A link-aware containment check reports a symlinked occupant as absent, so a name picker needs a separate is-occupied test that treats an escaping path as taken (1/3): score pending
> **Key**: `a-link-aware-containment-check-reports-a-symlinked-occupant-a`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: A link-aware containment check reports a symlinked occupant as absent, so a name picker needs a separate is-occupied test that treats an escaping path as taken
> **Why**: ExistsInside and ResolveInside both resolve links, so a collision loop sees a name taken by an escaping symlink as free and the later ResolveInside throws instead of advancing the suffix. FilePathContainment.IsOccupied answers the name-picker question.
> **Source**: `correction 2026-11-11T16:38:12Z`, occurrence 1 of 3

### Captured: Never write a probe or scratch class into real library code; analyzer probes belong in dotnet-reference tests (1/3): score pending
> **Key**: `never-write-a-probe-or-scratch-class-into-real-library-code-a`
> **Class**: `design`, **Lang**: `cs`, **File**: `(none)`
> **Pattern**: Never write a probe or scratch class into real library code; analyzer probes belong in dotnet-reference tests
> **Why**: pugworks is only a client of the E128 analyzers. A scratch class in src/ ships in the library if forgotten, and it puts analyzer research in the wrong repo. Put the probe in dotnet-reference as a unit test instead.
> **Source**: `correction 2026-11-12T02:18:06Z`, occurrence 1 of 3
