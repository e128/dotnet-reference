# Code Quality Review Plan

*Updated: 2026-10-31T21:01:10Z*

## Target

The uncommitted working tree against `a1a70a5` on `main`. The change adds three
analyzers (E128139, E128140, E128141), three code fix providers, and six test
files. It also edits the analyzer README, the release bookkeeping files, the
lode, and the prompt.

## Findings

| #  | Severity | Location                                                  | Finding                                                                        | Source            |
| -  | -------- | --------------------------------------------------------- | ------------------------------------------------------------------------------ | ----------------- |
| 1  | High     | `Performance/Utf8GetBytesLiteralAnalyzer.cs:154`          | A `Span<byte>` target takes the bare `u8` literal, so the fix does not compile | 4 reviewers       |
| 2  | Medium   | `Performance/Utf8GetBytesLiteralAnalyzer.cs:116`          | A hard cast of `SyntaxTree.Options` can throw inside the analyzer              | code-reviewer     |
| 3  | Medium   | `Performance/Utf8GetBytesLiteralAnalyzer.cs:131`          | Positional argument lookup ignores a named argument                            | security-reviewer |
| 4  | Medium   | `Performance/ElementLoopToBulkSpanAnalyzer.cs:121`        | A zero literal becomes `Clear()`, which is not equal to `default`              | 2 reviewers       |
| 5  | Medium   | `Performance/ElementLoopToBulkSpanAnalyzer.cs:222`        | `IsSimpleTarget` accepts a property or a call in the receiver                  | 2 reviewers       |
| 6  | Medium   | `Unsafe/UnsafeUnalignedNonBlittableAnalyzer.cs:82`        | The message renders an empty type name for an array type argument              | functionality     |
| 7  | Medium   | `Performance/ElementLoopToBulkSpanAnalyzer.cs:22`         | The `KindKey` diagnostic property has no reader                                | 2 reviewers       |
| 8  | Medium   | `lode/infrastructure/claude-code-maintenance.md:183`      | The lode claims a prompt pointer that the prompt does not carry                | code-reviewer     |
| 9  | Low      | `README.md:194`                                           | The E128139 table row is one character too wide                                | 2 reviewers       |
| 10 | Low      | `lode/dotnet/analyzer-candidates.md:114`                  | The third-read table has no common column width                                | 2 reviewers       |
| 11 | Low      | `lode/dotnet/analyzer-candidates.md:112`                  | Prose says every rule has a code fix and the table says otherwise              | code-reviewer     |
| 12 | Low      | `lode/dotnet/analyzer-candidates.md:49`                   | The lode carries dated change history                                          | quality           |
| 13 | Low      | `README.md:1440`                                          | The prose says constant string and the rule matches a literal only             | 2 reviewers       |
| 14 | Low      | `README.md:1452`                                          | Nineteen unrelated rule sections ride in this change                           | quality           |
| 15 | Low      | `tests/E128.Analyzers.Tests/`                             | Five guards have no test                                                       | 3 reviewers       |
| 16 | Low      | `UnsafeUnalignedNonBlittable*Tests.cs:22`                 | A null-forgiving operator breaches a repo gate                                 | security-reviewer |
| 17 | Medium   | `Unsafe/UnsafeUnalignedNonBlittableCodeFixProvider.cs:78` | The fix turns an expression statement into `(x != 0);`, which is CS0201        | code-review       |
| 18 | Medium   | `lode/dotnet/analyzer-candidates.md:106`                  | The lode claims E128139 never held an ID, and it did                           | code-review       |
| 19 | Medium   | `prompts/SystemPrompt.txt:164`                            | The prompt delegates the checklist to the lint script, and the script lacks it | code-review       |
| 20 | Low      | `lode/dotnet/analyzers.md:114`                            | A 48-word row exceeds the descriptive sentence cap                             | code-review       |

## Phases

### Phase 1: Repair the E128141 code fix

Finding 1 is the only confirmed High. The fix trades compiling code for code
that does not compile, and the README states the wrong behaviour as intended.

Split `IsByteSpan` into two questions. The bare literal is valid only for
`ReadOnlySpan<byte>`. A `Span<byte>` target needs `"abc"u8.ToArray()`, which
reaches the target through the implicit `byte[]` to `Span<byte>` conversion.

### Phase 2: Repair the E128140 semantics

Findings 4 and 5 change the meaning of user code.

`Clear()` writes `default(T)`. A literal zero is not `default(T)` for a nullable
element type, and it can pass through a user-defined conversion. Route a zero
literal to `Fill` unless the element type makes the two forms equal.

`IsSimpleTarget` must resolve the receiver symbol. Reject a property getter and
reject a call anywhere in the receiver chain.

### Phase 3: Harden the analyzer guards

Findings 2, 3, 6, 7, and 17 are defects that the build gate cannot see.

Replace the hard cast with the `is not CSharpParseOptions` pattern that the
sibling analyzers use. Map a named argument to its parameter by name. Report the
type through `ToDisplayString` instead of `Name`. Delete `KindKey` or read it in
the fix. Handle the expression statement parent in the E128139 fix, so the
replacement stays a valid statement.

### Phase 4: Cover every repaired guard

Finding 15. Each repair in phases 1 to 3 needs a test that fails without it. Add
a `Span<byte>` parameter case, a `Span<byte>` local case, an `int?[]` zero-fill
case, a C# 10 silence case, and a named-argument case.

### Phase 5: Repair the documentation

Findings 8, 9, 10, 11, 12, 13, 18, 19, and 20.

The lode must state the current state. Remove the dated change notes. Restore
the true E128139 history. Fix the prompt pointer claim, or add the pointer to
the prompt. Name the skill file in the prompt. Align both tables.

### Phase 6: Delivery hygiene

Finding 14 and 16. Split the nineteen backfilled rule sections into their own
change. Remove the two null-forgiving operators. Branch before the commit and
stage the new source files.
