# Quality Gates

## Format and Build

`scripts/check.sh` owns the C# verification gate: shell lint, format check, build, and tests.
Run it after the final C# edit. Do not bypass its format step with `--no-format`.
If format fails, run `scripts/format.sh --changed`, then rerun the gate.
Run `scripts/format-invalidate.sh` after format changes. Follow `read-before-edit.md` before further edits.

Before any commit, `scripts/format.sh --check` must pass.

## Shell and Documentation

After a shell edit, run `scripts/lint-shell.sh` and the affected behavior checks.
Do not call raw `shellcheck` in a gate.
Shell authoring details live in `lode/infrastructure/shell-authoring.md`.
After a Lode edit, run `scripts/lode-check.sh` on the changed files.

## Test Results

Use `scripts/test.sh ClassName` or `scripts/test.sh --all`. Use `scripts/coverage.sh` for coverage collection.
Do not run raw `dotnet test` from an agent command.
Read the test count. A passing exit code with zero tests proves nothing.
If the count is wrong, run the class or assembly again.

Use real assertions before running a gate or committing.
A placeholder stub uses `Assert.Fail(message)`, never `Assert.True(false)` or `NotImplementedException`.
E128171 reports a test whose entire body is `Assert.Fail`. A placeholder belongs only in the working tree.
Do not finish with a test commented out or skipped. Report a skip that predates the change.
Report failures and skipped checks. Do not claim verification until the affected checks pass.
The test execution order lives in `AGENTS.md` Workflow.

## Diagnostic Safety

Get explicit approval before using `#pragma warning disable`, `[SuppressMessage]`, or an editorconfig severity downgrade.
Do not weaken a gate to make it pass.
Never use the null-forgiving operator to silence CS8600 through CS8604.
When fixing a diagnostic pattern, search the whole file for the same pattern.

## Generated Regular Expressions

MA0190 requires a partial property, not a partial method:

```csharp
[GeneratedRegex(@"...", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
private static partial Regex MyRegex { get; }
```
