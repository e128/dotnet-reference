# .NET 10 Project Structure
*Updated: 2026-04-11T14:10:21Z*

## Solution Format

.NET 10 SDK defaults to `.slnx` (XML-based) when running `dotnet new sln`. The format is GA, supported by Visual Studio 2022+, Rider, and the .NET CLI. Benefits: fewer merge conflicts (no GUIDs), human-readable XML, aligns with `.csproj` format.

Migration: `dotnet sln migrate` converts `.sln` to `.slnx`.

## Directory Layout

```
├── Directory.Build.props      # Shared MSBuild PROPERTIES (TFM, analyzers, code analysis)
├── Directory.Build.targets    # Conditional TARGETS (test project config — needs IsTestProject set first)
├── Directory.Packages.props   # Central Package Management version pins
├── global.json                # SDK version + MTP test runner
├── nuget.config               # Package sources, trusted signers, source mapping
├── .editorconfig              # Code style (formatting, naming, IDE rules)
├── .globalconfig              # Analyzer diagnostic severities
├── src/
│   ├── E128.Analyzers/        # Solution-local Roslyn analyzer
│   ├── E128.Reference.Cli/    # CLI application
│   ├── E128.Reference.Core/   # Core library
│   └── E128.Reference.Web/    # Web application
├── tests/
│   ├── Architecture.Tests/    # ArchUnitNET structural tests
│   ├── E128.Analyzers.Tests/  # Roslyn analyzer unit tests
│   ├── E128.Reference.Tests/  # Core + Web integration tests
│   └── .globalconfig          # Test-specific severity overrides (global_level=101)
```

## Central Package Management (CPM)

All package versions declared in `Directory.Packages.props`. Individual `.csproj` files use `<PackageReference Include="..." />` without `Version` attributes.

Key properties in `Directory.Build.props`:
- `ManagePackageVersionsCentrally=true` — enables CPM
- `CentralPackageTransitivePinningEnabled=true` — prevents transitive version drift
- `CentralPackageVersionOverrideEnabled=false` — prevents per-project overrides

## Directory.Build.props vs .targets

**Props** (evaluated before project files): TFM, language version, nullable, analyzers, code analysis settings, artifact paths, build performance flags, configuration-specific settings.

**Targets** (evaluated after project files): Conditional logic that depends on properties set in `.csproj` — primarily `<IsTestProject>true</IsTestProject>` detection for setting `OutputType=Exe` and MTP runner properties. Also contains the E128.Analyzers `ProjectReference` — all projects except the analyzer itself (`IsRoslynComponent != true`) automatically reference it as an `OutputItemType="Analyzer"` with `ReferenceOutputAssembly="false"`. The condition also gates on the `.csproj` existing, so the solution builds cleanly if the analyzer project is removed.

## global.json

```json
{
  "sdk": { "version": "10.0.201", "rollForward": "latestMajor", "allowPrerelease": false },
  "test": { "runner": "Microsoft.Testing.Platform" }
}
```

The `test.runner` setting is **required** for `dotnet test` on .NET 10 SDK. Without it, MTP projects fail with a VSTest compatibility error.

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
