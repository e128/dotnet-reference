# Tech Debt Audit — E128.Reference
Generated: 2026-05-09

## Executive summary

- 0 CRITICAL
- 3 HIGH: God file (1033 LOC), dead code module (6 unused types), missing PublicApiAnalyzers on published NuGet package
- 8 MEDIUM: 2 more god files (611, 515 LOC), 7 RCS9004 pragma suppressions, manual health endpoint, DI inconsistency (Cli vs Web), 6 untested helper files, bus factor = 1
- 4 LOW: No InternalsVisibleTo, unshipped analyzer releases, 5 rules in unshipped manifest, Architecture.Tests layer coverage gaps

## Architectural mental model

This is a .NET 10 reference repository with four production assemblies. `E128.Reference.Core` provides a shared greeting domain (models, services, repositories). `E128.Reference.Web` is a minimal API app consuming Core. `E128.Reference.Cli` is a System.CommandLine tool also consuming Core. `E128.Analyzers` is a standalone Roslyn analyzer package (the only NuGet-published artifact) with no dependency on the reference apps. Five test projects cover each production assembly, plus ArchUnitNET architecture tests enforcing structural invariants.

The analyzer package is the active development surface — nearly all churn (38/50 recent file modifications) concentrates there. Core, Web, and Cli are near-static demo implementations. The Core greeting domain (GreetingService, repositories, models) appears to be reference code for demonstrating patterns but is not wired into any production entry point.

## Findings

| ID   | Category                | File:Line                                                        | Severity | Effort | Description                                                                                              | Recommendation                                                                               |
| ---- | ----------------------- | ---------------------------------------------------------------- | -------- | ------ | -------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| F001 | Architectural decay     | src/E128.Analyzers/Reliability/DiskRoundtripAnalyzer.cs:1        | HIGH     | L      | 1033-line god file — single analyzer with complex IO catalog logic mixed with Roslyn analysis             | Extract DiskIoCatalog methods into a separate utility class                                  |
| F002 | Architectural decay     | src/E128.Analyzers/Reliability/GeneratedRegexAnalyzer.cs:1       | MEDIUM   | M      | 611-line analyzer handling 4 distinct diagnostic rules (compiled, timeout, nested, overlapping)           | Split into 4 focused analyzers or extract shared regex analysis logic                        |
| F003 | Architectural decay     | src/E128.Analyzers/FileSystem/FileSystemPathAnalyzer.cs:1        | MEDIUM   | M      | 515-line analyzer — manages complex path pattern matching inline                                         | Extract pattern matching tables to a companion catalog file like IoMethodCatalog              |
| F004 | Architectural decay     | src/E128.Reference.Core/Services/GreetingService.cs:12           | HIGH     | S      | GreetingService, IGreetingService, IGreetingRepository, InMemoryGreetingRepository, Greeting, GreetingRequest — 6 types never registered in DI, never used by Web or Cli entry points | Either wire into Web/Cli DI or delete — dead reference code creates false test coverage      |
| F005 | Architectural decay     | src/E128.Reference.Core/Repositories/IGreetingRepository.cs:11   | HIGH     | S      | IGreetingRepository and InMemoryGreetingRepository are unused outside their own assembly + tests          | Part of F004 — delete or use                                                                 |
| F006 | Consistency rot         | src/E128.Reference.Cli/CliApp.cs:21                              | MEDIUM   | S      | `new Greeter()` direct instantiation bypasses DI; Web uses `AddSingleton<Greeter>()` — inconsistent pattern | Inject via DI in Cli or acknowledge Cli is intentionally DI-free                             |
| F007 | Consistency rot         | src/E128.Reference.Web/Program.cs:13                             | MEDIUM   | S      | Manual `/health` endpoint (`Results.Ok(new { status = "healthy" })`) instead of `AddHealthChecks()` + `MapHealthChecks()` — non-standard health contract | Use `builder.Services.AddHealthChecks()` + `app.MapHealthChecks("/health")`                  |
| F008 | Test debt               | src/E128.Analyzers/FileSystem/IoMethodCatalog.cs                 | MEDIUM   | S      | 6 helper/utility files have no direct test coverage: IoMethodCatalog, PathNamePatterns, SuggestedType, InModifierHelper, DiskIoCatalog, SequentialRenameFixAllProvider | Tested indirectly via analyzer tests — acceptable if intentional; add direct tests for catalog correctness |
| F009 | Dependency debt         | src/E128.Analyzers/FileSystem/FileSystemPathAnalyzer.cs:245      | MEDIUM   | S      | 7 `#pragma warning disable RCS9004` suppressions across 4 analyzer files — all suppress "use .Any() instead of .Count" | Add RCS9004 to analyzer project .editorconfig with `dotnet_diagnostic.RCS9004.severity = none` instead of per-site pragmas |
| F010 | Service contract        | Directory.Packages.props                                         | HIGH     | M      | E128.Analyzers is NuGet-published but has no Microsoft.CodeAnalysis.PublicApiAnalyzers — breaking API changes can ship silently | Add PublicApiAnalyzers package + ship/unship tracking files                                   |
| F011 | Knowledge concentration | (repo-wide)                                                      | MEDIUM   | —      | Single author (millerb@gmail.com) wrote 104/121 commits (86%) in last 12 months — bus factor = 1        | Document architecture decisions in lode/ for onboarding; this is inherent to a personal reference repo |
| F012 | Test debt               | src/E128.Reference.Core/Services/IGreetingService.cs:10          | LOW      | S      | No InternalsVisibleTo in any src project — prevents testing internal types without reflection             | Add `[InternalsVisibleTo("ProjectName.Tests")]` to projects with internal types worth testing |
| F013 | Fitness functions       | tests/Architecture.Tests/                                        | LOW      | M      | Architecture tests verify layers, naming, sealed — but don't verify circular dependency prevention or assembly size budgets | Add circular dep check and assembly size budget assertion                                     |
| F014 | Service contract        | src/E128.Analyzers/AnalyzerReleases.Unshipped.md                 | LOW      | S      | 5 analyzer rules (E128066-E128070) sitting in Unshipped.md — need to be released or documented as pre-release | Ship or document the release timeline                                                        |
| F015 | Documentation drift     | src/E128.Reference.Core/                                         | LOW      | S      | XML doc comments present on all 7 Core types but Web/Cli public types lack them                          | Add XML docs to Program.cs entry points and CliApp.cs                                        |

