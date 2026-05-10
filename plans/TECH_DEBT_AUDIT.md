# Tech Debt Audit — E128.Reference
Generated: 2026-05-10

## Executive summary

- 0 CRITICAL, 0 HIGH
- 2 MEDIUM remaining (F011 bus factor — inherent, F022 NuGet.* blocked by Roslyn)
- 1 LOW remaining (F016 System.Composition held by Roslyn)
- 20 findings resolved from prior runs: F001–F010, F012–F015, F017–F021, F023–F025

## Architectural mental model

This is a .NET 10 reference repository with four production assemblies. `E128.Reference.Core` provides a shared greeting domain (models, services, repositories). `E128.Reference.Web` is a minimal API app consuming Core. `E128.Reference.Cli` is a System.CommandLine tool also consuming Core. `E128.Analyzers` is a standalone Roslyn analyzer package (the only NuGet-published artifact) with no dependency on the reference apps. Five test projects cover each production assembly, plus ArchUnitNET architecture tests enforcing structural invariants including circular dependency prevention.

The analyzer package remains the active development surface. 75 unique diagnostic IDs (E128001–E128075) exist, all shipped in release 1.26.1. Package version is 1.26.2. FIPS compliance guardrails (CA5350–CA5403) are explicitly pinned in `.globalconfig`; no crypto code exists in production apps. All production projects are fully up-to-date on direct and transitive packages. Zero deprecated, zero vulnerable packages. Zero build warnings. Zero TODO/FIXME markers. 744 test methods across 5 test projects. 11 architecture tests in ArchUnitNET enforce structural invariants.

## Findings

