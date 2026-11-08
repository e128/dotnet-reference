# C# Conventions

Write these idioms on the first pass. `.editorconfig` and the analyzer set
cover most C# style. The rules below cover the gaps.

## Null Handling

- Write `is null` and `is not null`. Never write `== null` or `!= null`.
- Trust the nullable annotation. Do not add a null check when the type proves
  the value is present.
- Use the null-conditional operator where it applies, for example
  `scope?.Dispose()`.
- Use `ObjectDisposedException.ThrowIf` instead of a hand-written disposed
  check.

## Expressions and Patterns

- Use pattern matching and a switch expression in place of an `if` chain.
- Use `nameof` for every member name that you write as a string literal.
- Keep the final `return` statement of a method on its own line.

## Collection Counts

- Read a collection count from `.Count`, `.Length`, or a lookup property.
- Never loop or sum to derive a count that the collection already exposes.
- Use the count directly to presize a collection or to initialize an array.

## Formatting

- Declare one file-scoped namespace per file.
- Write one `using` directive per line.
- Put a blank line before an XML documentation comment that follows code.
- Leave no trailing whitespace on any line.

## Repository Files

- Never hand-edit `global.json`. The dependency automation owns the SDK pin.
- Keep build output under `artifacts/`. No `bin` or `obj` folder sits beside a
  project.

## See Also

- [dotnet-anti-patterns.md](dotnet-anti-patterns.md): constructs to avoid
- [quality-gates.md](quality-gates.md): the format gate
