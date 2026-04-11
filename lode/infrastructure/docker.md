# Docker
*Updated: 2026-04-10T19:00:00Z*

## Build Commands

| Command                                | Loads locally? | BuildKit features? | Notes                              |
| -------------------------------------- | -------------- | ------------------- | ---------------------------------- |
| `docker build -t img .`               | Yes            | Yes (Docker 23+)    | Preferred for local builds         |
| `docker buildx build -t img .`        | Depends        | Yes                 | Only with `docker` driver          |
| `docker buildx build --load -t img .` | Yes            | Yes                 | Explicit — works with all drivers  |
| `DOCKER_BUILDKIT=0 docker build`      | Yes            | No                  | Legacy fallback, avoid             |

### The `-t` + `--load` Issue

`docker buildx build -t` does NOT guarantee the image appears in `docker images`. The `-t` flag only tags; loading depends on the builder driver:

- **`docker` driver** (default on Docker Desktop): `--load` implied, image loads automatically
- **`docker-container` / `remote` driver**: image stays in build cache only — must add `--load`

`--load` does not work with multi-platform builds (`--platform linux/amd64,linux/arm64`). Use `--push` instead.

## This Project

### `scripts/docker.sh`

Subcommands: `build`, `run`, `test`, `stop`, `clean`. Uses `DOCKER_BUILDKIT=0` to force legacy builder. This sidesteps the buildx loading issue but loses parallel stage builds and better caching.

### `DockerSmokeTests.cs`

xUnit tests using `IAsyncLifetime` to build/start container in `InitializeAsync` and tear down in `DisposeAsync`. Uses `HttpClient` to hit `/` and `/health` endpoints.

**Known issue**: calls `docker buildx build -t` without `--load`. Works on default `docker` driver but fails on `docker-container` driver (e.g., some CI runners). Fix: add `--load` or switch to `docker build`.

Hardcoded port `58080` — could conflict in parallel CI. Tests tagged `[Trait("Category", "Docker")]` for selective execution.

## Dockerfile Structure

Three-stage Alpine-based build (`Dockerfile` at repo root):

1. **restore** — `sdk:10.0-alpine`, copies build infra + csproj files, runs `dotnet restore`
2. **build** — copies source, runs `dotnet publish --configuration Release`
3. **runtime** — `aspnet:10.0-alpine`, hardened (apk removed), non-root user, health check via `wget`

## Smoke Test Patterns

| Pattern                          | Complexity | Best for                                |
| -------------------------------- | ---------- | --------------------------------------- |
| Shell script (`docker.sh test`)  | Low        | Quick CI gate, no test framework needed |
| xUnit + `IAsyncLifetime`        | Medium     | Integrated test reporting, C# type safe |
| Testcontainers NuGet package    | High       | Dynamic ports, DB deps, auto-cleanup   |

This project uses both shell script and xUnit approaches. Testcontainers (v4.11.0) is worth adopting when infrastructure dependencies (databases, queues) are added.

## Sources

- [Docker Buildx Build Docs](https://docs.docker.com/reference/cli/docker/buildx/build/) — scraped 2026-04-10
- [buildx `--load` explanation](https://www.codestudy.net/blog/docker-buildx-fails-to-show-result-in-image-list/) — scraped 2026-04-10
- [Andrew Lock: Smoke tests for ASP.NET Core in CI](https://andrewlock.net/running-smoke-tests-for-asp-net-core-apps-in-ci-using-docker/) — scraped 2026-04-10
- [Testcontainers for .NET](https://dotnet.testcontainers.org/) — scraped 2026-04-10
