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

**Detection:** `tda-detect-dimensions.sh` → `AOT_TRIMMING` section.

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

**Detection:** `tda-detect-dimensions.sh` → `BLAZOR_WASM` section.

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

**Detection:** `tda-detect-dimensions.sh` → `EF_CORE` section.

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

**Detection:** `tda-detect-dimensions.sh` → `CLOUD_CONTAINER` section.

### FIPS Compliance (Conditional)

Only evaluated when `System.Security.Cryptography` is referenced, or when the project targets government/regulated environments (FIPS keywords in docs, FedRAMP/FISMA/NIST references, `CryptoConfig` usage).

| Finding                                                              | Severity | Logic                                                         |
| -------------------------------------------------------------------- | -------- | ------------------------------------------------------------- |
| `MD5.Create()` or `MD5.HashData()` usage                            | CRITICAL | FIPS 140-2 non-compliant; use SHA-256+ for integrity checks   |
| `SHA1.Create()` or `SHA1.HashData()` for security purposes          | CRITICAL | FIPS deprecated; use SHA-256/384/512                          |
| `DES`, `RC2`, `TripleDES` usage                                     | CRITICAL | Broken/weak algorithms; use AES                              |
| `Rijndael` instead of `Aes`                                         | HIGH     | Legacy API; `Aes` is the FIPS-compliant equivalent            |
| `RNGCryptoServiceProvider` instead of `RandomNumberGenerator`       | MEDIUM   | Obsolete (SYSLIB0023); use `RandomNumberGenerator.Create()`   |
| `System.Random` for security-sensitive values                       | CRITICAL | Not cryptographically secure; use `RandomNumberGenerator`     |
| `ECB` cipher mode usage                                             | CRITICAL | Deterministic encryption; use CBC or GCM                      |
| Hardcoded encryption keys or IVs                                    | CRITICAL | Keys must come from key management, never source code         |
| RSA key size < 2048 bits                                            | HIGH     | NIST minimum is 2048; prefer 3072+                            |
| `DSA` usage                                                         | HIGH     | FIPS 186-5 deprecated DSA; use ECDSA or EdDSA                |
| `SslProtocols.Ssl3` or `SslProtocols.Tls` (1.0) or `Tls11`        | CRITICAL | Deprecated protocols; enforce TLS 1.2+ minimum               |
| `ServicePointManager.SecurityProtocol` set manually                 | HIGH     | Let the OS/framework negotiate; setting manually risks pinning weak protocols |
| `PasswordDeriveBytes` instead of `Rfc2898DeriveBytes`               | CRITICAL | Non-standard PBKDF1; use PBKDF2 (`Rfc2898DeriveBytes`) with SHA-256+ |
| `Rfc2898DeriveBytes` with SHA1 (default before .NET 8)              | HIGH     | Explicitly specify `HashAlgorithmName.SHA256` or higher       |
| Missing CA53xx FIPS rules in `.globalconfig` / `.editorconfig`      | MEDIUM   | CA5350-CA5403 should be error-severity for FIPS compliance    |
| `HMACMD5` or `HMACRIPEMD160` usage                                 | HIGH     | Non-FIPS HMACs; use HMACSHA256+                              |
| `Convert.ToBase64String` on raw secrets without encryption          | MEDIUM   | Encoding is not encryption; flag for review                   |

**Detection:** `tda-detect-dimensions.sh` → `FIPS_COMPLIANCE` section.

**Analyzer guardrail check:** Verify that these CA rules are set to `error` in `.globalconfig` or `.editorconfig`. If missing, flag as MEDIUM — the blanket default may cover them, but explicit pinning prevents regression:
- CA5350 (weak crypto), CA5351 (broken crypto), CA5358 (ECB), CA5364 (deprecated TLS)
- CA5379 (weak KDF), CA5384 (DSA), CA5385 (RSA key size), CA5394 (insecure random)
- CA5397 (deprecated SslProtocols), CA5401/CA5402 (IV handling), CA5403 (hardcoded certs)

### Service Contract Drift (Conditional)

Only evaluated when OpenAPI specs, published NuGet packages, or gRPC/protobuf definitions are present.

| Finding                                                  | Severity | Logic                                     |
| -------------------------------------------------------- | -------- | ----------------------------------------- |
| OpenAPI spec diverges from controller signatures         | HIGH     | Consumers get stale contracts              |
| Published package with no `PublicApiAnalyzers`           | MEDIUM   | Breaking changes ship silently             |
| gRPC `.proto` files not matching generated code          | HIGH     | Contract mismatch at wire level            |
| Event/message schemas with no versioning strategy        | MEDIUM   | Breaking changes propagate to consumers    |
| Missing consumer-driven contract tests                   | LOW      | No consumer-side validation                |

**Detection:** `tda-detect-dimensions.sh` → `SERVICE_CONTRACT` section.

## .NET Tooling

Use the co-located scripts in `../scripts/` for detection and NuGet analysis. For build/test, prefer repo scripts (`scripts/check.sh`, `scripts/build.sh`) over raw `dotnet` commands.

## Severity Conventions

| Severity | Meaning                                                                      |
| -------- | ---------------------------------------------------------------------------- |
| CRITICAL | Build breaks, security vulnerabilities, circular dependencies, data loss     |
| HIGH     | Anti-patterns, likely bugs, significant performance issues, license concerns  |
| MEDIUM   | Style/consistency issues, missing best practices, informational warnings     |
| LOW      | Minor polish, aspirational improvements, documentation gaps                  |
