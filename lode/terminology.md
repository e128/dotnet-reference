# Terminology
*Updated: 2026-04-11T14:11:43Z*

- **CPM** — Central Package Management. NuGet feature where all package versions are declared in `Directory.Packages.props` rather than per-project.
- **MTP** — Microsoft Testing Platform. The modern test execution platform used by xUnit v3, replacing the legacy vstest runner.
- **Lode** — Structured AI-owned markdown repository for persistent project knowledge. Lives in `lode/`.
- **Plan** — A structured planning document set (`plan.md`, `context.md`, `tasks.md`) that lives in `plans/{name}/`.
- **TDD** — Test-Driven Development. Red-Green-Refactor cycle.
- **Roslyn Analyzer** — A .NET compiler extension that provides real-time code analysis and diagnostics. This project ships `E128.Analyzers`, a custom analyzer package with design, style, and file-system rules.
- **ArchUnitNET** — Architecture testing library for .NET. Used in `tests/Architecture.Tests/` to enforce layer dependencies, naming conventions, and sealed-class policies.
- **Trusted Publishing** — OIDC-based NuGet publishing. The `publish.yml` workflow authenticates to NuGet via OIDC rather than long-lived API keys.
- **Renovate** — Dependency management bot. Configured in `renovate.json` to group updates, auto-merge patch/minor, and flag majors for review.
- **BuildKit** — Docker's modern build subsystem. Currently disabled (`DOCKER_BUILDKIT=0`) in `scripts/docker.sh` for compatibility.
- **Kestrel** — ASP.NET Core's cross-platform web server.
