# Codebase Map — E128.Reference

> **One sentence:** Nine projects — four production (Core, Web, Cli, Analyzers) and five test — where the analyzer project dominates the line count and the application projects are intentionally tiny.

*Updated: 2026-05-29T19:02:39Z*

---

## Project Inventory

| Project                     | Type        | SDK                     | TFM              | Purpose                                  |
| --------------------------- | ----------- | ----------------------- | ---------------- | ---------------------------------------- |
| `E128.Reference.Core`       | Production  | `Microsoft.NET.Sdk`     | `net10.0`        | Shared domain (Greeter, models, services, repos) |
| `E128.Reference.Web`        | Production  | `Microsoft.NET.Sdk.Web` | `net10.0`        | Minimal-API web service                  |
| `E128.Reference.Cli`        | Production  | `Microsoft.NET.Sdk`     | `net10.0`        | System.CommandLine console app           |
| `E128.Analyzers`            | Production  | `Microsoft.NET.Sdk`     | `netstandard2.0` | Roslyn analyzer + code-fix NuGet package |
| `E128.Reference.Core.Tests` | Test        | `Microsoft.NET.Sdk`     | `net10.0`        | Core unit tests                          |
| `E128.Reference.Cli.Tests`  | Test        | `Microsoft.NET.Sdk`     | `net10.0`        | CLI unit tests                           |
| `E128.Reference.Tests`      | Test        | `Microsoft.NET.Sdk`     | `net10.0`        | Web integration tests                    |
| `Architecture.Tests`        | Test        | `Microsoft.NET.Sdk`     | `net10.0`        | ArchUnitNET structural tests             |
| `E128.Analyzers.Tests`      | Test        | `Microsoft.NET.Sdk`     | `net10.0`        | Analyzer + code-fix verification         |

> Live counts: `scripts/codebase-stats.sh --json` (files/LOC per project) and `scripts/analyzer-stats.sh --json` (rule/fix/API counts). As of 2026-05-29 the analyzer project is by far the largest (~24k LOC across ~171 files); the application projects total a few hundred LOC.

## Dependency Graph

```mermaid
graph TB
  core["E128.Reference.Core"]
  web["E128.Reference.Web"] --> core
  cli["E128.Reference.Cli"] --> core
  ana["E128.Analyzers"]

  coret["E128.Reference.Core.Tests"] --> core
  clit["E128.Reference.Cli.Tests"] --> cli
  webt["E128.Reference.Tests"] --> web
  archt["Architecture.Tests"] --> core
  archt --> web
  archt --> cli
  anat["E128.Analyzers.Tests"] --> ana

  ana -. "build-time analyzer (all non-Roslyn projects)" .-> core
  ana -. .-> web
  ana -. .-> cli
```

Production reference dependencies are deliberately shallow: only Web and Cli depend on Core; the analyzer stands alone and is consumed by everything *as a build-time analyzer*, not as a runtime reference.

## Directory Structure

```
src/
├── E128.Reference.Core/
│   ├── Greeter.cs
│   ├── Models/            # GreetingRequest, Greeting, ...
│   ├── Repositories/      # IGreetingRepository, InMemoryGreetingRepository
│   └── Services/          # IGreetingService, GreetingService
├── E128.Reference.Web/
│   └── Program.cs         # composition root + endpoint maps
├── E128.Reference.Cli/
│   ├── Program.cs         # top-level entry → CliApp.CreateRootCommand
│   └── CliApp.cs
└── E128.Analyzers/
    ├── Design/  Reliability/  Performance/  Security/  Style/  Testing/  FileSystem/
    ├── PublicAPI.Shipped.txt / Unshipped.txt
    ├── AnalyzerReleases.Shipped.md / Unshipped.md
    └── README.md          # rule catalog (NuGet package page)
tests/
├── E128.Reference.Core.Tests/  Cli.Tests/  Tests(web)/
├── Architecture.Tests/         # ArchUnitNET
└── E128.Analyzers.Tests/       # one test class per rule
```

## Shared Libraries

| Project               | Provides                                                                 |
| --------------------- | ------------------------------------------------------------------------ |
| `E128.Reference.Core` | The domain seam shared by Web and Cli: `Greeter`, `IGreetingService`/`GreetingService`, `IGreetingRepository`/`InMemoryGreetingRepository`, and `Models`. No external package dependencies — inherits analyzers from `Directory.Build.props`. |
| `E128.Analyzers`      | The compile-time analyzer/code-fix engine; shared `SequentialRenameFixAllProvider` and `DiskIoCatalog` underpin rename and disk-I/O rules. Published as a NuGet package. |

## Navigation Aids (scripts)

| Need                     | Script                                              |
| ------------------------ | --------------------------------------------------- |
| Find a class/method      | `scripts/find.sh --class\|--method NAME`            |
| Find callers             | `scripts/find.sh --callers MethodName`              |
| File outline             | `scripts/file-outline.sh PATH`                      |
| Extract one method/class | `scripts/code-read.sh --method\|--class NAME PATH`  |
| Type dependencies        | `scripts/deps.sh TYPE [--callers]`                  |
