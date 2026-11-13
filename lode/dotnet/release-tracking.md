# Roslyn Analyzer Release Tracking
*Updated: 2026-11-13T14:40:22Z*

## Overview

Two manually-maintained Markdown files track analyzer rule lifecycle for NuGet consumers and build-time validation. Enforced by the release tracking analyzer (RS2000-RS2008) which ships inside `Microsoft.CodeAnalysis.Analyzers`, activated by `<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>`.

**No MSBuild target generates or migrates these files.** `scripts/internal/analyzer-promote.sh` moves the rows at release time. The `/yeet` skill runs it after the version bump and before the squash commit.

## File Format

### AnalyzerReleases.Shipped.md

Requires `## Release X.Y` header before each section. Multiple releases are listed top-down (newest first).

```markdown
## Release 1.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
E128001 | Design | Warning | Description here
```

### AnalyzerReleases.Unshipped.md

This file stays in the project, even when no pending rule exists. RS2000 fails
the build for a `DiagnosticDescriptor` that appears in neither file. Removing
the file needs a suppression, which the repository forbids without approval.

Must **NOT** have a `## Release` header. Sections appear directly at top level.

```markdown
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
E128006 | Style | Warning | Description here

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-------
E128003 | Design       | Warning      | Reliability  | Warning      | Recategorized

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
E128099 | Design | Warning | No longer needed
```

### Parsing Gotchas

- Headers matched with `String.StartsWith`, not standard Markdown parsing
- Extra spaces in header column names can break parsing
- Separator line must use `--------` segments separated by `|`
- Valid severities: `Disabled`, `Hidden`, `Info`, `Warning`, `Error`
- `### Changed Rules` uses 6 columns (not 4); incorrect column count triggers RS2007

## RS Diagnostic Rules

| Rule   | Fires when                                                                 |
| ------ | -------------------------------------------------------------------------- |
| RS2000 | DiagnosticDescriptor exists but is not listed in any release file          |
| RS2001 | Diagnostic properties changed but release file entry not updated           |
| RS2002 | Rule in Unshipped.md has no matching DiagnosticDescriptor                  |
| RS2003 | Previously shipped rule removed from code without `Removed Rules` entry    |
| RS2004 | Duplicate diagnostic ID within a single release section                    |
| RS2005 | Duplicate diagnostic ID across different shipped releases                  |
| RS2006 | Non-shipped entry for diagnostic no longer reported                        |
| RS2007 | Format error: missing/invalid release header or table format               |
| RS2008 | Analyzer project has DiagnosticDescriptors but no release tracking files   |

## Release Workflow

1. During development: add all new/changed/removed rules to `Unshipped.md`
2. Bump the package version with `scripts/internal/version-bump.sh`
3. At NuGet publish: run `scripts/internal/analyzer-promote.sh`. It moves every New Rules row and every Changed Rules row into a `## Release X.Y` section at the top of `Shipped.md`
4. Leave `Unshipped.md` with its table headers and no rows. A `Removed Rules` row stays there for a hand-written note
5. Commit as part of release

The promote script refuses a version that already has a section at the top of
`Shipped.md`. Bump the version first, then promote. Run it with `--dry-run` to
see the counts before it writes.

`version-bump.sh` skips a project whose branch changes no `.cs` file. A
metadata-only change to the analyzer project therefore needs a hand edit of
`<Version>`, then the release checks. Never leave the version on a number that
nuget.org already carries, because the publish job skips an existing version.

**Pre-first-release:** keep all rules in `Unshipped.md`; `Shipped.md` starts with `; No shipped releases yet.`

The `/yeet` skill enforces the whole workflow on every run. It calls
`scripts/internal/analyzer-release-check.sh` for the release files,
`scripts/readme-table-diff.sh --analyzer` for the analyzer README, and
`scripts/internal/analyzer-promote.sh` after the version bump. A rule that is
missing from either file blocks the ship, even on a docs-only change set.

## Consumers

