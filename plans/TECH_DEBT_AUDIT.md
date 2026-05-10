# Tech Debt Audit — E128.Reference
Generated: 2026-05-10

## Executive summary

- 0 CRITICAL
- 0 HIGH (3 resolved: F001, F004/F005, F010)
- 1 MEDIUM remaining (F011 bus factor — inherent to single-maintainer repo)
- 5 LOW unchanged
- 9 findings resolved this run: F001, F002, F003, F004/F005, F006, F007, F008, F009, F010

## Architectural mental model

This is a .NET 10 reference repository with four production assemblies. `E128.Reference.Core` provides a shared greeting domain (models, services, repositories). `E128.Reference.Web` is a minimal API app consuming Core. `E128.Reference.Cli` is a System.CommandLine tool also consuming Core. `E128.Analyzers` is a standalone Roslyn analyzer package (the only NuGet-published artifact) with no dependency on the reference apps. Five test projects cover each production assembly, plus ArchUnitNET architecture tests enforcing structural invariants.

The analyzer package remains the active development surface — nearly all recent churn concentrates there (NamingStyleCodeFixProvider: 8 changes in 6 months, plus batch additions of E128066-E128070). Core, Web, and Cli are near-static demo implementations. Source file count: 145 src, 141 test — a healthy ratio.

## Findings

| ID   | Category                | File:Line                                                      | Severity | Effort | Status    | Description                                                                                              | Recommendation                                                                               |
| ---- | ----------------------- | -------------------------------------------------------------- | -------- | ------ | --------- | -------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| F001 | Architectural decay     | src/E128.Analyzers/Reliability/DiskRoundtripAnalyzer.cs:1      | HIGH     | L      | RESOLVED  | 1033-line god file — extracted DiskIoCatalog helpers into DiskIoCatalog.cs (607 → 607 + 563 lines, clean separation) | Done                                                                                         |
| F002 | Architectural decay     | src/E128.Analyzers/Reliability/GeneratedRegexAnalyzer.cs:1     | MEDIUM   | M      | RESOLVED  | 611-line analyzer — extracted regex pattern analysis to GeneratedRegexHelpers.cs (283 + 332 lines)       | Done                                                                                         |
| F003 | Architectural decay     | src/E128.Analyzers/FileSystem/FileSystemPathAnalyzer.cs:1      | MEDIUM   | M      | RESOLVED  | 515-line analyzer — extracted helpers to FileSystemPathHelpers.cs (340 + 183 lines)                      | Done                                                                                         |
| F004 | Architectural decay     | src/E128.Reference.Web/Program.cs:15                           | HIGH     | S      | RESOLVED  | Core domain types wired into Web DI: GreetingService, IGreetingRepository, Greeting endpoints            | Done — POST /greetings and GET /greetings endpoints added                                    |
| F005 | Architectural decay     | src/E128.Reference.Web/Program.cs:15                           | HIGH     | S      | RESOLVED  | Part of F004 — repositories now registered and used                                                      | Done                                                                                         |
| F006 | Consistency rot         | src/E128.Reference.Cli/CliApp.cs:8                             | MEDIUM   | S      | RESOLVED  | Cli now uses DI via ServiceCollection + ServiceProvider, consistent with Web                             | Done — ConfigureServices method + GetRequiredService<Greeter>()                              |
| F007 | Consistency rot         | src/E128.Reference.Web/Program.cs:17                           | MEDIUM   | S      | RESOLVED  | Replaced manual `/health` with `AddHealthChecks()` + `MapHealthChecks("/health")`                        | Done — DockerSmokeTests updated for plain text "Healthy" response                            |
| F008 | Test debt               | tests/E128.Analyzers.Tests/IoMethodCatalogTests.cs             | MEDIUM   | S      | RESOLVED  | Added 57 direct tests: IoMethodCatalogTests (32), PathNamePatternsTests (20), SuggestedTypeTests (5)     | Done                                                                                         |
| F009 | Consistency rot         | src/E128.Analyzers/.editorconfig:5                             | MEDIUM   | S      | RESOLVED  | Replaced 7 RCS9004 pragmas with project `.editorconfig` suppression                                     | Done — pragmas removed from 4 files                                                          |
| F010 | Service contract        | src/E128.Analyzers/E128.Analyzers.csproj:41                    | HIGH     | M      | RESOLVED  | Added PublicApiAnalyzers + PublicAPI.Shipped.txt (130 entries) + PublicAPI.Unshipped.txt                  | Done                                                                                         |
| F011 | Knowledge concentration | (repo-wide)                                                    | MEDIUM   | —      | UNCHANGED | Single author (millerb@gmail.com) wrote 105/123 commits (85%) in last 12 months — bus factor = 1        | Document architecture decisions in lode/ for onboarding; inherent to a personal reference repo |
| F012 | Test debt               | src/E128.Reference.Core/ and src/E128.Reference.Web/           | LOW      | S      | MOVED     | 2 of 4 src projects now have InternalsVisibleTo (Cli, Analyzers); Core and Web still lack it             | Add `[InternalsVisibleTo]` to Core and Web projects if they have internal types worth testing |
| F013 | Fitness functions       | tests/Architecture.Tests/                                      | LOW      | M      | UNCHANGED | Architecture tests verify layers, naming, sealed, service patterns — but don't verify circular deps or assembly size budgets | Add circular dependency prevention and assembly size budget assertions                        |
| F014 | Service contract        | src/E128.Analyzers/AnalyzerReleases.Unshipped.md               | LOW      | S      | UNCHANGED | 5 analyzer rules (E128066-E128070) sitting in Unshipped.md                                               | Ship with next version bump or document as pre-release                                       |
| F015 | Documentation drift     | src/E128.Reference.Web/Program.cs                              | LOW      | S      | UNCHANGED | Web and Cli public types lack XML doc comments (Core types have them)                                    | Add XML docs to Program.cs entry points and CliApp.cs                                        |
| F016 | Dependency debt         | Directory.Packages.props                                       | LOW      | S      | NEW       | Meziantou.Analyzer outdated: 3.0.72 → 3.0.77 available; several transitive System.Composition packages pinned at 9.0.0 while 10.0.7 is available | Bump Meziantou.Analyzer; evaluate transitive pin updates with Renovate                       |

