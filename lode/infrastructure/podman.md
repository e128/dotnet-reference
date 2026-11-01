# Podman
*Updated: 2026-11-01T20:54:56Z*

## Why Podman

Podman is daemonless and rootless by default. It runs no long-running root process and needs no Docker Desktop license. CLI is near drop-in compatible with docker (`podman build/run/rm/rmi` match the docker syntax). The `podman generate kube` and `podman play kube` commands give a closer round-trip to Kubernetes manifests than docker-compose. This suits this repo, because `compose.yaml` already runs the container with K8s-style hardening (`read_only`, `cap_drop: ALL`, rootless).

## This Project

### `scripts/podman.sh`

Subcommands: `build`, `run`, `test`, `stop`, `clean`. No BuildKit driver concept to manage. Podman builds multi-stage Dockerfiles natively, so no `DOCKER_BUILDKIT` equivalent exists to set.

### `PodmanSmokeTests.cs`

xUnit v3 tests using `IAsyncLifetime` (`ValueTask` overloads) to build/start the container in `InitializeAsync` and tear down in `DisposeAsync`. Uses `FindRepoRoot()` to locate the Dockerfile directory and sets `WorkingDirectory` on the process. `RunPodmanAsync` checks exit codes and throws `InvalidOperationException` on failure (suppressible via `throwOnError: false`). Uses `HttpClient` to hit `/` and `/health` endpoints.

Hardcoded port `58080` could conflict in parallel CI. The tests carry `[Trait("Category", "Podman")]` for selective execution. `IsPodmanAvailableAsync` runs `podman info` and the test suite skips gracefully (`Assert.Skip`) when Podman is unavailable, so they never fail on a machine without it.

## Dockerfile Structure

The `Dockerfile` at the repo root has three stages on Resolute images. Podman looks for `Containerfile` first and falls back to `Dockerfile`, so no rename is needed.

1. **restore**: `sdk:10.0-resolute`, copies build infra + csproj files, runs `dotnet restore`
2. **build**: copies source, runs `dotnet publish --configuration Release`
3. **runtime**: `aspnet:10.0-resolute`, installs `curl` (for the healthcheck), non-root user

The image has no FIPS 140-2 setup. The stock Ubuntu Resolute `openssl` apt package ships no FIPS provider module on arm64 or amd64. Ubuntu certifies its FIPS OpenSSL module only behind an Ubuntu Pro subscription (`pro attach` and `pro enable fips-updates`).

## `.dockerignore`

Excludes build output, Git metadata, IDE files, `.claude/`, docs (`lode/`, `plans/`, `prompts/`, `*.md` except `README.md`), test and benchmark projects, `src/E128.Analyzers/`, scripts, CI config, secrets, and OS artifacts. `src/E128.Reference.Core/`, `src/E128.Reference.Web/`, and `src/E128.Reference.Cli/` all reach the build context, since `.dockerignore` does not exclude the Cli project. The Dockerfile publishes only `E128.Reference.Web`. Podman checks `.containerignore` first, then falls back to `.dockerignore`. This repo has no `.containerignore`.

## macOS Setup

Podman needs a Linux VM on macOS. Docker Desktop bundles one transparently. Run `podman machine init && podman machine start` once after install.

## Smoke Test Patterns

| Pattern                          | Complexity | Best for                                |
| -------------------------------- | ---------- | --------------------------------------- |
| Shell script (`podman.sh test`)  | Low        | Quick CI gate, no test framework needed |
| xUnit + `IAsyncLifetime`        | Medium     | Integrated test reporting, C# type safe |
| Testcontainers NuGet package    | High       | Dynamic ports, DB deps, auto-cleanup   |

This project uses both shell script and xUnit approaches. Testcontainers supports podman via `DOCKER_HOST`/`CONTAINERS_MACHINE_PROVIDER` env vars when the project adds infrastructure dependencies such as databases or queues.

## Sources

- [Podman Documentation](https://docs.podman.io/): official docs
- [Podman vs Docker](https://podman.io/whatis.html): daemonless/rootless architecture overview
