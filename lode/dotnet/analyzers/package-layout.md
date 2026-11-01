# Analyzer Package Layout
*Updated: 2026-11-01T14:05:34Z*

How the analyzer assembly is built, packed, and loaded. See
[Analyzers](../analyzers.md) for the rule strategy.

## Two Assemblies

The package ships two assemblies:

| Project                     | Reference                                  | Content       |
| --------------------------- | ------------------------------------------ | ------------- |
| `E128.Analyzers`            | `Microsoft.CodeAnalysis.CSharp`             | analyzers     |
| `E128.Analyzers.CodeFixers` | `Microsoft.CodeAnalysis.CSharp.Workspaces`  | fix providers |

The compiler host loads the analyzer assembly. RS1038 forbids a Workspaces
reference in that assembly, so the fix providers live in their own project.

The fixer project sets `RootNamespace` to `E128.Analyzers`. Its files keep the
`E128.Analyzers.Design` namespace, so an analyzer and its fix provider read
alike. The analyzer project declares `InternalsVisibleTo` for the fixer
project. That exposes the `DiagnosticId` constants and `InModifierHelper`.

Both DLLs pack to `analyzers/dotnet/cs`. `dotnet pack` builds one project, so
the analyzer project runs a `BuildCodeFixersBeforePack` target first. That
target restores the fixer project in one build and then builds it in a second
build. The second build passes a distinct global property, so MSBuild evaluates
the project again and imports the NuGet props that the restore wrote. Without
that property, MSBuild reuses the evaluation from the restore call and the pack
fails with CS0518 on a clean checkout.

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
- **SDK roll forward**: `global.json` sets `latestPatch`. A new SDK patch keeps
  analyzer output stable, because a feature-band bump needs an edit to the pin.
- **Banned symbols**: no `BannedSymbols.txt` file exists. No shared diagnostic
  helper exists to route a report through, so a banned entry has nothing to
  guard. RS1038 already guards the Workspaces boundary.
- **Test parallelism**: every test compiles a project. `xunit.runner.json` caps
  the analyzer test project at four threads to hold peak memory down.

## Related

- [Analyzers](../analyzers.md): rule strategy and configuration
- [Project Structure](../project-structure.md): solution format and build props
- [Release Tracking](../release-tracking.md): Shipped and Unshipped files
