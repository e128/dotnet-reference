# .NET 10 Testing
*Updated: 2026-04-09T13:07:38Z*

## Microsoft Testing Platform (MTP)

MTP is the required test runner for .NET 10. VSTest is no longer supported. MTP is built natively into xUnit v3 (not an adapter layer).

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

Test `.csproj` files only need `<IsTestProject>true</IsTestProject>` — everything else is inherited.

## xUnit v3

### Package Choice

- `xunit.v3.mtp-v2` — MTP v2 runner (recommended for .NET 10). Direct MTP integration.
- `xunit.v3` — includes MTP v1 runner + VSTest adapter. Needed only for IDE compatibility with older tooling.

### Key Differences from v2

- `IAsyncLifetime.InitializeAsync()` returns `ValueTask` (not `Task`)
- `IAsyncLifetime` extends `IAsyncDisposable` — `DisposeAsync()` also returns `ValueTask`
- Test projects must be executables (`OutputType=Exe`)
- Primary constructors supported for fixture injection

## Running Tests

### Via scripts (preferred)

```bash
# CI tests (default — filters to Category=CI)
scripts/test.sh

# Specific test class
scripts/test.sh GreeterTests

# All tests including Docker and Manual
scripts/test.sh --all

# Custom trait filter
scripts/test.sh --trait "Category=Docker"

# JSON output for automation
scripts/test.sh --json
```

### Via dotnet test (raw)

```bash
dotnet test --solution X.slnx -- --filter-trait "Category=CI"
```

The `--` separator passes arguments to MTP. `--filter` (VSTest syntax) does not work — use `-- --filter-trait` instead.

## Test Categories

Use `[Trait("Category", "...")]` to organize tests:

| Category   | Purpose                                      | Runs in CI |
| ---------- | -------------------------------------------- | ---------- |
| `CI`       | Fast, deterministic, no external deps        | Yes        |
| `Docker`   | Requires Docker daemon, builds/tests images  | No         |
| `Manual`   | Requires manual setup or external services   | No         |

## Integration Testing

`WebApplicationFactory<Program>` from `Microsoft.AspNetCore.Mvc.Testing` is the standard approach for testing minimal API apps. The Web project needs `<InternalsVisibleTo Include="TestProject" />` to expose the generated `Program` class.

## Test Project Structure

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3.mtp-v2" />
    <!-- Add other test packages as needed -->
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Project\Project.csproj" />
  </ItemGroup>
</Project>
```

## Conventions

- Test naming: `Method_Condition_Result` (e.g., `Greet_ReturnsGreeting_WithDefaultName`)
- No reflection in tests — use `internal` + `InternalsVisibleTo`
- RED phase stubs use `Assert.Fail("message")`
- Every test method has `[Trait("Category", "CI")]` (or appropriate category)

## Related

- [Project Structure](project-structure.md)
- [Analyzers](analyzers.md)
