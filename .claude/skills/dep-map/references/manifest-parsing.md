# dep-map — Manifest Parsing Reference

Per-language parsing instructions for Step 3 of the dep-map workflow.

---

## Step 3a — Detect language and locate manifests

Run these Glob patterns **in parallel** (excluding `node_modules/`, `**/bin/**`, `**/obj/**`):

| Glob                                     | Purpose                      |
| ---------------------------------------- | ---------------------------- |
| `<repo-dir>/**/*.csproj`                 | .NET NuGet                   |
| `<repo-dir>/**/pom.xml`                  | Java Maven                   |
| `<repo-dir>/**/package.json`             | Node / npm / yarn            |
| `<repo-dir>/**/Directory.Packages.props` | Central NuGet versions       |
| `<repo-dir>/**/go.mod`                   | Go modules                   |
| `<repo-dir>/**/Cargo.toml`               | Rust crates                  |
| `<repo-dir>/**/requirements*.txt`        | Python pip                   |
| `<repo-dir>/**/pyproject.toml`           | Python pyproject             |

From the manifest types found, determine the repo's **primary language(s)**:

| Manifests present                       | Language             |
| --------------------------------------- | -------------------- |
| `.csproj` only                          | C# / .NET            |
| `pom.xml` only                          | Java                 |
| `package.json` only                     | TypeScript / JS      |
| `.csproj` + root `package.json`         | C# + TypeScript      |
| `go.mod`                                | Go                   |
| `Cargo.toml`                            | Rust                 |
| `requirements*.txt` / `pyproject.toml`  | Python               |

---

## Step 3b — Parse NuGet dependencies (.csproj)

For each `.csproj` file found:

1. Skip test/bench projects: `*.Tests.csproj`, `*.Test.csproj`, `*.UnitTests.csproj`, `*.IntegrationTests.csproj`, `*.Benchmarks.csproj`.
2. Read the file. Extract all `<PackageReference>` lines. Read `Directory.Packages.props` if present.
3. Grep with pattern `PackageReference Include="([^"]+)"` to extract package names. Resolve versions from `Version=` attribute or `Directory.Packages.props`.
4. Classify each package (see [dependency-classification.md](dependency-classification.md)).
5. Build two lists per repo:
   - **External service integrations** — client packages (`.Api.Client`, `.Client.Abstractions`, `.Private.Api.Client`, `.Public.Api.Client`, `-sdk-client`)
   - **Key third-party** — messaging, ORM/DB, DI, auth, observability, scheduler libs

---

## Step 3c — Parse Maven dependencies (pom.xml)

For each `pom.xml` found at depth ≤ 2 within the repo:

1. Read the file. Grep to extract `<groupId>` / `<artifactId>` / `<version>` triples from `<dependencies>`. Skip `<parent>` block.
2. Classify each dependency (see [dependency-classification.md](dependency-classification.md)).
3. Grep Java source files for cross-repo service client patterns:
   ```
   Grep pattern: "\w+ClientBuilder\(\)"
   Grep pattern: "\w+ClientImpl\.builder\(\)"
   Grep pattern: "\w+ClientFactory\."
   ```
   Run against `<repo-dir>/src/**/*.java`.

---

## Step 3d — Parse npm / yarn dependencies (package.json)

For each `package.json` not inside `node_modules/`:

1. Read the file. Extract `dependencies` and `devDependencies` objects.
2. If `name` field contains `test` or `spec`, classify all deps as dev/test.
3. Classify packages (see [dependency-classification.md](dependency-classification.md)).
4. For frontend SPA repos (Angular, React, Vue), always list:
   - Framework: `@angular/*`, `react`, `vue`
   - State/data: `rxjs`, `ngrx`, `zustand`, `tanstack-query`
   - Build: webpack, `@module-federation/*`, vite
   - UI: ReactFlow, ag-grid, primeng, material
   - Testing: vitest, playwright, jest, cypress

---

## Step 3e — Parse Go modules (go.mod)

For each `go.mod` found:

1. Read the file. Extract `require` block entries.
2. Classify: stdlib (`golang.org/x/*`, `google.golang.org/*`), first-party (same module prefix), third-party.
3. Note key frameworks: gin, echo, fiber, chi (HTTP); gorm, pgx, sqlx (DB); cobra (CLI); zerolog, zap (logging).

---

## Step 3f — Parse Rust crates (Cargo.toml)

For each `Cargo.toml` found:

1. Read the file. Extract `[dependencies]` and `[dev-dependencies]` sections.
2. Classify workspace members vs external crates.
3. Note key crates: tokio, actix-web, axum (async/HTTP); sqlx, diesel (DB); serde (serialization); tracing (observability).

---

## Step 3g — Parse Python dependencies (requirements*.txt / pyproject.toml)

1. `requirements*.txt`: read and extract package specs (name + optional version pin).
2. `pyproject.toml`: extract `[project.dependencies]` and optional tool sections.
3. Note key frameworks: FastAPI, Flask, Django (HTTP); SQLAlchemy, Alembic (DB); pytest (test); pydantic (validation); celery (tasks).

---

## Step 3h — Detect infrastructure dependencies from config

Grep config files in parallel:

