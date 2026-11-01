# Analyzer Fix Infrastructure Plan

*Updated: 2026-10-31T22:20:00Z*

## Target

Adopt the fix-provider techniques recorded in
`lode/dotnet/analyzers/code-fix-techniques.md` and the package layout recorded in
`lode/dotnet/analyzers/package-layout.md`.

## Findings

| #  | Severity | Location                                     | Finding                                                        | Decision |
| -- | -------- | -------------------------------------------- | -------------------------------------------------------------- | -------- |
| 1  | High     | `src/E128.Analyzers/Design/SealedByDefaultAnalyzer.cs` | A generic constraint type was reported, and sealing it is CS0701 | Fixed    |
| 2  | High     | `src/E128.Analyzers/Design/AsyncDisposableWithoutDisposableCodeFixProvider.cs` | A second `Dispose()` was inserted, which is CS0111 | Fixed    |
| 3  | High     | `src/E128.Analyzers/Testing/TempDirCleanupCodeFixProvider.cs` | `InitializeAsync` and `DisposeAsync` were inserted unconditionally | Fixed |
| 4  | High     | `src/E128.Analyzers/E128.Analyzers.csproj`   | The analyzer assembly carries a Workspaces reference and suppresses RS1038 | Deferred |
| 5  | Medium   | `src/E128.Analyzers/**/*CodeFixProvider.cs`  | Almost every provider builds roots by hand instead of `DocumentEditor` | Deferred |
| 6  | Medium   | `src/E128.Analyzers/**/*CodeFixProvider.cs`  | No provider uses `SyntaxGenerator` or the `Simplifier` annotation | Deferred |
| 7  | Medium   | `src/E128.Analyzers/E128.Analyzers.csproj`   | One assembly targets one Roslyn version, so an older host cannot load it | Deferred |
| 8  | Medium   | `Directory.Build.targets`                    | No banned-symbol file, so the shared helpers are optional | Deferred |
| 9  | Low      | `global.json`                                | `rollForward: latestMajor` lets a new SDK change analyzer output | Deferred |
| 10 | Low      | `tests/`                                     | No test cap on parallel compilation, and each test compiles a project | Deferred |

## Non-Goals

- Copy another suite's fix providers. This repository has its own formatter
  settings and its own using-directive rules.
- Add rules from another catalog. A rule enters this repository through
  `lode/dotnet/analyzer-candidates.md`.

## Related

- [Code Fix Techniques](../../lode/dotnet/analyzers/code-fix-techniques.md)
- [Code Fix Output](../../lode/dotnet/analyzers/code-fix-output.md)
- [Package Layout](../../lode/dotnet/analyzers/package-layout.md)
