# Arch-Scan -- Dependency Scanning Reference

Reference for the `dependency-map`, `external-integrations`, and `runtime-pinning` perspectives.
Scoped to a .NET solution using Central Package Management.

---

## NuGet Manifest Parsing

For each source directory in the scan scope:

1. `Glob("src/{Project}/**/*.csproj")` — locate all project files
2. Skip test/bench projects: `*.Tests.csproj`, `*.Benchmarks.csproj`
3. Read each `.csproj`. Extract `<PackageReference Include="..." Version="...">` lines
4. If `Directory.Packages.props` exists at solution root, resolve versions from there (Central Package Management)
5. Classify each package and build two lists per project:
   - **External service integrations** -- client packages
   - **Key third-party** -- messaging, ORM/DB, DI, auth, observability, scheduler libs

---

## Package Classification

| Package pattern | Classification |
|-----------------|----------------|
| `Microsoft.*`, `System.*`, `Azure.*` | Microsoft / Azure SDK |
| Same namespace prefix as another project in the solution | First-party (internal) |
| All others | Third-party open source |

### External Service Integration Detection

A package is an **external service integration** if its name matches:
- `*.Api.Client`, `*.Api.Client.Abstractions`
- `*.Private.Api.Client`, `*.Public.Api.Client`
- `*.Client` (when the base name is clearly a service, not a framework utility)

If a client package has no known service mapping, record it verbatim and mark:
`warning: service mapping unknown -- verify against service list`.

### Test-Only Dependencies (omit from production tables)

| Pattern | Skip reason |
|---------|-------------|
| `xunit.*`, `NUnit.*`, `MSTest.*` | Test frameworks |
| `Moq`, `NSubstitute`, `FakeItEasy` | Mocking |
| `testcontainers.*` | Containerized tests |
| `Bogus`, `AutoFixture` | Test data |
| Any package in a `*.Tests.csproj` exclusively | Test project only |

---

## Infrastructure Detection

Grep config files for infrastructure dependencies:

**Files to scan:** `**/appsettings*.json` (exclude `appsettings.Test*.json`), `**/.env`, `**/.env.example`

| Grep pattern (`-i`) | Infrastructure |
|----------------------|----------------|
| `"ConnectionStrings"` or `jdbc:sqlserver` or `postgres://` | SQL (MSSQL/Postgres) |
| `"Redis"` or `REDIS_URL` | Redis Cache |
| `"RabbitMQ"` or `RABBITMQ_` | RabbitMQ |
| `"CosmosDb"` or `"CosmosDB"` | Azure CosmosDB |
| `BlobStorage` or `AzureWebJobsStorage` | Blob Storage |
| `ServiceBus` or `EVENT_HUB` | Azure Service Bus |
| `"Elasticsearch"` or `ELASTIC_` | Elasticsearch |
| `"MongoDB"` or `MONGO_URI` | MongoDB |
| `Quartz` | Quartz Job Store |
| `HangfireSettings` or `"Hangfire"` | Hangfire scheduler |

Record: **confirmed** (pattern found in config) vs **inferred** (implied by package dep only).

---

## Runtime & Docker Image Detection

Collect runtime pinning from these files:

| File | Provides |
|------|----------|
| `**/Dockerfile`, `**/Dockerfile.*` | Base runtime + SDK build image |
| `**/global.json` | .NET SDK version pin |
| `**/*.csproj` (`<TargetFramework>`) | .NET target framework per project |
| `**/.github/workflows/*.yml` | CI SDK versions (`actions/setup-dotnet` → `dotnet-version`) |

### Dockerfile Parsing

Extract every `FROM` instruction. Distinguish:
- **Runtime stages** -- contains `aspnet`, `runtime`, `alpine` without `sdk`
- **Build stages** -- contains `sdk`, `build`

Use the final `FROM` (or stage named `runtime`/`final`) as the production image.

