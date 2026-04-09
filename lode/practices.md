# Practices
*Updated: 2026-04-09T00:52:00Z*

## Design Principles

- **Immutability by default.** Prefer `record`, `readonly struct`, `init`-only properties, immutable collections (`IReadOnlyList<T>`, `FrozenSet<T>`). Mutable state requires explicit justification.
- **SOLID principles.** Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion. Balance with YAGNI.
- **Rob Pike's 5 Rules.** See [Rob Pike's Rules](rob-pikes-rules.md).

## Code Style Choices

- File-scoped namespaces (enforced by `.editorconfig`)
- `var` everywhere (enforced by `.editorconfig`)
- 120-character line limit
- 4-space indentation
- Allman brace style (opening brace on new line)
- `using` directives outside namespace, sorted with System first

## AI Assistant Preferences

- Terse responses — code over explanation unless asked
- ISO 8601 UTC timestamps everywhere: `2026-04-09T12:00:00Z`
- No time estimates
- Numbered menus for choices, never open-ended questions
- All decisions via `AskUserQuestion` tool, not inline text

## Verification Workflow

After any code change:
1. `scripts/format.sh --changed` — apply format
2. `scripts/check.sh --no-format` — build + test
3. If changing services/DI: check for appsettings drift
4. If renaming: `rg "OldName" lode/` to find stale lode references

## Cross-Platform File Paths

macOS is case-insensitive; Linux CI is case-sensitive. Always use exact filesystem casing. After any rename, search for stale references.
