# Codebase Map: E128.Reference

> **One sentence:** Eleven projects: five production, one benchmark, and five test projects, where the analyzer projects dominate the line count and the application projects stay deliberately small.

*Updated: 2026-11-11T03:24:51Z*

---

## Project Inventory

| Project                      | Type       | SDK                     | TFM              | Purpose                                                |
| ---------------------------- | ---------- | ----------------------- | ---------------- | ------------------------------------------------------ |
| `E128.Reference.Core`        | Production | `Microsoft.NET.Sdk`     | `net11.0`        | Shared domain: Greeter, models, services, repositories |
| `E128.Reference.Web`         | Production | `Microsoft.NET.Sdk.Web` | `net11.0`        | Minimal API web service                                |
| `E128.Reference.Cli`         | Production | `Microsoft.NET.Sdk`     | `net11.0`        | System.CommandLine console app                         |
| `E128.Analyzers`             | Production | `Microsoft.NET.Sdk`     | `netstandard2.0` | Roslyn analyzer suite and NuGet package                |
| `E128.Analyzers.CodeFixers`  | Production | `Microsoft.NET.Sdk`     | `netstandard2.0` | Code fix providers, packed into the same package       |
| `E128.Benchmarks`            | Benchmark  | `Microsoft.NET.Sdk`     | `net11.0`        | BenchmarkDotNet starter benchmarks                     |
| `E128.Reference.Core.Tests`  | Test       | `Microsoft.NET.Sdk`     | `net11.0`        | Core unit tests                                        |
| `E128.Reference.Cli.Tests`   | Test       | `Microsoft.NET.Sdk`     | `net11.0`        | CLI invocation tests                                   |
| `E128.Reference.Tests`       | Test       | `Microsoft.NET.Sdk`     | `net11.0`        | Web integration and Podman smoke tests                 |
| `Architecture.Tests`         | Test       | `Microsoft.NET.Sdk`     | `net11.0`        | ArchUnitNET structural tests                           |
| `E128.Analyzers.Tests`       | Test       | `Microsoft.NET.Sdk`     | `net11.0`        | Analyzer and code fix tests                            |

Live counts come from `scripts/codebase-stats.sh --json` and `scripts/analyzer-stats.sh --json`. As of 2026-11-11 the analyzer projects hold about 63k lines across 396 files and 214 diagnostic IDs, while the application projects total a few hundred lines.

## Dependency Graph

```mermaid
graph TB
  core["E128.Reference.Core"]
  web["E128.Reference.Web"] --> core
  cli["E128.Reference.Cli"] --> core
  ana["E128.Analyzers"]
  fix["E128.Analyzers.CodeFixers"] --> ana
  bench["E128.Benchmarks"] --> core

  coret["E128.Reference.Core.Tests"] --> core
  clit["E128.Reference.Cli.Tests"] --> cli
  webt["E128.Reference.Tests"] --> web
  archt["Architecture.Tests"] --> core
  archt --> web
  archt --> cli
  anat["E128.Analyzers.Tests"] --> ana
  anat --> fix

  ana -. "build-time analyzer reference<br/>on every non-Roslyn project" .-> core
  ana -. .-> web
  ana -. .-> cli
```

Production references stay shallow on purpose. Only Web and Cli depend on Core. The analyzer stands alone, and every other project consumes it as a build-time analyzer rather than a runtime reference.

## Directory Structure

```text
src/
├── E128.Reference.Core/
│   ├── Greeter.cs
│   ├── Models/            # GreetingRequest, Greeting, GreetingError, GreetingResult
│   ├── Repositories/      # IGreetingRepository, InMemoryGreetingRepository
│   └── Services/          # IGreetingService, GreetingService
├── E128.Reference.Web/
│   ├── Program.cs         # composition root and endpoint maps
│   └── AppJsonContext.cs  # source-generated JSON metadata
├── E128.Reference.Cli/
│   ├── Program.cs         # top-level entry that invokes CliApp
│   └── CliApp.cs          # command, result mapping, exit codes
├── E128.Analyzers/
│   ├── Design/  Reliability/  Performance/  Security/  Unsafe/  Style/  Testing/
│   ├── PublicAPI.Shipped.txt and PublicAPI.Unshipped.txt
│   ├── AnalyzerReleases.Shipped.md and AnalyzerReleases.Unshipped.md
│   └── README.md          # rule catalog, the NuGet package page
└── E128.Analyzers.CodeFixers/
    └── <Category>/        # one fix provider per rule that has a fix
benchmarks/
└── E128.Benchmarks/
tests/
├── E128.Reference.Core.Tests/  E128.Reference.Cli.Tests/  E128.Reference.Tests/
├── Architecture.Tests/         # ArchUnitNET
└── E128.Analyzers.Tests/       # one test class per rule
```

## Shared Libraries

| Project                      | Provides                                                                                                                                                                                                          |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `E128.Reference.Core`        | The domain seam shared by Web and Cli: `Greeter`, `IGreetingService`, `IGreetingRepository`, and the models. It carries no package dependency of its own and inherits the analyzers from `Directory.Build.props`. |
| `E128.Analyzers`             | The compile-time analyzer engine. Shared helpers such as `SequentialRenameFixAllProvider` and `DiskIoCatalog` underpin the rename and disk I/O rules. It ships as a NuGet package.                                |
| `E128.Analyzers.CodeFixers`  | The fix providers. The assembly is separate because a fix needs `Microsoft.CodeAnalysis.Workspaces`, and RS1038 forbids one analyzer assembly from carrying both roles.                                           |

## Navigation Aids

| Need                        | Script                                                 |
| --------------------------- | ------------------------------------------------------ |
| Find a class or method      | `scripts/find.sh --class` or `--method NAME`           |
| Find callers                | `scripts/find.sh --callers MethodName`                 |
| Outline a file              | `scripts/file-outline.sh PATH`                         |
| Extract one method or class | `scripts/code-read.sh --method` or `--class NAME PATH` |
| Type dependencies           | `scripts/deps.sh TYPE --callers`                       |
