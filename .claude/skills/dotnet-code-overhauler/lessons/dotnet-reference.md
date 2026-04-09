# Lessons: dotnet-reference

Project-specific false positives and compiler limitations discovered during overhaul runs.
These reduce false positives when running the overhauler against this specific codebase.

## False Positives — mark as INFO, do not fix

- `!` on tuple-deconstruct error patterns: `var (value, error) = TryXxx(); if (error is not null) return; Use(value!)` — compiler cannot track that error-null implies value-non-null across tuple deconstruction. The `!` is correct.
- `default!` in `out` param of `TryXxx` methods — standard C# pattern; required by compiler for definite assignment. Not fixable without restructuring.

## Compiler Limitations

- Nullable flow analysis does NOT cross tuple deconstruction. `var (v, e) = f(); if (e is null) { /* v is still string? */ }`. The `!` is genuinely necessary in these patterns.
- `string.Empty` is `static readonly`, NOT `const` — cannot be used as a default parameter value. Default params must use `""`.
