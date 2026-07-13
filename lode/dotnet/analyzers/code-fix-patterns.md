# Code Fix Patterns
*Updated: 2026-07-13T17:50:43Z*

Implementation patterns and gotchas for Roslyn code fix providers in E128.Analyzers.

## AddUsingIfMissing + Blank Line Behavior

When a code fix adds a `using` directive via `CompilationUnitSyntax.AddUsings()` or equivalent, and the original source has no existing `using` block, Roslyn inserts the new `using` followed by an `ElasticLineFeed` trivia. This produces a **blank line** between the new `using` and the first type declaration.

In `CSharpCodeFixVerifier` tests, the `FixedCode` string must include this blank line. Otherwise the verifier reports a diff mismatch even though the code is semantically correct.

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
| Wrap in method call              | E128070 (Math.Min)      | Context-specific — often no auto fix   |

## Related

- [New Analyzer Checklist](new-analyzer-checklist.md)
- [Analyzers](../analyzers.md) — main analyzer documentation