| ID   | Category             | File:Line                                                      | Severity | Effort | Status    | Description                                                                                              | Recommendation                                                                               |
| ---- | -------------------- | -------------------------------------------------------------- | -------- | ------ | --------- | -------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| F001 | Architectural decay  | src/E128.Analyzers/Reliability/DiskRoundtripAnalyzer.cs:1      | HIGH     | L      | RESOLVED  | 1033-line god file — extracted DiskIoCatalog helpers into DiskIoCatalog.cs                                | Done                                                                                         |
| F002 | Architectural decay  | src/E128.Analyzers/Reliability/GeneratedRegexAnalyzer.cs:1     | MEDIUM   | M      | RESOLVED  | 611-line analyzer — extracted regex pattern analysis to GeneratedRegexHelpers.cs                          | Done                                                                                         |
| F003 | Architectural decay  | src/E128.Analyzers/FileSystem/FileSystemPathAnalyzer.cs:1      | MEDIUM   | M      | RESOLVED  | 515-line analyzer — extracted helpers to FileSystemPathHelpers.cs                                         | Done                                                                                         |
| F004 | Architectural decay  | src/E128.Reference.Web/Program.cs:15                           | HIGH     | S      | RESOLVED  | Core domain types wired into Web DI with POST/GET endpoints                                              | Done                                                                                         |
| F005 | Architectural decay  | src/E128.Reference.Web/Program.cs:15                           | HIGH     | S      | RESOLVED  | Part of F004 — repositories registered and used                                                          | Done                                                                                         |
| F006 | Consistency rot      | src/E128.Reference.Cli/CliApp.cs:8                             | MEDIUM   | S      | RESOLVED  | Cli uses DI via ServiceCollection + ServiceProvider, consistent with Web                                 | Done                                                                                         |
| F007 | Consistency rot      | src/E128.Reference.Web/Program.cs:17                           | MEDIUM   | S      | RESOLVED  | Manual `/health` replaced with `AddHealthChecks()` + `MapHealthChecks("/health")`                        | Done                                                                                         |
| F008 | Test debt            | tests/E128.Analyzers.Tests/IoMethodCatalogTests.cs             | MEDIUM   | S      | RESOLVED  | Added direct tests for IoMethodCatalog, PathNamePatterns, SuggestedType                                  | Done                                                                                         |
| F009 | Consistency rot      | src/E128.Analyzers/.editorconfig:5                             | MEDIUM   | S      | RESOLVED  | Replaced 7 RCS9004 pragmas with project `.editorconfig` suppression                                     | Done                                                                                         |
| F010 | Service contract     | src/E128.Analyzers/E128.Analyzers.csproj:41                    | HIGH     | M      | RESOLVED  | Added PublicApiAnalyzers + PublicAPI.Shipped.txt + PublicAPI.Unshipped.txt                                | Done                                                                                         |
| F011 | Knowledge conc.      | (repo-wide)                                                    | MEDIUM   | —      | UNCHANGED | Single author wrote 100% of recent commits — bus factor = 1                                              | Document architecture decisions in lode/ for onboarding; inherent to a personal reference repo |
| F012 | Test debt            | src/E128.Reference.Core/ and src/E128.Reference.Web/           | LOW      | S      | RESOLVED  | Added InternalsVisibleTo to Core (→ Core.Tests) and Web (→ Reference.Tests)                              | Done                                                                                         |
| F013 | Fitness functions    | tests/Architecture.Tests/CircularDependencyTests.cs            | LOW      | M      | RESOLVED  | Added CircularDependencyTests with 2 assertions preventing layer back-references                         | Done                                                                                         |
| F014 | Service contract     | src/E128.Analyzers/AnalyzerReleases.Shipped.md                 | LOW      | S      | RESOLVED  | Moved 10 rules (E128066–E128075) from Unshipped.md to Shipped.md under Release 1.26.1                   | Done                                                                                         |
| F015 | Documentation drift  | src/E128.Reference.Web/Program.cs                              | LOW      | S      | RESOLVED  | No public types exist in Web or Cli — all are internal or top-level statements. Finding was invalid       | N/A                                                                                          |
| F016 | Dependency debt      | Directory.Packages.props                                       | LOW      | S      | UNCHANGED | Transitive System.Composition pins at 9.0.0 remain — held by Roslyn 5.3.0 compatibility                 | Monitor for Roslyn package updates; bump when Roslyn supports .NET 10 transitives             |
| F017 | Reliability          | src/E128.Reference.Web/Program.cs:24                           | MEDIUM   | S      | RESOLVED  | Async endpoints now accept `CancellationToken` and propagate to service/repository calls                 | Done                                                                                         |
| F018 | Consistency rot      | src/E128.Analyzers/FileSystem/FileSystemPathAnalyzer.cs:266    | LOW      | S      | RESOLVED  | Removed 2 redundant RCS9004 pragmas; simplified exposed `HasFirstArgumentNamed` (IDE0046)                | Done                                                                                         |
| F019 | Documentation drift  | src/E128.Analyzers/E128.Analyzers.csproj:30                    | LOW      | S      | RESOLVED  | Updated NuGet `<Description>` from "64 rules" to "75 rules" with new categories                         | Done                                                                                         |
| F020 | Dependency debt      | Directory.Packages.props                                       | CRITICAL | S      | RESOLVED  | Pinned `System.Security.Cryptography.Pkcs` from deprecated 5.0.0 to 10.0.7                              | Done                                                                                         |
| F021 | Dependency debt      | Directory.Packages.props                                       | HIGH     | M      | RESOLVED  | Pinned `Humanizer.Core` from 2.14.1 to 3.0.10 — no compatibility issues                                 | Done                                                                                         |
| F022 | Dependency debt      | E128.Analyzers.Tests (transitive)                              | MEDIUM   | M      | UNCHANGED | NuGet.* 6.3.4 → 7.3.1 — BLOCKED: `NuGetFrameworkSorter` constructor changed accessibility in 7.x, breaking `Microsoft.CodeAnalysis.Testing.ReferenceAssemblies`. Requires upstream Roslyn testing package update | Wait for Microsoft.CodeAnalysis.CSharp.Analyzer.Testing to support NuGet 7.x                 |
| F023 | Dependency debt      | Directory.Packages.props                                       | MEDIUM   | S      | RESOLVED  | Pinned `Microsoft.ApplicationInsights` from 2.23.0 to 3.1.1 — no compatibility issues                   | Done                                                                                         |
| F024 | Dependency debt      | All test projects (transitive)                                 | MEDIUM   | S      | RESOLVED  | `System.Security.Cryptography.ProtectedData` 4.5.0 → 10.0.7 resolved by F020 Pkcs pin                  | Done                                                                                         |
| F025 | Service contract     | src/E128.Analyzers/PublicAPI.Unshipped.txt                     | MEDIUM   | S      | RESOLVED  | 16 public API entries (8 types: ReadonlyStructInitProperty*, Sha256CreateObsolete*, FipsUnapprovedHash*, InsecureRandomInCryptoContext*, MissingTraitCategory*) moved from Unshipped.txt to Shipped.txt — now in sync with AnalyzerReleases.Shipped.md Release 1.26.1 | Done                                                                                         |

## Top 5 "if you fix nothing else, fix these"

All CRITICAL, HIGH, and most MEDIUM/LOW findings resolved. Only unfixable items remain:

1. **F022 — MEDIUM (blocked).** NuGet.* 6→7 bump causes `MethodAccessException` in Roslyn test infrastructure. Blocked until upstream `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` supports NuGet 7.x.
2. **F011 — MEDIUM (inherent).** Bus factor = 1. Inherent to personal reference repo.
3. **F016 — LOW (held).** System.Composition 9.0.0 transitives held by Roslyn 5.3.0 netstandard2.0 compatibility.

