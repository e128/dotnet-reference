# Quality Gates

**Format gate.** `dotnet format` must pass before any commit. Run
`scripts/format.sh` to apply fixes. Run `scripts/format.sh --check` to verify.
Generate format-compliant C# on the first write.

**`check.sh` is the single format gate.** `scripts/check.sh` runs
`format.sh --check` as its first step. Do not pre-format and pass
`--no-format`. When the format step fails, run `scripts/format.sh --changed`,
then re-run `check.sh`.

**Analyzer suppressions.** Never use `#pragma warning disable`,
`[SuppressMessage]`, or an editorconfig severity downgrade without first
prompting the user through `AskUserQuestion`.

**Null-forgiving operator.** Never use `!` to silence CS8600 through CS8604.

**Fix the class, not the instance.** When you fix a diagnostic pattern, search
the whole file for the same pattern before you commit.

**`[GeneratedRegex]` uses a partial property, not a partial method** (MA0190).

```csharp
// CORRECT
[GeneratedRegex(@"...", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
private static partial Regex MyRegex { get; }
```
