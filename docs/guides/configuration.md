# Configuration — E128.Reference

> **One sentence:** Configuration is build-time first — MSBuild props, `.editorconfig`, and `.globalconfig` govern the solution, while consumers of the analyzer package tune individual rule severities in their own `.editorconfig`.

*Updated: 2026-05-29T19:02:39Z*

---

## Configuration Sources

```mermaid
graph TB
  gj["global.json<br/>SDK pin + MTP runner"] --> build["Build"]
  props["Directory.Build.props<br/>TFM, analysis level/mode, analyzer pkgs"] --> build
  targets["Directory.Build.targets<br/>test config + analyzer ref"] --> build
  pkgs["Directory.Packages.props<br/>CPM version pins"] --> build
  gc[".globalconfig<br/>diagnostic severities"] --> build
  ec[".editorconfig<br/>style + inline severities"] --> build
  tgc["tests/.globalconfig<br/>test-only overrides (global_level=101)"] --> build
  ng["nuget.config<br/>sources, signers, mapping"] --> restore["Restore"]
```

## Key MSBuild Properties (`Directory.Build.props`)

| Property                          | Value             | Effect                                            |
| --------------------------------- | ----------------- | ------------------------------------------------- |
| `AnalysisLevel`                   | `latest-all`      | Enable the latest analyzer rule set               |
| `AnalysisMode`                    | `All`             | Every CA rule runs, including rules off by default |
| `EnableNETAnalyzers`              | `true`            | .NET analyzers active                             |
| `EnforceCodeStyleInBuild`         | `true`            | IDE* rules fire during build                      |
| `TreatWarningsAsErrors`           | `true`            | Any warning fails the build                       |
| `ManagePackageVersionsCentrally`  | `true`            | CPM on                                            |
| `CentralPackageTransitivePinningEnabled` | `true`     | Pin transitive versions                           |
| `CentralPackageVersionOverrideEnabled`   | `false`    | Forbid per-project overrides                      |
| `ImplicitUsings`                  | `disable`         | Every file declares explicit `using`s             |

## Severity Configuration

Three files, by responsibility ([details in lode/dotnet/analyzers.md](../../lode/dotnet/analyzers.md)):

| File                  | Owns                                          | Scope              |
| --------------------- | --------------------------------------------- | ------------------ |
| `.editorconfig`       | Style/formatting/naming + inline severities   | All projects       |
| `.globalconfig`       | `dotnet_diagnostic.*` severities + enabling    | All projects       |
| `tests/.globalconfig` | Test-only relaxations (`global_level=101`)     | Test projects only |

Blanket rule: `.globalconfig` sets `dotnet_analyzer_diagnostic.severity = error`. Overrides are explicit.

### Tuning E128.Analyzers (as a consumer)

```ini
# .editorconfig in the consuming repo

# Promote a rule to error
dotnet_diagnostic.E128008.severity = error

# Disable a rule
dotnet_diagnostic.E128005.severity = none

# E128062: which TFM is "current"
e128_minimum_framework_version = 100

# E128103: service types whose Add* registration lives in another assembly
# Entries are comma-separated simple type names, for example ITerminal, IPreferenceStore
e128_registered_services = ITerminal, IPreferenceStore
```

### ConfigureAwait scoping (CA2007)

```ini
dotnet_diagnostic.CA2007.severity = error
dotnet_code_quality.CA2007.output_kind = DynamicallyLinkedLibrary  # libraries only
```

## Common Test-Project Overrides (`tests/.globalconfig`)

| Rule      | Reason                                                  |
| --------- | ------------------------------------------------------- |
| CA2007    | Conflicts with xUnit1030 (`ConfigureAwait(false)`)      |
| MA0004    | Same xUnit1030 conflict                                 |
| VSTHRD111 | Same xUnit1030 conflict                                 |
| CA1707    | Test names use `Method_Condition_Result`                |
| S100      | Same naming form                                        |
| RCS1046   | Same naming form: test names carry no `Async` suffix    |
| S4018     | Test runner helpers take explicit type arguments        |
| E128043   | Guard-clause tests pass `null!`                         |
| MA0191    | Same guard-clause tests                                 |
| E128097   | Test-specific exception                                 |

## Environment-Specific Overrides

The reference apps carry no per-environment config (no `appsettings.{Environment}.json`, no connection strings). Environment variation lives in CI (`ci.yml` sets `DOTNET_*` flags) and the container image (Resolute base image tags pinned to `10.0-resolute`).

## Secrets vs Config

| Item                    | Classification                                  |
| ----------------------- | ----------------------------------------------- |
| SDK version, TFM        | Config (`global.json`, props)                   |
| Package versions        | Config (`Directory.Packages.props`)             |
| Rule severities         | Config (`.globalconfig` / `.editorconfig`)      |
| NuGet publish credential| Secret — issued via OIDC at publish time only   |
| Application secrets      | None present                                    |