No additional actionable findings identified.

## Quick wins

- [x] F009: `.editorconfig` suppression for RCS9004
- [x] F007: `AddHealthChecks()` + `MapHealthChecks("/health")`
- [x] F020: Pin `System.Security.Cryptography.Pkcs` 10.0.7
- [x] F017: `CancellationToken` on async Web endpoints
- [x] F021: Pin `Humanizer.Core` 3.0.10
- [x] F023: Pin `Microsoft.ApplicationInsights` 3.1.1
- [x] F018: Remove 2 redundant RCS9004 pragmas + simplify `HasFirstArgumentNamed`
- [x] F019: Update NuGet `<Description>` rule count to 75
- [x] F014: Ship 10 rules to `AnalyzerReleases.Shipped.md` under Release 1.26.1
- [x] F012: Add InternalsVisibleTo to Core and Web
- [x] F013: Add CircularDependencyTests to Architecture.Tests
- [x] F015: Verified no public types exist — finding invalid
- [x] F025: Move 16 PublicAPI entries from Unshipped.txt to Shipped.txt

## Things that look bad but are actually fine

- **DiskRoundtripAnalyzer.cs (607 lines) and DiskIoCatalog.cs (562 lines).** Exceed 500-line threshold. Both are cohesive after decomposition — further splitting scatters related logic.

- **NamingStyleCodeFixProvider.cs (406 lines, highest churn: 8 changes in 6 months).** Complex code fix provider handling multiple naming convention transformations. Churn is legitimate feature additions.

- **`count ?? 10` in MapGet "/greetings".** Default pagination limit in a demo endpoint — naming a constant for single-use adds ceremony without value.

- **No `ConfigureAwait(false)` in Web endpoints.** ASP.NET Core SynchronizationContext doesn't require it; CA2007 correctly scoped to DLL projects only.

- **Transitive System.Composition 9.0.0 → 10.0.7 gap (F016).** Pulled by Roslyn 5.3.0. Bumping may break netstandard2.0 analyzer compatibility. Held intentionally.

- **Microsoft.Testing.Platform 2.0.2 → 2.2.2 gap.** Minor version behind — pulled by xunit.v3.mtp-v2. Pinning separately risks diamond dependency conflicts.

- **No `async void`, sync-over-async, or direct `HttpClient` instantiation in production code.** These appear only in analyzer diagnostic message strings.

- **No crypto code, no FIPS violations.** Zero `System.Security.Cryptography` API usage in production apps. CA53xx guardrails pinned preventively. FIPS-related code in E128.Analyzers is the analyzers themselves (FipsUnapprovedHashAnalyzer, Sha256CreateObsoleteAnalyzer, InsecureRandomInCryptoContextAnalyzer) — they detect FIPS violations, they don't use crypto.

- **Microsoft.VisualStudio.Composition 16→17 and Microsoft.VisualStudio.Validation 15→17 gaps.** Roslyn test infrastructure transitives — pinning may break analyzer test harness.

- **System.ComponentModel.Composition 4.5.0 → 10.0.7 gap.** Roslyn test infrastructure transitive. Same upstream constraint as F022.

- **Multiple Azure.Core / OpenTelemetry / Microsoft.Identity.* minor version gaps in test projects.** All patch/minor, no security impact. Pulled by test infrastructure.

- **System.Security.Cryptography.ProtectedData 4.5.0 in test projects.** Only appears in test project transitives (pulled by Roslyn test infrastructure), not production. Same upstream constraint as F022.

- **Newtonsoft.Json 13.0.1 vs 13.0.4 gap in test projects.** Transitive from Roslyn test infrastructure. Patch-only, no security impact.

- **DiffPlex 1.7.2 → 1.9.0 gap.** Test infrastructure transitive. Minor version, no security impact.

- **Zero TODO/FIXME/HACK/XXX markers in source.** Clean codebase.

- **Zero pragma warning suppressions in production code.** All pragma references are in analyzer code that analyzes pragma usage (SuppressionAuditAnalyzer, PragmaBundlingAnalyzer, PragmaBalanceAnalyzer).

- **AOT_TRIMMING dimension active but no trimming gaps.** Directory.Build.props has conditional AOT/trim properties but no production project declares `PublishAot=true`. The conditional section is preparatory infrastructure, not active — so no `DynamicDependency` attributes or `JsonSerializerContext` source generators are needed yet.

## Open questions for the maintainer

- None remaining. All prior open questions have been resolved by testing (Humanizer independently pinnable, NuGet.* not pinnable, InternalsVisibleTo added proactively).
