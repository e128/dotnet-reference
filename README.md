# E128.Reference: source repository for the E128.Analyzers NuGet package

[![CI](https://github.com/e128/dotnet-reference/actions/workflows/ci.yml/badge.svg)](https://github.com/e128/dotnet-reference/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/E128.Analyzers?logo=nuget&label=E128.Analyzers)](https://www.nuget.org/packages/E128.Analyzers/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](./LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)

## E128.Analyzers

This repository is the source for [**E128.Analyzers**](https://www.nuget.org/packages/E128.Analyzers/).
The package holds Roslyn analyzers and code fixes that enforce opinionated .NET conventions at compile time.

```bash
dotnet add package E128.Analyzers
```

### Install With a Coding Agent

Paste this prompt into a coding agent in the repository you want to check:

```text
Add the latest stable E128.Analyzers package to this repository. Resolve the version from nuget.org only, and write the exact version number. Never use a floating version.

Add one `<PackageReference Include="E128.Analyzers" PrivateAssets="all" />` to the root `Directory.Build.props`, inside an existing analyzer `ItemGroup` when one exists, in alphabetical position. Create the file if it is missing. Leave every other analyzer where it is.

If this repository uses Central Package Management, add one `PackageVersion` entry to `Directory.Packages.props`. Never duplicate a version entry. Otherwise set `Version` on the reference.

If `nuget.config` defines more than one package source, add a `packageSourceMapping` entry that maps `E128.*` to nuget.org.

In the root `.editorconfig`, under `[*.cs]`, set these categories to `error` and set `e128_max_complexity = linear`:

dotnet_analyzer_diagnostic.category-E128-Design.severity = error
dotnet_analyzer_diagnostic.category-E128-Maintainability.severity = error
dotnet_analyzer_diagnostic.category-E128-Performance.severity = error
dotnet_analyzer_diagnostic.category-E128-Reliability.severity = error
dotnet_analyzer_diagnostic.category-E128-Security.severity = error
dotnet_analyzer_diagnostic.category-E128-Style.severity = error
dotnet_analyzer_diagnostic.category-E128-Testing.severity = error
dotnet_analyzer_diagnostic.category-E128-Unsafe.severity = error
e128_max_complexity = linear

If the root `.editorconfig` does not exist, create it with `root = true`. Preserve the existing settings and every non-E128 analyzer severity.

Do not fix source findings, suppress or downgrade E128 rules, or change other analyzer severities.

Run the Release build of the solution at the repository root. Report the package version, the project count, and the build status. Report the warning and error totals, and the error count per E128 rule ID. Count each diagnostic once when the output repeats it. Leave every finding unfixed unless I approve a fix.
```

`e128_max_complexity` sets the algorithm level. `linear` is the strictest tier and reports linearithmic or worse code. Other tiers: linearithmic, quadratic, cubic, exponential.

- Analyzers: [`src/E128.Analyzers/`](src/E128.Analyzers/). The [README](src/E128.Analyzers/README.md) lists every rule.
- Code fixes: [`src/E128.Analyzers.CodeFixers/`](src/E128.Analyzers.CodeFixers/)
- Packaging: the package ships `E128.Analyzers.dll` and `E128.Analyzers.CodeFixers.dll` under `analyzers/dotnet/cs`.
- Tests: [`tests/E128.Analyzers.Tests/`](tests/E128.Analyzers.Tests/)
- Release notes: [`AnalyzerReleases.Shipped.md`](src/E128.Analyzers/AnalyzerReleases.Shipped.md)
- Publishing: `.github/workflows/publish.yml` pushes the package to nuget.org on each merge to `main` that changes `src/E128.Analyzers/` or `src/E128.Analyzers.CodeFixers/`

Report an analyzer defect, or ask for a new rule, in the [issues](https://github.com/e128/dotnet-reference/issues) of this repository.

## Summary

The rest of the repository is a .NET 10 reference application that runs the analyzers on its own code.
It shows conventions for web, CLI, and container applications.
The build uses deny-by-default code analysis with third-party Roslyn analyzers.
Tests run on xUnit v3 with the Microsoft Testing Platform. ArchUnitNET tests check structural invariants.
Central Package Management pins transitive versions, and Renovate updates dependencies.
GitHub Actions publishes the package with OIDC trusted publishing.

The repository also includes a Claude Code development harness with bash scripts, rules, skills, and agents.
It uses the [Lode Coding Toolkit][lode-toolkit] for structured project documentation.

## Quick Start

```bash
# Build
scripts/build.sh

# Test (default: the CI category only)
scripts/test.sh

# Full CI pipeline (shell lint, format, build, and test)
scripts/ci.sh

# Podman
scripts/podman.sh build
scripts/podman.sh test
```

## What Is Included

| Component                     | Description                                                                    |
| ----------------------------- | ------------------------------------------------------------------------------ |
| **E128.Reference.Web**        | Minimal API web app with Kestrel and a health endpoint                         |
| **E128.Reference.Cli**        | System.CommandLine CLI with a `--name` option                                  |
| **E128.Reference.Core**       | Shared library (Greeter service, models, repositories, services)               |
| **E128.Analyzers**            | Roslyn analyzers (E128001 to E128218), published to nuget.org                  |
| **E128.Analyzers.CodeFixers** | Code fix providers for the analyzer rules, shipped in the same package         |
| **E128.Reference.Tests**      | xUnit v3 and MTP with the CI, Podman, and Manual test categories               |
| **Architecture.Tests**        | ArchUnitNET structural invariant tests (layers, naming, sealed)                |
| **E128.Analyzers.Tests**      | Analyzer and code fix unit tests                                               |
| **E128.Reference.Cli.Tests**  | Unit tests for the CLI app                                                     |
| **E128.Reference.Core.Tests** | Unit tests for the Greeter, GreetingService, and repositories                  |
| **E128.Benchmarks**           | BenchmarkDotNet starter benchmarks for Core, the Web endpoints, and analyzers  |
| **Podman**                    | Hardened Resolute multi-stage Dockerfile and compose.yaml                      |
| **Bash scripts**              | Build, test, format, CI, Podman, and lode tasks ([catalog](scripts/README.md)) |
| **Claude Code harness**       | CLAUDE.md, rules, hooks, skills, and agents (see `.claude/`)                   |
| **CI/CD**                     | GitHub Actions CI and NuGet trusted publishing                                 |
| **Renovate**                  | Automatic dependency updates with grouped PRs and a security bypass            |
| **Lode**                      | Structured documentation with the [Lode Coding Toolkit][lode-toolkit]          |

## Prerequisites

### Required

| Tool         | Version | macOS                        | Ubuntu/Debian                    | Windows                                  |
| ------------ | ------- | ---------------------------- | -------------------------------- | ---------------------------------------- |
| .NET SDK     | 10.0+   | `brew install dotnet`        | [Microsoft docs][dotnet-install] | `winget install Microsoft.DotNet.SDK.10` |
| bash         | 4.0+    | `brew install bash`          | Included (5.1+)                  | WSL2 recommended                         |
| shellcheck   | 0.11.0+ | `brew install shellcheck`    | `sudo apt install shellcheck`    | WSL2 recommended                         |
| ripgrep (rg) | 14+     | `brew install ripgrep`       | `sudo apt install ripgrep`       | `winget install BurntSushi.ripgrep.MSVC` |
| fd           | 9+      | `brew install fd`            | `sudo apt install fd-find`       | `winget install sharkdp.fd`              |
| jq           | 1.7+    | `brew install jq`            | `sudo apt install jq`            | `winget install jqlang.jq`               |
| Podman       | 4+      | `brew install podman`        | `sudo apt install podman`        | `winget install RedHat.Podman`           |

[dotnet-install]: https://learn.microsoft.com/en-us/dotnet/core/install/linux
[lode-toolkit]: https://fjzeit.github.io/lode

### Optional

| Tool               | Purpose                             | Install                                                  |
| ------------------ | ----------------------------------- | -------------------------------------------------------- |
| dotnet-outdated    | NuGet update checker                | `dotnet tool install -g dotnet-outdated-tool`            |
| jb (ReSharper CLI) | Semantic code cleanup (`format.sh`) | `dotnet tool install -g JetBrains.ReSharper.GlobalTools` |

> **Note (jb):** `scripts/format.sh` runs `jb cleanupcode` before `dotnet format` when `jb` is on `$PATH`, and skips that step when it is absent. `--check` always skips `jb` because `jb` has no verify-only mode.

> **Note (macOS):** The default `/bin/bash` on macOS is version 3.2. Install bash 5 or later with Homebrew and put `/opt/homebrew/bin/bash` before `/bin/bash` in `$PATH`.

> **Note (Ubuntu):** The `fd` package is named `fd-find` and its binary is `fdfind`. Create an alias: `ln -s $(which fdfind) ~/.local/bin/fd`.

> **Note (macOS and Podman):** Podman needs a Linux VM on macOS and does not bundle one. After install, run `podman machine init && podman machine start`.

## Project Structure

```
.
├── .claude/                  # Claude Code harness
│   ├── agents/               # Agent definitions
│   ├── hooks/                # Session and guardrail hooks
│   ├── rules/                # Contextual rule files
│   ├── settings.json         # Permissions and hook config
│   ├── skills/               # Skill directories
│   └── tmp/                  # Session artifacts (gitignored)
├── .editorconfig             # Code style (120 char, 4-space, file-scoped ns)
├── .globalconfig             # Analyzer severities (deny-by-default)
├── .github/workflows/ci.yml  # GitHub Actions CI
├── .github/workflows/publish.yml # NuGet trusted publishing
├── .gitleaks.toml            # Secret scan config
├── .mcp.json                 # Claude Code MCP server config (Roslyn Navigator)
├── .opencode/                # opencode mirror of the Claude Code harness
│   ├── agent-assets/         # Generated agent asset copies
│   ├── agents/               # Generated agent definitions
│   └── plugins/              # opencode plugins (guardrail parity with .claude/hooks)
├── AGENTS.md                 # Cross-harness AI instructions
├── CLAUDE.md                 # Always-loaded AI instructions
├── Directory.Build.props     # Shared build properties
├── Directory.Build.targets   # Conditional targets (test project config)
├── Directory.Packages.props  # Central package versions
├── Dockerfile                # Multi-stage web app image
├── compose.yaml              # Container orchestration
├── E128.Reference.slnx       # Solution file
├── global.json               # SDK version and MTP test runner config
├── benchmarks/
│   └── E128.Benchmarks/      # BenchmarkDotNet starter benchmarks
├── docs/                     # Architecture and guide documents
├── dotnet-tools.json         # Repository-local .NET tool manifest
├── lode/                     # Project knowledge documentation
├── nuget.config              # Single source and source mapping
├── opencode.json             # opencode v2 config: model, instructions, MCP, permissions, policies
├── plans/                    # Structured planning documents
├── prompts/                  # System prompt for Lode sessions
├── renovate.json             # Renovate dependency update config
├── scripts/                  # Bash development scripts ([catalog](scripts/README.md))
├── src/
│   ├── E128.Analyzers/       # Roslyn analyzers (published NuGet package)
│   ├── E128.Analyzers.CodeFixers/ # Code fix providers (second DLL in the package)
│   ├── E128.Reference.Core/  # Shared library
│   ├── E128.Reference.Web/   # ASP.NET Core minimal API
│   └── E128.Reference.Cli/   # System.CommandLine CLI
└── tests/
    ├── Architecture.Tests/       # ArchUnitNET structural invariants
    ├── E128.Analyzers.Tests/     # Analyzer unit tests
    ├── E128.Reference.Cli.Tests/ # CLI unit tests
    ├── E128.Reference.Core.Tests/# Core library unit tests
    └── E128.Reference.Tests/     # Web integration tests (xUnit v3 and MTP)
```

## Analyzer Configuration

The repository uses a **deny-by-default** analyzer strategy:

- `dotnet_analyzer_diagnostic.severity = error` makes each diagnostic an error unless a rule overrides it
- Third-party analyzer packages (see [`Directory.Packages.props`](./Directory.Packages.props)) add more than 1000 rules
- `.globalconfig` sets every off-by-default rule to `error`, and its `Not Enabled` section lists each rule left below `error` with a reason
- Test projects use a separate `tests/.globalconfig` at `global_level = 101` for test overrides

### Analyzer Packages

| Package                                     | Focus                    |
| ------------------------------------------- | ------------------------ |
| AsyncFixer                                  | Async and await patterns |
| IDisposableAnalyzers                        | Resource ownership       |
| Meziantou.Analyzer                          | General best practices   |
| Microsoft.VisualStudio.Threading.Analyzers  | Threading correctness    |
| Roslynator.Analyzers                        | Code style and quality   |
| Roslynator.CodeAnalysis.Analyzers           | Advanced code analysis   |
| Roslynator.Formatting.Analyzers             | Formatting consistency   |
| SharpSource                                 | Common pitfalls          |
| SonarAnalyzer.CSharp                        | Security and reliability |

## Testing

Tests run on **xUnit v3** with the **Microsoft Testing Platform** (MTP) runner:

```bash
# Run CI tests (default category)
scripts/test.sh

# Run one test class (bare or fully qualified name)
scripts/test.sh E128.Reference.Core.Tests.GreeterTests

# Run all tests, including Podman and Manual
scripts/test.sh --all

# Verbose output (human-readable)
scripts/test.sh --verbose
```

Test categories:

- `[Trait("Category", "CI")]`: runs in the CI pipeline. Each test must be fast and deterministic.
- `[Trait("Category", "Podman")]`: builds and tests the container image. Podman must be installed.
- `[Trait("Category", "Manual")]`: needs an external dependency or a manual setup step.

## Podman

```bash
# Build, run, and test (scripts/podman.sh)
scripts/podman.sh build
scripts/podman.sh run
scripts/podman.sh test
scripts/podman.sh stop

# Or use podman compose directly
podman compose up -d
curl http://localhost:8080/health
# → Healthy
podman compose down
```

## CI/CD

**GitHub Actions** (`.github/workflows/ci.yml`) runs four jobs:

1. **Format + Build + Test**: format check (`dotnet format --verify-no-changes`), Release and Debug builds, CI-category tests with coverage, and an 80% line and assembly coverage gate
2. **Shell Lint**: `scripts/lint-shell.sh` runs shellcheck over every tracked shell script
3. **Agent Mirror Check**: checks that the opencode agent mirror is current, then verifies every guardrail hook referenced by the plugin exists
4. **Secret Scan**: gitleaks scan over the full git history

**NuGet publishing** (`.github/workflows/publish.yml`) starts on a push to `main` that changes `src/E128.Analyzers/` or `src/E128.Analyzers.CodeFixers/`. The workflow uses OIDC trusted publishing, so it needs no API key. It skips the push when the version already exists on nuget.org.

## dotnet-overhaul Skill

The [`dotnet-overhaul`](.claude/skills/dotnet-overhaul/) skill is a portable .NET overhaul loop for Claude Code.
It modernizes language usage and enforces strict code analysis.
It also reviews performance, concurrency, and security, and it verifies that all tests pass.
Copy it into any .NET repository and run it there.

**Opinionated.** The skill enforces fixed conventions: deny-by-default analyzers, immutability, the MTP test runner, and strict analysis. Edit `conventions.md` after you copy the skill to match your project.

**Iterative.** For a first large overhaul, run the skill several times. Approve a subset of findings per run, commit, then run again. After the codebase is clean, run the skill periodically to catch drift from new code, updated analyzers, or a TFM upgrade.

### Install

```bash
# Run this from inside your target repository
cp -r /path/to/dotnet-reference/.claude/skills/dotnet-overhaul .claude/skills/
```

The skill includes its own scripts, pattern files, and conventions template. It needs no other file from this repository.

### Run

Create a branch first, because the skill changes many files across the codebase:

```bash
git checkout -b refactor/dotnet-overhaul

# Then in Claude Code
/dotnet-overhaul
/dotnet-overhaul src/MyProject
```

### Customize

After you copy the skill, you can edit these files in `.claude/skills/dotnet-overhaul/`:

| File             | Purpose                                                                                     |
| ---------------- | ------------------------------------------------------------------------------------------- |
| `conventions.md` | Project coding standards, analyzer overrides, auto-approved fixes, and test relaxations     |
| `lessons/*.md`   | Known false positives and compiler edge cases found during overhaul runs                    |

The skill detects the test framework, the solution format, and the analyzer configuration.
The external agents (`build-validator`, `sme-researcher`, `tdd-loop-optimizer`) are optional, so the skill works without them.

## Contributing

Issues and pull requests are welcome. For a large change, open an issue first to discuss the approach.

## License

[MIT](./LICENSE)
