# Solution Audit — Checks Catalog

All 10 dimensions with severity mappings and edge cases.

## Overview

| #  | Dimension             | Agent | Key checks                                              |
| -- | --------------------- | ----- | ------------------------------------------------------- |
| D1 | Dependency Graph      | A     | Circular deps, direction violations, redundant refs     |
| D2 | Solution Sync         | A     | Orphans, folder mismatches, missing test projects       |
| D3 | CPM Compliance        | B     | Hardcoded versions, overrides, unused central packages  |
| D4 | Package Health        | B     | Test-only leaks, deprecated, license compliance         |
| D5 | Framework Consistency | C     | TFM drift, multi-target without justification           |
| D6 | IVT & Encapsulation   | C     | Stale targets, legacy syntax, naming mismatches         |
| D7 | Build Config          | C     | Directory.Build.props correctness, analyzer setup       |
| D8 | Analyzer Config       | C     | .globalconfig/.editorconfig consistency                 |
| D9 | NuGet Config          | B     | nuget.config hygiene, audit properties                  |
| D10| Suppression Hygiene   | C     | Unjustified pragmas, broad suppressions, security rules |

## Edge Cases

### D1 — Dependency Graph
- **Test → Exe**: valid when testing a CLI tool's public API
- **Standalone tools**: Exe with no inbound refs is intentional — flag as LOW not MEDIUM
- **Analyzer project**: often has no inbound refs from src (consumed as NuGet) — not isolated

### D2 — Solution Sync
- **Analyzer project without test project**: the analyzer test project may use a different naming convention (e.g., `MyAnalyzers.Tests` not `MyAnalyzers.Test`)
- **Shared test project**: one test project covering multiple src projects is valid

### D3 — CPM Compliance
- **Roslyn analyzer authoring**: projects targeting `netstandard2.0` may need explicit version pins for `Microsoft.CodeAnalysis.*` due to API surface requirements

### D4 — Package Health
- **SonarAnalyzer.CSharp**: LGPL-3.0 but always used with `PrivateAssets="all"` — LOW not CRITICAL
- **Analyzer packages**: never ship in output, so license is informational only

### D6 — IVT & Encapsulation
- **CLI tools**: kebab-case AssemblyName with PascalCase namespace is convention, not a mismatch

### D7 — Build Config
- **Analyzer projects**: may legitimately override `TargetFramework` to `netstandard2.0`
- **`IlcFoldIdenticalMethodBodies`**: valid in props (affects all configurations)
