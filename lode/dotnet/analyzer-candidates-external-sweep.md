# External Analyzer Candidate Sweep
*Updated: 2026-11-05T13:32:58Z*

Source: a research scroll on e128.info, snapshotted 2026-11-05. The scroll lists
thirty defect shapes. Each was checked against the E128 set and six third-party
rule libraries.

The verification read every rule in the Microsoft CA and IDE indexes, the
Meziantou catalog, the Roslynator catalog, the sonar-dotnet C# catalog, the
SharpSource catalog, AsyncFixer, the VS Threading analyzers, the xUnit
analyzers, the EF Core analyzers, and the ASP.NET Core diagnostics. A rule
counts as an owner only when its description covers the shape.

This file holds the recorded candidates from that sweep. The mining pass in
[analyzer-candidates.md](analyzer-candidates.md) owns the review-driven
candidates. The rule table in `src/E128.Analyzers/README.md` owns the shipped
rule text.

## Standing

No shape stays open. An E128 rule closes every candidate: twenty-four new rules
and three extensions, of E128103, E128154, and E128159. Three shapes have a real
owner and are rejected below. The sweep is complete.

The next free ID is **E128199**. E128095, E128130, and E128158 are also free.
E128022 is retired and stays out of use.

The ownership check reads rule metadata, not a compiled run. Test each candidate
against the installed analyzer packages before you build it. The installed set
is AsyncFixer, Meziantou, Roslynator, SharpSource, Sonar, and VS Threading. See
`Directory.Packages.props` for the current versions.

## Candidates

No candidate stays open. The last two, candidate 27 and candidate 28, shipped as
E128197 and E128198. The number matches the source scroll, so a shipped or
rejected row stays traceable.

E128057 does not cover candidate 4. E128057 flags unprotected cleanup calls in
`finally`. It does not flag an `await` there. Candidate 4 shipped as E128186.

Twenty-four candidates shipped as new rules. Candidate 2 is E128175, candidate 1
is E128182, candidate 3 is E128183, candidate 29 is E128184, and candidate 25 is
E128185. Candidate 8 is E128181, candidate 12 is E128176, candidate 15 is
E128177, and candidate 16 is E128178. Candidate 22 is E128179, candidate 24 is
E128180, candidate 4 is E128186, candidate 6 is E128187, candidate 30 is
E128188, and candidate 11 is E128189. Candidate 7 is E128190, candidate 9 is
E128191, candidate 10 is E128192, candidate 13 is E128193, candidate 19 is
E128194, candidate 21 is E128195, and candidate 17 is E128196. Candidate 27 is
E128197 and candidate 28 is E128198. Candidate 20 extended E128103, candidate
23 extended E128154, and candidate 5 extended E128159. The detection shape and
the exclusions live in
[analyzers/rule-details-reliability.md](analyzers/rule-details-reliability.md),
[analyzers/rule-details-reliability-concurrency.md](analyzers/rule-details-reliability-concurrency.md),
[analyzers/rule-details-reliability-resources.md](analyzers/rule-details-reliability-resources.md),
[analyzers/rule-details-performance.md](analyzers/rule-details-performance.md),
and [analyzers/rule-details.md](analyzers/rule-details.md).

## Checked And Rejected

A second rule on these shapes would report a line that already fires.

| #   | Shape                                    | Owner                                        |
| --- | ---------------------------------------- | -------------------------------------------- |
| 14  | `Timer` created and never stored in a field | CA2000 `Dispose objects before losing scope` |
| 18  | Persisted `string.GetHashCode`           | MA0021 `Use StringComparer.GetHashCode`      |
| 26  | String sort without a comparer           | MA0002 `IComparer<string> is missing`        |

CA2000 does not exempt `System.Threading.Timer`, so a discarded or local-only
timer already reports. MA0021 fires on every `string.GetHashCode` call, which is
broader than the persisted case. MA0002 covers `OrderBy`, `ThenBy`, `Distinct`,
and the sorted collection types.

Two slivers stay open after a rejection. `List.Sort()` and `Array.Sort()` on
strings have no owner. Both are thin, so neither earns its own rule yet.

## Detection Notes

Data-flow candidates need `AnalyzeDataFlow` or a method-local walk. Keep them
method-scoped so the cost stays bounded. Candidate 28 shipped as E128198.

Compilation candidates need `RegisterCompilationStartAction` and a symbol table
built once per compilation. Candidate 21 shipped as E128195. It reads
`context.Options.AdditionalFiles`, parses each `*.json` file with
`System.Text.Json` 10.0.14, and requires a documented consumer contract
(`<AdditionalFiles Include="appsettings.json" />`). The package carries the JSON
DLL closure in `analyzers/dotnet/cs`.

A destructive-path rule defaults to `Info`. A false positive on a build error
blocks work for no gain.

A rule with a code fix reports only the shape its fix can repair. The E128 set
already uses that invariant.

Candidate 28 has a partial third-party owner. CA1514 flags a `Substring` length
that repeats the start index. It does not cover the underflow.

## Not Analyzer Work

Four review checks in the source scroll stay human. They are design intent, test
adequacy, pull-request scope, and a cross-service threat model. Each needs
context that a single compilation does not hold.