## Top 5 "if you fix nothing else, fix these"

1. **F004/F005 — Delete or wire the dead Core domain.** GreetingService, repositories, and models exist solely for test demonstration but aren't used by any entry point. They inflate code coverage metrics falsely and mislead readers about the app's actual architecture. Either register them in Web's DI pipeline or delete them and let Greeter stand alone.

2. **F010 — Add PublicApiAnalyzers to E128.Analyzers.** This is a published NuGet package. Without PublicApiAnalyzers, any method signature change ships as a silent breaking change. Add `Microsoft.CodeAnalysis.PublicApiAnalyzers`, generate initial `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`, and let CI enforce API compatibility.

3. **F001 — Decompose DiskRoundtripAnalyzer.cs.** At 1033 lines, this is the largest file and a hotspot (4 changes in 6 months). The disk IO catalog logic should extract to a separate file, similar to how `IoMethodCatalog.cs` already exists for `FileSystemPathAnalyzer`.

4. **F009 — Replace 7 RCS9004 pragmas with an editorconfig rule.** Seven identical `#pragma warning disable RCS9004` scattered across 4 files. A single `.editorconfig` entry in the analyzer project (`dotnet_diagnostic.RCS9004.severity = none`) replaces all of them.

5. **F007 — Use ASP.NET health check middleware.** The manual health endpoint returns `{ "status": "healthy" }` but doesn't integrate with orchestrator health check protocols. `AddHealthChecks()` + `MapHealthChecks()` gives you standard health reporting, degraded/unhealthy states, and Docker HEALTHCHECK compatibility for free.

## Quick wins

- [ ] F009: Add `dotnet_diagnostic.RCS9004.severity = none` to `src/E128.Analyzers/.editorconfig` and remove 7 pragma suppressions
- [ ] F007: Replace manual `/health` with `AddHealthChecks()` + `MapHealthChecks("/health")`
- [ ] F014: Move unshipped rules to `AnalyzerReleases.Shipped.md` on next version bump
- [ ] F012: Add `[InternalsVisibleTo]` to projects with testable internals
- [ ] F015: Add XML doc comments to `CliApp.cs` public API

## Things that look bad but are actually fine

- **Pragma suppressions (RCS9004) in analyzer code.** These look like suppression sprawl, but RCS9004 ("use .Any() instead of .Count") fires on syntax tree `.Count` checks where the analyzer genuinely needs the count value, not just existence. The fix is an editorconfig disable (F009), not changing the code.

- **`new Greeter()` in Cli without DI.** This looks like a DI inconsistency (F006), but Cli is a System.CommandLine app where the DI container setup is intentionally minimal. Direct instantiation is a valid pattern for simple CLI tools. Flagged as MEDIUM not HIGH because both approaches are defensible.

- **No `TimeProvider` injection in Core.** `GreetingService` accepts `TimeProvider` via primary constructor — this is correct. `Greeter` doesn't use time at all. No `DateTime.Now`/`DateTime.UtcNow` found in any production source.

- **Single `AddSingleton<Greeter>()` without interface.** `Greeter` has no interface, registered as concrete type. This normally triggers E128032 (the repo's own ConcreteOnlyDiRegistrationAnalyzer), but `Greeter` is a leaf service with no need for abstraction. The analyzer exempts types with no implemented interfaces.

- **E128.Analyzers has no project references to the reference apps.** This is by design — analyzers are standalone Roslyn packages, they must not reference consumer assemblies.

- **130 test files vs 135 source files.** Looks like missing coverage, but the delta is 5 utility/helper files (IoMethodCatalog, PathNamePatterns, SuggestedType, InModifierHelper, DiskIoCatalog) that are tested indirectly via the analyzer tests that consume them. Plus SequentialRenameFixAllProvider which is infrastructure.

## Open questions for the maintainer

- Is the Core greeting domain (GreetingService, repositories, Greeting model) intentionally dead code kept for demonstration purposes, or should it be wired into the Web app?
- Should the 5 unshipped analyzer rules (E128066-E128070) be released with the next version bump, or are they intentionally held back for further testing?
- Is the manual `/health` endpoint intentional (to demonstrate minimal API patterns) or should it use the standard health check middleware?
- Is the lack of `InternalsVisibleTo` intentional (keeping all testable APIs public) or an oversight?
