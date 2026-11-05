# Analyzer Package Layout
*Updated: 2026-11-05T15:20:29Z*

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

## NuGet Layout Rule

NuGet defines the analyzer path. `dotnet` is the only allowed framework name,
because Roslyn is the only host that runs an analyzer.

```text
analyzers/{framework_name}{version}/{supported_architecture}/{language}/{name}.dll
```

- Omit the language folder to apply the analyzer to every language.
- Omit the framework folder to apply it to every target.
- A supporting non-DLL file ships through a `.props` or `.targets` file. Put the
  `.props` file in `build/`.
- The pack target names both DLLs with `PackagePath` set to `analyzers/dotnet/cs`.
- An empty `_._` file packs to `lib/netstandard2.0`. The package has no runtime
  assembly, so the placeholder gives nuget.org a framework to list on the
  Supported Frameworks tab. The nuspec still lists no dependency.

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

## Package Metadata

`src/E128.Analyzers/E128.Analyzers.csproj` sets the packaging flags. Each flag
has one job.

| Property                          | Value  | Effect                                              |
| --------------------------------- | ------ | --------------------------------------------------- |
| `IncludeBuildOutput`              | false  | The DLLs pack through `PackagePath`, not through `lib` |
| `DevelopmentDependency`           | true   | The consumer does not get a transitive compile reference |
| `SuppressDependenciesWhenPacking` | true   | The nuspec lists no dependency, so Roslyn does not flow to the consumer |
| `PackageLicenseExpression`        | MIT    | Replaces the deprecated `PackageLicenseUrl`          |
| `PackageReadmeFile`               | README  | Publishes the rule tables to nuget.org              |

`SuppressDependenciesWhenPacking` is the mechanism that keeps
`Microsoft.CodeAnalysis.CSharp` out of the consumer graph. Do not rely on a
`PrivateAssets` entry on that reference instead.

A consumer inside this repository references the analyzer project directly:

```xml
<ProjectReference Include="src/E128.Analyzers/E128.Analyzers.csproj"
                  OutputItemType="Analyzer"
                  ReferenceOutputAssembly="false" />
```

`Directory.Build.targets` adds that item for every project except the analyzer
itself. A consumer outside the repository takes a plain `PackageReference`,
because the DLL sits under `analyzers/dotnet/cs`.

## Analyzer Dependencies

The package carries no third-party assembly. The configuration-key rule reads
appsettings files with `AppSettingsKeyReader`, a small hand-written scanner in
`src/E128.Analyzers/Reliability`. It replaced a `System.Text.Json` reference and
a closure of nine more DLLs. That closure risked a version clash with the
copy that the compiler host loads.

- A `dotnet pack` listing shows the analyzer DLL, the code fixer DLL, the README,
  and the `lib/netstandard2.0/_._` placeholder.
- `SuppressDependenciesWhenPacking` keeps the nuspec free of a dependency list.

## Related

- [Analyzers](../analyzers.md): rule strategy and configuration
- [Project Structure](../project-structure.md): solution format and build props
- [Release Tracking](../release-tracking.md): Shipped and Unshipped files
