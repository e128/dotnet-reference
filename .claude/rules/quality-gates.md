# Quality Gates

**Format gate.** `dotnet format` must pass before any commit. Always use `scripts/format.sh` (apply) or `scripts/format.sh --check` (verify). Generate format-compliant C# on the first write.

**`check.sh` is the single format gate.** `scripts/check.sh` runs `format.sh --check` as its first step — no need to pre-format and pass `--no-format`. If the format step fails, run `scripts/format.sh --changed` to apply fixes, then re-run `check.sh`.

**Analyzer suppressions.** Never use `#pragma warning disable`, `[SuppressMessage]`, or editorconfig severity downgrades without prompting the user via `AskUserQuestion`.

**Null-forgiving operator.** Never use `!` to silence CS8600-CS8604.

**Fix the class, not the instance.** When fixing a diagnostic pattern, search the entire affected file for the same pattern before committing.

**`[GeneratedRegex]` must use partial properties, not partial methods.** (MA0190)
```csharp
// CORRECT
[GeneratedRegex(@"...", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
private static partial Regex MyRegex { get; }
```
