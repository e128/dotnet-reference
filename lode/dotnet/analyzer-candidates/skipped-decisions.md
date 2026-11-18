# Analyzer Candidates: Skipped Decisions
confidence: high
*Updated: 2026-11-18T12:18:45Z*

The settled side of the open register. A skipped row keeps `/analyzer` from reporting the same idea twice. Each decision names the reason and the installed rule or the site count that settled it. The open side lives in [index.md](index.md), and the shipped rules live in [catalog.md](catalog.md).

## Inline `new Option<T>(...)` Inside `Command.Create()`

**Pattern**: `System.CommandLine` `Option<T>` instances created inline inside the `Create()` method body. A static readonly field at the type level is reusable across unit tests and follows the PascalCase convention for static members.

**Why skipped**: style only, score 2/5. A Roslyn fix is possible, but the analyzer would report a convention that most projects follow.

**Evidence**: 1 site in a consumer repository, refactored on 2026-05-26.

**Source**: `analyzer-scout 2026-07-16` (code-review-2026-05-26)

## HttpClient Per-Request Instantiation

**Pattern**: `new HttpClient()` inside a method that runs many times, instead of `IHttpClientFactory`, a singleton `HttpClient`, or an `AddHttpClient` registration.

**Why skipped**: E128004 already flags the zero-arg form. No real handler-arg evidence exists in the consumer repository. The one candidate site is a sanctioned custom-handler exception.

**Evidence**: zero occurrences in the consumer repository. Score 4 (frequency 2, coverage gap 1, expressibility 1, value 1).

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

**Why skipped**: score 2.5/5, below the bar. Frequency is 0, because the consumer repository holds one plain `struct`, and it implements `IEquatable` on purpose. Every other value type is a `record struct`, which generates the members the three rules ask for. The cheaper fix is two scoped suppressions: one around the union declaration, and one on that struct, which overloads no `operator ==` on purpose. That restores the guard repo-wide at the cost of a few lines. Re-score when a second hand-written struct lands without equality members, or when a second union declaration appears.

**Source**: `session 2026-11-13` (net11-upgrade close)

## JSON Enumerator Without a `using` Declaration

**Pattern**: `foreach (var x in element.EnumerateArray())` or
`element.EnumerateObject()` with no `using` declaration over the enumerator.

**Why skipped**: the enumerator releases nothing, and `foreach` already disposes
it. `JsonElement.ArrayEnumerator.Dispose()` and
`JsonElement.ObjectEnumerator.Dispose()` both set `_curIdx = _endIdxOrVersion`,
which only marks the cursor finished. Both types implement
`System.IDisposable`, so the compiler disposes the hidden enumerator local. The
`using` declaration is correct but redundant, and a rule would report correct
code.

**Evidence**: a consumer repository moved 7 sites to a `using` declaration.
Four direct `foreach` calls over an enumerator remain today, 3 of them in
one test file (as of 2026-11-13). Both `Dispose`
bodies were checked against the runtime source on 2026-11-13.

**Source**: `analyzer discover --from-commits 2026-11-10`

## `AsReadOnly()` In A Property Getter

**Pattern**: a property getter returns `_items.AsReadOnly()` and allocates a new
`ReadOnlyCollection<T>` on every access.

**Why skipped**: E128058 and E128060 require `.AsReadOnly()` in exactly this
getter shape. A second rule would report the code the shipped rules demand. The
allocation is the price of the guard those rules buy.

**Evidence**: 10 getter sites in a consumer repository (as of 2026-11-14), including
`Names.SelectedNames` and `SynthesisOptions.Priors`.

**Source**: Tier 2 register triage, 2026-11-14

## Shapes With an Installed Owner

The BlazingFastDotnet skill guide lists each shape below. An installed rule
already reports it, and `AnalysisLevel=latest-all` enables that rule in build.

| Shape | Owner |
| ----- | ----- |
| A lambda that captures an outer local where a `static` lambda takes the state | IDE0320, "Make anonymous function static" |
| `lock` on a plain `object` field instead of `System.Threading.Lock` | IDE0330, "Use `System.Threading.Lock`" |
| `response.Headers["Content-Type"]` instead of the typed property | The ASP.NET Core analyzer titled "Use IHeaderDictionary properties instead of indexer" |
| A `RequestDelegate` that returns `Task<T>` | The ASP.NET Core analyzer titled "Do not return a value from RequestDelegate" |

**Evidence**: the installed SDK style configuration sets both IDE rules to
warning. Both ASP.NET Core analyzer titles come from
`Microsoft.AspNetCore.App.Analyzers.dll` in the installed ref pack. Checked on
2026-11-17.

**Source**: BlazingFastDotnet skill guide, read 2026-11-17

## Declined from the .NET 11 Band

The net11-upgrade close on 2026-11-13 checked each shape below and declined it. The conversion shapes that clear the bar live in [net11-conversion-sweep.md](net11-conversion-sweep.md).

| Shape | Reason |
| ----- | ------ |
| `EqualityComparer<T>.Create` over a hand-rolled `IEqualityComparer<T>` | One implementation in a consumer repository, and it compares natural-sort keys rather than selecting one. `Create` takes a key selector only. Score 2. |
| `Type.GetNullableUnderlyingType()` over `Nullable.GetUnderlyingType` or `typeof(Nullable<>)` reflection | Zero sites in a consumer repository. The .NET 11 behavior change touches a custom `Type` subclass only, and no such type exists here. Score 2.5. Re-score when a reflection helper lands. |
| `Process.TryGetProcessById` over a `try`/`catch` around `Process.GetProcessById` | Zero sites. Score 2. |
| `Enumerable.LeftJoin` over `GroupJoin` plus `DefaultIfEmpty` | Zero sites. Score 2. |
| A direct open-generic `JsonConverter` over a `JsonConverterFactory` | Zero `JsonConverterFactory` implementations. Score 2. |
| A source conversion for runtime async | The switch is assembly level, and the bad async shapes already report. CS1998 owns an `async` body with no `await`, E128007 owns `async void`, E128008 owns sync-over-async, and the task-run rule owns a `Task.Run` wrapper. No rule remains. |

## Project File Shapes From Slopwatch

**Pattern**: a project file that adds `NoWarn`, sets
`TreatWarningsAsErrors` to `false`, sets `Nullable` to `disable`, leaves
`WarningsAsErrors` empty, or bypasses Central Package Management with a
`VersionOverride` attribute or an inline `Version` attribute.

**Why skipped**: a Roslyn `DiagnosticAnalyzer` receives a `Compilation`. It
reads source files and additional files, never the project file that produced
the compilation. E128062 reads an xUnit constant named `ReferenceAssemblies`, so
it is no precedent for project-file access. A project-file guard belongs in a
script or an MSBuild target that fails the build.

**Evidence**: slopwatch rules SW005 and SW006 implement these checks against
`*.csproj`, `*.props`, and `*.targets` files. The repository already routes
dependency questions through `scripts/deps-graph.sh` and
`scripts/nuget-heat-map.sh`.

**Source**: [slopwatch-sweep.md](slopwatch-sweep.md), read 2026-11-18

## Slopwatch Baseline And Diff Scope

**Pattern**: a JSON baseline that hashes each finding, so a scan reports only
new slop, plus report filtering to the added and modified lines of a diff.

**Why skipped**: both mechanisms hold tool state, not a code shape. A
diff-scoped Roslyn report duplicates the IDE. A baseline lets a known defect
stay in the tree while the build passes, which conflicts with the repository
rule that a skip and a suppression each need a tracked reason.

**Source**: [slopwatch-sweep.md](slopwatch-sweep.md), read 2026-11-18
