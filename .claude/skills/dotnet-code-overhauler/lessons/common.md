# Lessons: Common (cross-project)

Universal C# compiler limitations and analyzer gotchas discovered across multiple overhaul runs.
These apply to any .NET project, not just a specific codebase.

## False Positives — mark as INFO, do not fix

- `IComparer.Compare(object? x, object? y)` with `(T)x!` cast — only a false positive if T is a struct (unboxing null always throws regardless). Flag only if T is a reference type. Use pattern matching fix anyway for correctness.
- `null!` returns in System.CommandLine `CustomParser` error paths — framework ignores the return value when `AddError` is called. Intentional.

## Compiler Limitations

- After `if (a is null || b is null) { (a, b) = Compute(); }`, the compiler still sees `b` as `string?` even though both paths guarantee non-null. Use `b!` at the call site and document why. This is a flow-analysis limitation — the compiler does not track that the re-assignment inside the `if` body guarantees non-null on exit.

## Analyzer Notes

- **MA0015 (Meziantou):** `ArgumentException` must use the overload with `paramName`. Always: `throw new ArgumentException("msg", nameof(param))`. Omitting `paramName` triggers MA0015 even when the message is clear — the analyzer requires the overload unconditionally.
