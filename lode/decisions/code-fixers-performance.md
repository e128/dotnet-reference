# Code Fixer Decisions: Performance
*Updated: 2026-11-22T04:00:12Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## AggressiveOptimizationCodeFixProvider
- Hands the first attribute list's leading trivia to the next token. That list owns the member's doc comment and indentation, and removing it without the move would drop both.

## ChainedStringReplaceCodeFixProvider
- Formats the collapse character with SymbolDisplay.FormatLiteral. The character is a runtime value, and FormatLiteral restores the quotes and escapes that a character literal needs.
- Collapses a single-character search whose replacement does not contain that character into one `Replace` call. No new occurrence can form, so the loop would run once.
- Declines a replacement that contains the search character, because the loop would not terminate.

## DllImportToLibraryImportCodeFixProvider
- Rewrites only attributes whose arguments all translate. LibraryImport has no CharSet, CallingConvention, PreserveSig, ExactSpelling, or BestFitMapping argument.

## HttpClientResponseHeadersReadCodeFixProvider
- Builds the options argument through the code action generator. The generator attaches the import annotation. As a result, the action adds the using and the simplifier shortens the name.

## ImmutableArrayCreateToArrayCodeFixProvider
- Leaves a call with an explicit type argument unfixed. An explicit argument can widen the result, and the rewrite infers the element type from the array.

## LoggerArgumentGuardCodeFixProvider
- Requires a receiver that contains no call. The receiver appears twice in the rewritten code, and a call would run twice.

## MultiStringEqualsOrChainCodeFixProvider
- Tries comparison prefixes longest first. The global-qualified form must be matched before the shorter form, or it reads as a plain member name.
- Stays single-diagnostic and offers no fix-all. Two chains on the same operand need the same field name, and a batch merge would emit that field twice.
- Formats each captured literal with SymbolDisplay.FormatLiteral. The analyzer captures Token.ValueText, which is already unescaped. As a result, the escapes must be put back for valid C#.

## ParallelForPerItemBodyCodeFixProvider
- Declines the fix when another symbol named Partitioner is in scope. The rewrite writes Partitioner unqualified, and the added using would make the call ambiguous.
- Accepts only a positional call. The fix emits positional arguments. As a result, a named-argument call has no rewrite.
- Treats only a foreign Partitioner symbol as a blocker. The real System.Collections.Concurrent.Partitioner is usable because the rewrite names its own namespace.
- Declines a body parameter that shares the range parameter name. The rewrite declares its range parameter with that name. As a result, the two would collide.

## ReadonlyFieldLoopHoistCodeFixProvider
- Rewrites all targets in one ReplaceNodes pass over the original tree. That keeps every replacement on one tree identity.
- Replaces the whole member access when the identifier is its name. `this._items` and `C._items` then rewrite as one node, while a receiver identifier such as `_items` in `_items.Length` rewrites itself.
- Emits the field type through ToMinimalDisplayString. That keeps the type arguments and qualifies the name only when it is out of scope.

## RedundantHashSetInFrozenSetCodeFixProvider
- Rewrites only a bare HashSet creation. A capacity, comparer, or collection initializer carries behavior the rewrite would drop, and a collection expression has no type of its own.

## RuntimeRegexToGeneratedCodeFixProvider
- Rewrites only a constant pattern and a plain flag list. GeneratedRegex arguments must be compile-time constants.
- Places `partial` directly before the type keyword. The modifier position is a language requirement.
- Moves the keyword's leading trivia to `partial` when the type has no other modifiers. The keyword owns the indentation and doc comments.

## SearchValuesHoistingCodeFixProvider
- Rebuilds the gap before the first member explicitly. It keeps the existing trivia so a doc comment survives, skips the line break after the open brace, and leaves exactly one blank line.

## SelectManyToListCodeFixProvider
- Replaces a whole statement. A ToList call nested inside a larger expression cannot be replaced without dropping the rest of that expression.

## StringBuilderPoolCodeFixProvider
- Drops a capacity hint but never a seed string. Rent takes no seed, and a seed would change the resulting content, while a capacity hint only costs a later resize.

## StringConcatInLoopCodeFixProvider
- Stays single-diagnostic and offers no fix-all. Two loops in one method would each pick the same builder name, and a batch merge would emit two declarations.
- Rewrites every concatenation on the local, not only the reported one. A second fix pass would otherwise declare a second builder for the same variable.
- Stops at a lambda, member declaration, or local function. A lambda can outlive the loop. As a result, a captured builder would be read before the appends run.
- Collects every local and parameter name in the member. A name declared later in the method, or inside the loop, still conflicts with a new local in an enclosing scope.

## StringFormatToInterpolationCodeFixProvider
- Escapes source syntax into the text token and keeps format-string content in the value text. Doubled braces belong to the value text.

Related: [Decision Records](summary.md) explains how to use this page.
