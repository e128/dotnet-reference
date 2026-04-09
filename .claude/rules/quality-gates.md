# Quality Gates

**Format gate.** `dotnet format` must pass before any commit. Always use `scripts/format.sh` (apply) or `scripts/format.sh --check` (verify). Generate format-compliant C# on the first write.

**Format before check, never after.** Always run `scripts/format.sh --changed` BEFORE `scripts/check.sh`, not after it fails. Then pass `--no-format` to `check.sh`.

**Analyzer suppressions.** Never use `#pragma warning disable`, `[SuppressMessage]`, or editorconfig severity downgrades without prompting the user via `AskUserQuestion`.

**Null-forgiving operator.** Never use `!` to silence CS8600-CS8604.

**Fix the class, not the instance.** When fixing a diagnostic pattern, search the entire affected file for the same pattern before committing.

**`[GeneratedRegex]` must use partial properties, not partial methods.** (MA0190)
```csharp
// CORRECT
[GeneratedRegex(@"...", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
private static partial Regex MyRegex { get; }
```
