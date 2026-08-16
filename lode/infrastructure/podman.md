# Podman
*Updated: 2026-08-16T12:36:24Z*

## Why Podman

Daemonless and rootless by default — no long-running root process, no Docker Desktop license. CLI is near drop-in compatible with docker (`podman build/run/rm/rmi` match docker's syntax). `podman generate kube` / `podman play kube` give a closer round-trip to Kubernetes manifests than docker-compose, useful since this repo's `compose.yaml` already runs the container with K8s-style hardening (`read_only`, `cap_drop: ALL`, rootless).

## This Project

### `scripts/podman.sh`

Subcommands: `build`, `run`, `test`, `stop`, `clean`. No BuildKit driver concept to manage — podman builds multi-stage Dockerfiles natively, so there's no `DOCKER_BUILDKIT` equivalent to set.

### `PodmanSmokeTests.cs`

xUnit v3 tests using `IAsyncLifetime` (`ValueTask` overloads) to build/start the container in `InitializeAsync` and tear down in `DisposeAsync`. Uses `FindRepoRoot()` to locate the Dockerfile directory and sets `WorkingDirectory` on the process. `RunPodmanAsync` checks exit codes and throws `InvalidOperationException` on failure (suppressible via `throwOnError: false`). Uses `HttpClient` to hit `/` and `/health` endpoints.

Hardcoded port `58080` — could conflict in parallel CI. Tests tagged `[Trait("Category", "Podman")]` for selective execution; `IsPodmanAvailableAsync` runs `podman info` and the test suite skips gracefully (`Assert.Skip`) when Podman isn't available, so these never hard-fail on a machine without it.

## Dockerfile Structure

Three-stage Noble-based build (`Dockerfile` at repo root, unchanged filename — podman looks for `Containerfile` first, falls back to `Dockerfile`):

1. **restore** — `sdk:10.0-noble`, copies build infra + csproj files, runs `dotnet restore`
2. **build** — copies source, runs `dotnet publish --configuration Release`
3. **runtime** — `aspnet:10.0-noble`, installs `curl` (for the healthcheck), non-root user

No FIPS 140-2 setup: the stock Ubuntu Noble `openssl` apt package ships no FIPS provider module on either arm64 or amd64 — Ubuntu's certified FIPS OpenSSL module is gated behind an Ubuntu Pro subscription (`pro attach` + `pro enable fips-updates`), not available via plain `apt-get install`. A prior version of this Dockerfile ran `openssl fipsinstall` against a `find`-located `fips.so` that doesn't exist in this image, so the build always failed — this went undetected until Podman's build was first actually run locally.

## `.dockerignore`

Excludes build output, Git metadata, IDE files, `.claude/`, docs (`lode/`, `plans/`, `prompts/`, `*.md` except `README.md`), test and benchmark projects, `src/E128.Analyzers/`, scripts, CI config, secrets, and OS artifacts. `src/E128.Reference.Core/`, `src/E128.Reference.Web/`, and `src/E128.Reference.Cli/` all reach the build context, since `.dockerignore` does not exclude the Cli project. The Dockerfile publishes only `E128.Reference.Web`. Podman checks `.containerignore` first. It falls back to `.dockerignore`. No rename is needed.

## macOS Setup

Podman needs a Linux VM on macOS (unlike Docker Desktop, which bundles one transparently): `podman machine init && podman machine start` once after install.

## Smoke Test Patterns

| Pattern                          | Complexity | Best for                                |
| -------------------------------- | ---------- | --------------------------------------- |
| Shell script (`podman.sh test`)  | Low        | Quick CI gate, no test framework needed |
| xUnit + `IAsyncLifetime`        | Medium     | Integrated test reporting, C# type safe |
| Testcontainers NuGet package    | High       | Dynamic ports, DB deps, auto-cleanup   |

This project uses both shell script and xUnit approaches. Testcontainers (v4.11.0) supports podman via `DOCKER_HOST`/`CONTAINERS_MACHINE_PROVIDER` env vars if infrastructure dependencies (databases, queues) are added later.

## Sources

- [Podman Documentation](https://docs.podman.io/) — official docs
- [Podman vs Docker](https://podman.io/whatis.html) — daemonless/rootless architecture overview
