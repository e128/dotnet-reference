# .NET 10 Roslyn Analyzers
*Updated: 2026-04-09T12:47:56Z*

## Strategy: Deny by Default

All analyzer diagnostics default to `error` severity. Rules are explicitly relaxed only when justified. This prevents new violations from being introduced silently.

```
# .globalconfig
dotnet_analyzer_diagnostic.severity = error
```

## Configuration Split

| File               | Purpose                            | Scope             |
| ------------------ | ---------------------------------- | ----------------- |
| `.globalconfig`    | Analyzer diagnostic severities     | All projects      |
| `.editorconfig`    | Code style, formatting, naming     | All projects      |
| `tests/.globalconfig` | Test-specific overrides         | Test projects only |

The `tests/.globalconfig` uses `global_level = 101` to override the root `global_level = 100`.

## MSBuild Properties

In `Directory.Build.props`:

```xml
<AnalysisLevel>latest-all</AnalysisLevel>
<AnalysisMode>Recommended</AnalysisMode>
<AnalysisModeSecurity>All</AnalysisModeSecurity>
<AnalysisModeReliability>All</AnalysisModeReliability>
<AnalysisModePerformance>All</AnalysisModePerformance>
<EnableNETAnalyzers>true</EnableNETAnalyzers>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

Key interactions:
- `AnalysisLevel=latest-all` enables rules; per-category `AnalysisMode` overrides specifics
- `EnforceCodeStyleInBuild` makes IDE rules (IDE*) fire during `dotnet build`
- `TreatWarningsAsErrors` promotes all enabled warnings to errors

## Third-Party Analyzer Packages

Declared in `Directory.Build.props` with `PrivateAssets="all"` (zero runtime impact). Versions pinned in `Directory.Packages.props`.

| Package                                    | Prefix    | Focus                    |
| ------------------------------------------ | --------- | ------------------------ |
| AsyncFixer                                 | AF*       | Async/await anti-patterns |
| Meziantou.Analyzer                         | MA*       | Code quality, performance, security |
| Microsoft.VisualStudio.Threading.Analyzers | VSTHRD*   | Threading correctness    |
| Roslynator.Analyzers                       | RCS*      | Code style + quality     |
| Roslynator.CodeAnalysis.Analyzers          | RCS*      | Advanced code analysis   |
| Roslynator.Formatting.Analyzers            | RCS*      | Formatting consistency   |
| SharpSource                                | SS*       | Common pitfalls          |
| SonarAnalyzer.CSharp                       | S*        | Security + reliability   |

## Suppression Policy

- Never use `#pragma warning disable` or `[SuppressMessage]` without user approval
- Never use the null-forgiving operator `!` to silence CS8600-CS8604
- Editorconfig severity downgrades require justification
- Test project relaxations go in `tests/.globalconfig`, not inline suppressions

## Common Test Overrides

Rules suppressed in test projects (via `tests/.globalconfig` at `global_level=101`):

| Rule       | Reason                                          |
| ---------- | ----------------------------------------------- |
| CA1515     | xUnit requires public types for discovery       |
| CA1707     | Underscores in test method names                |
| CA1062     | xUnit fixture injection is always non-null      |
| CA2007     | ConfigureAwait not needed in test methods       |
| VSTHRD200  | Test methods don't need Async suffix            |
| MA0040     | Ambient CancellationToken too noisy in tests    |

## CA2007 (ConfigureAwait) Scoping

Library code must use `ConfigureAwait(false)`. App code (ASP.NET, CLI) must not. Scope CA2007 to DLL projects only:

```
dotnet_diagnostic.CA2007.severity = error
dotnet_code_quality.CA2007.output_kind = DynamicallyLinkedLibrary
```

## Source Generators

`[GeneratedRegex]` must use partial **properties** (not partial methods) per MA0190:

```csharp
[GeneratedRegex(@"...", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
private static partial Regex MyRegex { get; }
```

## Related

- [Project Structure](project-structure.md)
- [Testing](testing.md)
- [Practices](../practices.md)
