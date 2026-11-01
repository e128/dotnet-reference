# Analyzer Package Layout
*Updated: 2026-11-01T01:49:31Z*

How the analyzer assembly is built, packed, and loaded. See
[Analyzers](../analyzers.md) for the rule strategy.

## One Assembly Today

`E128.Analyzers` builds one assembly that holds the analyzers and the code fix
providers. The fix providers need `Microsoft.CodeAnalysis.Workspaces`, so the
project references it and suppresses RS1038.

One assembly has two costs:

- The compiler process loads an assembly that references Workspaces.
- The package ships one DLL under `analyzers/dotnet/cs`, so a host that cannot
  load the Roslyn API it was built against gets no rules at all.

## The Two-Assembly Split

Two assemblies remove the suppression:

| Project                     | Reference                              | Content            |
| --------------------------- | -------------------------------------- | ------------------ |
| `E128.Analyzers`            | `Microsoft.CodeAnalysis.CSharp`         | analyzers          |
| `E128.Analyzers.CodeFixers` | `Microsoft.CodeAnalysis.CSharp.Workspaces` | fix providers   |

Share a table through a `Compile Include` link, so one file serves both sides.
Pack both DLLs under `analyzers/dotnet/cs`. See
[Code Fix Patterns](code-fix-patterns.md) § One Table for Analyzer and Fix.

The split is tracked in `plans/analyzer-fix-infrastructure/`.

## Per-Roslyn-Version Assemblies

A package can also ship one assembly per Roslyn version, at
`analyzers/dotnet/<version>/cs`. The compiler picks the newest folder that it can
load, so an older host falls back to an older build instead of loading nothing.

That layout needs one analyzer project, one fixer project, and one test project
per supported version. Generate the version from the project file name so a new
version costs three files and no edits.

## Build Metadata

- **Target framework**: the analyzer targets `netstandard2.0`. A `net10.0` build
  in the same package loads faster in a modern host and unlocks the modern API
  surface.
- **SDK roll forward**: `global.json` sets `latestMajor`. A `latestPatch` pin
  keeps a new SDK from changing analyzer output silently.
- **Banned symbols**: a `BannedSymbols.txt` turns a project convention into a
  compile error. The useful entries are the direct reporting overloads and the
  direct generated-code configuration call, because both bypass a shared helper.
- **Test parallelism**: every test compiles a project. An unbounded parallel run
  trades wall-clock time for peak memory.

## Related

- [Analyzers](../analyzers.md): rule strategy and configuration
- [Project Structure](../project-structure.md): solution format and build props
- [Release Tracking](../release-tracking.md): Shipped and Unshipped files
