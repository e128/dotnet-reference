# Tech Debt Audit — E128.Reference
Generated: 2026-05-10

## Executive summary

- 0 CRITICAL
- 0 HIGH (3 resolved: F001, F004/F005, F010)
- 2 MEDIUM remaining (F011 bus factor, F017 missing CancellationToken in Web endpoints)
- 6 LOW (F012–F016 unchanged, F018 redundant pragmas, F019 stale NuGet description)
- 9 findings resolved from prior runs: F001, F002, F003, F004/F005, F006, F007, F008, F009, F010
- F016 updated: Meziantou.Analyzer bumped to 3.0.77

## Architectural mental model

This is a .NET 10 reference repository with four production assemblies. `E128.Reference.Core` provides a shared greeting domain (models, services, repositories). `E128.Reference.Web` is a minimal API app consuming Core. `E128.Reference.Cli` is a System.CommandLine tool also consuming Core. `E128.Analyzers` is a standalone Roslyn analyzer package (the only NuGet-published artifact) with no dependency on the reference apps. Five test projects cover each production assembly, plus ArchUnitNET architecture tests enforcing structural invariants.

The analyzer package remains the active development surface — nearly all recent churn concentrates there (NamingStyleCodeFixProvider: 8 changes in 6 months, plus batch additions of E128066-E128070). Core, Web, and Cli are near-static demo implementations. Source file count: 147 src, 144 test — a healthy ratio. FIPS compliance guardrails (CA5350-CA5403) were explicitly pinned in `.globalconfig` this session; no crypto code exists in the codebase.

## Findings

