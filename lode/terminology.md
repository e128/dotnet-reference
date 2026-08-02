# Terminology
*Updated: 2026-08-02T00:00:00Z*

- **CPM** — Central Package Management. NuGet feature where all package versions are declared in `Directory.Packages.props` rather than per-project.
- **MTP** — Microsoft Testing Platform. The modern test execution platform used by xUnit v3, replacing the legacy vstest runner.
- **Lode** — Structured AI-owned markdown repository for persistent project knowledge. Lives in `lode/`.
- **Plan** — A structured planning document set (`{name}-plan.md`, `{name}-context.md`, `{name}-tasks.md`) that lives in `plans/{name}/`.
- **TDD** — Test-Driven Development. Red-Green-Refactor cycle.
- **STE** — Simplified Technical English (after ASD-STE100). The repo's writing standard for docs, READMEs, PR bodies, error messages, comments, and lode files. Rules live in `.claude/rules/writing-style.md`.
- **Roslyn Analyzer** — A .NET compiler extension that provides real-time code analysis and diagnostics. This project ships `E128.Analyzers`, a custom analyzer package with design, style, and file-system rules.
- **ArchUnitNET** — Architecture testing library for .NET. Used in `tests/Architecture.Tests/` to enforce layer dependencies, naming conventions, and sealed-class policies.
- **Trusted Publishing** — OIDC-based NuGet publishing. The `publish.yml` workflow authenticates to NuGet via OIDC rather than long-lived API keys.
- **Renovate** — Dependency management bot. Configured in `renovate.json` to group updates, auto-merge patch/minor, and flag majors for review.
- **Kestrel** — ASP.NET Core's cross-platform web server.
