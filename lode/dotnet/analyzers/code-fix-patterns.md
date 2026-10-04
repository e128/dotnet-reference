# Code Fix Patterns
*Updated: 2026-10-04T12:43:02Z*

Implementation patterns and gotchas for Roslyn code fix providers in E128.Analyzers.

## AddUsingIfMissing + Blank Line Behavior

When a code fix adds a `using` directive via `CompilationUnitSyntax.AddUsings()` with default trivia, and the original source has no existing `using` block, Roslyn inserts the new `using` followed by an `ElasticLineFeed` trivia. This produces a **blank line** between the new `using` and the first type declaration.

In `CSharpCodeFixVerifier` tests, the `FixedCode` string must include this blank line when the fix provider relies on default trivia. Otherwise the verifier reports a diff mismatch even though the code is semantically correct.

Most fix providers in this codebase avoid the blank line. They call
`.WithTrailingTrivia(SyntaxFactory.LineFeed)` on the new `UsingDirective` before
`AddUsings()`. Examples: `FileSystemPathCodeFixProvider`,
`AsyncVoidCodeFixProvider`, `CollectionPathCodeFixProvider`,
`MutableCollectionExposureCodeFixProvider`. Use this pattern for a new fix
provider unless a test proves the blank line is required.
A few providers still rely on default trivia and need the blank line in their
`FixedCode` string: `DiskRoundtripCodeFixProvider`,
`DateTimeParseRoundtripCodeFixProvider`, `GeneratedRegexTimeoutCodeFixProvider`,
and `MultiStringEqualsOrChainCodeFixProvider`.

```csharp
// If original code has NO usings and the fix adds "using System.Text;":
const string FixedCode = """
    using System.Text;

    namespace Example
    {
        // ...
    }
    """;
// Note the blank line between "using System.Text;" and "namespace Example"
```

When the original code already has `using` directives, the new one is merged into the existing block without an extra blank line.

## BatchFixer vs SequentialRenameFixAllProvider

- **`WellKnownFixAllProviders.BatchFixer`** — standard choice for most code fixes. Computes all fixes from the original snapshot and merges. Works well when fixes are independent local edits (expression replacements, type swaps).
- **`SequentialRenameFixAllProvider`** — required when the fix uses `Renamer.RenameSymbolAsync`. BatchFixer fails when multiple renames touch the same document because the rename API modifies the solution globally. See `analyzers.md § SequentialRenameFixAllProvider`.

## Common Fix Patterns

| Pattern                          | Example analyzers       | Notes                                  |
| -------------------------------- | ----------------------- | -------------------------------------- |
| Replace expression               | E128061, E128064        | Swap one expression for another        |
| Add using + replace expression   | E128066 (ToHashSet)     | Must handle blank line in tests        |
| Rename symbol                    | E128063, IDE1006        | Requires SequentialRenameFixAllProvider |
| Remove node                      | E128022 (ConfigureAwait)| Remove a method call from a chain      |
| Delete member                    | E128107, E128108        | Removes a whole test method            |
| Wrap in method call              | E128070 (Math.Min)      | Context-specific — often no auto fix   |

## Removing a Member Node

`RemoveNode(node, SyntaxRemoveOptions.KeepNoTrivia)` drops the trivia of the removed node. The
line break that ended the previous token stays. That result is correct for the common case.

A blank line between the removed member and the closing brace survives the removal. Trim that
line only when the leading trivia of the closing brace holds whitespace and line breaks alone.
Keep the whitespace, and drop the line breaks. Never clear the whole trivia list.

Clearing the whole list moves the closing brace to column 0 when the type is nested. It also
joins a trailing comment and the brace onto one line, which comments the brace out.

`LowValueTestCodeFixProvider` follows this rule.

## Related

- [New Analyzer Checklist](new-analyzer-checklist.md)
- [Analyzers](../analyzers.md) — main analyzer documentation
