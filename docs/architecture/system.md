# System Architecture — E128.Reference

> **One sentence:** A single .NET 10 solution of four production projects (Core, Web, Cli, Analyzers) and five test projects, governed by shared MSBuild props and a build-time analyzer ProjectReference that analyzes every project except itself.

*Updated: 2026-05-29T19:02:39Z*

---

## Component Map

```mermaid
graph TB
  subgraph build["Shared build configuration (repo root)"]
    props["Directory.Build.props<br/>TFM, analysis, analyzer packages"]
    targets["Directory.Build.targets<br/>test config + analyzer ProjectReference"]
    pkgs["Directory.Packages.props<br/>CPM version pins"]
    gj["global.json<br/>SDK 10.0.400 + MTP runner"]
    gc[".globalconfig<br/>deny-by-default severities"]
    ec[".editorconfig<br/>style + formatting"]
  end

  subgraph prod["src/ — production"]
    core["E128.Reference.Core<br/>(net10.0) domain library"]
    web["E128.Reference.Web<br/>(net10.0) minimal API"]
    cli["E128.Reference.Cli<br/>(net10.0) System.CommandLine"]
    ana["E128.Analyzers<br/>(netstandard2.0) Roslyn suite"]
  end

  subgraph test["tests/"]
    coret["E128.Reference.Core.Tests"]
    webt["E128.Reference.Tests<br/>(integration)"]
    clit["E128.Reference.Cli.Tests"]
    archt["Architecture.Tests<br/>(ArchUnitNET)"]
    anat["E128.Analyzers.Tests"]
  end

  web --> core
  cli --> core
  props --> prod
  targets --> prod
  ana -. "OutputItemType=Analyzer<br/>(all projects except itself)" .-> prod
  coret --> core
  webt --> web
  clit --> cli
  archt --> prod
  anat --> ana
```

## Architectural Layers

### Layer 1 — Build & Analysis Configuration

The repository root holds all cross-cutting configuration. Every project inherits it; no project redefines it.

| File                      | Responsibility                                                            |
| ------------------------- | ------------------------------------------------------------------------- |
| `Directory.Build.props`   | TFM, language version, nullable, analysis level/mode, analyzer packages   |
| `Directory.Build.targets` | `IsTestProject` config (MTP, `OutputType=Exe`); analyzer `ProjectReference` |
| `Directory.Packages.props`| Central Package Management version pins                                   |
| `global.json`             | SDK pin (`10.0.400`) and MTP test runner selection                        |
| `.globalconfig`           | Analyzer diagnostic severities (blanket `error`)                          |
| `.editorconfig`           | Code style, formatting, naming, inline severity hints                     |

### Layer 2 — Production Code

| Project               | SDK                       | TFM              | Role                              |
| --------------------- | ------------------------- | ---------------- | --------------------------------- |
| `E128.Reference.Core` | `Microsoft.NET.Sdk`       | `net10.0`        | Shared domain library             |
| `E128.Reference.Web`  | `Microsoft.NET.Sdk.Web`   | `net10.0`        | Minimal-API web service (Kestrel) |
| `E128.Reference.Cli`  | `Microsoft.NET.Sdk`       | `net10.0`        | Console app (System.CommandLine)  |
| `E128.Analyzers`      | `Microsoft.NET.Sdk`       | `netstandard2.0` | Roslyn analyzer + code-fix suite  |

### Layer 3 — Tests

Five projects, each carrying `<IsTestProject>true</IsTestProject>` and inheriting MTP config. See [../engineering/testing-strategy.md](../engineering/testing-strategy.md).

## Inter-Service Communication

There is no runtime inter-service communication — the production apps are independent processes that share a library. The most important "communication" is at **build time**: the analyzer wires itself into every project's compilation.

```mermaid
graph LR
  ana["E128.Analyzers.dll"] -->|"ProjectReference<br/>OutputItemType=Analyzer<br/>ReferenceOutputAssembly=false"| compile["Roslyn compilation<br/>of every other project"]
  compile -->|"diagnostics (error by default)"| build["dotnet build result"]
```

Rules:
- The analyzer is referenced via `Directory.Build.targets`, gated on `IsRoslynComponent != true` so the analyzer does not analyze itself.
- The condition also checks the `.csproj` exists, so the solution still builds if the analyzer project is removed.

## Multi-Tenancy Model

Not applicable. The reference web service is single-tenant and stateless (in-memory store only).

## Authentication & Authorization

The reference apps implement **no authentication** — they are demonstrators. The security posture that matters is build-time and supply-chain (see [security.md](security.md)). The one authenticated flow in the system is the **release pipeline**:

```mermaid
sequenceDiagram
  participant GA as GitHub Actions
  participant OIDC as GitHub OIDC
  participant NG as nuget.org
  GA->>OIDC: Request OIDC token (id-token: write)
  OIDC-->>GA: Short-lived token
  GA->>NG: Exchange token for temporary API key
  NG-->>GA: Scoped publish credential
  GA->>NG: dotnet nuget push E128.Analyzers
```

## Observability Stack

```mermaid
graph TB
  health["GET /health<br/>(AddHealthChecks)"] --> ops["Operational liveness"]
  ci["CI logs<br/>(format/build/test)"] --> dev["Developer feedback"]
  trx["TRX report<br/>(Microsoft.Testing.Extensions.TrxReport)"] --> dev
  hang["HangDump<br/>(Microsoft.Testing.Extensions.HangDump)"] --> dev
```

The reference apps include a health endpoint; deeper telemetry (logging/metrics/tracing) is intentionally out of scope for the reference surface. Test observability comes from TRX reports and hang dumps in CI.

## Technology Decisions Summary

| Decision                          | Chosen                              | Alternatives considered                 |
| --------------------------------- | ----------------------------------- | --------------------------------------- |
| Web style                         | Minimal API                         | MVC controllers                         |
| CLI framework                     | System.CommandLine                  | Spectre.Console, raw args               |
| DI container                      | `Microsoft.Extensions.DependencyInjection` | none / manual wiring             |
| Test runner                       | Microsoft Testing Platform (xUnit v3)| VSTest (retired on .NET 10)            |
| Architecture enforcement          | ArchUnitNET                         | convention-only / code review           |
| Analyzer TFM                      | `netstandard2.0`                    | `net10.0`                               |
| Package management                | CPM + transitive pinning            | per-project versions                    |
| Solution format                   | `.slnx` (XML)                       | legacy `.sln` (GUID-based)              |