| Image pattern | Runtime |
|---------------|---------|
| `mcr.microsoft.com/dotnet/aspnet:<ver>` | .NET runtime (production) |
| `mcr.microsoft.com/dotnet/sdk:<ver>` | .NET SDK (build stage) |

### CI Pipeline SDK Versions

Grep `.github/workflows/*.yml` for `actions/setup-dotnet` — extract the `dotnet-version` field.

### global.json Roll-Forward Policy

Record `sdk.version` + `rollForward`. `patch`/`latestPatch` = safe; `major`/`latestMajor` = permissive, flag it.

---

## Output Tables

When generating the `dependency-map` perspective, include these structured sections alongside the Mermaid diagram:

### Per-Project Package Table

```markdown
#### Key Third-Party Packages

| Package | Version | Role |
|---------|---------|------|
| MassTransit | 8.x | RabbitMQ messaging |
| Dapper | 2.x | SQL micro-ORM |
```

For projects with > 30 packages, list only architecturally significant ones. Add:
`> N additional packages omitted. See <relative path to .csproj>.`

### Cross-Project Dependency Heat Map

Aggregate view showing which dependencies are shared across multiple projects. Include for both NuGet packages and infrastructure:

```markdown
## Cross-Project Dependency Heat Map

### Shared NuGet Packages

| Package | Projects (count) | Projects |
|---------|-----------------|----------|
| System.CommandLine | 2 | MyApp.Cli, ... |
| Microsoft.Extensions.DependencyInjection | 3 | MyApp.Core, ... |

### Shared Infrastructure

| Infrastructure | Projects |
|----------------|----------|
| SQLite | MyApp.Core, ... |
| File System | MyApp.Cli, ... |
```

Sort by project count descending. Only include packages shared by 2+ projects.

### Infrastructure Summary

```markdown
#### Infrastructure Dependencies

| Infrastructure | Projects | Detection |
|----------------|----------|-----------|
| SQLite | MyApp.Core | confirmed (Microsoft.Data.Sqlite + appsettings) |
| File System | MyApp.Cli | confirmed (config path patterns) |
| Redis | MyApp.Web | inferred (StackExchange.Redis package only) |
```

Always distinguish **confirmed** (config pattern found) vs **inferred** (package dep only).

### Runtime Version Matrix

```markdown
#### Runtime & SDK Versions

| Artifact | Value | Source |
|----------|-------|--------|
| .NET SDK pin | `10.0.100` | `global.json` |
| Target framework | `net10.0` | `*.csproj` |
| Runtime image | `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` | `Dockerfile` |
```

---

## Edge Cases

| Scenario | Handling |
|----------|----------|
| Multiple `.csproj` per project dir | Aggregate non-test `.csproj`. De-duplicate packages across files. Note count: `**Projects:** 3 (.csproj, tests excluded)` |
| Large dependency list (> 30) | List architecturally significant only. Footnote with path to manifest |
| No Dockerfile | Mark runtime image as `--`. Note: `No container image -- library/tooling project` |
| Multi-stage Dockerfile | Final stage = production image. Build stages in SDK column, comma-separated |
| Unpinned image tag (`:latest` or no tag) | Flag as `warning: unpinned` -- supply chain risk |
| CI SDK version differs from global.json | Record both with `warning: mismatch` |
| global.json rollForward policy | Record `sdk.version` + `rollForward`. `patch`/`latestPatch` = safe; `major`/`latestMajor` = permissive, flag it |
| Analyzer/build-only project (netstandard2.0) | Include in dep map but note TFM difference. Not a runtime service |
| Standalone project (no internal refs, no inbound refs) | Include as "standalone" in topology |
| Full-scope scan exceeds 25-node diagram limit | Split into domain-scoped subgraphs within one diagram, or create per-domain sub-diagrams |
| Package in both production and test projects | Classify by production usage. Test-only consumers don't count for heat map |
