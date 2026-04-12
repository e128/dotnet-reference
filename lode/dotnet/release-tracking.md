# Roslyn Analyzer Release Tracking

*Updated: 2026-04-12T06:00:00Z*

## Overview

Two manually-maintained Markdown files track analyzer rule lifecycle for NuGet consumers and build-time validation. Enforced by the release tracking analyzer (RS2000-RS2008) which ships inside `Microsoft.CodeAnalysis.Analyzers`, activated by `<EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>`.

**No MSBuild target generates or migrates these files.** Content moves from Unshipped to Shipped manually at release time.

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
2. At NuGet publish: cut content from `Unshipped.md` into new `## Release X.Y` section at top of `Shipped.md`
3. Leave `Unshipped.md` empty (or with empty section headers) after migration
4. Commit as part of release

**Pre-first-release:** keep all rules in `Unshipped.md`; `Shipped.md` starts with `; No shipped releases yet.`

## Consumers

| Consumer                         | Purpose                                                       |
| -------------------------------- | ------------------------------------------------------------- |
| RS* release tracking analyzer    | Build-time validation: descriptors match file entries          |
| Humans / NuGet consumers         | Changelog of rule additions, changes, removals per version    |
| Warning waves (SDK analyzers)    | Version-gated rule enablement (SDK-internal, not third-party) |

## E128.Analyzers Status

- `src/E128.Analyzers/E128.Analyzers.csproj` suppresses `RS2007` (comment: Roslyn 4.14.0 meta-analyzer rejects valid content) and `RS1038`
- Files registered as `<AdditionalFiles>` in csproj
- Published to NuGet: https://www.nuget.org/packages/E128.Analyzers/
- `Shipped.md` has 10 release sections (1.0.0 through 1.6.0), 53 rules total
- `Unshipped.md` tracks pending changes only (category recategorizations, new rules not yet published)

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

## Sources

- [Issue #5866: Incorrect Documentation for Release Tracking](https://github.com/dotnet/roslyn-analyzers/issues/5866) (2022)
- [Issue #3816: Additional Documentation Request](https://github.com/dotnet/roslyn-analyzers/issues/3816) (2020)
- [Issue #5663: RS2008 Fix](https://github.com/dotnet/roslyn-analyzers/issues/5663) (2022)
- [Issue #7631: Help.md Missing](https://github.com/dotnet/roslyn-analyzers/issues/7631) (2024)
- [DeepWiki: Versioning and Releases](https://deepwiki.com/dotnet/roslyn-analyzers/2.1-versioning-and-releases) (2025)
