# Practices
*Updated: 2026-04-17T13:23:13Z*

## Design Principles

- **Immutability by default.** Prefer `record`, `readonly struct`, `init`-only properties, immutable collections (`IReadOnlyList<T>`, `FrozenSet<T>`). Mutable state requires explicit justification.
- **SOLID principles.** Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion. Balance with YAGNI.
- **Sealed by default.** All non-abstract classes must be sealed. Enforced by custom analyzer E128005 and ArchUnitNET architecture tests (`SealedClassTests`).
- **Design trade-off priority.** When trade-offs arise: Immutability > Memory efficiency (`Span<T>`, `Memory<T>`) > CPU efficiency > Parallelism.
- **Rob Pike's 5 Rules.** See [Rob Pike's Rules](rob-pikes-rules.md).

## Code Style Choices

- File-scoped namespaces (enforced by `.editorconfig`)
- `var` everywhere (enforced by `.editorconfig`)
- 120-character line limit
- 4-space indentation
- Allman brace style (opening brace on new line)
- `using` directives outside namespace, sorted with System first
- Implicit usings disabled — every `.cs` file has explicit `using` directives
- Primary constructors disabled (`csharp_style_prefer_primary_constructors = false`)
- Collection expressions disabled (`dotnet_style_prefer_collection_expression = never`)

## AI Assistant Preferences

- Terse responses — code over explanation unless asked
- ISO 8601 UTC timestamps everywhere: `2026-04-09T12:00:00Z`
- No time estimates
- Numbered menus for choices, never open-ended questions
- All decisions via `AskUserQuestion` tool, not inline text

## Development Methodology

- **TDD (Red-Green-Refactor)** for new features and significant changes. RED phase stubs use `Assert.Fail(message)`.
- **Architecture testing** with ArchUnitNET enforces layer dependencies, naming conventions, sealed-by-default, and service patterns. See [Architecture Testing](dotnet/architecture-testing.md).

## Verification Workflow

After any code change:
1. `scripts/format.sh --changed` — runs `jb cleanupcode` (semantic cleanup) then `dotnet format`
2. `scripts/check.sh --no-format` — build + test
3. If renaming: `rg "OldName" lode/` to find stale lode references

`--check` mode skips `jb` (no verify-only equivalent). Pass `--no-jb` to run dotnet format only.
Requires `jb` from `JetBrains.ReSharper.GlobalTools` (`dotnet tool install -g JetBrains.ReSharper.GlobalTools`); gracefully skipped if not installed.

## Cross-Platform File Paths

macOS is case-insensitive; Linux CI is case-sensitive. Always use exact filesystem casing. After any rename, search for stale references.