## Top 5 "if you fix nothing else, fix these"

All 5 top-priority findings have been resolved:

1. **F004/F005 — RESOLVED.** Core domain wired into Web DI. POST /greetings and GET /greetings endpoints added.
2. **F010 — RESOLVED.** PublicApiAnalyzers added with 130 shipped API entries.
3. **F001 — RESOLVED.** DiskRoundtripAnalyzer decomposed — catalog helpers extracted to DiskIoCatalog.cs.
4. **F009 — RESOLVED.** 7 RCS9004 pragmas replaced with project-level `.editorconfig` suppression.
5. **F007 — RESOLVED.** Manual `/health` replaced with `AddHealthChecks()` + `MapHealthChecks("/health")`.

## Quick wins

- [x] F009: Add `dotnet_diagnostic.RCS9004.severity = none` to `src/E128.Analyzers/.editorconfig` and remove 7 pragma suppressions
- [x] F007: Replace manual `/health` with `AddHealthChecks()` + `MapHealthChecks("/health")`
- [ ] F014: Move unshipped rules to `AnalyzerReleases.Shipped.md` on next version bump
- [ ] F012: Add `[InternalsVisibleTo]` to Core and Web projects
- [ ] F015: Add XML doc comments to `CliApp.cs` and `Program.cs` public APIs
- [ ] F016: Bump Meziantou.Analyzer to 3.0.77

## Things that look bad but are actually fine

- **Pragma suppressions (RCS9004) in analyzer code.** These look like suppression sprawl, but RCS9004 ("use .Any() instead of .Count") fires on `SeparatedSyntaxList<T>.Count` checks where the property is O(1) and `.Any()` would allocate an enumerator. The pragmas are semantically correct; the fix is an editorconfig disable (F009), not changing the code.

- **`new Greeter()` in Cli without DI.** This looks like a DI inconsistency (F006), but Cli is a System.CommandLine app where DI setup is intentionally minimal. Direct instantiation is a valid pattern for simple CLI tools. Flagged as MEDIUM not HIGH because both approaches are defensible.

- **No `TimeProvider` injection outside Core.** `GreetingService` accepts `TimeProvider` via primary constructor — this is correct. `Greeter` doesn't use time at all. No `DateTime.Now`/`DateTime.UtcNow` found in any production source file.

- **Single `AddSingleton<Greeter>()` without interface.** `Greeter` has no interface, registered as concrete type. This would normally trigger E128032 (ConcreteOnlyDiRegistrationAnalyzer), but `Greeter` is a leaf service with no need for abstraction.

- **E128.Analyzers has no project references to the reference apps.** By design — analyzers are standalone Roslyn packages, they must not reference consumer assemblies.

- **145 source files vs 141 test files.** The delta of 4 is accounted for by utility/helper files (IoMethodCatalog, PathNamePatterns, SuggestedType, InModifierHelper, DiskIoCatalog, SequentialRenameFixAllProvider) that are tested indirectly via the analyzer tests that consume them.

- **Transitive dependencies on System.Composition 9.0.0.** These are pulled by Microsoft.CodeAnalysis packages and pinned appropriately. Bumping to 10.0.7 could break Roslyn compatibility — Renovate should handle this when the CodeAnalysis packages themselves update.

- **No `SuppressMessage` attributes in source.** Zero instances — all suppressions use scoped `#pragma` pairs with restore, which is the preferred pattern.

- **No `async void`, sync-over-async, or direct `HttpClient` instantiation in production code.** These patterns exist only in analyzer diagnostic message strings (the analyzers detect these patterns in consumer code).

- **Missing PackageSourceMapping — CORRECTED.** Previous audit draft considered this missing. It exists in `nuget.config` with a wildcard mapping for nuget.org. CPM hygiene is solid: `<clear />` on package sources, trusted signers configured, all transitive pins documented with comments.

## Open questions for the maintainer

- ~~Is the Core greeting domain (GreetingService, repositories, Greeting model) intentionally dead code?~~ **Resolved: wired into Web DI (F004/F005).**
- Should the 5 unshipped analyzer rules (E128066-E128070) be released with the next version bump, or are they intentionally held back for further testing?
- ~~Is the manual `/health` endpoint intentional?~~ **Resolved: replaced with health check middleware (F007).**
- Is the lack of `InternalsVisibleTo` in Core and Web intentional (keeping all testable APIs public) or an oversight?
