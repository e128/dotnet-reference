# Analyzer Package Layout
*Updated: 2026-11-18T16:45:38Z*

How the analyzer assembly is built, packed, and loaded. See
[Analyzers](../analyzers.md) for the rule strategy.

## Three Assemblies

The package ships three assemblies:

- `E128.Analyzers` holds the base analyzers and compiles against Roslyn 5.9.
- `E128.Analyzers.CodeFixers` holds the fix providers and compiles against
  Workspaces 5.9.
- `E128.Analyzers.Union` holds E128220 and E128221 and compiles against
  Roslyn 5.12.

The compiler host loads the analyzer assembly. RS1038 forbids a Workspaces
reference in that assembly, so the fix providers live in their own project.

The union rules read the typed `UnionDeclarationSyntax` node, which needs
Roslyn 5.12. Every other rule compiles against 5.9. The split keeps the base
rules loadable on a .NET SDK 10.0 host.

The fixer and union projects set `RootNamespace` to `E128.Analyzers`. Their
files keep the `E128.Analyzers.Design` namespace, so an analyzer and its fix
provider read alike. The base project declares `InternalsVisibleTo` for both
projects. That exposes the `DiagnosticId` constants, `InModifierHelper`,
`TypeSymbols`, and `ClosedHierarchy`.

The union fix provider does not reference the analyzer type. It names E128220
as a literal id, because the fixer assembly compiles at 5.9 and the analyzer
needs 5.12. Roslyn matches a fixer to a diagnostic by id.

`dotnet pack` builds one project, so the base analyzer project runs a
`BuildComponentsBeforePack` target first. That target restores and builds the
fixer project and the union project. Each build passes a distinct global
property, so MSBuild evaluates the project again and imports the NuGet props
that the restore wrote. Without that property, MSBuild reuses the evaluation
from the restore call and the pack fails with CS0518 on a clean checkout.

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
- The pack target places the base and fixer DLLs in both Roslyn folders, and it
  places the union DLL in the 5.12 folder.
- An empty `_._` file packs to `lib/netstandard2.0`. The package has no runtime
  assembly, so the placeholder gives nuget.org a framework to list on the
  Supported Frameworks tab. The nuspec still lists no dependency.

## Per-Roslyn-Version Folders

The package ships one folder per Roslyn band:

```text
analyzers/dotnet/roslyn5.9/cs    base analyzers and fix providers
analyzers/dotnet/roslyn5.12/cs   base analyzers, fix providers, and union rules
```

The SDK computes `CompilerApiVersion` from its Roslyn tasks assembly and picks
exactly one folder per package: the highest versioned folder at or below that
band. A host above 5.12 picks the 5.12 folder.

Two rules follow from that selection:

1. The base DLL must sit in every folder, because the host loads one folder
   only.
2. The package must not use the unversioned `analyzers/dotnet/cs` folder
   beside a versioned folder. The unversioned folder wins, and the versioned
   assembly never loads.

Verified on 2026-11-18 with the .NET SDK 10.0.404 and 11.0.100 hosts:
`roslyn5.9` plus `roslyn5.12` selects 5.9 on the first host and 5.12 on the
second. A folder above the host band, such as `roslyn5.13` on a 5.12 host,
selects nothing.

## Build Metadata

- **Target framework**: every analyzer assembly targets `netstandard2.0`, and
  the package carries one build per Roslyn band. The base build references
  `Microsoft.CodeAnalysis` 5.9.0.0, so a Roslyn 5.9 host loads it. The union
  build references 5.12.0.0, so only a 5.12 host loads it. Roslyn loads an
  assembly built on an older band into a host on that band or above. It never
  loads one into an older host. That host reports CS9057 and loads no rule from
  the assembly.
- **SDK roll forward**: `global.json` sets `latestPatch`. A new SDK patch keeps
  analyzer output stable, because a feature-band bump needs an edit to the pin.
- **Banned symbols**: no `BannedSymbols.txt` file exists. No shared diagnostic
  helper exists to route a report through, so a banned entry has nothing to
  guard. RS1038 already guards the Workspaces boundary.
- **Test parallelism**: every test compiles a project. `xunit.runner.json` caps
  the analyzer test project at four threads to hold peak memory down.

## Compiler Host Guard

`build/E128.Analyzers.targets` checks the compiler host before it runs. The
package packs that file to `build`, so every consumer imports it.

The target `E128CheckCompilerVersion` runs before `CoreCompile`. It reads the
version of `Microsoft.CodeAnalysis.dll` beside the compiler. It fails with
`E128PKG0001` when the version is below `5.9`, and the message names the host
version and the fix.

The target tests `$(RoslynTargetsPath)/bincore` first and then
`$(RoslynTargetsPath)`. Those paths cover the dotnet SDK and Visual Studio
MSBuild. The target skips the check when neither file exists, so it never
blocks a host it cannot read.

The version in the targets file must equal the base
`Microsoft.CodeAnalysis.CSharp` pin in `Directory.Packages.props`. A Roslyn
bump changes both.

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
because the DLL sits under a Roslyn-versioned analyzer folder.

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
