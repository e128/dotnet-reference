# Code Fixer Decisions: Style
*Updated: 2026-11-20T17:16:51Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## NamingStyleCodeFixProvider
- Reads Roslyn's IDE1006 diagnostic properties by key. The naming analyzer embeds SuggestedName, Prefix, Suffix, and the capitalization keys.
- Uses an acronym lookback of two characters. An acronym split looks two characters back at the uppercase run before the lowercase letter.
- Exposes a single-argument ComputeCompliantName for tests. FakeNamingViolationAnalyzer embeds SymbolName in the diagnostic properties.
- Separates its two overloads by their name source. Roslyn's naming analyzer embeds SuggestedName but not SymbolName. As a result, the production path passes the resolved symbol name.
- Strips a wrong-case single-letter prefix. Without the strip, `iFetcher` with prefix `I` becomes `IFetcher` and then `IIFetcher`.
- Counts a single-letter prefix only when the next character is uppercase. `IndexFilenames` starts with `I`, but the letter belongs to the word.

## PragmaBalanceCodeFixProvider
- Emits a bare restore without an error code. A bare restore clears every open suppression.
- Attaches the restore directive to the last token's trailing trivia. Attaching to the file's end token can add a blank line at the end of the file.

## PragmaBundlingCodeFixProvider
- Keeps everything after the last error code with the last split line. That includes the separator space and any justification comment, and the generated directive supplies its own line break.

## SequentialRenameFixAllProvider
- Applies renames back to front within a document. A rename at a higher position would otherwise shift the spans of symbols earlier in the file.

Related: [Decision Records](summary.md) explains how to use this page.