**Files to check:** `**/appsettings*.json` (exclude `appsettings.Test*.json`), `**/application.yml`, `**/application.properties`, `**/application-*.yml`, `**/.env`, `**/.env.example`, `**/config.yml`, `**/config.yaml`, `**/config.json`

| Grep pattern (use `-i`)                              | Infrastructure dep     |
| ---------------------------------------------------- | ---------------------- |
| `"ConnectionStrings"` or `jdbc:sqlserver` or `postgres://` | SQL (MSSQL/Postgres) |
| `"Redis"` or `spring\.data\.redis` or `REDIS_URL`   | Redis Cache            |
| `"RabbitMQ"` or `spring\.rabbitmq` or `RABBITMQ_`   | RabbitMQ               |
| `"CosmosDb"` or `"CosmosDB"`                         | Azure CosmosDB         |
| `BlobStorage` or `AzureWebJobsStorage` or `S3_BUCKET`| Blob / Object Storage  |
| `ServiceBus` or `EVENT_HUB`                          | Azure Service Bus      |
| `"Elasticsearch"` or `spring\.elasticsearch` or `ELASTIC_` | Elasticsearch    |
| `"ArangoDB"` or `arangodb`                           | ArangoDB               |
| `"MongoDB"` or `MONGO_URI`                           | MongoDB                |
| `kafka\.bootstrap` or `KAFKA_BROKERS`                | Apache Kafka           |
| `Quartz` or `quartz\.datasource`                     | Quartz Job Store       |
| `HangfireSettings` or `"Hangfire"`                   | Hangfire scheduler     |
| `NATS_URL` or `nats://`                              | NATS messaging         |

Record: confirmed (pattern found in config) vs inferred (implied by package dep only).

---

## Step 3i — Build the Mermaid service layer diagram

For each repo, build one `graph TB` diagram. Rules:

1. **External services = depth 1 only.** Show only services this repo calls directly.
2. **Service name sources** (priority): existing lode `architecture.md` / `tech-stack.md` → Dockerfile `ENV`/`LABEL` → Kubernetes YAML → repo name (camelCase fallback).
3. **Edge labels:** state the integration mechanism — NuGet package name, `groupId:artifactId`, npm package name, `"HTTP: ClientClassName"`, `"MQ: queue.name"`.
4. **Mermaid node ID rules:** alphanumeric + underscore only.
5. **Size limit:** keep under 25 nodes. Group minor deps into `"Other services"`.
6. **Non-service repos** (SQL schemas, Infra/Terraform, QA): draw a minimal single-node diagram or note "No running services — infrastructure/tooling repo."
7. **Shape conventions:** service = rectangle `svc["Label"]`; database = cylinder `db[("Label")]`; message queue = rectangle `mq["Label (queue)"]`.
8. **Docker base image in service labels** when known: `svc1["admin-api\nmcr.microsoft.com/dotnet/aspnet:10.0-alpine"]`.

---

## Step 3j — Detect runtime versions and Docker images

Collect runtime pinning from these files (run all Globs in parallel):

| File                                          | What it provides                  |
| --------------------------------------------- | --------------------------------- |
| `**/Dockerfile` and `**/Dockerfile.*`         | Base runtime + SDK build image    |
| `**/global.json`                              | .NET SDK version pin              |
| `**/.nvmrc` or `**/.node-version`             | Node.js version pin               |
| `**/.java-version` or `**/.tool-versions`     | Java / SDKMAN version pin         |
| `**/*.csproj` (`<TargetFramework>`)           | .NET target framework per project |
| `**/pom.xml` (`java.version`)                 | Java compile target               |
| `**/package.json` (`engines` field)           | Node.js / npm engine constraints  |
| `**/go.mod` (`go` directive)                  | Go toolchain version              |
| `**/azure-pipelines.yml`, `**/.azure/**/*.yml`| CI SDK task versions              |
| `**/.github/workflows/*.yml`                  | GitHub Actions SDK versions       |

**Dockerfile parsing:** Extract every `FROM` instruction. Distinguish runtime stages (contains `aspnet`, `runtime`, `jre`, `node`, `alpine` without `sdk`) from build stages (contains `sdk`, `build`, `maven`, `gradle`). Record the final `FROM` or stage named `runtime`/`final` as the production image.

**Common Docker image patterns:**

| Image pattern                              | Runtime                      |
| ------------------------------------------ | ---------------------------- |
| `mcr.microsoft.com/dotnet/aspnet:<ver>`    | .NET runtime (production)    |
| `mcr.microsoft.com/dotnet/sdk:<ver>`       | .NET SDK (build stage)       |
| `eclipse-temurin:<ver>-jre*`               | OpenJDK runtime              |
| `node:<ver>-alpine`                        | Node.js runtime              |
| `python:<ver>-slim`                        | Python runtime               |
| `golang:<ver>-alpine`                      | Go build/runtime             |

**CI pipeline SDK tasks:** Grep for `UseDotNet`, `JavaToolInstaller`, `NodeTool`, `actions/setup-*` and extract their version fields.

**Per-repo runtime record fields:** `runtime_image`, `sdk_image`, `dotnet_sdk_pin`, `target_framework`, `java_version`, `node_version`, `go_version`, `ci_sdk_version`. Mark undetected as `—`.
