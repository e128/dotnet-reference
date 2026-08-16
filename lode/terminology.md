# Terminology
*Updated: 2026-08-16T12:37:39Z*

- **CPM** — Central Package Management. NuGet feature where all package versions are declared in `Directory.Packages.props` rather than per-project.
- **MTP** — Microsoft Testing Platform. The modern test execution platform used by xUnit v3, replacing the legacy vstest runner.
- **Lode** — Structured AI-owned markdown repository for persistent project knowledge. Lives in `lode/`.
- **Plan** — A structured planning document set (`{name}-plan.md`, `{name}-context.md`, `{name}-tasks.md`) that lives in `plans/{name}/`.
- **TDD** — Test-Driven Development. Red-Green-Refactor cycle.
- **STE** — Simplified Technical English (after ASD-STE100). The repo's writing standard for docs, READMEs, PR bodies, error messages, comments, and lode files. Rules live in `.claude/rules/writing-style.md`.
- **Roslyn Analyzer** — A .NET compiler extension that provides real-time code analysis and diagnostics. This project ships `E128.Analyzers`, a custom analyzer package with design, style, and file-system rules.
- **ArchUnitNET** — Architecture testing library for .NET. Used in `tests/Architecture.Tests/` to enforce layer dependencies, naming conventions, and sealed-class policies.
- **Trusted Publishing** — OIDC-based NuGet publishing. The `publish.yml` workflow authenticates to NuGet via OIDC rather than long-lived API keys.
- **OIDC**: OpenID Connect. The token protocol behind Trusted Publishing. It exchanges a short-lived token for a publish credential instead of a stored API key.
- **Renovate** — Dependency management bot. Configured in `renovate.json` to group updates, auto-merge patch/minor, and flag majors for review.
- **Kestrel** — ASP.NET Core's cross-platform web server.
- **TFM**: Target Framework Moniker. A string that names a project's target runtime, for example `net10.0` or `netstandard2.0`.
- **Diagnostic ID**: The `E128xxx` number assigned to each custom analyzer rule, for example `E128064`. The `E128.Analyzers` README lists the full rule table.
- **YAGNI**: You Aren't Gonna Need It. A principle that balances SOLID. Build an abstraction only when the code needs it now, not for a hypothetical future need.
- **TOCTOU**: Time-Of-Check To Time-Of-Use. A race condition where a file changes between a check and a later use. Analyzer `E128064` flags this pattern.
- **FIPS**: Federal Information Processing Standards. A hash-algorithm compliance target that analyzer `E128071` enforces. The container's stock Ubuntu Noble OpenSSL package ships no FIPS provider.
- **Deterministic Script**: A `scripts/*.sh` wrapper that replaces an ad hoc command with a repeatable, token-free operation. See `.claude/rules/deterministic-scripts.md` for the full routing table.
