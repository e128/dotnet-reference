# E128.Reference — .NET 10 Reference Repository

[![CI](https://github.com/e128/dotnet-reference/actions/workflows/ci.yml/badge.svg)](https://github.com/e128/dotnet-reference/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](./LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

## Summary

A .NET 10 reference repository demonstrating modern conventions for web, CLI, and Docker applications.
Features strict deny-by-default code analysis with third-party Roslyn analyzers, custom E128.Analyzers (NuGet-packable), ArchUnitNET architecture tests, xUnit v3 on the Microsoft Testing Platform, and Central Package Management with transitive pinning.
Automated dependency updates via Renovate and NuGet trusted publishing via GitHub Actions OIDC.
Includes a complete Claude Code development harness with bash scripts, contextual rules, skills, and agents.
Uses the [Lode Coding Toolkit][lode-toolkit] for structured, AI-owned project documentation.

## Quick Start

```bash
# Build
scripts/build.sh

# Test (CI category only — default)
scripts/test.sh

# Full CI pipeline (format + build + test)
scripts/ci.sh

# Docker
scripts/docker.sh build
scripts/docker.sh test
```

## What's Included

| Component                | Description                                                         |
| ------------------------ | ------------------------------------------------------------------- |
| **E128.Reference.Web**   | Minimal API web app with Kestrel, health endpoint                   |
| **E128.Reference.Cli**   | System.CommandLine CLI with `--name` option                         |
| **E128.Reference.Core**  | Shared library (Greeter service, models, repositories, services)    |
| **E128.Analyzers**       | Custom Roslyn analyzers (E128001–E128014) with code fixes, NuGet-packable |
| **E128.Reference.Tests** | xUnit v3 + MTP with CI, Docker, and Manual test categories          |
| **Architecture.Tests**   | ArchUnitNET structural invariant tests (layers, naming, sealed)     |
| **E128.Analyzers.Tests** | Analyzer and code fix unit tests                                    |
| **Docker**               | Hardened Alpine multi-stage Dockerfile + docker-compose.yml         |
| **Bash scripts**         | Build, test, format, CI, Docker, lode management ([catalog](scripts/README.md)) |
| **Claude Code harness**  | CLAUDE.md, rules, hooks, skills, agents (see `.claude/`)            |
| **CI/CD**                | GitHub Actions CI + NuGet trusted publishing + Azure DevOps YAML    |
| **Renovate**             | Automated dependency updates with grouped PRs and security bypass   |
| **Lode**                 | Structured documentation via [Lode Coding Toolkit][lode-toolkit]    |

## Prerequisites

### Required

| Tool         | Version | macOS                        | Ubuntu/Debian                    | Windows                          |
| ------------ | ------- | ---------------------------- | -------------------------------- | -------------------------------- |
| .NET SDK     | 10.0+   | `brew install dotnet`        | [Microsoft docs][dotnet-install] | `winget install Microsoft.DotNet.SDK.10` |
| bash         | 5.0+    | `brew install bash`          | Included (5.1+)                  | WSL2 recommended                 |
| ripgrep (rg) | 14+     | `brew install ripgrep`       | `sudo apt install ripgrep`       | `winget install BurntSushi.ripgrep.MSVC` |
| fd           | 9+      | `brew install fd`            | `sudo apt install fd-find`       | `winget install sharkdp.fd`      |
| jq           | 1.7+    | `brew install jq`            | `sudo apt install jq`            | `winget install jqlang.jq`       |
| Docker       | 24+     | Docker Desktop or `colima`   | `sudo apt install docker.io`     | Docker Desktop                   |

[dotnet-install]: https://learn.microsoft.com/en-us/dotnet/core/install/linux
[lode-toolkit]: https://fjzeit.github.io/lode

### Optional

| Tool               | Purpose                  | Install                                        |
| ------------------ | ------------------------ | ---------------------------------------------- |
| shellcheck         | Bash script linter       | `brew install shellcheck`                      |
| dotnet-outdated    | NuGet update checker     | `dotnet tool install -g dotnet-outdated-tool`  |

> **Note (macOS):** The default `/bin/bash` on macOS is 3.2 (2007). Install bash 5+ via Homebrew and ensure `/opt/homebrew/bin/bash` is in your `$PATH` before `/bin/bash`.

> **Note (Ubuntu):** The `fd` package is named `fd-find`. The binary is `fdfind`. Create an alias: `ln -s $(which fdfind) ~/.local/bin/fd`.

## Project Structure

```
.
├── .claude/                  # Claude Code harness
│   ├── agents/               # Agent definitions
│   ├── hooks/                # Session/guardrail hooks
│   ├── rules/                # Contextual rule files
│   ├── settings.json         # Permissions and hook config
│   ├── skills/               # Skill directories
│   └── tmp/                  # Session artifacts (gitignored)
├── .editorconfig             # Code style (120 char, 4-space, file-scoped ns)
├── .globalconfig             # Analyzer severities (deny-by-default)
├── .github/workflows/ci.yml  # GitHub Actions CI
├── .github/workflows/publish.yml # NuGet trusted publishing
├── azure-pipelines.yml       # Azure DevOps CI
├── CLAUDE.md                 # Always-loaded AI instructions
├── Directory.Build.props     # Shared build properties
├── Directory.Build.targets   # Conditional targets (test project config)
├── Directory.Packages.props  # Central package versions
├── Dockerfile                # Multi-stage web app image
├── docker-compose.yml        # Container orchestration
├── E128.Reference.slnx       # Solution file
├── global.json               # SDK version + MTP test runner config
├── lode/                     # Project knowledge documentation
├── nuget.config              # Single source + source mapping
├── plans/                    # Structured planning documents
├── renovate.json             # Renovate dependency update config
├── scripts/                  # Bash development scripts ([catalog](scripts/README.md))
├── src/
│   ├── E128.Analyzers/       # Custom Roslyn analyzers (NuGet package)
│   ├── E128.Reference.Core/  # Shared library
│   ├── E128.Reference.Web/   # ASP.NET Core minimal API
│   └── E128.Reference.Cli/   # System.CommandLine CLI
└── tests/
    ├── Architecture.Tests/   # ArchUnitNET structural invariants
    ├── E128.Analyzers.Tests/ # Analyzer unit tests
    └── E128.Reference.Tests/ # xUnit v3 + MTP
```

## Analyzer Configuration

This repo uses a **deny-by-default** analyzer strategy:

- `dotnet_analyzer_diagnostic.severity = error` — every diagnostic is an error unless explicitly overridden
- Third-party analyzer packages (see [`Directory.Packages.props`](./Directory.Packages.props)) provide ~1000+ rules
- ~60 rules explicitly disabled or set to `suggestion` (documented in `.globalconfig`)
- Test projects have a separate `tests/.globalconfig` at `global_level = 101` for test-appropriate overrides

### Analyzer Packages

| Package                                     | Focus                    |
| ------------------------------------------- | ------------------------ |
| AsyncFixer                                  | Async/await patterns     |
| Meziantou.Analyzer                          | General best practices   |
| Microsoft.VisualStudio.Threading.Analyzers  | Threading correctness    |
| Roslynator.Analyzers                        | Code style + quality     |
| Roslynator.CodeAnalysis.Analyzers           | Advanced code analysis   |
| Roslynator.Formatting.Analyzers             | Formatting consistency   |
| SharpSource                                 | Common pitfalls          |
| SonarAnalyzer.CSharp                        | Security + reliability   |

## Testing

Tests use **xUnit v3** with the **Microsoft Testing Platform** (MTP) runner:

```bash
# Run CI tests (default category)
scripts/test.sh

# Run specific test class
scripts/test.sh GreeterTests

# Run all tests including Docker and Manual
scripts/test.sh --all

# JSON output (for scripts/agents)
scripts/test.sh --json
```

Test categories:
- `[Trait("Category", "CI")]` — runs in CI pipeline, must be fast and deterministic
- `[Trait("Category", "Docker")]` — builds and tests the Docker image, requires Docker daemon
- `[Trait("Category", "Manual")]` — requires external dependencies or manual setup

## Docker

```bash
# Build, run, and test (scripts/docker.sh)
scripts/docker.sh build
scripts/docker.sh run
scripts/docker.sh test
scripts/docker.sh stop

# Or use docker-compose directly
docker compose up -d
curl http://localhost:8080/health
# → {"status":"healthy"}
docker compose down
```

## CI/CD

**GitHub Actions** (`.github/workflows/ci.yml`) and **Azure DevOps** (`azure-pipelines.yml`) run:

1. Format check (`dotnet format --verify-no-changes`)
2. Release build
3. CI-category tests only

**NuGet publishing** (`.github/workflows/publish.yml`) — triggers on push to `main` when `src/E128.Analyzers/` changes. Uses OIDC trusted publishing (no API keys). Skips if the version already exists on nuget.org.

## dotnet-overhaul Skill

The [`dotnet-overhaul`](.claude/skills/dotnet-overhaul/) skill is a portable, opinionated .NET code overhaul loop that can be copied into any .NET repo and run with Claude Code. It systematically modernizes language usage, enforces strict code analysis, reviews performance/concurrency/security, and verifies all tests pass.

**Opinionated.** The skill enforces specific conventions (deny-by-default analyzers, immutability, MTP test runner, strict code analysis). Review and edit `conventions.md` after copying to match your project's preferences.

**Iterative.** For initial large overhauls, run the skill multiple times — approve a subset of findings per run, commit, then run again. Once the codebase is clean, run periodically to catch drift from new code, updated analyzers, or TFM upgrades.

### Install

```bash
# From inside your target repo
cp -r /path/to/dotnet-reference/.claude/skills/dotnet-overhaul .claude/skills/
```

The skill includes its own scripts, pattern files, and conventions template. No other files from this repo are required.

### Run

Create a branch first — the skill makes many changes across the codebase:

```bash
git checkout -b refactor/dotnet-overhaul

# Then in Claude Code
/dotnet-overhaul
/dotnet-overhaul src/MyProject
```

### Customize

After copying, optionally edit these files in `.claude/skills/dotnet-overhaul/`:

| File                | Purpose                                                  |
| ------------------- | -------------------------------------------------------- |
| `conventions.md`    | Project-specific coding standards, analyzer overrides, auto-approved fixes, test relaxations |
| `lessons/*.md`      | Known false positives and compiler edge cases discovered during overhaul runs |

The skill auto-detects the test framework, solution format, and analyzer configuration. External agents (`build-validator`, `sme-researcher`, `tdd-loop-optimizer`) are optional enhancements — the skill works without them.

## Contributing

Issues and pull requests are welcome. For large changes, open an issue first to discuss the approach.

## License

[MIT](./LICENSE)
