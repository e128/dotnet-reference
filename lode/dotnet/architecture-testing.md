# Architecture Testing
*Updated: 2026-04-10T00:00:00Z*

## Overview

`tests/Architecture.Tests/` uses [TngTech.ArchUnitNET](https://github.com/TngTech/ArchUnitNET) to enforce structural invariants at the assembly and namespace level. Tests run as part of the CI suite via xUnit v3 and analyze compiled IL using Mono.Cecil.

## Package

- `TngTech.ArchUnitNET.xUnitV3` — xUnit v3 integration (includes core + assertions). Version pinned in `Directory.Packages.props`.

## How It Works

ArchUnitNET loads compiled assemblies via Mono.Cecil and builds an in-memory architecture model (`ArchUnitNET.Domain.Architecture`). Tests define rules using a fluent API and check them against the model. Failures throw `FailedArchRuleException`, which xUnit reports as test failures.

### Architecture Baseline

`ArchitectureBaseline.cs` loads the Core assembly once per test run (static field). All test classes reference this shared instance to avoid repeated IL parsing.

```csharp
public static readonly Architecture Instance =
    new ArchLoader()
        .LoadAssemblies(typeof(Greeter).Assembly)
        .Build();
```

### Fluent Rule API

Rules are defined using `ArchRuleDefinition` static methods:

```csharp
using static ArchUnitNET.Fluent.ArchRuleDefinition;

IArchRule rule = Types()
    .That().ResideInNamespace("E128.Reference.Core.Models")
    .Should().NotDependOnAny(
        Types().That().ResideInNamespace("E128.Reference.Core.Services"));

rule.Check(Architecture);
```

## Current Rules

### Layer Dependencies (`LayerDependencyTests`)

Enforces dependency direction: Models → (nothing), Repositories → Models only.

- Models must not depend on Services
- Models must not depend on Repositories
- Repositories must not depend on Services

### Naming Conventions (`NamingConventionTests`)

- Interfaces must start with "I"
- Concrete classes must not use interface naming pattern (`I[A-Z][a-z]...`)

### Sealed Classes (`SealedClassTests`)

- All non-abstract classes must be sealed (project convention for favoring immutability)

### Service Patterns (`ServicePatternTests`)

- Service classes must implement their corresponding interface
- Service classes must have "Service" suffix
- Repository classes must implement their corresponding interface

## Adding New Rules

1. Create a new test class in `tests/Architecture.Tests/`
2. Reference `ArchitectureBaseline.Instance`
3. Define rules using `ArchRuleDefinition` fluent API
4. Tag with `[Trait("Category", "CI")]`
5. If analyzing additional assemblies, add them to `ArchitectureBaseline`

## Key Namespaces

| Using                                          | Purpose                          |
| ---------------------------------------------- | -------------------------------- |
| `ArchUnitNET.Fluent`                           | `IArchRule` and fluent builders  |
| `ArchUnitNET.xUnitV3`                          | `.Check()` extension for xUnit   |
| `static ArchUnitNET.Fluent.ArchRuleDefinition` | `Types()`, `Classes()`, etc.     |
| `ArchUnitNET.Loader`                           | `ArchLoader` for IL loading      |
| `ArchUnitNET.Domain`                           | `Architecture` model type        |

## Related

- [Testing](testing.md)
- [Project Structure](project-structure.md)
- [Analyzers](analyzers.md)