| Consumer                         | Purpose                                                       |
| -------------------------------- | ------------------------------------------------------------- |
| RS* release tracking analyzer    | Build-time validation: descriptors match file entries          |
| Humans / NuGet consumers         | Changelog of rule additions, changes, removals per version    |
| Warning waves (SDK analyzers)    | Version-gated rule enablement (SDK-internal, not third-party) |

## E128.Analyzers Status

- `src/E128.Analyzers/E128.Analyzers.csproj` suppresses `RS2007` (comment: Roslyn 4.14.0 meta-analyzer rejects valid content), `RS0016`, `RS0017`, and `NU5128`
- Files registered as `<AdditionalFiles>` in csproj
- Published to NuGet: https://www.nuget.org/packages/E128.Analyzers/
- `Shipped.md` lists all shipped rules across release sections; run `scripts/internal/analyzer-release-check.sh` for current shipped/unshipped/source ID counts
- `Unshipped.md` tracks pending changes only; it holds the `### New Rules` header and an empty table when every rule is shipped

### Release-to-Rule Mapping

| Version | New Rules     | Notes                         |
| ------- | ------------- | ----------------------------- |
| 1.0.0   | E128001-005   | Initial release               |
| 1.1.0   | E128006-008   | Encoding, async void, sync-over-async |
| 1.2.0   | E128009-010   | OrderBy, ResponseHeadersRead  |
| 1.2.1   | (none)        | Bug fix only                  |
| 1.3.0   | E128011-014   | GeneratedRegex suite          |
| 1.3.1   | (none)        | Bug fix only                  |
| 1.3.5   | E128015-018   | string.Format, DateTime.Parse, primary ctor, ToList |
| 1.4.0   | E128019-026   | in-modifier, ConfigureAwait, hardcoded tmp, etc. |
| 1.5.0   | E128027-030   | FrozenSet, TaskFromResult, multi-string, FileSystemInfo |
| 1.6.0   | E128031-053   | DI patterns, concurrency, JSON lifetime, etc. |
| 1.8.0   | E128054       | Temp dir cleanup; E128003/004 recategorized to Design |
| 1.23.3  | E128055-065   | Pragma, TOCTOU, readonly array, interface param, etc. |
| 1.26.1  | E128066-075   | O(n²) loops, pool bounds, FIPS hash, init accessor, crypto RNG |
| 1.31.0  | E128076-090   | DOM-detection reliability, ByteSize, marshal/span perf, bare Parse, test reflection (E128088 shipped later, in 1.39.0) |
| 1.35.0  | E128091-092   | ProcessStartInfo argument list, async File overload |
| 1.38.0  | E128093, 096, 098-100 | Async DB overload, sync call to async local function, chained string Replace, process exit timeout, catch filter on cancellation |
| 1.39.0  | E128094, 097, 088 | Pragma before namespace, xUnit comment placement, enum switch exhaustiveness |
| 1.40.0-1.40.3 | E128101, E128102 | Patch bumps on feature commits. Both rules shipped inside the assembly but stayed listed in `Unshipped.md` until 1.41.0 |
| 1.41.0  | E128101-106 | Six rules moved from `Unshipped.md` to a new `Release 1.41.0` section |
| 1.41.3  | E128107-108 | Low-value test conditions moved from `Unshipped.md` to a new `Release 1.41.3` section |
| 1.41.12 | (none)      | E128107/E128108 changed: disabled by default, predicates recalibrated. Recorded as a `Changed Rules` section in `Shipped.md` |
| 1.41.13 | (none)      | Analyzer hot-path performance only. Rule set unchanged. |
| 1.42.0  | E128109-111 | Buffer safety rules |
| 1.42.1  | E128112-129 | Unsafe and performance rules |
| 1.42.2  | (none)      | Bug fixes in E128116, E128122 and E128124. `Shipped.md` gained the 1.42.0 and 1.42.1 sections at this release. |
| 1.42.3  | (none)      | Symbol matching and API gating in E128018, 028, 032, 051, 056, 068, 072, 081, 083, 090, 102. Fix providers move to `UsingDirectives.AddIfMissing`. |
| 1.60.4  | E128131-141 | Interop, span-bulk, and reflection rules. First new-rule release since 1.42.1. |
| 1.66.0  | E128142-152 | FIPS cryptography policy rules and the complexity ceiling. E128071 and E128075 move to Error. |
| 1.67.0  | E128153 | `List.RemoveAt(0)` in a loop. E128152 gains constant-trip exclusion, a `cubic` tier, argument-contraction recursion classification, and read-plus-write memoization. |
| 2.2.0   | E128154-198   | Review-practice and external sweep rules. E128187 and E128197 change severity in 2.3.0. |
| 2.3.0   | E128199-201   | NASA Power of Ten rules. E128187 and E128197 lower to Info. E128022 removed. |
| 2.3.1   | (none)        | Accuracy fixes across the E128176 to E128201 rules. The pack carries no third-party dependency. |
| 2.4.0   | E128202       | Unvalidated JWT claims used as an identity. Info severity. |
| 2.4.1   | (none)        | Review-sweep accuracy fixes with no rule set change. |
| 2.5.0   | E128203-217   | Performance rules from the twenty-practice sweep. |
| 2.5.1   | (none)        | Shared-helper refactor across analyzers and code fixers. No rule set change. |
| 2.6.0   | E128218       | Implicit state machine rule. Info severity. |
| 2.6.1   | (none)        | Shared-helper refactor. E128056 also flags `File.OpenText`, and E128058 also fires on `Task<T>` and `ValueTask<T>` returns. |
| 2.6.2   | (none)        | Shared-helper extraction, FIPS name-table dedup, and rule false-negative fixes. No rule set change. |
| 2.6.3   | (none)        | Build-dependency update: Meziantou.Analyzer 3.0.322 and PolySharp 1.17.0. No rule set change. |
| 2.7.0   | (none)        | The repository moves to `net11.0`. E128062 recognizes `Net110` as a known reference-assembly version. |
| 2.7.1   | (none)        | Packaging metadata only. The package claim narrows to the `net11.0` host, and the `net10.0` reach proof is removed. |
| 2.8.0   | (none)        | Shared-helper refactor. E128218 also recognizes a forwarding accessor pair, a mutable struct, and a cross-type write, and it reads the `e128_state_min_transition_bodies` option. |
| 2.8.1   | (none)        | Version-only release. Repacks the 2.8.0 content with no rule set change. |
| 2.9.0   | E128222       | Root-join containment. The package reached nuget.org before the `Unshipped.md` rows were promoted, so the release files recorded it afterwards. |
| 2.9.8   | (none)        | The member-level symbol rules report a partial member once at its defining part through `PartialMembers.IsImplementationPart`, and the attribute rules gain partial-shape fixtures. No rule set change. |
| 2.10.0  | E128219-221   | Union rule family: closed abstract base, union switch null arm, and union pattern-match containment. |
| 2.10.1  | (none)        | E128014 exempts a separator-disjoint repeated group and fires on an unbounded `{n,}` outer quantifier. The GeneratedRegex fixes keep the pattern language. No rule set change. |
| 2.11.0  | E128223-226   | Hand-rolled process capture, read-only `MemoryStream`, closed base with members, and comparer set construction. E128220 also reports a closed-hierarchy switch that ends in a discard arm. |
| 3.0.0   | (none)        | E128160 removed. E128223 reports the read-then-wait shape that replaced the hand-rolled process capture. |

## Sources

- [Issue #5866: Incorrect Documentation for Release Tracking](https://github.com/dotnet/roslyn-analyzers/issues/5866) (2022)
- [Issue #3816: Additional Documentation Request](https://github.com/dotnet/roslyn-analyzers/issues/3816) (2020)
- [Issue #5663: RS2008 Fix](https://github.com/dotnet/roslyn-analyzers/issues/5663) (2022)
- [Issue #7631: Help.md Missing](https://github.com/dotnet/roslyn-analyzers/issues/7631) (2024)
- [DeepWiki: Versioning and Releases](https://deepwiki.com/dotnet/roslyn-analyzers/2.1-versioning-and-releases) (2025)
