# Code Coverage
*Updated: 2026-11-18T09:50:29Z*

Coverage collects in process through the Microsoft.Testing.Platform extension.
The CI gate requires 80 percent line coverage in total and in every measured
assembly.

## Architecture

```mermaid
flowchart LR
    config[coverage.config.xml] --> ext["Microsoft.Testing.Extensions.CodeCoverage<br/>tests/Directory.Build.targets"]
    ext --> reports["test-results/GUID.cobertura.xml<br/>one report per test module"]
    reports --> html["scripts/coverage.sh<br/>ReportGenerator HTML"]
    reports --> gate["scripts/coverage-areas.sh<br/>line, assembly, and module gates"]
    reports --> summary["scripts/coverage-areas.sh --markdown<br/>job summary tables"]
```

## Collection

`tests/Directory.Build.targets` imports the root targets and adds the coverage
package to every `IsTestProject`. The extension loads inside the test process,
so it needs no profiler attach step.

`scripts/coverage.sh` builds the solution and runs:

```bash
dotnet test --solution E128.Reference.slnx \
  --configuration Debug \
  --no-build \
  --filter-trait "Category=CI" \
  --coverage \
  --coverage-output-format cobertura \
  --coverage-settings coverage.config.xml \
  --results-directory ./test-results
```

The script also passes `DOTNET_SAFEGUARDS` to the build and
`DOTNET_TEST_SAFEGUARDS` to the test run. Both arrays come from `scripts/lib.sh`.
`--filter TRAIT` replaces the default `Category=CI` trait.

The script deletes the git-ignored `test-results` folder at the start of each
run. A stale module report therefore cannot satisfy the gate.

The extension writes one GUID-named Cobertura file per test module. Consumers
read the whole `test-results` directory, then merge classes across reports.

CI builds Release to validate that configuration. That validation build runs
with `ReadyToRun=false`, because it discards the precompiled images. CI then
builds Debug and runs the coverage pass in Debug. This avoids the ReadyToRun
images that stall the analyzer test host under instrumentation.

## Instrumentation Contracts

- `CollectAspDotNet` is `True`. `WebApplicationFactory` runs the ASP.NET Core
  host in process, and the engine skips that host without the flag.
- `CompilerGeneratedAttribute` stays measured. The minimal API endpoints are
  lambdas in top-level statements, so excluding that attribute removes the
  whole `E128.Reference.Web` assembly.
- `GeneratedCodeAttribute` stays excluded. It removes the untestable top-level
  builder and host boilerplate.
- `ExcludeFromCodeCoverageAttribute` stays excluded.
- The extension writes one report per module. A fixed output name makes modules
  overwrite each other, so the scripts read a glob.

## Coverage Scope

`coverage.config.xml` holds an `Exclude` list and no `Include` list. Every
loaded assembly with a PDB is measured, except the test assemblies. A new
project joins the measured set when it is created.

Measured assemblies:

- `E128.Analyzers`
- `E128.Analyzers.CodeFixers`
- `E128.Reference.Core`
- `E128.Reference.Cli`
- `E128.Reference.Web`

`scripts/coverage-areas.sh` treats a measured assembly that is absent from the
reports as a gate failure.

The script parses Cobertura with `tr` and `awk`. It splits each report on the
`<` character so one element sits on each line, then reads the attributes it
needs. The gate needs `jq` and nothing else, so CI installs no XML tool.

## Gate

CI runs:

```bash
bash scripts/coverage-areas.sh --min-line 80 --min-assembly 80
```

The gate fails in four cases:

1. Total line coverage is below the floor.
2. Any measured assembly is below the assembly floor.
3. Any expected module is absent from the reports.
4. The reports hold no line data.

`--min-line 0` and `--min-assembly 0` disable the two floors. `--namespace`
checks one namespace for any line or branch gap. `--source-file` checks one
source file and exits 1 when no class matches or the file is below the floor.
`--self-test` proves the gate against its fixtures.

## CRAP Estimates

CRAP combines cyclomatic complexity with uncovered-path risk.
The measured Cobertura files contain method-level `complexity`, `line-rate`, and `branch-rate` attributes.
The technical-debt audit uses source-level complexity and branch coverage for manual CRAP estimates.
ReportGenerator displays supplied metrics rather than calculating complexity from source.

CI enforces line floors, not CRAP thresholds.
An absent Risk Hotspots list does not prove a low score for every method.
Verify threshold enforcement with known hotspot input before adding a CRAP gate.
See the [CRAP Reference](../../.claude/skills/tech-debt-audit/references/crap-formula.md) for the formula and report limits.

## Feature Areas

`coverage-areas.json` maps an assembly name or a namespace prefix to a feature
area. A longer matching key wins. Areas:

| Area | Match |
|------|-------|
| Analyzers | `E128.Analyzers` |
| Code Fixers | `E128.Analyzers.CodeFixers` |
| Reference Core | `E128.Reference.Core` |
| Reference CLI | `E128.Reference.Cli` |
| Reference Web | `E128.Reference.Web` |

Classes that match no key land in the Unmapped bucket.

## Baseline

Measured 2026-11-17 over five module reports. Classes merge across reports, and
each line keeps its highest hit count. Total: 36204 of 38213 lines, 94.74
percent.

| Assembly | Lines | Line |
|----------|-------|------|
| E128.Analyzers | 28748/29925 | 96.07% |
| E128.Analyzers.CodeFixers | 7344/8166 | 89.93% |
| E128.Reference.Cli | 22/24 | 91.67% |
| E128.Reference.Core | 57/57 | 100.0% |
| E128.Reference.Web | 33/41 | 80.49% |

The Reference Web assembly sits closest to the floor.

## Local Setup

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
```

```bash
bash scripts/coverage.sh                         # Run and open the HTML report
bash scripts/coverage.sh --no-open               # Generate without opening
bash scripts/coverage.sh --configuration Release # Collect from a Release build
bash scripts/coverage.sh --filter "Category=CI"  # Set the trait filter
bash scripts/coverage.sh --json                  # Totals as JSON
bash scripts/coverage-areas.sh --min-line 80 --min-assembly 80
bash scripts/coverage-areas.sh --source-file Greeter.cs --min-line 80
bash scripts/coverage-areas.sh --self-test
```

## See Also

- [Testing](testing.md)
- [Architecture Testing](architecture-testing.md)
- [Test Patterns](test-patterns.md)
