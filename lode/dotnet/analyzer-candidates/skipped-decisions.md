# Analyzer Candidates: Skipped Decisions
confidence: high
*Updated: 2026-11-13T12:12:44Z*

The settled side of the open register. A skipped row keeps `/analyzer` from reporting the same idea twice. Each decision names the reason and the installed rule or the site count that settled it. The open side lives in [index.md](index.md), and the shipped rules live in [catalog.md](catalog.md).

## Inline `new Option<T>(...)` Inside `Command.Create()`

**Pattern**: `System.CommandLine` `Option<T>` instances created inline inside the `Create()` method body. A static readonly field at the type level is reusable across unit tests and follows the PascalCase convention for static members.

**Why skipped**: style only, score 2/5. A Roslyn fix is possible, but the analyzer would report a convention that most projects follow.

**Evidence**: 1 site in pugworks `src/TravelSearch/Commands/HotelCommand.cs` refactored in commit 792196a68 (2026-05-26).

**Source**: `analyzer-scout 2026-07-16` (code-review-2026-05-26)

## HttpClient Per-Request Instantiation

**Pattern**: `new HttpClient()` inside a method that runs many times, instead of `IHttpClientFactory`, a singleton `HttpClient`, or an `AddHttpClient` registration.

**Why skipped**: E128004 already flags the zero-arg form. No real handler-arg evidence exists in the pugworks repo. The one candidate site, `PugFetcher.ExecuteSessionFetchAsync`, is a sanctioned custom-handler exception.

**Evidence**: zero occurrences in pugworks. Score 4 (frequency 2, coverage gap 1, expressibility 1, value 1).

**Source**: `analyzer discover 2026-08-11`

## Two Boolean Fields Encoding One State

**Pattern**: two `bool` members the caller must keep in sync, such as `IsComplete` with `IsSuccess`, or a flag beside a nullable snapshot.

**Why skipped**: E128218 already reports the implicit state machine shape at `error`. Commit 3dd5c99fd and commit 7d7de220d are findings from that rule, so a second rule would double-report.

**Source**: `analyzer-scout 2026-11-10` (session repeat)

## Disposable Local From a Factory Call

**Pattern**: `var x = await factory.GetConnectionAsync(ct)` with no `using` or `await using`.

**Why skipped**: IDISP001 owns the shape. Commit 2381b8535 fixed 6 sites from a review, and 11 suppression sites declare ownership transfer. The remaining work is the ownership declaration, not a rule.

**Source**: `analyzer-scout 2026-11-10` (fix 2381b8535)

## Struct Equality Guards Lost to a Union Relaxation

**Pattern**: `.globalconfig` sets `ca1815`, `ca2231`, and `ss017` to `none` so one union declaration compiles, which drops the struct equality guards for every value type in the repo. A rule would restore them by firing only on a hand-written `struct` or `record struct` that neither declares a union nor generates equality members.

**Why skipped**: score 2.5/5, below the bar. Frequency is 0, because pugworks holds one plain `struct` (`ListRenderItem` in `src/Harvest/DocumentModel/MarkdownRenderer.cs`), and it implements `IEquatable` on purpose. Every other value type is a `record struct`, which generates the members the three rules ask for. The cheaper fix is two scoped suppressions: one around the union declaration in `src/Pug.Core/Results/Result.cs`, and one on `ListRenderItem`, which overloads no `operator ==` on purpose. That restores the guard repo-wide at the cost of a few lines. Re-score when a second hand-written struct lands without equality members, or when a second union declaration appears.

**Source**: `session 2026-11-13` (net11-upgrade close)

## Declined from the .NET 11 Band

The net11-upgrade close on 2026-11-13 checked each shape below and declined it. The conversion shapes that clear the bar live in [net11-conversion-sweep.md](net11-conversion-sweep.md).

| Shape | Reason |
| ----- | ------ |
| `EqualityComparer<T>.Create` over a hand-rolled `IEqualityComparer<T>` | One implementation in pugworks, `NaturalSortingComparer`, and it compares natural-sort keys rather than selecting one. `Create` takes a key selector only. Score 2. |
| `Type.GetNullableUnderlyingType()` over `Nullable.GetUnderlyingType` or `typeof(Nullable<>)` reflection | Zero sites in pugworks. The .NET 11 behavior change touches a custom `Type` subclass only, and no such type exists here. Score 2.5. Re-score when a reflection helper lands. |
| `Process.TryGetProcessById` over a `try`/`catch` around `Process.GetProcessById` | Zero sites. Score 2. |
| `Enumerable.LeftJoin` over `GroupJoin` plus `DefaultIfEmpty` | Zero sites. Score 2. |
| A direct open-generic `JsonConverter` over a `JsonConverterFactory` | Zero `JsonConverterFactory` implementations. Score 2. |
| A source conversion for runtime async | The switch is assembly level, and the bad async shapes already report. CS1998 owns an `async` body with no `await`, E128007 owns `async void`, E128008 owns sync-over-async, and the task-run rule owns a `Task.Run` wrapper. No rule remains. |
