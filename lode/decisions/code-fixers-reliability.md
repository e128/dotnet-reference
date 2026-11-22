# Code Fixer Decisions: Reliability
*Updated: 2026-11-21T21:17:18Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## ArrayPoolSqliteParameterSizeCodeFixProvider
- Keeps a second copy of the call only when the statement reads the parameter elsewhere. A statement that only performs the call moves it into the declaration.
- Declines an ambiguous name. Two declarators of one name can sit in different scopes, and a reassigned buffer can hold a different length at the call.

## AsyncSiblingCodeFixHelper
- Accepts only an ordinary enclosing method. PromoteContainingMethod understands MethodDeclarationSyntax alone, and promoting a local function, constructor, or lambda would put an await outside an async body.

## CollectionMutationInEnumerationCodeFixProvider
- Adds System.Linq only when the collection lacks an instance ToArray. List<T> and the other mutable collections declare their own.

## ConcreteTypeDiDependencyCodeFixProvider
- Rewrites only a bare registration statement. A fluent chain carries more registrations after the matched call.

## ConcurrencyLimitCodeFixProvider
- Searches the reported span for the literal zero. The diagnostic can cover a whole creation or invocation, or a literal on the right of an assignment.

## DiskRoundtripCodeFixProvider
- Converts only a matching kind, or a text and bytes pair. Any other pair would put a value of the wrong type at the read site.
- Declines an unknown kind pair instead of falling back to a direct replacement.
- Declines a replacement equal to the read expression, so the fix never offers a no-op.

## DisposableSingletonFactoryCodeFixProvider
- Keeps a two-type-argument registration that passes a factory argument. A factory that builds from an argument produces something the container cannot reproduce.

## FileInfoToctouCodeFixProvider
- Rewrites a statement whose rewrite returns default. In a value-returning method any other statement would leave the try block, lose the local, and leave the catch with nothing to return.

## FinallyBlockCleanupCodeFixProvider
- Declines a local declaration and a yield statement. A declared local would leave the try block, and a yield inside a try with a catch clause does not compile.

## GeneratedRegexCompiledCodeFixProvider
- Drops the parentheses around a lone remaining option. A remaining OR-chain keeps them.

## GeneratedRegexNestedCodeFixProvider
- Removes the outer quantifier when the body is a single atom. When removal would lose the empty match, as in `(a+)*`, it makes the group non-capturing instead so the language stays the same.
- Keeps the outer quantifier when the body needs at least one character and the outer quantifier is a star. `(a+)*` loses the empty match, while `(a*)*` and `(a+)+` lose nothing.
- Steps back over a lazy marker when it looks for the inner quantifier. The marker belongs to the inner atom.
- Turns `(a+)*` into `(?:a+)*`. The language keeps the empty match and the group stops capturing per iteration, and an already non-capturing group returns unchanged.

## GeneratedRegexOverlappingCodeFixProvider
- Repeats the analyzer scan so the rewrite targets the element that the diagnostic reports. A required `\s+` collapses to `\s`, and an optional `\s*` goes away with its whole token.

## GeneratedRegexTimeoutCodeFixProvider
- Declines an attribute that already uses a named argument. A positional argument cannot follow a named one.

## ImmutableArrayDefaultCodeFixProvider
- Skips a struct instance field. Older language versions do not allow an initializer on a struct instance field.

## No-op reliability fix providers
- `ConstructorNewDiRegisteredCodeFixProvider`, `JsonDocumentLifetimeCodeFixProvider`, and `UnboundedTaskWhenAllCodeFixProvider` register no action on purpose.
- Each remedy changes control flow or a method signature, so no mechanical rewrite is safe.
- The registration keeps the diagnostic ID available for suppression.

## OptionsBindInitCodeFixProvider
- Falls back to the token parent when FindNode returns a child. FindNode returns the accessor directly only when the diagnostic span covers the whole accessor.

## PooledBufferZeroingCodeFixProvider
- Declines a Return outside a finally block. The zeroing call clears the buffer on every exit path only when it shares the finally that holds the Return.

## RegexLiteralFix
- Passes quoted text to the one-argument SyntaxFactory.Literal overload. That overload builds a token from text. As a result, it needs the quotes and the escapes.

## TaskCompletionSourceOptionsCodeFixProvider
- Declines a call with a named argument. A named argument moves the options value out of the position the fix expects.
- Replaces the enum in the first argument. That argument binds to the object state parameter, and appending would leave the enum as the state.

## TaskFromResultSyncIoCodeFixProvider
- Compares the bound type against the per-compilation symbol. A user type that shares the metadata name is then not a match.
- Binds the wrapper semantically, so a user `Task.FromResult` is left alone.
- Unwraps only a call that is the returned value. Any other call may feed a Task-typed variable or a continuation, where the bare value does not compile.
- Builds an intermediate document after the unwrap, because a rewritten tree needs its own semantic model for the sync-call scan.

## TaskWhenAllCancellationPropagationCodeFixProvider
- Appends the token only to an overload that ends with a CancellationToken. The Playwright calls accept only an options object. As a result, they get no fix.

## TextContentGuardCodeFixProvider
- Extends the outermost enclosing && chain. The guard must sit to the left of the match in the same expression.
- Parenthesizes the guard under a negated match. Without the parentheses the rewrite would negate the guard alone.

## UntimedProcessExitCodeFixProvider
- Fixes only the no-argument WaitForExit call. That form returns void and is always an ExpressionStatement. As a result, the result has one use.
- Fixes only WaitForExitAsync with no argument or CancellationToken.None. Neither carries a caller token, and replacing a parameter-sourced token would be a behavior regression.

Related: [Decision Records](summary.md) explains how to use this page.
