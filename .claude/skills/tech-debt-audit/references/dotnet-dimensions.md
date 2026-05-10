# .NET Audit Dimensions Reference

.NET-specific audit dimensions. These extend the base tech-debt-audit dimensions.

## Conditional Dimensions

Some dimensions are only evaluated when certain conditions are met.

### AOT & Trimming (Conditional)

Only evaluated when `PublishAot=true` or `IsAotCompatible=true` found in any project.

| Finding                                                      | Severity | Logic                                                        |
| ------------------------------------------------------------ | -------- | ------------------------------------------------------------ |
| `PublishAot=true` on Library project                         | HIGH     | AOT belongs on Exe projects — libraries don't publish        |
| `IsAotCompatible=true` with incompatible dependencies        | MEDIUM   | Pre-release packages often lack trim annotations             |
| Missing trim annotations on public APIs                      | MEDIUM   | Consumers can't trim safely without annotations              |
| Reflection-heavy code paths without `DynamicDependency`      | HIGH     | Breaks at runtime under trimming                             |
| `System.Text.Json` source generator not configured for AOT   | MEDIUM   | Critical for Blazor WASM, informational otherwise            |

**Detection:**
```bash
rg "PublishAot|IsAotCompatible" src/ -l
rg "DynamicDependency" src/
rg "JsonSerializerContext" src/
```

### Blazor WASM Health (Conditional)

Only evaluated when `Microsoft.NET.Sdk.BlazorWebAssembly` SDK found in any project.

| Finding                                                             | Severity | Logic                                          |
| ------------------------------------------------------------------- | -------- | ---------------------------------------------- |
| `BlazorCacheBootResources` property present                         | CRITICAL | Removed in .NET 10, causes build error         |
| `BlazorEnableCompression` instead of `CompressionEnabled`           | MEDIUM   | Renamed in .NET 8+                             |
| `OverrideHtmlAssetPlaceholders=true` without companion HTML         | HIGH     | Fingerprinting incomplete                      |
| Global JS scripts instead of ES modules                             | MEDIUM   | Anti-pattern — use `.razor.js` co-located      |
| `IJSObjectReference` without `IAsyncDisposable`                     | HIGH     | JS reference leak in browser heap              |
| JS interop calls in `OnInitializedAsync`                            | MEDIUM   | JS not available during prerender               |
| `JsonSerializerIsReflectionEnabledByDefault=false`                  | CRITICAL | Fatal for Blazor WASM                          |

**Detection:**
```bash
rg "Microsoft\.NET\.Sdk\.BlazorWebAssembly" src/ -l
rg "BlazorCacheBootResources|BlazorEnableCompression|JsonSerializerIsReflectionEnabledByDefault" src/
rg "IJSObjectReference" src/
```

### Data / Schema Debt (Conditional)

Only evaluated when EF Core (`Microsoft.EntityFrameworkCore`) is referenced.

| Finding                                               | Severity | Logic                                              |
| ----------------------------------------------------- | -------- | -------------------------------------------------- |
| Migration `Down()` throws `NotImplementedException`   | HIGH     | Irreversible migrations — can't roll back           |
| Migration `Down()` is empty                           | MEDIUM   | Silent no-op on rollback; may be intentional        |
| Entity model drift from actual schema                 | HIGH     | Model says one thing, database says another          |
| Missing indexes on foreign key columns                | MEDIUM   | Performance degradation on joins                     |
| Implicit type coercions in value conversions           | MEDIUM   | Silent data truncation risk                          |
| No migration history table or schema versioning       | HIGH     | Schema state is untracked                            |
| Shadow properties used without documentation          | LOW      | Hidden columns that surprise maintainers             |

**Detection:**
```bash
rg "Microsoft\.EntityFrameworkCore" src/ -l
rg "NotImplementedException" src/ -g "*Migration*.cs"
rg "protected override void Down" src/ -g "*Migration*.cs" -A 5
fd "Migration" src/ -e cs
```

### Cloud / Container Readiness (Conditional)

Only evaluated when Dockerfiles, container config, or cloud deployment targets are present.

| Finding                                                          | Severity | Logic                                                    |
| ---------------------------------------------------------------- | -------- | -------------------------------------------------------- |
| `Environment.GetEnvironmentVariable` without fallback            | MEDIUM   | 12-factor violation; silent null in containers            |
| Hardcoded file paths (`C:\`, `/Users/`, absolute paths)          | HIGH     | Breaks in containers and cross-platform                  |
| Windows-specific APIs (`Registry`, COM interop)                  | HIGH     | Fails silently or crashes in Linux containers            |
| Missing health check endpoint (`IHealthChecksBuilder`)           | MEDIUM   | Orchestrator can't probe liveness                        |
| No graceful shutdown handling (`IHostApplicationLifetime`)        | MEDIUM   | Container SIGTERM causes abrupt termination              |
| Missing `HEALTHCHECK` instruction in Dockerfile                  | LOW      | Orchestrator relies on process exit only                 |
| Large Docker image layers (no multi-stage build)                 | LOW      | Slow deploys, wasted bandwidth                           |

**Detection:**
```bash
fd Dockerfile .
rg "Environment\.GetEnvironmentVariable" src/ -g "*.cs"
rg "Registry\.|GetFolderPath|COM\b" src/ -g "*.cs"
rg "IHealthChecksBuilder|AddHealthChecks" src/ -g "*.cs"
rg "IHostApplicationLifetime|ApplicationStopping" src/ -g "*.cs"
```

### Service Contract Drift (Conditional)

Only evaluated when OpenAPI specs, published NuGet packages, or gRPC/protobuf definitions are present.

| Finding                                                  | Severity | Logic                                     |
| -------------------------------------------------------- | -------- | ----------------------------------------- |
| OpenAPI spec diverges from controller signatures         | HIGH     | Consumers get stale contracts              |
| Published package with no `PublicApiAnalyzers`           | MEDIUM   | Breaking changes ship silently             |
| gRPC `.proto` files not matching generated code          | HIGH     | Contract mismatch at wire level            |
| Event/message schemas with no versioning strategy        | MEDIUM   | Breaking changes propagate to consumers    |
| Missing consumer-driven contract tests                   | LOW      | No consumer-side validation                |

**Detection:**
```bash
fd "openapi|swagger" . -e json -e yaml -e yml
rg "PublicApiAnalyzers|Microsoft\.CodeAnalysis\.PublicApiAnalyzers" . -l
fd ".proto" src/
rg "Pact|PactNet" tests/ -l
```

## .NET Tooling

Use repo-specific build/test scripts when available, fall back to raw `dotnet` commands otherwise.

```bash
# Coverage (if script exists)
scripts/coverage.sh 2>/dev/null

# Build diagnostics (prefer repo scripts)
scripts/check.sh --all 2>/dev/null || dotnet build --no-incremental

# Package analysis
dotnet list package --vulnerable --include-transitive
dotnet list package --outdated --include-transitive
```

## Severity Conventions

| Severity | Meaning                                                                      |
| -------- | ---------------------------------------------------------------------------- |
| CRITICAL | Build breaks, security vulnerabilities, circular dependencies, data loss     |
| HIGH     | Anti-patterns, likely bugs, significant performance issues, license concerns  |
| MEDIUM   | Style/consistency issues, missing best practices, informational warnings     |
| LOW      | Minor polish, aspirational improvements, documentation gaps                  |
