# .NET 11 Testing
*Updated: 2026-11-12T00:54:46Z*

## Microsoft Testing Platform (MTP)

MTP is the required test runner for the .NET SDK. VSTest is no longer supported. MTP is built natively into xUnit v3 (not an adapter layer).

### Required Configuration

In `global.json`:
```json
{ "test": { "runner": "Microsoft.Testing.Platform" } }
```

In `Directory.Build.targets` (conditioned on `IsTestProject`):
```xml
<PropertyGroup Condition="'$(IsTestProject)' == 'true'">
  <UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
  <TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
  <OutputType>Exe</OutputType>
</PropertyGroup>
```

Test `.csproj` files only need `<IsTestProject>true</IsTestProject>`, everything else is inherited.

## xUnit v3

### Package Choice

- `xunit.v3.mtp-v2`, MTP v2 runner (recommended for the .NET SDK). Direct MTP integration.
- `xunit.v3`, includes MTP v1 runner + VSTest adapter. Needed only for IDE compatibility with older tooling.

### Key Differences from v2

- `IAsyncLifetime.InitializeAsync()` returns `ValueTask` (not `Task`)
- `IAsyncLifetime` extends `IAsyncDisposable`, `DisposeAsync()` also returns `ValueTask`
- Test projects must be executables (`OutputType=Exe`)
- Primary constructors supported for fixture injection

## Running Tests

### Test Scripts

```bash
# CI tests (default, filters to Category=CI)
scripts/test.sh

# Specific test class. A bare name expands to a suffix wildcard.
scripts/test.sh GreeterTests

# Fully qualified name also works
scripts/test.sh E128.Reference.Core.Tests.GreeterTests

# All tests including Podman and Manual
scripts/test.sh --all

# Custom trait filter
scripts/test.sh --trait "Category=Podman"

# Terse output is the default (--json is a legacy no-op alias)
scripts/test.sh --json

# One framework leg of the multi-target analyzer test project
scripts/test.sh --all --framework net11.0
```

### MTP Arguments

The scripts pass arguments to MTP through the `--` separator.
Use `scripts/test.sh --trait` for a trait filter. VSTest `--filter` syntax does not work.
Use `scripts/coverage.sh` for coverage collection. Agent commands do not invoke the raw runner.
The execution gate lives in [Quality Gates](../../.claude/rules/quality-gates.md).

`--filter-class` matches the fully qualified class name. A short name matches
nothing. `scripts/test.sh` expands a name without a dot to a suffix wildcard,
so `test.sh MyTests` reaches `*MyTests`. A name that matches no test returns
`status: "no-match"` and exit code 3, so a typo cannot pass as a green run. A run with no filter (`--all` or an
empty `--trait`) that parses zero tests returns `status: "no-summary"` and exit
code 3.

## Test Categories

Use `[Trait("Category", "...")]` to organize tests:

| Category   | Purpose                                      | Runs in CI |
| ---------- | -------------------------------------------- | ---------- |
| `CI`       | Fast, deterministic, no external deps        | Yes        |
| `Podman`   | Requires Podman, builds/tests images         | No         |
| `Manual`   | Requires manual setup or external services   | No         |

## Integration Testing

`WebApplicationFactory<Program>` from `Microsoft.AspNetCore.Mvc.Testing` is the standard approach for testing minimal API apps. The Web project declares `<InternalsVisibleTo Include="E128.Reference.Tests" />` in its `.csproj` to expose the generated `Program` class.

## Test Project Structure

Five test projects, all sharing `<IsTestProject>true</IsTestProject>` (inherits MTP config from `Directory.Build.targets`):

| Project                     | Purpose                                 | Notable Packages (beyond `xunit.v3.mtp-v2`)                                                       |
| --------------------------- | --------------------------------------- | ------------------------------------------------------------------------------------------------- |
| `E128.Reference.Tests`      | Web integration tests                   | `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.Diagnostics.Testing`                    |
| `E128.Reference.Core.Tests` | Core library unit tests                 | `Microsoft.Testing.Extensions.HangDump`, `Microsoft.Testing.Extensions.TrxReport`                 |
| `E128.Reference.Cli.Tests`  | CLI unit tests                          | `Microsoft.Testing.Extensions.HangDump`, `Microsoft.Testing.Extensions.TrxReport`                 |
| `Architecture.Tests`        | ArchUnitNET structural invariant tests  | `TngTech.ArchUnitNET.xUnitV3`                                                                     |
| `E128.Analyzers.Tests`      | Roslyn analyzer + code fix verification | `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing`, `Microsoft.CodeAnalysis.CSharp.CodeFix.Testing` |

Minimal `.csproj` template (all versions from CPM):

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3.mtp-v2" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Project\Project.csproj" />
  </ItemGroup>
</Project>
```

`Microsoft.Testing.Extensions.HangDump` and `Microsoft.Testing.Extensions.TrxReport` are added to projects that need hang diagnostics and TRX CI reporting. Every test project receives `Microsoft.Testing.Extensions.CodeCoverage` from `tests/Directory.Build.targets`. See [Code Coverage](code-coverage.md).

## Test Authoring

- Name tests `MethodName_ExpectedBehavior_WhenCondition`, for example `GreetAsync_ReturnsGreeting_WithDefaultName`.
- Add a test to the class that already covers the behavior. Create a file only for a new unit under test.
- Expose test-only members with `internal` and `[InternalsVisibleTo]`. Reflection bypasses access checks and breaks under renaming.
- If reflection is genuinely required, explain why in a comment.
- Give every test method an appropriate category trait, normally `[Trait("Category", "CI")]`.

The authoring and execution order lives in [AGENTS.md](../../AGENTS.md) Workflow.
The placeholder assertion rules, nonzero test-count check, and disabled-test policy live in
[Quality Gates](../../.claude/rules/quality-gates.md).

## Target Changes

Before changing `<TargetFramework>`, run `dotnet --list-sdks` and confirm the target SDK is installed.
A missing target SDK fails the local build. CI uses its pinned SDK and a case-sensitive Linux filesystem.
The supported targets live in [AGENTS.md](../../AGENTS.md) .NET Constraints.

## Related

- [Project Structure](project-structure.md)
- [Architecture Testing](architecture-testing.md)
- [Analyzers](analyzers.md)
- [Code Coverage](code-coverage.md)
