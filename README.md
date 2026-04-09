# E128.Reference

## Summary

A .NET 10 reference repository demonstrating modern conventions for web, CLI, and Docker applications.
Features strict deny-by-default code analysis with third-party Roslyn analyzers, xUnit v3 on the Microsoft Testing Platform, and Central Package Management with transitive pinning.
Includes a complete Claude Code development harness with bash scripts, contextual rules, skills, and agents.

## What's Included

| Component              | Description                                                          |
| ---------------------- | -------------------------------------------------------------------- |
| **E128.Reference.Web** | Minimal API web app with Kestrel, health endpoint                    |
| **E128.Reference.Cli** | System.CommandLine CLI with `--name` option                          |
| **E128.Reference.Core**| Shared library (Greeter service)                                     |
| **E128.Reference.Tests**| xUnit v3 + MTP with CI, Docker, and Manual test categories          |
| **Docker**             | Multi-stage Dockerfile + docker-compose.yml                          |
| **Bash scripts**       | Build, test, format, CI, lode management (`scripts/help.sh`)        |
| **Claude Code harness**| CLAUDE.md, rules, hooks, skills, agents (see `.claude/`)            |
| **CI/CD**              | GitHub Actions + Azure DevOps pipeline YAML                          |
| **Lode**               | Structured documentation (practices, coding standards, terminology)  |

## Prerequisites

### Required

| Tool         | Version | macOS                        | Ubuntu/Debian                    | Windows                          |
| ------------ | ------- | ---------------------------- | -------------------------------- | -------------------------------- |
| .NET SDK     | 10.0+   | `brew install dotnet`        | [Microsoft docs][dotnet-install] | `winget install Microsoft.DotNet.SDK.10` |
| bash         | 5.0+    | `brew install bash`          | Included (5.1+)                  | WSL2 recommended                 |
| ripgrep (rg) | 14+     | `brew install ripgrep`       | `sudo apt install ripgrep`       | `winget install BurntSushi.ripgrep.MSVC` |
| fd           | 9+      | `brew install fd`            | `sudo apt install fd-find`       | `winget install sharkdp.fd`      |
| jq           | 1.7+    | `brew install jq`            | `sudo apt install jq`            | `winget install jqlang.jq`       |
| Docker       | 24+     | Docker Desktop               | `sudo apt install docker.io`     | Docker Desktop                   |

[dotnet-install]: https://learn.microsoft.com/en-us/dotnet/core/install/linux

### Optional

| Tool               | Purpose                  | Install                                        |
| ------------------ | ------------------------ | ---------------------------------------------- |
| shellcheck         | Bash script linter       | `brew install shellcheck`                      |
| dotnet-outdated    | NuGet update checker     | `dotnet tool install -g dotnet-outdated-tool`  |

> **Note (macOS):** The default `/bin/bash` on macOS is 3.2 (2007). Install bash 5+ via Homebrew and ensure `/opt/homebrew/bin/bash` is in your `$PATH` before `/bin/bash`.

> **Note (Ubuntu):** The `fd` package is named `fd-find`. The binary is `fdfind`. Create an alias: `ln -s $(which fdfind) ~/.local/bin/fd`.

## Quick Start

```bash
# Build
scripts/build.sh

# Test (CI category only)
scripts/test.sh --all

# Full CI pipeline (format + build + test)
scripts/ci.sh

# Docker
docker build -t e128-reference-web .
docker compose up -d
curl http://localhost:8080/health
```

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
├── scripts/                  # Bash development scripts
│   ├── lib.sh                # Shared functions (sourced by all scripts)
│   ├── build.sh              # dotnet build wrapper
│   ├── test.sh               # xUnit v3 MTP runner wrapper
│   ├── format.sh             # dotnet format wrapper
│   ├── check.sh              # Composed format+build+test
│   ├── ci.sh                 # Full CI pipeline
│   ├── help.sh               # Script catalog
│   └── internal/             # Scripts for skills/agents only
├── src/
│   ├── E128.Reference.Core/  # Shared library
│   ├── E128.Reference.Web/   # ASP.NET Core minimal API
│   └── E128.Reference.Cli/   # System.CommandLine CLI
└── tests/
    └── E128.Reference.Tests/ # xUnit v3 + MTP
```

## Script Catalog

Run `scripts/help.sh` for the full list. Key scripts:

| Script             | Purpose                                              |
| ------------------ | ---------------------------------------------------- |
| `build.sh`         | Build solution or project (`--json`, `--project`)    |
| `test.sh`          | Run tests (`--all`, `--json`, class name targeting)  |
| `format.sh`        | Format check/apply (`--check`, `--changed`)          |
| `check.sh`         | Composed: format + build + test (`--all`)            |
| `ci.sh`            | Full CI pipeline (`--skip-*` flags)                  |
| `status.sh`        | Git status (`--json`, `--classify`)                  |
| `diff.sh`          | Diff summary (`--json`, `--files`)                   |
| `branch.sh`        | Branch info vs base (`--json`, `--human`)            |
| `assert.sh`        | Pre-commit gates (`--clean-working-tree`, etc.)      |
| `ts.sh`            | ISO 8601 timestamp (optionally updates file)         |
| `help.sh`          | List all scripts with descriptions                   |
| `task.sh`          | Plan task management (check/next/progress)           |
| `plan-context.sh`  | Active plans and roadmap query                       |

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
# Run all CI tests
scripts/test.sh --all

# Run specific test class
scripts/test.sh GreeterTests

# JSON output (for scripts/agents)
scripts/test.sh --all --json
```

Test categories:
- `[Trait("Category", "CI")]` — runs in CI pipeline, must be fast and deterministic
- `[Trait("Category", "Docker")]` — builds and tests the Docker image, requires Docker daemon
- `[Trait("Category", "Manual")]` — requires external dependencies or manual setup

## Docker

```bash
# Build the image
docker build -t e128-reference-web .

# Run with docker-compose
docker compose up -d

# Health check
curl http://localhost:8080/health
# → {"status":"healthy"}

# Stop
docker compose down
```

## CI/CD

Both **GitHub Actions** (`.github/workflows/ci.yml`) and **Azure DevOps** (`azure-pipelines.yml`) pipelines are included. Both run:

1. Format check (`dotnet format --verify-no-changes`)
2. Release build
3. CI-category tests only

## License

Private repository. All rights reserved.
