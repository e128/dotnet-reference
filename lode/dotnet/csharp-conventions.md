# C# Conventions
*Updated: 2026-11-12T00:54:46Z*

Write these idioms on the first pass. `.editorconfig` and the analyzer set
cover most C# style. This file covers the gaps, the constructs to avoid, and
the search to run before you write a utility.

## Null Handling

- Write `is null` and `is not null`. Never write `== null` or `!= null`.
- Trust the nullable annotation. Do not add a null check when the type proves
  the value is present.
- Use the null-conditional operator where it applies, for example
  `scope?.Dispose()`.
- Use `ObjectDisposedException.ThrowIf` instead of a hand-written disposed
  check.

The null-forgiving operator ban lives in
[Quality Gates](../../.claude/rules/quality-gates.md).

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

Implicit usings are disabled. Give every C# file explicit `using` directives.
The format gate lives in [Quality Gates](../../.claude/rules/quality-gates.md).

## Repository Files

- Never hand-edit `global.json`. The dependency automation owns the SDK pin.
- Keep build output under `artifacts/`. No `bin` or `obj` folder sits beside a
  project.

## Constructs to Avoid

Never generate these. Each one compiles cleanly and produces incorrect or
fragile code.

- Inject `TimeProvider` through DI instead of calling `DateTime.Now` or `DateTime.UtcNow` directly.
  Direct calls hide the clock dependency and prevent controlled tests.
- Use `IHttpClientFactory` instead of `new HttpClient()`.
  The factory handles socket lifetime, DNS changes, and the HTTP pipeline.
- Use `async Task` instead of `async void` outside an event handler.
  An `async void` caller cannot observe completion or exceptions.
- Await throughout instead of calling `.Result` or `.GetAwaiter().GetResult()`.
  Blocking on async work can deadlock.

Never hardcode a URL, a secret, or another security-relevant default in
source. Expose it through a strongly-typed options class. See
[security.md](../../.claude/rules/security.md).

## Reuse Before You Write

Search for an existing implementation before you write new utility code.
Duplicate code is a maintenance burden. Call the existing code. When it almost
fits, refactor it so both callers share it. Never copy a helper into a second
file.

Search first before you implement any of these:

- String helpers or extensions: `rg 'static.*string' src/ -g '*.cs'`.
- Path manipulation utilities: `rg 'Path\.|FileInfo' src/`.
- Collection helpers: `rg 'static.*IEnumerable' src/`.
- HTTP or fetching: `rg 'HttpClient|IHttpClientFactory' src/`.

Use `rg` or `fd` for these lookups. Both are fast and cheap. Analyzer and
code fix helpers have their own catalog in
[shared-helpers.md](analyzers/shared-helpers.md).

## Regex

The partial-property contract and example live in
[Quality Gates](../../.claude/rules/quality-gates.md).

## Related

- [Practices](../practices.md): design principles and code style choices
- [SOLID](../coding-standards/solid.md): design order and module depth
- [Analyzers](analyzers.md): the rules that enforce these idioms
- [Analyzer Authoring](analyzers/authoring.md): rules for writing an analyzer
- [Shell Authoring](../infrastructure/shell-authoring.md): the shell script gate