| ID   | Category                | File:Line                                                      | Severity | Effort | Status    | Description                                                                                              | Recommendation                                                                               |
| ---- | ----------------------- | -------------------------------------------------------------- | -------- | ------ | --------- | -------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| F001 | Architectural decay     | src/E128.Analyzers/Reliability/DiskRoundtripAnalyzer.cs:1      | HIGH     | L      | RESOLVED  | 1033-line god file — extracted DiskIoCatalog helpers into DiskIoCatalog.cs                                | Done                                                                                         |
| F002 | Architectural decay     | src/E128.Analyzers/Reliability/GeneratedRegexAnalyzer.cs:1     | MEDIUM   | M      | RESOLVED  | 611-line analyzer — extracted regex pattern analysis to GeneratedRegexHelpers.cs                          | Done                                                                                         |
| F003 | Architectural decay     | src/E128.Analyzers/FileSystem/FileSystemPathAnalyzer.cs:1      | MEDIUM   | M      | RESOLVED  | 515-line analyzer — extracted helpers to FileSystemPathHelpers.cs                                         | Done                                                                                         |
| F004 | Architectural decay     | src/E128.Reference.Web/Program.cs:15                           | HIGH     | S      | RESOLVED  | Core domain types wired into Web DI with POST/GET endpoints                                              | Done                                                                                         |
| F005 | Architectural decay     | src/E128.Reference.Web/Program.cs:15                           | HIGH     | S      | RESOLVED  | Part of F004 — repositories registered and used                                                          | Done                                                                                         |
| F006 | Consistency rot         | src/E128.Reference.Cli/CliApp.cs:8                             | MEDIUM   | S      | RESOLVED  | Cli uses DI via ServiceCollection + ServiceProvider, consistent with Web                                 | Done                                                                                         |
| F007 | Consistency rot         | src/E128.Reference.Web/Program.cs:17                           | MEDIUM   | S      | RESOLVED  | Manual `/health` replaced with `AddHealthChecks()` + `MapHealthChecks("/health")`                        | Done                                                                                         |
| F008 | Test debt               | tests/E128.Analyzers.Tests/IoMethodCatalogTests.cs             | MEDIUM   | S      | RESOLVED  | Added direct tests for IoMethodCatalog, PathNamePatterns, SuggestedType                                  | Done                                                                                         |
| F009 | Consistency rot         | src/E128.Analyzers/.editorconfig:5                             | MEDIUM   | S      | RESOLVED  | Replaced 7 RCS9004 pragmas with project `.editorconfig` suppression                                     | Done                                                                                         |
| F010 | Service contract        | src/E128.Analyzers/E128.Analyzers.csproj:41                    | HIGH     | M      | RESOLVED  | Added PublicApiAnalyzers + PublicAPI.Shipped.txt + PublicAPI.Unshipped.txt                                | Done                                                                                         |
| F011 | Knowledge concentration | (repo-wide)                                                    | MEDIUM   | —      | UNCHANGED | Single author (millerb@gmail.com) wrote 106/124 commits (85%) in last 12 months — bus factor = 1        | Document architecture decisions in lode/ for onboarding; inherent to a personal reference repo |
| F012 | Test debt               | src/E128.Reference.Core/ and src/E128.Reference.Web/           | LOW      | S      | UNCHANGED | 2 of 4 src projects have InternalsVisibleTo (Cli, Analyzers); Core and Web lack it — but neither has internal members currently | No action unless internal types are added to Core or Web                                      |
| F013 | Fitness functions       | tests/Architecture.Tests/                                      | LOW      | M      | UNCHANGED | Architecture tests verify layers, naming, sealed, service patterns — but don't verify circular deps or assembly size budgets | Add circular dependency prevention and assembly size budget assertions                        |
| F014 | Service contract        | src/E128.Analyzers/AnalyzerReleases.Unshipped.md               | LOW      | S      | UNCHANGED | 5 analyzer rules (E128066-E128070) sitting in Unshipped.md                                               | Ship with next version bump or document as pre-release                                       |
| F015 | Documentation drift     | src/E128.Reference.Web/Program.cs                              | LOW      | S      | UNCHANGED | Web and Cli public types lack XML doc comments (Core types have them)                                    | Add XML docs to Program.cs entry points and CliApp.cs                                        |
| F016 | Dependency debt         | Directory.Packages.props                                       | LOW      | S      | MOVED     | Meziantou.Analyzer bumped to 3.0.77. Transitive System.Composition pins at 9.0.0 remain — held by Roslyn compatibility | Monitor for Roslyn package updates via Renovate                                               |
| F017 | Reliability             | src/E128.Reference.Web/Program.cs:23                           | MEDIUM   | S      | NEW       | Async endpoints (`MapPost "/greetings"`, `MapGet "/greetings"`) don't accept `CancellationToken` — request abort won't propagate to `GreetAsync` or `GetRecentAsync` | Add `CancellationToken cancellationToken` parameter to each lambda; ASP.NET Core injects it from `HttpContext.RequestAborted` |
| F018 | Consistency rot         | src/E128.Analyzers/FileSystem/FileSystemPathAnalyzer.cs:267    | LOW      | S      | NEW       | 2 residual `#pragma warning disable RCS9004` in FileSystem files — redundant since `src/E128.Analyzers/.editorconfig` disables RCS9004 project-wide | Remove the 2 remaining pragmas and their matching restores                                    |
| F019 | Documentation drift     | src/E128.Analyzers/E128.Analyzers.csproj:30                    | LOW      | S      | NEW       | NuGet `<Description>` says "64 rules" — actual count is 70 unique diagnostic IDs (E128001-E128070, with 5 unshipped) | Update description to reflect current rule count: "70 rules" (or "65 shipped + 5 pre-release") |

## Top 5 "if you fix nothing else, fix these"

All prior top-5 findings have been resolved. Current priorities:

1. **F017 — MEDIUM.** Add `CancellationToken` to async Web endpoints. Small change, high reliability impact — without it, cancelled HTTP requests continue executing in the background.
2. **F014 — LOW.** Ship E128066-E128070 (5 unshipped analyzer rules) with the next version bump.
3. **F019 — LOW.** Update NuGet package description — "64 rules" is stale.
4. **F018 — LOW.** Remove 2 redundant RCS9004 pragmas.
5. **F013 — LOW.** Add circular dependency prevention architecture tests.

