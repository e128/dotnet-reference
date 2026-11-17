# .NET 11 Project Structure
*Updated: 2026-11-17T13:33:06Z*

## Solution Format

The .NET SDK defaults to `.slnx` (XML-based) when running `dotnet new sln`. The format is GA, supported by Visual Studio 2022+, Rider, and the .NET CLI. Benefits: fewer merge conflicts (no GUIDs), human-readable XML, aligns with `.csproj` format.

Migration: `dotnet sln migrate` converts `.sln` to `.slnx`.

## Directory Layout

```
├── Directory.Build.props      # Shared MSBuild PROPERTIES (TFM, analyzers, code analysis)
├── Directory.Build.targets    # Conditional TARGETS (runtime async, test project config, analyzer reference)
├── Directory.Packages.props   # Central Package Management version pins
├── global.json                # SDK version + MTP test runner
├── nuget.config               # Package sources, trusted signers, source mapping
├── .editorconfig              # Code style (formatting, naming, IDE rules)
├── .globalconfig              # Analyzer diagnostic severities
├── src/
│   ├── E128.Analyzers/        # Solution-local Roslyn analyzer (own .editorconfig)
│   ├── E128.Analyzers.CodeFixers/  # Code fix providers for the analyzer (own .editorconfig)
│   ├── E128.Reference.Cli/    # CLI application
│   ├── E128.Reference.Core/   # Core library
│   └── E128.Reference.Web/    # Web application
├── benchmarks/
│   ├── .globalconfig               # Benchmark severity override (global_level=101)
│   └── E128.Benchmarks/            # BenchmarkDotNet starter benchmarks (see [Benchmarks](benchmarks.md))
├── tests/
│   ├── Architecture.Tests/         # ArchUnitNET structural tests
│   ├── E128.Analyzers.Tests/       # Roslyn analyzer unit tests (own .globalconfig, global_level=102)
│   ├── E128.Reference.Cli.Tests/   # CLI unit tests
│   ├── E128.Reference.Core.Tests/  # Core library unit tests
│   ├── E128.Reference.Tests/       # Web integration tests
│   ├── Directory.Build.targets     # Imports the root targets, adds the coverage extension to test projects
│   └── .globalconfig               # Test-specific severity overrides (global_level=101)
```

## Central Package Management (CPM)

All package versions declared in `Directory.Packages.props`. Individual `.csproj` files use `<PackageReference Include="..." />` without `Version` attributes.

Key properties:
- `ManagePackageVersionsCentrally=true`, enables CPM
- `CentralPackageTransitivePinningEnabled=true`, prevents transitive version drift
- `CentralPackageVersionOverrideEnabled=false`, prevents per-project overrides

`Directory.Packages.props` owns the two `CentralPackage*` properties inside a setup-owned block. The block opens with a `repository-setup:begin cpm-hardening` comment that carries a SHA-256 hash. Never edit the block by hand. `Directory.Build.props` sets no CPM property.

## Directory.Build.props vs .targets

**Props** (evaluated before project files): TFM, language version, nullable, analyzers, code analysis settings, artifact paths, build performance flags, configuration-specific settings.

**Targets** (evaluated after project files): Conditional logic that depends on properties set in `.csproj`. `<IsTestProject>true</IsTestProject>` detection sets `OutputType=Exe` and the MTP runner properties. The `runtime-async=on` compiler feature applies to `net11.0` projects other than Roslyn component, test, and benchmark projects. `tests/Directory.Build.targets` imports this file and adds the coverage extension to test projects. The file also contains the E128.Analyzers `ProjectReference`: all projects except the two Roslyn components (`IsRoslynComponent != true`) automatically reference it as an `OutputItemType="Analyzer"` with `ReferenceOutputAssembly="false"`. The condition also gates on the `.csproj` existing, so the solution builds cleanly if the analyzer project is removed.

## global.json

```json
{
  "sdk": { "version": "<pinned in global.json>", "rollForward": "latestPatch", "allowPrerelease": false },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

The exact SDK version is the source of truth in `global.json` (`scripts/sdk-version.sh`).

The `test.runner` setting is **required** for `dotnet test`. Without it, MTP projects fail with a VSTest compatibility error.

## NuGet Security

`nuget.config` should include:
- `<clear />` in packageSources (remove implicit sources)
- Explicit `nuget.org` source with `protocolVersion="3"`
- `<trustedSigners>` with certificate fingerprints
- `<packageSourceMapping>` to restrict which packages come from which source

## Related

- [Testing](testing.md)
- [Analyzers](analyzers.md)
- [Dependency Policy](../dependency-policy.md)
