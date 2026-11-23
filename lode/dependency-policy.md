# Dependency Policy
*Updated: 2026-11-23T15:56:34Z*

## Selection Criteria

Before adding a new NuGet dependency, evaluate:

1. **License**: MIT, Apache 2.0, or BSD only. No GPL, no AGPL, no commercial-only.
2. **Maintenance**: actively maintained (commits in last 6 months). Check GitHub pulse.
3. **Size**: prefer small, focused packages over large frameworks.
4. **Transitive impact**: check what the package pulls in. Avoid packages with heavy transitive chains.
5. **Alternatives**: is this in the BCL already? Can we write 20 lines instead of adding a dependency?
6. **Capability check**: do not assume a library lacks a capability without checking its documentation and types.

## Package Categories

| Category             | Policy                                                          |
| -------------------- | --------------------------------------------------------------- |
| Analyzers            | Always allowed: zero runtime impact                            |
| Roslyn authoring     | Allowed for custom analyzer/code-fix projects (Microsoft.CodeAnalysis.*) |
| Architecture testing | Allowed in test projects only (ArchUnitNET)                     |
| Testing              | Allowed in test projects only                                   |
| CLI                  | System.CommandLine for all console entry points                 |
| Microsoft            | Preferred for BCL extensions (Microsoft.Extensions.*)           |
| Third-party          | Requires justification: evaluate against the six criteria        |

## Version Management

All versions are managed centrally in `Directory.Packages.props` (CPM). Individual projects use `<PackageReference Include="..." />` without version attributes. `Directory.Packages.props` sets `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` to enable CPM. A setup-owned, hash-marked block in the first `PropertyGroup` of `Directory.Packages.props` sets `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>` to pin transitive versions. The same block sets `<CentralPackageVersionOverrideEnabled>false</CentralPackageVersionOverrideEnabled>`, so a project cannot override a central version with `VersionOverride`. Explicit transitive pins (version-only `PackageVersion` entries with no direct reference) live under the "Transitive pins" comments in `Directory.Packages.props`. This prevents diamond-dependency drift. A pin only does its job while the package appears in the restore graph. Check with `dotnet list package --include-transitive`. Once a package no longer appears, its pin is inert. Remove it.

## Manual Updates

`scripts/update.sh --apply` runs `dotnet outdated --upgrade --version-lock Major`. That command rewrites the central `PackageVersion` lines in `Directory.Packages.props`. The major lock stops a jump to a new .NET major.

The script snapshots `Directory.Packages.props`, then runs `dotnet restore` with `DOTNET_SAFEGUARDS` from `scripts/lib.sh`, so no build server outlives the command. When restore fails, the script copies the snapshot back and exits with a failure. `dotnet outdated` can propose a version whose own dependency does not exist, so the restore gate decides, not the version check. `scripts/update.sh --json` reports one count per unique outdated package.

## Pruning Unused Dependencies

`/prune-deps` audits the solution for dead dependencies: orphaned `PackageVersion` entries (transitive-pin aware), unused `PackageReference` (code-usage based), and unused `ProjectReference`. It reports findings, then removes approved entries and runs one verification build. Analyzers (in `Directory.Build.props`), test SDK/runner packages, and runtime-only/DI-glue packages never surface as `using` directives. They are not unused just because no `using` references them. `/solution-audit` surfaces the same three categories as part of its broader read-only health sweep.

The ReferenceTrimmer `GlobalPackageReference` in `Directory.Packages.props` enforces reference hygiene at build time. The build fails on a removable `Reference`, `ProjectReference`, or `PackageReference` (RT0001 to RT0003). `Directory.Build.props` sets `ReferenceTrimmerUseSymbolAnalysis=true`, so the analyzer tracks referenced symbols instead of `GetUsedAssemblyReferences`. Symbol mode needs no XML documentation output. The repository keeps `GenerateDocumentationFile=true`, so `cref` uses still count. A reference the compiler does not use but the runtime needs carries `TreatAsUsed="true"`. `tests/Architecture.Tests` uses that metadata for the assemblies ArchUnit loads by name. ReferenceTrimmer caches its declared reference list under `artifacts/obj/`. A change to only the suppression metadata leaves that cache stale. Delete the `_ReferenceTrimmer_*` files for the project after such a change.

## Automated Updates

Renovate manages dependency PRs via `renovate.json`. Key rules:

- Updates wait 3 days after release before Renovate proposes them (`minimumReleaseAge`). `internalChecksFilter` is `strict`, so a pending age check holds back the branch.
- Runner image updates (`runs-on`) skip the age gate, because the images publish no release timestamp.
- Renovate runs before 6am Central time on every day of the week.
- Patch and minor updates group as `non-major` and auto-merge.
- Major version bumps require manual review.
- Security updates bypass schedule and grouping and auto-merge. They still wait for the release age.
- Renovate limits PRs and branches to one concurrent each to keep the pipeline green.

## Supply Chain Security

`nuget.config` enforces a locked-down supply chain:

- **Cleared sources**: `<clear />` removes inherited feeds, and only `nuget.org` is configured.
- **Trusted signers**: three NuGet.org certificate fingerprints (SHA-256) are pinned.
- **Package source mapping**: all packages (`*`) are mapped to the `nuget.org` source, preventing feed substitution.
- **Minimum publish age**: the `nuget.org` source sets `minPublishAgeHours` to 72, which matches the Renovate 3 day delay. The setting gates automatic version selection only. It covers `dotnet package add`, `dotnet package update`, and a floating reference. An ordinary restore of a pinned version ignores it. Do not treat it as an age check on the whole dependency graph.
