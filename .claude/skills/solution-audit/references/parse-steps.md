# Solution Audit — Phase 1 Parse Steps

Detailed parsing instructions for each config source. The orchestrator reads these
files once and builds the structured project table passed to all three agents.

## 1.2 Read global.json

Extract SDK version and test runner:
```
sdk_version: "10.0.201"
target_framework: "net10.0"  (inferred from SDK major)
test_runner: "mtp" | null
```

If `global.json` missing: infer TFM from `Directory.Build.props`.

## 1.3 Read the solution file

Extract all `<Project>` entries and `<Folder>` groupings. Build:
```
folder_map = {
  "/src/": ["ProjectA", "ProjectB.Web", ...],
  "/tests/": ["ProjectA.Tests", ...],
}
```

## 1.4 Read all .csproj files

For each project in the solution, extract:
- `AssemblyName` (explicit or inherited from project folder name)
- `RootNamespace` (explicit or inherited)
- `TargetFramework` / `TargetFrameworks` (explicit or inherited)
- `OutputType` (Exe, Library, or default)
- `Sdk` attribute (e.g., `Microsoft.NET.Sdk.Web`)
- `PublishAot` flag
- `<ProjectReference>` list (resolved to project names)
- `<InternalsVisibleTo>` declarations
- `<PackageReference>` list (names + whether `Version=` is present)
- `<IsPackable>` flag

## 1.5 Read Directory.Build.props

Extract defaults: TFM, OutputType, analyzer packages, analysis settings,
NuGet audit properties, build performance settings.

## 1.6 Read Directory.Packages.props

Extract all `<PackageVersion>` entries. Note transitive pin boundary if marked.

## 1.7 Read nuget.config

Extract: `<clear />` presence, source URLs, HTTPS status, protocol version,
`<packageSourceMapping>` presence, `<trustedSigners>` presence.

## 1.8 Read config files

- `.globalconfig` (root)
- `tests/.globalconfig` (if exists)
- `.editorconfig` (root, first 100 lines of `[*.cs]` section)

## 1.9 Scan for suppressions

```
Grep: pattern="#pragma warning disable", path=src/, glob=*.cs, output_mode=content
```

Exclude `*.g.cs` and `obj/` paths.