## Quick wins

- [x] F009: Add `dotnet_diagnostic.RCS9004.severity = none` to `src/E128.Analyzers/.editorconfig` and remove pragma suppressions
- [x] F007: Replace manual `/health` with `AddHealthChecks()` + `MapHealthChecks("/health")`
- [ ] F017: Add `CancellationToken cancellationToken` to the two async endpoint lambdas in Program.cs
- [ ] F018: Remove 2 residual RCS9004 pragmas in FileSystemPathAnalyzer.cs:267 and FileSystemPathHelpers.cs:97
- [ ] F019: Update NuGet `<Description>` rule count from "64" to "70"
- [ ] F014: Move unshipped rules to `AnalyzerReleases.Shipped.md` on next version bump
- [ ] F015: Add XML doc comments to `CliApp.cs` and `Program.cs` public APIs
- [ ] F016: Monitor Roslyn package updates via Renovate for System.Composition transitive bump

## Things that look bad but are actually fine

- **DiskRoundtripAnalyzer.cs (607 lines) and DiskIoCatalog.cs (562 lines).** These exceed the 500-line threshold and were previously flagged (F001). After decomposition, DiskRoundtripAnalyzer has the core analysis logic and DiskIoCatalog has the method catalog and helpers. Both are cohesive — further splitting would scatter related logic without reducing complexity.

- **NamingStyleCodeFixProvider.cs (406 lines, highest churn).** This is the most frequently modified file (8 changes in 6 months). However, it's a complex code fix provider that handles multiple naming convention transformations (PascalCase, camelCase, underscore prefixes). The churn is legitimate feature additions, not rework.

- **`count ?? 10` in MapGet "/greetings".** Looks like a magic number, but it's a default pagination limit in a demo endpoint — naming a constant for a single-use default in a reference app adds ceremony without value.

- **No `ConfigureAwait(false)` in Web endpoints.** Web app code runs in the ASP.NET Core SynchronizationContext — `ConfigureAwait(false)` is not needed and CA2007 is correctly scoped to DLL projects only.

- **No `IHostApplicationLifetime` in Web.** `WebApplication.RunAsync()` handles SIGTERM gracefully via the host's built-in shutdown — no custom handler needed for this simple app.

- **2 remaining RCS9004 pragmas (F018).** These are in FileSystem analyzer files where `SeparatedSyntaxList<T>.Count` is O(1) and `.Any()` would allocate. The pragmas are semantically correct — they're just redundant now that the .editorconfig disables RCS9004 project-wide.

- **No `async void`, sync-over-async, or direct `HttpClient` instantiation in production code.** These patterns exist only in analyzer diagnostic message strings (the analyzers detect these patterns in consumer code).

- **No crypto code, no FIPS violations.** The codebase uses zero `System.Security.Cryptography` APIs. CA53xx FIPS guardrails are explicitly pinned in `.globalconfig` as preventive controls.

- **`new Greeter()` pattern not used — Cli uses DI.** F006 was resolved; `CliApp.ConfigureServices` registers `Greeter` via `ServiceCollection`.

- **Single `AddSingleton<Greeter>()` without interface.** `Greeter` has no interface, registered as concrete type. This would normally trigger E128032 (ConcreteOnlyDiRegistrationAnalyzer), but `Greeter` is a leaf service with no need for abstraction.

- **Transitive dependencies on System.Composition 9.0.0.** These are pulled by Microsoft.CodeAnalysis packages and pinned appropriately. Bumping to 10.0.7 could break Roslyn compatibility.

- **No `SuppressMessage` attributes in source.** Zero instances — all suppressions use scoped `#pragma` pairs with restore.

## Open questions for the maintainer

- Should the 5 unshipped analyzer rules (E128066-E128070) be released with the next version bump, or are they intentionally held back for further testing?
- Is the lack of `InternalsVisibleTo` in Core and Web intentional (all types are public currently) or should it be added proactively for future internal types?
